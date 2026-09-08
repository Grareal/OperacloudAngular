namespace FirmaOperaCloud.Domain.Entities;

/// <summary>Evento inmutable de auditoría enlazado criptográficamente con el anterior.</summary>
public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public long? ActorUserId { get; set; }
    public string ActorUsername { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? HotelId { get; set; }
    public string? ConfirmationNumber { get; set; }
    public string? AccessReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Outcome { get; set; } = "Success";
    public string? DetailJson { get; set; }
    public string? PreviousHash { get; set; }
    public string EventHash { get; set; } = string.Empty;
}
