using System.Security.Cryptography;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Communications.Manage"), Route("api/communication-documents")]
public sealed class CommunicationDocumentsController(FirmaOperaCloudDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string hotelId = "VINV", CancellationToken ct = default) =>
        Ok(await db.CommunicationDocuments.AsNoTracking().Where(x => x.HotelId == hotelId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Type).ThenByDescending(x => x.Version)
            .Select(x => new { x.Id, x.HotelId, x.Type, x.Name, x.Language, x.Version, x.Source, x.SourceUrl, x.FileName,
                x.ContentType, FileSize = x.FileData == null ? 0 : x.FileData.Length, x.DocumentHash, x.IsPublished,
                x.IsRequired, x.RequiresMarketingConsent, x.SortOrder, x.EffectiveFromUtc, x.EffectiveToUtc, x.CreatedAtUtc }).ToListAsync(ct));

    [HttpPost("upload"), RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload([FromForm] CommunicationDocumentUpload request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0) return BadRequest(new { message = "Seleccione un archivo." });
        if (request.File.Length > 20_000_000) return BadRequest(new { message = "El archivo supera 20 MB." });
        await using var stream = request.File.OpenReadStream(); using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var version = (await db.CommunicationDocuments.Where(x => x.HotelId == request.HotelId && x.Type == request.Type && x.Name == request.Name && x.Language == request.Language)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var row = New(request.HotelId, request.Type, request.Name, request.Language, version, request.IsRequired,
            request.RequiresMarketingConsent, request.SortOrder, request.EffectiveFromUtc, request.EffectiveToUtc);
        row.Source = CommunicationDocumentSources.Upload; row.FileName = Path.GetFileName(request.File.FileName);
        row.ContentType = string.IsNullOrWhiteSpace(request.File.ContentType) ? "application/octet-stream" : request.File.ContentType;
        row.FileData = bytes; row.DocumentHash = Convert.ToHexString(SHA256.HashData(bytes)); row.CreatedBy = User.Identity?.Name;
        db.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id, row.Version });
    }

    [HttpPost("remote")]
    public async Task<IActionResult> Remote(CommunicationDocumentRemote request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return BadRequest(new { message = "La fuente remota debe ser una URL HTTPS válida." });
        var version = (await db.CommunicationDocuments.Where(x => x.HotelId == request.HotelId && x.Type == request.Type && x.Name == request.Name && x.Language == request.Language)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var row = New(request.HotelId, request.Type, request.Name, request.Language, version, request.IsRequired,
            request.RequiresMarketingConsent, request.SortOrder, request.EffectiveFromUtc, request.EffectiveToUtc);
        row.Source = CommunicationDocumentSources.RemoteUrl; row.SourceUrl = uri.ToString();
        row.FileName = string.IsNullOrWhiteSpace(request.FileName) ? Path.GetFileName(uri.LocalPath) : Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(row.FileName)) row.FileName = $"documento-{row.Id:N}.pdf";
        row.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/pdf" : request.ContentType;
        row.CreatedBy = User.Identity?.Name; db.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id, row.Version });
    }

    [HttpPut("{id:guid}/publication")]
    public async Task<IActionResult> Publication(Guid id, CommunicationDocumentPublication request, CancellationToken ct)
    {
        var row = await db.CommunicationDocuments.FindAsync([id], ct); if (row is null) return NotFound();
        if (request.IsPublished)
        {
            var previous = await db.CommunicationDocuments.Where(x => x.Id != id && x.HotelId == row.HotelId && x.Type == row.Type && x.Name == row.Name && x.Language == row.Language && x.IsPublished).ToListAsync(ct);
            previous.ForEach(x => x.IsPublished = false);
        }
        row.IsPublished = request.IsPublished; row.IsRequired = request.IsRequired; row.RequiresMarketingConsent = request.RequiresMarketingConsent;
        row.SortOrder = request.SortOrder; row.EffectiveFromUtc = request.EffectiveFromUtc; row.EffectiveToUtc = request.EffectiveToUtc;
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> File(Guid id, CancellationToken ct)
    {
        var row = await db.CommunicationDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
        if (row.FileData is not null) return base.File(row.FileData, row.ContentType, row.FileName);
        return Ok(new { row.SourceUrl });
    }

    [HttpGet("deliveries")]
    public async Task<IActionResult> Deliveries([FromQuery] string hotelId = "VINV", [FromQuery] int limit = 100, CancellationToken ct = default) =>
        Ok(await db.GuestEmailDeliveries.AsNoTracking().Where(x => x.HotelId == hotelId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500))
            .Select(x => new { x.Id, x.ConfirmationNumber, x.GuestName, x.RecipientEmail, x.Status, x.AttemptCount, x.MarketingConsent,
                x.CreatedAtUtc, x.SentAtUtc, x.LastError, ItemCount = x.Items.Count }).ToListAsync(ct));

    [HttpPost("deliveries/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var row = await db.GuestEmailDeliveries.FindAsync([id], ct); if (row is null) return NotFound();
        // Esta acción también permite reenviar una entrega ya enviada. Es una acción
        // deliberada del administrador y puede producir un correo duplicado.
        row.Status = "Pending"; row.AttemptCount = 0; row.LastError = null;
        row.LastAttemptAtUtc = null; row.SentAtUtc = null; row.ProviderMessageId = null;
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private static CommunicationDocument New(string hotel, string type, string name, string language, int version, bool required, bool marketing, int order, DateTime? from, DateTime? to) => new()
    {
        HotelId = hotel.Trim().ToUpperInvariant(), Type = type.Trim(), Name = name.Trim(), Language = language.Trim().ToUpperInvariant(), Version = version,
        IsRequired = required, RequiresMarketingConsent = marketing || type.Equals(CommunicationDocumentTypes.Promotion, StringComparison.OrdinalIgnoreCase), SortOrder = order, EffectiveFromUtc = from, EffectiveToUtc = to
    };
}

public sealed class CommunicationDocumentUpload
{
    public string HotelId { get; set; } = "VINV"; public string Type { get; set; } = CommunicationDocumentTypes.Other;
    public string Name { get; set; } = string.Empty; public string Language { get; set; } = "EN"; public IFormFile? File { get; set; }
    public bool IsRequired { get; set; } public bool RequiresMarketingConsent { get; set; } public int SortOrder { get; set; }
    public DateTime? EffectiveFromUtc { get; set; } public DateTime? EffectiveToUtc { get; set; }
}
public sealed class CommunicationDocumentRemote
{
    public string HotelId { get; set; } = "VINV"; public string Type { get; set; } = CommunicationDocumentTypes.Other;
    public string Name { get; set; } = string.Empty; public string Language { get; set; } = "EN"; public string SourceUrl { get; set; } = string.Empty;
    public string? FileName { get; set; } public string? ContentType { get; set; } public bool IsRequired { get; set; }
    public bool RequiresMarketingConsent { get; set; } public int SortOrder { get; set; } public DateTime? EffectiveFromUtc { get; set; } public DateTime? EffectiveToUtc { get; set; }
}
public sealed class CommunicationDocumentPublication
{
    public bool IsPublished { get; set; } public bool IsRequired { get; set; } public bool RequiresMarketingConsent { get; set; }
    public int SortOrder { get; set; } public DateTime? EffectiveFromUtc { get; set; } public DateTime? EffectiveToUtc { get; set; }
}
