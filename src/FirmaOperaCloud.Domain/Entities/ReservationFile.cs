namespace FirmaOperaCloud.Domain.Entities;

public static class ReservationFileStatuses
{
    public const string Open = "Open";
    public const string Sealed = "Sealed";
}

/// <summary>Expediente lógico versionado de una reserva.</summary>
public sealed class ReservationFile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string HotelId { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string? ReservationId { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = ReservationFileStatuses.Open;
    public string ManifestJson { get; set; } = "[]";
    public string ManifestHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SealedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? SealedBy { get; set; }
    public ICollection<LocalDocument> Documents { get; set; } = [];
}
