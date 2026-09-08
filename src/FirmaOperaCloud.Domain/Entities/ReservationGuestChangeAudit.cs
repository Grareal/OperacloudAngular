namespace FirmaOperaCloud.Domain.Entities;

/// <summary>Auditoría local de una escritura controlada de acompañantes en OPERA.</summary>
public sealed class ReservationGuestChangeAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string HotelId { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string ReservationId { get; set; } = string.Empty;
    public string RequestedProfileId { get; set; } = string.Empty;
    public string RequestedProfileName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = "Started";
    public string BeforeJson { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
