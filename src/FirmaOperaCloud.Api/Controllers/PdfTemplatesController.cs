using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Infrastructure.Persistence;
using FirmaOperaCloud.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "PdfTemplates.Manage"), Route("api/pdf-templates")]
public sealed class PdfTemplatesController(FirmaOperaCloudDbContext db, IReservationService reservations, PdfTemplateRenderService renderer) : ControllerBase
{
    public static readonly object[] FieldCatalog =
    [
        new { key = "ConfirmationNumber", label = "Número de confirmación", type = "Text" },
        new { key = "GuestFullName", label = "Nombre del huésped", type = "Text" },
        new { key = "ArrivalDate", label = "Fecha de llegada", type = "Text" },
        new { key = "DepartureDate", label = "Fecha de salida", type = "Text" },
        new { key = "RoomNumber", label = "Habitación", type = "Text" },
        new { key = "RoomType", label = "Tipo de habitación", type = "Text" },
        new { key = "Adults", label = "Adultos", type = "Text" },
        new { key = "Children", label = "Menores", type = "Text" },
        new { key = "Email", label = "Correo", type = "Text" },
        new { key = "Phone", label = "Teléfono", type = "Text" },
        new { key = "City", label = "Ciudad", type = "Text" },
        new { key = "State", label = "Estado", type = "Text" },
        new { key = "Country", label = "País", type = "Text" },
        new { key = "Citizenship", label = "Nacionalidad", type = "Text" },
        new { key = "RateAmount", label = "Tarifa", type = "Text" },
        new { key = "PrimarySignature", label = "Firma principal", type = "Signature" },
        new { key = "OccupantName", label = "Nombre de acompañante", type = "Text" },
        new { key = "OccupantSignature", label = "Firma de acompañante", type = "Signature" }
        ,new { key = "UDFC02", label = "UDFC02 · Códigos de promociones", type = "Multiline" }
        ,new { key = "UDFC16", label = "UDFC16 · Beneficios descritos", type = "Multiline" }
        ,new { key = "UDFC20", label = "UDFC20 · Contexto técnico", type = "Multiline" }
        ,new { key = "PromotionCampaigns", label = "Promociones interpretadas (UDFC02)", type = "Multiline" }
        ,new { key = "PromotionBenefits", label = "Beneficios para el huésped (UDFC16)", type = "Multiline" }
        ,new { key = "PromotionSource", label = "Origen de promociones (UDFC24)", type = "Text" }
        ,new { key = "PromotionClassification", label = "Clasificación (UDFC09)", type = "Text" }
    ];

    [HttpGet("catalog")]
    public IActionResult Catalog() => Ok(FieldCatalog);

    [HttpGet("settings/{hotelId}")]
    public async Task<IActionResult> GetSettings(string hotelId, CancellationToken ct)
    {
        var normalized = hotelId.Trim().ToUpperInvariant();
        var row = await db.HotelDocumentSettings.AsNoTracking().FirstOrDefaultAsync(x => x.HotelId == normalized, ct);
        return Ok(new HotelDocumentSettingResponse(normalized, row?.SourceMode ?? DocumentSourceModes.Auto, row?.PreferredPdfTemplateId));
    }

    [HttpPut("settings/{hotelId}")]
    public async Task<IActionResult> SaveSettings(string hotelId, SaveHotelDocumentSettingRequest request, CancellationToken ct)
    {
        var normalized = hotelId.Trim().ToUpperInvariant();
        var allowed = new[] { DocumentSourceModes.Auto, DocumentSourceModes.Local, DocumentSourceModes.Opera };
        var mode = allowed.FirstOrDefault(x => x.Equals(request.SourceMode, StringComparison.OrdinalIgnoreCase));
        if (mode is null) return BadRequest(new { message = "Modo de documento inválido." });
        if (request.PreferredPdfTemplateId.HasValue && !await db.PdfTemplates.AnyAsync(x => x.Id == request.PreferredPdfTemplateId && x.HotelId == normalized && x.IsPublished && x.TemplateType == PdfTemplateTypes.RegistrationCard, ct))
            return BadRequest(new { message = "La plantilla preferida debe estar publicada y pertenecer al hotel." });
        var row = await db.HotelDocumentSettings.FirstOrDefaultAsync(x => x.HotelId == normalized, ct);
        if (row is null) { row = new HotelDocumentSetting { HotelId = normalized }; db.HotelDocumentSettings.Add(row); }
        row.SourceMode = mode; row.PreferredPdfTemplateId = request.PreferredPdfTemplateId; row.UpdatedAtUtc = DateTime.UtcNow; row.UpdatedBy = User.Identity?.Name;
        await db.SaveChangesAsync(ct); return Ok(new HotelDocumentSettingResponse(normalized, row.SourceMode, row.PreferredPdfTemplateId));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? hotelId, CancellationToken ct) => Ok(await db.PdfTemplates.AsNoTracking()
        .Where(x => hotelId == null || x.HotelId == hotelId).OrderBy(x => x.HotelId).ThenBy(x => x.Name).ThenByDescending(x => x.Version)
        .Select(x => new PdfTemplateSummary(x.Id, x.HotelId, x.Name, x.TemplateType, x.Version, x.FileName, x.IsPublished, x.IsDefault, x.RoomTypePrefix, x.Language, x.CreatedAtUtc, x.Fields.Count))
        .ToListAsync(ct));

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload([FromForm] string hotelId, [FromForm] string name, [FromForm] string? templateType, IFormFile file, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hotelId) || string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Hotel y nombre son obligatorios." });
        if (file.Length < 4 || file.Length > 20_000_000) return BadRequest(new { message = "El PDF debe medir entre 4 bytes y 20 MB." });
        await using var stream = file.OpenReadStream(); using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F') return BadRequest(new { message = "El archivo no es un PDF válido." });
        var normalizedHotel = hotelId.Trim().ToUpperInvariant(); var normalizedName = name.Trim(); var normalizedType = NormalizeType(templateType);
        var version = (await db.PdfTemplates.Where(x => x.HotelId == normalizedHotel && x.TemplateType == normalizedType && x.Name == normalizedName).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var template = new PdfTemplate { HotelId = normalizedHotel, Name = normalizedName, TemplateType = normalizedType, Version = version, FileName = Path.GetFileName(file.FileName), PdfData = bytes, CreatedBy = User.Identity?.Name };
        db.PdfTemplates.Add(template); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, new { template.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await db.PdfTemplates.AsNoTracking().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? NotFound() : Ok(new PdfTemplateDetail(row.Id, row.HotelId, row.Name, row.TemplateType, row.Version, row.FileName, row.IsPublished, row.IsDefault, row.RoomTypePrefix, row.Language,
            row.Fields.OrderBy(x => x.PageNumber).Select(ToField).ToList()));
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var row = await db.PdfTemplates.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.PdfData, x.FileName }).FirstOrDefaultAsync(ct);
        return row is null ? NotFound() : File(row.PdfData, "application/pdf", row.FileName, enableRangeProcessing: true);
    }

    [HttpPut("{id:guid}/fields")]
    public async Task<IActionResult> SaveFields(Guid id, List<SavePdfTemplateFieldRequest> request, CancellationToken ct)
    {
        if (!await db.PdfTemplates.AnyAsync(x => x.Id == id, ct)) return NotFound();
        if (request.Count > 250) return BadRequest(new { message = "La plantilla no puede contener más de 250 campos." });
        foreach (var x in request)
        {
            if (string.IsNullOrWhiteSpace(x.FieldKey) || x.XPercent is < 0 or > 100 || x.YPercent is < 0 or > 100 ||
                x.WidthPercent <= 0 || x.HeightPercent <= 0 || x.XPercent + x.WidthPercent > 100.5 || x.YPercent + x.HeightPercent > 100.5)
                return BadRequest(new { message = $"Coordenadas inválidas para {x.Label}." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.PdfTemplateFields.Where(x => x.PdfTemplateId == id).ExecuteDeleteAsync(ct);
        var fields = request.Select(x => new PdfTemplateField { PdfTemplateId = id, FieldKey = x.FieldKey, Label = x.Label, FieldType = x.FieldType,
                PageNumber = Math.Max(1, x.PageNumber), XPercent = x.XPercent, YPercent = x.YPercent,
                WidthPercent = x.WidthPercent, HeightPercent = x.HeightPercent, FontSize = x.FontSize, OccupantIndex = x.OccupantIndex }).ToList();
        db.PdfTemplateFields.AddRange(fields);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(fields.Select(ToField));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, PublishPdfTemplateRequest request, CancellationToken ct)
    {
        var template = await db.PdfTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        if (request.IsDefault)
            await db.PdfTemplates.Where(x => x.HotelId == template.HotelId && x.TemplateType == template.TemplateType && x.IsDefault).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false), ct);
        template.IsPublished = true; template.IsDefault = request.IsDefault;
        template.RoomTypePrefix = string.IsNullOrWhiteSpace(request.RoomTypePrefix) ? null : request.RoomTypePrefix.Trim().ToUpperInvariant();
        template.Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct); return Ok();
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var template = await db.PdfTemplates.FirstOrDefaultAsync(x => x.Id == id, ct); if (template is null) return NotFound();
        template.IsPublished = false; template.IsDefault = false;
        await db.HotelDocumentSettings.Where(x => x.PreferredPdfTemplateId == id).ExecuteUpdateAsync(
            updates => updates
                .SetProperty(x => x.PreferredPdfTemplateId, (Guid?)null)
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedBy, User.Identity != null ? User.Identity.Name : null), ct);
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("{id:guid}/render/{confirmationNumber}")]
    public async Task<IActionResult> Render(Guid id, string confirmationNumber, CancellationToken ct)
    {
        var template = await db.PdfTemplates.AsNoTracking().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        var reservation = (await reservations.GetByConfirmationNumberAsync(template.HotelId, confirmationNumber, ct)).FirstOrDefault();
        if (reservation is null) return NotFound(new { message = $"No se encontró la reserva {confirmationNumber}." });
        var signatures = await db.StoredSignatures.AsNoTracking().Where(x => x.HotelId == template.HotelId &&
            x.ConfirmationNumber == confirmationNumber && x.IsActive).OrderBy(x => x.SignedAtUtc).ToListAsync(ct);
        return File(renderer.Render(template, reservation, signatures), "application/pdf", $"{template.Name}-{confirmationNumber}-PREVIEW.pdf");
    }

    private static string ResolveValue(PdfTemplateField field, Reservation r, List<StoredSignature> signatures) => field.FieldKey switch
    {
        "ConfirmationNumber" => r.ConfirmationNumber ?? "", "GuestFullName" => r.Guest.FullName,
        "ArrivalDate" => r.RoomStay.ArrivalDate, "DepartureDate" => r.RoomStay.DepartureDate,
        "RoomNumber" => r.RoomStay.RoomId, "RoomType" => r.RoomStay.RoomType,
        "Adults" => r.RoomStay.AdultCount.ToString(), "Children" => r.RoomStay.ChildCount.ToString(),
        "Email" => r.Guest.Email, "Phone" => r.Guest.PhoneNumber, "City" => r.Guest.Address.City,
        "State" => r.Guest.Address.StateProvCode, "Country" => r.Guest.Address.CountryCode,
        "RateAmount" => $"{r.RoomStay.RateAmount:0.00} {r.RoomStay.CurrencyCode}",
        "OccupantName" => signatures.Where(x => x.SignerRole == "Occupant").Skip(Math.Max(0, (field.OccupantIndex ?? 1) - 1)).Select(x => x.SignerName).FirstOrDefault()
            ?? r.AccompanyingGuestNames.Skip(Math.Max(0, (field.OccupantIndex ?? 1) - 1)).FirstOrDefault() ?? "",
        _ => ""
    };

    private static void DrawImage(XGraphics graphics, byte[] bytes, XRect box)
    {
        using var stream = new MemoryStream(bytes, false); using var image = XImage.FromStream(stream);
        var scale = Math.Min(box.Width / image.PointWidth, box.Height / image.PointHeight);
        var width = image.PointWidth * scale; var height = image.PointHeight * scale;
        graphics.DrawImage(image, box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
    }

    private static PdfTemplateFieldResponse ToField(PdfTemplateField x) => new(x.Id, x.FieldKey, x.Label, x.FieldType,
        x.PageNumber, x.XPercent, x.YPercent, x.WidthPercent, x.HeightPercent, x.FontSize, x.OccupantIndex);
    private static string NormalizeType(string? value) => value switch
    {
        PdfTemplateTypes.Promotion => PdfTemplateTypes.Promotion,
        PdfTemplateTypes.PrivacyNotice => PdfTemplateTypes.PrivacyNotice,
        PdfTemplateTypes.Regulations => PdfTemplateTypes.Regulations,
        PdfTemplateTypes.Other => PdfTemplateTypes.Other,
        _ => PdfTemplateTypes.RegistrationCard
    };
}

public sealed record PdfTemplateSummary(Guid Id, string HotelId, string Name, string TemplateType, int Version, string FileName, bool IsPublished, bool IsDefault, string? RoomTypePrefix, string? Language, DateTime CreatedAtUtc, int FieldCount);
public sealed record PdfTemplateDetail(Guid Id, string HotelId, string Name, string TemplateType, int Version, string FileName, bool IsPublished, bool IsDefault, string? RoomTypePrefix, string? Language, List<PdfTemplateFieldResponse> Fields);
public sealed record PdfTemplateFieldResponse(Guid Id, string FieldKey, string Label, string FieldType, int PageNumber, double XPercent, double YPercent, double WidthPercent, double HeightPercent, double FontSize, int? OccupantIndex);
public sealed record SavePdfTemplateFieldRequest(string FieldKey, string Label, string FieldType, int PageNumber, double XPercent, double YPercent, double WidthPercent, double HeightPercent, double FontSize, int? OccupantIndex);
public sealed record PublishPdfTemplateRequest(bool IsDefault, string? RoomTypePrefix, string? Language);
public sealed record HotelDocumentSettingResponse(string HotelId, string SourceMode, Guid? PreferredPdfTemplateId);
public sealed record SaveHotelDocumentSettingRequest(string SourceMode, Guid? PreferredPdfTemplateId);
