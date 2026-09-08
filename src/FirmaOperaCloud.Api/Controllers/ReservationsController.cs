using System.Net;
using System.Security.Cryptography;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Shared.Options;
using FirmaOperaCloud.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Api.Controllers;

/// <summary>
/// Endpoints de consulta de reservaciones (solo lectura) y generación de la
/// tarjeta de registro digital. No se modifica información en OPERA.
/// </summary>
[ApiController]
[Route("api/reservations")]
[Authorize(Policy = "RegistrationCard.Use")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IRegistrationCardService _cardService;
    private readonly IRegistrationCardStoreService _storeService;
    private readonly IOperaRegistrationCardService _operaCardService;
    private readonly IRegistrationCardPdfFiller _pdfFiller;
    private readonly OperaCloudOptions _options;
    private readonly LocalDocumentService _localDocuments;
    private readonly GuestEmailQueueService _guestEmails;

    public ReservationsController(
        IReservationService reservationService,
        IRegistrationCardService cardService,
        IRegistrationCardStoreService storeService,
        IOperaRegistrationCardService operaCardService,
        IRegistrationCardPdfFiller pdfFiller,
        LocalDocumentService localDocuments,
        GuestEmailQueueService guestEmails,
        IOptions<OperaCloudOptions> options)
    {
        _reservationService = reservationService;
        _cardService = cardService;
        _storeService = storeService;
        _operaCardService = operaCardService;
        _pdfFiller = pdfFiller;
        _localDocuments = localDocuments;
        _guestEmails = guestEmails;
        _options = options.Value;
    }

    /// <summary>
    /// Busca una reserva por número de confirmación.
    /// </summary>
    [HttpGet("{confirmationNumber}")]
    [ProducesResponseType(typeof(Reservation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByConfirmationNumber(string confirmationNumber, CancellationToken cancellationToken)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync(
            _options.DefaultHotelId, confirmationNumber, cancellationToken);

        if (reservations.Count == 0)
        {
            return NotFound(new { message = $"No se encontró la reserva {confirmationNumber}." });
        }

        return Ok(reservations);
    }

    /// <summary>
    /// Reservas con llegada dentro del rango de fechas (dashboard).
    /// </summary>
    [HttpGet("arrivals")]
    [ProducesResponseType(typeof(IReadOnlyList<Reservation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArrivals(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _reservationService.GetByArrivalDateRangeAsync(
            _options.DefaultHotelId, start, end, Math.Clamp(limit, 1, 500), cancellationToken);
        return Ok(reservations);
    }

    /// <summary>
    /// Reservas con salida dentro del rango de fechas (dashboard).
    /// </summary>
    [HttpGet("departures")]
    [ProducesResponseType(typeof(IReadOnlyList<Reservation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartures(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _reservationService.GetByDepartureDateRangeAsync(
            _options.DefaultHotelId, start, end, Math.Clamp(limit, 1, 500), cancellationToken);
        return Ok(reservations);
    }

    /// <summary>
    /// Búsqueda global por número de confirmación o apellido (buscador).
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<Reservation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string term,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Ok(Array.Empty<Reservation>());
        }

        if (term.All(char.IsDigit))
        {
            return Ok(await _reservationService.GetByConfirmationNumberAsync(
                _options.DefaultHotelId, term, cancellationToken));
        }

        return Ok(await _reservationService.GetBySurnameAsync(
            _options.DefaultHotelId, term, Math.Clamp(limit, 1, 200), cancellationToken));
    }

    /// <summary>
    /// Genera el PDF de la tarjeta de registro para una reserva por número de confirmación.
    /// </summary>
    [HttpGet("{confirmationNumber}/registration-card")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistrationCard(string confirmationNumber, CancellationToken cancellationToken)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync(
            _options.DefaultHotelId, confirmationNumber, cancellationToken);

        if (reservations.Count == 0)
        {
            return NotFound(new { message = $"No se encontró la reserva {confirmationNumber}." });
        }

        var card = _cardService.BuildCard(reservations[0]);
        var pdfBytes = _cardService.GeneratePdf(card);
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        // Metadatos de auditoría del documento generado (local, nunca a OPERA).
        var fileName = $"RegistrationCard_{confirmationNumber}.pdf";

        Response.Headers["X-Document-Hash"] = hash;
        Response.Headers["X-Document-Id"] = card.Id.ToString();

        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Genera la tarjeta de registro, la persiste en la BD local y devuelve sus datos.
    /// </summary>
    [HttpPost("{confirmationNumber}/registration-card")]
    [ProducesResponseType(typeof(RegistrationCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateRegistrationCard(string confirmationNumber, CancellationToken cancellationToken)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync(
            _options.DefaultHotelId, confirmationNumber, cancellationToken);

        if (reservations.Count == 0)
        {
            return NotFound(new { message = $"No se encontró la reserva {confirmationNumber}." });
        }

        var card = await _storeService.GenerateAndStoreAsync(
            reservations[0],
            Request.Headers["X-Receptionist"].FirstOrDefault(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].FirstOrDefault(),
            cancellationToken);

        return Ok(card);
    }

    /// <summary>
    /// Firma digitalmente una tarjeta de registro almacenada.
    /// </summary>
    [HttpPost("{confirmationNumber}/registration-card/{cardId}/sign")]
    [ProducesResponseType(typeof(RegistrationCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SignRegistrationCard(
        string confirmationNumber,
        Guid cardId,
        [FromBody] SignRegistrationCardRequest request,
        CancellationToken cancellationToken)
    {
        var card = await _storeService.GetByIdAsync(cardId, cancellationToken);
        if (card is null || !card.ConfirmationNumber.Equals(confirmationNumber, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = $"No se encontró la tarjeta {cardId} para la reserva {confirmationNumber}." });
        }

        var signed = await _storeService.SignAsync(
            cardId,
            request.SignaturePngBase64,
            request.SignatureSvgBase64,
            Request.Headers["X-Receptionist"].FirstOrDefault(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].FirstOrDefault(),
            cancellationToken);

        return Ok(signed);
    }

    /// <summary>
    /// Devuelve el PDF firmado de una tarjeta de registro almacenada.
    /// </summary>
    [HttpGet("{confirmationNumber}/registration-card/{cardId}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoredRegistrationCardPdf(
        string confirmationNumber,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var card = await _storeService.GetByIdAsync(cardId, cancellationToken);
        if (card is null || !card.ConfirmationNumber.Equals(confirmationNumber, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = $"No se encontró la tarjeta {cardId} para la reserva {confirmationNumber}." });
        }

        if (string.IsNullOrEmpty(card.PdfBase64))
        {
            return NotFound(new { message = "La tarjeta no tiene PDF almacenado." });
        }

        await _storeService.AuditAsync(
            cardId,
            "Downloaded",
            Request.Headers["X-Receptionist"].FirstOrDefault(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].FirstOrDefault(),
            $"Descarga del PDF (estado: {card.Status}).",
            cancellationToken);

        var pdfBytes = Convert.FromBase64String(card.PdfBase64);
        var fileName = $"RegistrationCard_{confirmationNumber}_{card.Id:N}.pdf";

        if (!string.IsNullOrEmpty(card.DocumentHash))
        {
            Response.Headers["X-Document-Hash"] = card.DocumentHash;
        }
        Response.Headers["X-Document-Id"] = card.Id.ToString();

        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>Obtiene directamente la Registration Card oficial generada por OPERA.</summary>
    [HttpGet("{confirmationNumber}/official-registration-card")]
    public async Task<IActionResult> GetOfficialRegistrationCard(
        string confirmationNumber,
        [FromQuery] string? template,
        CancellationToken cancellationToken)
    {
        var context = await GetOperaCardContextAsync(confirmationNumber, template, cancellationToken);
        Response.Headers["X-Opera-Template"] = context.Template;
        return File(context.Pdf, "application/pdf", $"REGCARD{confirmationNumber}.pdf");
    }

    /// <summary>Rellena y aplana una vista previa sin escribir en OPERA.</summary>
    [HttpPost("{confirmationNumber}/official-registration-card/preview")]
    public async Task<IActionResult> PreviewFilledOfficialRegistrationCard(
        string confirmationNumber,
        [FromBody] FillOfficialRegistrationCardRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetDocumentContextAsync(confirmationNumber, request, cancellationToken);
        var filledPdf = context.LocalTemplate is null ? _pdfFiller.Fill(context.BasePdf, request.ToInput(context.Reservation.Guest.FullName)) : _localDocuments.Fill(context.LocalTemplate, context.Reservation, request);
        Response.Headers["X-Document-Template"] = context.LocalTemplate?.Name ?? context.OperaTemplate ?? "official";
        Response.Headers["X-Document-Hash"] = Convert.ToHexString(SHA256.HashData(filledPdf));
        return File(filledPdf, "application/pdf", $"REGCARD{confirmationNumber}PREVIEW.pdf");
    }

    /// <summary>Rellena, aplana y sube la Registration Card a Attachments de OPERA.</summary>
    [HttpPost("{confirmationNumber}/official-registration-card/upload")]
    public async Task<IActionResult> UploadFilledOfficialRegistrationCard(
        string confirmationNumber,
        [FromBody] FillOfficialRegistrationCardRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PrimarySignaturePngBase64))
        {
            return BadRequest(new { message = "La firma del huésped principal es obligatoria para enviar el documento." });
        }

        var context = await GetDocumentContextAsync(confirmationNumber, request, cancellationToken);
        var filledPdf = context.LocalTemplate is null ? _pdfFiller.Fill(context.BasePdf, request.ToInput(context.Reservation.Guest.FullName)) : _localDocuments.Fill(context.LocalTemplate, context.Reservation, request);
        var userName = Request.Headers["X-Receptionist"].FirstOrDefault() ?? User.Identity?.Name ?? "FirmaOperaCloud";
        var stored = await _localDocuments.StoreAsync(context.Reservation, context.LocalTemplate, request, filledPdf, userName, cancellationToken);
        var attachment = await _operaCardService.UploadPdfAsync(
            _options.DefaultHotelId,
            context.ReservationId,
            confirmationNumber,
            filledPdf,
            userName,
            cancellationToken);
        stored.Document.AttachmentId = attachment.AttachmentId;
        stored.Document.AttachmentFileName = attachment.FileName;
        stored.Document.Status = "Uploaded";
        stored.Document.UploadedAtUtc = DateTime.UtcNow;
        await HttpContext.RequestServices.GetRequiredService<FirmaOperaCloud.Infrastructure.Persistence.FirmaOperaCloudDbContext>().SaveChangesAsync(cancellationToken);
        GuestEmailDelivery? emailDelivery = null;
        var emailStatus = "SkippedNoEmail";
        try
        {
            emailDelivery = await _guestEmails.QueueAsync(stored.Document,
                context.Reservation,
                request.Email ?? context.Reservation.Guest.Email,
                request.MarketingConsent,
                cancellationToken);
            if (emailDelivery is not null) emailStatus = emailDelivery.Status;
        }
        catch { emailStatus = "QueueFailed"; }

        return Ok(new
        {
            attachment.AttachmentId,
            attachment.FileName,
            attachment.FileSize,
            attachment.Description,
            Template = context.LocalTemplate?.Name ?? context.OperaTemplate,
            LocalDocumentId = stored.Document.Id,
            LocalDocumentVersion = stored.Document.Version,
            DocumentHash = stored.Document.DocumentHash,
            EmailDeliveryId = emailDelivery?.Id,
            EmailStatus = emailStatus
        });
    }

    private async Task<DocumentContext> GetDocumentContextAsync(string confirmationNumber, FillOfficialRegistrationCardRequest request, CancellationToken ct)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync(_options.DefaultHotelId, confirmationNumber, ct);
        var reservation = reservations.FirstOrDefault() ?? throw new KeyNotFoundException($"No se encontró la reservación {confirmationNumber}.");
        var reservationId = reservation.ReservationIdList.FirstOrDefault(x => x.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new InvalidOperationException("La reservación no contiene el ID interno de OPERA.");
        var local = await _localDocuments.ResolveTemplateAsync(reservation, request.LocalTemplateId, ct);
        if (local is not null) return new(reservation, reservationId, local.PdfData, local, null);
        var operaTemplate = _operaCardService.ResolveTemplate(reservation, request.Template);
        var pdf = await _operaCardService.GetOfficialPdfAsync(_options.DefaultHotelId, reservationId, operaTemplate, reservation.Guest.Language, ct);
        return new(reservation, reservationId, pdf, null, operaTemplate);
    }

    private async Task<OperaCardContext> GetOperaCardContextAsync(
        string confirmationNumber,
        string? requestedTemplate,
        CancellationToken cancellationToken)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync(
            _options.DefaultHotelId, confirmationNumber, cancellationToken);
        var reservation = reservations.FirstOrDefault()
            ?? throw new KeyNotFoundException($"No se encontró la reservación {confirmationNumber}.");
        var reservationId = reservation.ReservationIdList
            .FirstOrDefault(id => id.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new InvalidOperationException("La reservación no contiene el ID interno de OPERA.");
        var template = _operaCardService.ResolveTemplate(reservation, requestedTemplate);
        var pdf = await _operaCardService.GetOfficialPdfAsync(
            _options.DefaultHotelId,
            reservationId,
            template,
            reservation.Guest.Language,
            cancellationToken);
        return new(reservation, reservationId, template, pdf);
    }

    private sealed record OperaCardContext(Reservation Reservation, string ReservationId, string Template, byte[] Pdf);
    private sealed record DocumentContext(Reservation Reservation, string ReservationId, byte[] BasePdf, PdfTemplate? LocalTemplate, string? OperaTemplate);
}

/// <summary>
/// Cuerpo de la solicitud de firma de una tarjeta de registro.
/// </summary>
public sealed class SignRegistrationCardRequest
{
    /// <summary>Firma en PNG (base64) capturada en el navegador.</summary>
    public string SignaturePngBase64 { get; set; } = string.Empty;

    /// <summary>Firma en SVG (base64), opcional.</summary>
    public string? SignatureSvgBase64 { get; set; }
}

public sealed class FillOfficialRegistrationCardRequest
{
    public string? Template { get; set; }
    public Guid? LocalTemplateId { get; set; }
    public string? Citizenship { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? CellPhone { get; set; }
    public string? PrimaryGuestName { get; set; }
    public string? PrimarySignerId { get; set; }
    public string? PrimarySignaturePngBase64 { get; set; }
    public bool MarketingConsent { get; set; }
    public List<RegistrationCardOccupantRequest> Occupants { get; set; } = [];

    public RegistrationCardFillInput ToInput(string defaultPrimaryName) => new(
        Citizenship,
        City,
        State,
        Country,
        Email,
        CellPhone,
        string.IsNullOrWhiteSpace(PrimaryGuestName) ? defaultPrimaryName : PrimaryGuestName,
        PrimarySignaturePngBase64,
        Occupants.Select(item => new RegistrationCardOccupantInput(item.Name, item.SignaturePngBase64)).ToList());
}

public sealed class RegistrationCardOccupantRequest
{
    public string? SignerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SignaturePngBase64 { get; set; } = string.Empty;
}
