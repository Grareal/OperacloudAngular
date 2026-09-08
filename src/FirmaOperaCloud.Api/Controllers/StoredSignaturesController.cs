using FirmaOperaCloud.Infrastructure.Persistence;
using FirmaOperaCloud.Api.Services;
using FirmaOperaCloud.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Signatures.Read"), Route("api/stored-signatures")]
public sealed class StoredSignaturesController(
    FirmaOperaCloudDbContext db,
    IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string term, CancellationToken cancellationToken)
    {
        term = term.Trim();
        if (term.Length < 2) return BadRequest(new { message = "Capture al menos dos caracteres." });
        var rows = await db.StoredSignatures.AsNoTracking()
            .Where(item => item.IsActive && (item.ConfirmationNumber.Contains(term) ||
                (item.RoomNumber != null && item.RoomNumber.Contains(term)) || item.SignerName.Contains(term)))
            .OrderByDescending(item => item.SignedAtUtc).Take(200)
            .Select(item => new StoredSignatureSummary(item.Id, item.ConfirmationNumber, item.RoomNumber,
                item.SignerName, item.SignerKey, item.OperaProfileId, item.SignerRole,
                item.SignatureHash, item.SignedAtUtc,
                db.LocalDocumentSignatures.Where(link => link.StoredSignatureId == item.Id).Select(link => (Guid?)link.LocalDocumentId).FirstOrDefault(),
                db.LocalDocumentSignatures.Where(link => link.StoredSignatureId == item.Id).Select(link => (int?)link.LocalDocument!.Version).FirstOrDefault(),
                db.LocalDocumentSignatures.Where(link => link.StoredSignatureId == item.Id).Select(link => link.LocalDocument!.AttachmentId).FirstOrDefault())).ToListAsync(cancellationToken);
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Signatures.Searched", "StoredSignature",
            reason: AuditRequest.ReadReason(HttpContext) ?? "Consulta operativa de firmas"), cancellationToken);
        return Ok(rows);
    }

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var reason = AuditRequest.ReadReason(HttpContext);
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "Debe indicar el motivo de consulta de la firma." });
        var row = await db.StoredSignatures.AsNoTracking().Where(item => item.Id == id && item.IsActive)
            .Select(item => new { item.SignaturePng, item.HotelId, item.ConfirmationNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Signature.Viewed", "StoredSignature",
            id.ToString(), row.HotelId, row.ConfirmationNumber, reason), cancellationToken);
        return File(row.SignaturePng, "image/png");
    }
}

public sealed record StoredSignatureSummary(Guid Id, string ConfirmationNumber, string? RoomNumber,
    string SignerName, string SignerKey, string? OperaProfileId, string SignerRole,
    string SignatureHash, DateTime SignedAtUtc, Guid? LocalDocumentId, int? DocumentVersion, string? AttachmentId);
