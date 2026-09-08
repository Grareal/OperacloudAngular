using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using FirmaOperaCloud.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Promotions.Manage"), Route("api/promotion-catalog")]
public sealed class PromotionCatalogController(
    FirmaOperaCloudDbContext db,
    IOperaTokenService tokenService,
    IHttpClientFactory httpClientFactory,
    IOptions<OperaCloudOptions> operaOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string hotelId = "VINV", [FromQuery] string language = "ES",
        [FromQuery] string? search = null, [FromQuery] string status = "All", CancellationToken ct = default)
    {
        var query = db.PromotionCatalogEntries.AsNoTracking()
            .Where(x => x.HotelId == hotelId.ToUpper() && x.Language == language.ToUpper());
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.OperaCode.Contains(search) || x.OperaDescription.Contains(search) ||
                x.GuestTitle.Contains(search) || x.GuestDescription.Contains(search));
        query = status switch
        {
            "Approved" => query.Where(x => x.IsApprovedForGuest && x.IsActive),
            "Pending" => query.Where(x => !x.IsApprovedForGuest),
            "Inactive" => query.Where(x => !x.IsActive),
            _ => query
        };
        return Ok(await query.OrderBy(x => x.SortOrder).ThenBy(x => x.OperaCode).Take(3000).ToListAsync(ct));
    }

    /// <summary>Catálogo oficial Promotion Codes de OPERA Cloud. Solo ejecuta GET.</summary>
    [HttpGet("opera")]
    public async Task<IActionResult> GetOperaPromotionCodes([FromQuery] string? hotelId, CancellationToken ct)
    {
        var options = operaOptions.Value;
        var hotel = string.IsNullOrWhiteSpace(hotelId) ? options.DefaultHotelId : hotelId.Trim().ToUpperInvariant();
        var token = await tokenService.GetAccessTokenAsync(ct);
        var path = $"{options.GatewayUrl}/rtp/v1/hotels/{Uri.EscapeDataString(hotel)}/promotionCodes?limit=1000";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-hotelid", hotel);
        request.Headers.Add("x-app-key", options.AppKey);
        using var response = await httpClientFactory.CreateClient("GuestDocuments").SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, new { message = $"OHIP getPromotionCodes: {(int)response.StatusCode} {response.ReasonPhrase}." });

        using var document = JsonDocument.Parse(json);
        var result = new List<OperaPromotionCodeInfo>();
        if (!document.RootElement.TryGetProperty("propertyPromotionCodes", out var container) ||
            !container.TryGetProperty("propertyPromotionCodes", out var items) || items.ValueKind != JsonValueKind.Array)
            return Ok(result);

        foreach (var item in items.EnumerateArray())
        {
            var detail = item.TryGetProperty("promotionCodeDetails", out var detailElement) ? detailElement : default;
            var name = detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("promotionName", out var promotionName) &&
                promotionName.TryGetProperty("defaultText", out var defaultText) ? defaultText.GetString() : null;
            var rates = new List<OperaPromotionRateInfo>();
            if (item.TryGetProperty("propertyPromotionRateCodes", out var rateItems) && rateItems.ValueKind == JsonValueKind.Array)
                foreach (var rate in rateItems.EnumerateArray()) rates.Add(new(Text(rate, "rateCode"), Text(rate, "rateDescription")));

            result.Add(new OperaPromotionCodeInfo(
                Text(item, "promotionCode"), name ?? string.Empty,
                Text(detail, "promotionGroup"), Text(detail, "promotionGroupName"),
                NestedText(detail, "bookingDate", "startDate"), NestedText(detail, "bookingDate", "endDate"),
                NestedText(detail, "stayDate", "startDate"), NestedText(detail, "stayDate", "endDate"),
                Text(detail, "categoryDesc"), Text(item, "hotelId"), rates));
        }
        return Ok(result.OrderBy(x => x.Code));
    }

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value.GetString() ?? value.ToString() : string.Empty;

    private static string NestedText(JsonElement element, string parent, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(parent, out var nested) ? Text(nested, property) : string.Empty;

    [HttpPost]
    public async Task<IActionResult> Create(PromotionCatalogEdit request, CancellationToken ct)
    {
        var validation = Validate(request); if (validation is not null) return BadRequest(new { message = validation });
        var hotel = request.HotelId.Trim().ToUpperInvariant(); var code = request.OperaCode.Trim().ToUpperInvariant();
        var language = request.Language.Trim().ToUpperInvariant();
        if (await db.PromotionCatalogEntries.AnyAsync(x => x.HotelId == hotel && x.OperaCode == code && x.Language == language, ct))
            return Conflict(new { message = "El código ya existe para este hotel e idioma." });
        var row = Apply(new PromotionCatalogEntry { HotelId = hotel, OperaCode = code, Language = language }, request);
        row.Source = "Manual"; row.UpdatedBy = User.Identity?.Name; db.Add(row); await db.SaveChangesAsync(ct);
        return Ok(row);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PromotionCatalogEdit request, CancellationToken ct)
    {
        var validation = Validate(request); if (validation is not null) return BadRequest(new { message = validation });
        var row = await db.PromotionCatalogEntries.FindAsync([id], ct); if (row is null) return NotFound();
        var hotel = request.HotelId.Trim().ToUpperInvariant(); var code = request.OperaCode.Trim().ToUpperInvariant();
        var language = request.Language.Trim().ToUpperInvariant();
        if (await db.PromotionCatalogEntries.AnyAsync(x => x.Id != id && x.HotelId == hotel && x.OperaCode == code && x.Language == language, ct))
            return Conflict(new { message = "El código ya existe para este hotel e idioma." });
        row.HotelId = hotel; row.OperaCode = code; row.Language = language; Apply(row, request);
        row.UpdatedAtUtc = DateTime.UtcNow; row.UpdatedBy = User.Identity?.Name; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("import"), RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import([FromForm] PromotionCatalogImport request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0) return BadRequest(new { message = "Seleccione el CSV exportado de OPERA." });
        var hotel = request.HotelId.Trim().ToUpperInvariant(); var language = request.Language.Trim().ToUpperInvariant();
        using var reader = new StreamReader(request.File.OpenReadStream(), Encoding.UTF8, true);
        var inserted = 0; var updated = 0; var ignored = 0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var columns = line.Split(';');
            if (columns.Length < 3 || !columns[0].Trim().Equals(request.EntityName.Trim(), StringComparison.OrdinalIgnoreCase)) { ignored++; continue; }
            var code = columns[1].Trim().ToUpperInvariant(); var description = columns[2].Trim();
            if (string.IsNullOrWhiteSpace(code)) { ignored++; continue; }
            var row = db.PromotionCatalogEntries.Local.FirstOrDefault(x => x.HotelId == hotel && x.OperaCode == code && x.Language == language)
                ?? await db.PromotionCatalogEntries.FirstOrDefaultAsync(x => x.HotelId == hotel && x.OperaCode == code && x.Language == language, ct);
            if (row is null)
            {
                db.Add(new PromotionCatalogEntry { HotelId = hotel, OperaCode = code, OperaDescription = description,
                    Language = language, Source = "OperaExport", IsActive = true, IsApprovedForGuest = false,
                    UpdatedBy = User.Identity?.Name }); inserted++;
            }
            else
            {
                row.OperaDescription = description; row.Source = "OperaExport"; row.UpdatedAtUtc = DateTime.UtcNow;
                row.UpdatedBy = User.Identity?.Name; updated++;
            }
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { inserted, updated, ignored, message = $"Importación terminada: {inserted} nuevos y {updated} actualizados. Los nuevos quedaron pendientes de aprobación." });
    }

    private static PromotionCatalogEntry Apply(PromotionCatalogEntry row, PromotionCatalogEdit request)
    {
        row.OperaDescription = request.OperaDescription?.Trim() ?? ""; row.GuestTitle = request.GuestTitle?.Trim() ?? "";
        row.GuestDescription = request.GuestDescription?.Trim() ?? ""; row.IsActive = request.IsActive;
        row.IsApprovedForGuest = request.IsApprovedForGuest; row.SortOrder = request.SortOrder;
        row.EffectiveFromUtc = request.EffectiveFromUtc; row.EffectiveToUtc = request.EffectiveToUtc; return row;
    }
    private static string? Validate(PromotionCatalogEdit request)
    {
        if (string.IsNullOrWhiteSpace(request.HotelId) || string.IsNullOrWhiteSpace(request.OperaCode) || string.IsNullOrWhiteSpace(request.Language))
            return "Hotel, código e idioma son obligatorios.";
        if (request.IsApprovedForGuest && string.IsNullOrWhiteSpace(request.GuestTitle) && string.IsNullOrWhiteSpace(request.GuestDescription))
            return "Para aprobar una promoción debe capturar el texto que verá el huésped.";
        return null;
    }
}

public sealed record OperaPromotionCodeInfo(string Code, string Name, string GroupCode, string GroupName,
    string BookingStartDate, string BookingEndDate, string StayStartDate, string StayEndDate,
    string Description, string HotelId, IReadOnlyList<OperaPromotionRateInfo> Rates);
public sealed record OperaPromotionRateInfo(string Code, string Description);

public sealed class PromotionCatalogEdit
{
    public string HotelId { get; set; } = "VINV"; public string OperaCode { get; set; } = "";
    public string? OperaDescription { get; set; } public string? GuestTitle { get; set; } public string? GuestDescription { get; set; }
    public string Language { get; set; } = "ES"; public bool IsActive { get; set; } = true; public bool IsApprovedForGuest { get; set; }
    public int SortOrder { get; set; } public DateTime? EffectiveFromUtc { get; set; } public DateTime? EffectiveToUtc { get; set; }
}
public sealed class PromotionCatalogImport
{
    public string HotelId { get; set; } = "VINV"; public string Language { get; set; } = "ES";
    public string EntityName { get; set; } = "VIDA_PROMOTIONSTSW"; public IFormFile? File { get; set; }
}
