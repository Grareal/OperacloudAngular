using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FirmaOperaCloud.Api.Controllers;

/// <summary>
/// Consulta del historial local de tarjetas de registro generadas y firmadas.
/// </summary>
[ApiController]
[Route("api/registration-cards")]
[Authorize(Policy = "RegistrationCard.Use")]
public sealed class RegistrationCardsController : ControllerBase
{
    private readonly IRegistrationCardStoreService _storeService;
    private readonly IRegistrationCardService _cardService;
    private readonly IReservationService _reservationService;

    public RegistrationCardsController(
        IRegistrationCardStoreService storeService,
        IRegistrationCardService cardService,
        IReservationService reservationService)
    {
        _storeService = storeService;
        _cardService = cardService;
        _reservationService = reservationService;
    }

    /// <summary>
    /// Lista las tarjetas de registro más recientes.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RegistrationCard>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var cards = await _storeService.GetRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(cards);
    }

    /// <summary>
    /// Obtiene una tarjeta de registro por su Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RegistrationCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var card = await _storeService.GetByIdAsync(id, cancellationToken);
        if (card is null)
        {
            return NotFound(new { message = $"No se encontró la tarjeta {id}." });
        }

        return Ok(card);
    }

    /// <summary>
    /// Busca tarjetas por número de confirmación.
    /// </summary>
    [HttpGet("by-confirmation/{confirmationNumber}")]
    [ProducesResponseType(typeof(IReadOnlyList<RegistrationCard>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByConfirmationNumber(string confirmationNumber, CancellationToken cancellationToken)
    {
        var cards = await _storeService.GetByConfirmationNumberAsync(confirmationNumber, cancellationToken);
        return Ok(cards);
    }

    /// <summary>
    /// Búsqueda local sobre tarjetas almacenadas: confirmación, huésped,
    /// correo, teléfono o habitación.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<RegistrationCard>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int limit = 30, CancellationToken cancellationToken = default)
    {
        var cards = await _storeService.SearchAsync(term, Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(cards);
    }

    /// <summary>
    /// Genera una tarjeta de registro a partir de una reserva de OPERA,
    /// persiste el PDF en la BD local y registra auditoría.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(RegistrationCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate(
        [FromQuery] string confirmationNumber,
        [FromQuery] string signerEmail,
        CancellationToken cancellationToken)
    {
        var reservations = await _reservationService.GetByConfirmationNumberAsync("VINV", confirmationNumber, cancellationToken);
        var reservation = reservations.FirstOrDefault();
        if (reservation is null)
        {
            return NotFound(new { message = $"No se encontró la reserva {confirmationNumber} en OPERA." });
        }

        var card = await _storeService.GenerateAndStoreAsync(
            reservation, signerEmail, null, null, cancellationToken);
        return Ok(card);
    }

    /// <summary>
    /// Firma una tarjeta de registro con la firma proporcionada (PNG base64).
    /// </summary>
    [HttpPost("{id:guid}/sign")]
    [ProducesResponseType(typeof(RegistrationCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sign(
        Guid id,
        [FromBody] SignRequest request,
        CancellationToken cancellationToken)
    {
        var card = await _storeService.GetByIdAsync(id, cancellationToken);
        if (card is null)
        {
            return NotFound(new { message = $"No se encontró la tarjeta {id}." });
        }

        var signed = await _storeService.SignAsync(
            id, request.Signature, null, null, null, null, cancellationToken);
        return Ok(signed);
    }

    /// <summary>
    /// Descarga el PDF de una tarjeta de registro.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var card = await _storeService.GetByIdAsync(id, cancellationToken);
        if (card is null)
        {
            return NotFound(new { message = $"No se encontró la tarjeta {id}." });
        }

        if (string.IsNullOrEmpty(card.PdfBase64))
        {
            return NotFound(new { message = "La tarjeta no tiene PDF generado." });
        }

        var bytes = Convert.FromBase64String(card.PdfBase64);
        return File(bytes, "application/pdf", $"RegistrationCard_{card.ConfirmationNumber}.pdf");
    }

    public sealed record SignRequest(string Signature);
}
