namespace FirmaOperaCloud.Domain.Entities;

/// <summary>
/// Traducción local y aprobada de un código promocional recibido desde OPERA.
/// Nunca modifica la LOV ni la reservación de OPERA.
/// </summary>
public sealed class PromotionCatalogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HotelId { get; set; } = "VINV";
    public string OperaCode { get; set; } = string.Empty;
    public string OperaDescription { get; set; } = string.Empty;
    public string GuestTitle { get; set; } = string.Empty;
    public string GuestDescription { get; set; } = string.Empty;
    public string Language { get; set; } = "ES";
    public bool IsActive { get; set; } = true;
    public bool IsApprovedForGuest { get; set; }
    public int SortOrder { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string Source { get; set; } = "Manual";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
