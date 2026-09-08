namespace FirmaOperaCloud.Domain.Entities;

/// <summary>Firma capturada y conservada exclusivamente en el sistema local.</summary>
public sealed class StoredSignature
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string HotelId { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string? ReservationId { get; set; }
    public string? RoomNumber { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignerKey { get; set; } = string.Empty;
    public string? OperaProfileId { get; set; }
    public string SignerRole { get; set; } = "Occupant";
    public byte[] SignaturePng { get; set; } = [];
    public string SignatureHash { get; set; } = string.Empty;
    public DateTime SignedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CapturedBy { get; set; }
    public bool IsActive { get; set; } = true;

}
