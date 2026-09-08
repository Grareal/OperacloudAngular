using System.Security.Cryptography;
using System.Text;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Infrastructure.Persistence;

public sealed class AuditService(FirmaOperaCloudDbContext db) : IAuditService
{
    private static readonly SemaphoreSlim AppendLock = new(1, 1);

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        await AppendLock.WaitAsync(cancellationToken);
        try
        {
            var previous = await db.AuditEvents.AsNoTracking()
                .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
                .Select(x => x.EventHash).FirstOrDefaultAsync(cancellationToken);
            var item = new AuditEvent
            {
                ActorUserId = record.ActorUserId,
                ActorUsername = Limit(record.ActorUsername, 200) ?? string.Empty,
                Action = Limit(record.Action, 80) ?? string.Empty,
                ResourceType = Limit(record.ResourceType, 80) ?? string.Empty,
                ResourceId = Limit(record.ResourceId, 128),
                HotelId = Limit(record.HotelId, 20),
                ConfirmationNumber = Limit(record.ConfirmationNumber, 64),
                AccessReason = Limit(record.AccessReason, 500),
                IpAddress = Limit(record.IpAddress, 64),
                UserAgent = Limit(record.UserAgent, 500),
                CorrelationId = Limit(record.CorrelationId, 64) ?? string.Empty,
                Outcome = Limit(record.Outcome, 32) ?? "Success",
                DetailJson = record.DetailJson,
                PreviousHash = previous
            };
            item.EventHash = Hash(item);
            db.AuditEvents.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            AppendLock.Release();
        }
    }

    private static string Hash(AuditEvent x)
    {
        var canonical = string.Join('|', x.Id, x.OccurredAtUtc.ToUniversalTime().ToString("O"),
            x.ActorUserId, x.ActorUsername, x.Action, x.ResourceType, x.ResourceId,
            x.HotelId, x.ConfirmationNumber, x.AccessReason, x.IpAddress, x.UserAgent,
            x.CorrelationId, x.Outcome, x.DetailJson, x.PreviousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string? Limit(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
}
