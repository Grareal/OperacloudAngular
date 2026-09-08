using FirmaOperaCloud.Api.Services;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Documents.Read"), Route("api/local-documents")]
public sealed class LocalDocumentsController(
    FirmaOperaCloudDbContext db,
    ReservationFileService reservationFiles,
    IAuditService audit) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int limit = 200, CancellationToken ct = default)
    {
        var rows = await db.LocalDocuments.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 200))
            .Select(x => new
            {
                x.Id, x.ReservationFileId, x.HotelId, x.ConfirmationNumber, x.RoomNumber,
                x.Version, x.FileName, x.DocumentHash, x.Status, x.AttachmentId,
                x.AttachmentFileName, x.CreatedAtUtc, x.UploadedAtUtc,
                SignatureCount = x.Signatures.Count
            }).ToListAsync(ct);
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Documents.Listed", "LocalDocument",
            reason: AuditRequest.ReadReason(HttpContext) ?? "Consulta de historial operativo"), ct);
        return Ok(rows);
    }

    [HttpGet("reservation/{confirmationNumber}")]
    public async Task<IActionResult> ReservationPackage(string confirmationNumber, CancellationToken ct)
    {
        var reason = AuditRequest.ReadReason(HttpContext);
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = $"Indique el motivo en el encabezado {AuditRequest.ReasonHeader}." });

        var confirmation = confirmationNumber.Trim();
        if (confirmation.Length < 2)
            return BadRequest(new { message = "Indique un número de reserva válido." });

        var documents = await db.LocalDocuments.AsNoTracking()
            .Where(x => x.ConfirmationNumber == confirmation)
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.Id, x.ReservationFileId, x.HotelId, x.ConfirmationNumber, x.RoomNumber,
                x.Version, x.FileName, x.DocumentHash, x.Status, x.AttachmentId,
                x.AttachmentFileName, x.CreatedAtUtc, x.UploadedAtUtc,
                SignatureCount = x.Signatures.Count
            }).ToListAsync(ct);

        var files = await db.ReservationFiles.AsNoTracking()
            .Where(x => x.ConfirmationNumber == confirmation)
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.Id, x.HotelId, x.ConfirmationNumber, x.ReservationId, x.Version,
                x.Status, x.ManifestHash, x.CreatedAtUtc, x.SealedAtUtc, x.CreatedBy, x.SealedBy,
                DocumentCount = x.Documents.Count
            }).ToListAsync(ct);

        var signatures = await db.StoredSignatures.AsNoTracking()
            .Where(x => x.ConfirmationNumber == confirmation)
            .OrderByDescending(x => x.SignedAtUtc)
            .Select(x => new
            {
                x.Id, x.SignerName, x.SignerKey, x.OperaProfileId, x.SignerRole,
                x.SignatureHash, x.SignedAtUtc, x.RoomNumber
            }).ToListAsync(ct);

        var deliveries = await db.GuestEmailDeliveries.AsNoTracking()
            .Where(x => x.ConfirmationNumber == confirmation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id, x.GuestName, x.RecipientEmail, x.Language, x.MarketingConsent,
                x.Status, x.AttemptCount, x.CreatedAtUtc, x.SentAtUtc, x.LastError,
                Items = x.Items.OrderBy(i => i.DocumentType == "RegistrationCard" ? 0 : 1)
                    .ThenBy(i => i.Name)
                    .Select(i => new
                    {
                        i.Id, i.DocumentType, i.Name, i.Version, i.FileName,
                        i.ContentType, i.DocumentHash
                    }).ToList()
            }).ToListAsync(ct);

        var hotelId = documents.Select(x => x.HotelId).FirstOrDefault()
            ?? files.Select(x => x.HotelId).FirstOrDefault() ?? "VINV";
        var setting = await db.GuestEmailSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.HotelId == hotelId, ct);
        var latest = deliveries.FirstOrDefault();
        var guestName = latest?.GuestName ?? "Huésped Vidanta";
        var subject = (setting?.Subject ??
            "Documentos de su registro Vidanta - Reserva {confirmation}")
            .Replace("{confirmation}", confirmation, StringComparison.OrdinalIgnoreCase);
        var body = (setting?.BodyHtml ??
            "<p>Estimado(a) {guest},</p><p>Adjuntamos los documentos relacionados con su registro y estancia.</p><p>Reserva: <strong>{confirmation}</strong></p>")
            .Replace("{guest}", System.Net.WebUtility.HtmlEncode(guestName), StringComparison.OrdinalIgnoreCase)
            .Replace("{confirmation}", System.Net.WebUtility.HtmlEncode(confirmation), StringComparison.OrdinalIgnoreCase);

        await audit.AppendAsync(AuditRequest.Create(HttpContext, "ReservationFile.Viewed", "ReservationFile",
            hotelId: hotelId, confirmationNumber: confirmation, reason: reason), ct);

        return Ok(new
        {
            ConfirmationNumber = confirmation,
            Found = documents.Count > 0 || signatures.Count > 0 || deliveries.Count > 0,
            GuestName = latest?.GuestName,
            RoomNumber = documents.Select(x => x.RoomNumber)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            Files = files,
            Documents = documents,
            Signatures = signatures,
            Deliveries = deliveries,
            EmailPreview = new
            {
                FromName = setting?.FromName ?? "Vidanta",
                FromAddress = setting?.FromAddress ?? string.Empty,
                Recipient = latest?.RecipientEmail ?? string.Empty,
                Subject = subject,
                BodyHtml = body,
                Attachments = latest?.Items ?? []
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string term, CancellationToken ct)
    {
        term = term.Trim();
        if (term.Length < 2) return BadRequest();
        var rows = await db.LocalDocuments.AsNoTracking()
            .Where(x => x.ConfirmationNumber.Contains(term) ||
                (x.RoomNumber != null && x.RoomNumber.Contains(term)))
            .OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new
            {
                x.Id, x.ReservationFileId, x.HotelId, x.ConfirmationNumber, x.RoomNumber,
                x.Version, x.FileName, x.DocumentHash, x.Status, x.AttachmentId,
                x.AttachmentFileName, x.CreatedAtUtc, SignatureCount = x.Signatures.Count
            }).ToListAsync(ct);
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Documents.Searched", "LocalDocument",
            reason: AuditRequest.ReadReason(HttpContext) ?? "Búsqueda operativa"), ct);
        return Ok(rows);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var reason = AuditRequest.ReadReason(HttpContext);
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "Debe indicar el motivo de apertura del documento." });
        var row = await db.LocalDocuments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.PdfData, x.FileName, x.DocumentHash, x.HotelId, x.ConfirmationNumber
            }).FirstOrDefaultAsync(ct);
        if (row is null) return NotFound();
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Document.Downloaded", "LocalDocument",
            id.ToString(), row.HotelId, row.ConfirmationNumber, reason), ct);
        Response.Headers["X-Document-Hash"] = row.DocumentHash;
        return File(row.PdfData, "application/pdf", row.FileName);
    }

    [HttpGet("email-items/{id:guid}/file")]
    public async Task<IActionResult> EmailItem(Guid id, CancellationToken ct)
    {
        var reason = AuditRequest.ReadReason(HttpContext);
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "Debe indicar el motivo de apertura del archivo." });
        var row = await db.GuestEmailDeliveryItems.AsNoTracking()
            .Include(x => x.Delivery)!.ThenInclude(x => x!.LocalDocument)
            .Include(x => x.CommunicationDocument)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return NotFound();

        await audit.AppendAsync(AuditRequest.Create(HttpContext, "EmailDocument.Opened",
            "GuestEmailDeliveryItem", id.ToString(),
            confirmationNumber: row.Delivery?.ConfirmationNumber, reason: reason), ct);

        if (row.DocumentType == "RegistrationCard" && row.Delivery?.LocalDocument is not null)
            return File(row.Delivery.LocalDocument.PdfData, "application/pdf", row.FileName);
        if (row.GeneratedFileData is not null)
            return File(row.GeneratedFileData, row.ContentType, row.FileName);
        if (row.CommunicationDocument?.FileData is not null)
            return File(row.CommunicationDocument.FileData,
                row.CommunicationDocument.ContentType, row.FileName);
        if (Uri.TryCreate(row.CommunicationDocument?.SourceUrl, UriKind.Absolute, out var remote))
            return Redirect(remote.ToString());
        return NotFound(new { message = "El archivo relacionado ya no está disponible." });
    }

    [Authorize(Policy = "Documents.Seal")]
    [HttpPost("reservation-files/{id:guid}/seal")]
    public async Task<IActionResult> Seal(
        Guid id,
        [FromBody] SealReservationFileRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            return BadRequest(new { message = "El motivo del sellado debe tener al menos 10 caracteres." });
        var file = await db.ReservationFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (file is null) return NotFound();
        var username = User.FindFirst("username")?.Value ?? User.Identity?.Name ?? "unknown";
        await reservationFiles.SealAsync(file, username, ct);
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "ReservationFile.Sealed", "ReservationFile",
            file.Id.ToString(), file.HotelId, file.ConfirmationNumber, request.Reason), ct);
        return Ok(new { file.Id, file.Status, file.ManifestHash, file.SealedAtUtc });
    }
}

public sealed record SealReservationFileRequest(string Reason);
