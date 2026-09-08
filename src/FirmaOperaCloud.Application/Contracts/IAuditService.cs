namespace FirmaOperaCloud.Application.Contracts;

public sealed record AuditRecord(
    long? ActorUserId,
    string ActorUsername,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? HotelId,
    string? ConfirmationNumber,
    string? AccessReason,
    string? IpAddress,
    string? UserAgent,
    string CorrelationId,
    string Outcome = "Success",
    string? DetailJson = null);

public interface IAuditService
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken);
}
