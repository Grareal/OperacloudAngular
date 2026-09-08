using System.Security.Cryptography;
using System.Text;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Audit.Read"), Route("api/audit")]
public sealed class AuditController(FirmaOperaCloudDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? confirmationNumber,
        [FromQuery] string? actor,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(confirmationNumber))
            query = query.Where(x => x.ConfirmationNumber == confirmationNumber.Trim());
        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(x => x.ActorUsername.Contains(actor.Trim()));
        if (fromUtc.HasValue) query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value.ToUniversalTime());
        if (toUtc.HasValue) query = query.Where(x => x.OccurredAtUtc <= toUtc.Value.ToUniversalTime());

        return Ok(await query.OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(x => new
            {
                x.Id, x.OccurredAtUtc, x.ActorUsername, x.Action, x.ResourceType,
                x.ResourceId, x.HotelId, x.ConfirmationNumber, x.AccessReason,
                x.IpAddress, x.CorrelationId, x.Outcome, x.EventHash
            }).ToListAsync(ct));
    }

    [HttpGet("integrity")]
    public async Task<IActionResult> Integrity(CancellationToken ct)
    {
        var events = await db.AuditEvents.AsNoTracking()
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        string? previous = null;
        foreach (var item in events)
        {
            if (!string.Equals(previous, item.PreviousHash, StringComparison.Ordinal) ||
                !string.Equals(Hash(item), item.EventHash, StringComparison.Ordinal))
                return Ok(new { valid = false, brokenAt = item.Id, count = events.Count });
            previous = item.EventHash;
        }
        return Ok(new { valid = true, brokenAt = (Guid?)null, count = events.Count });
    }

    private static string Hash(FirmaOperaCloud.Domain.Entities.AuditEvent x)
    {
        var canonical = string.Join('|', x.Id, x.OccurredAtUtc.ToUniversalTime().ToString("O"),
            x.ActorUserId, x.ActorUsername, x.Action, x.ResourceType, x.ResourceId,
            x.HotelId, x.ConfirmationNumber, x.AccessReason, x.IpAddress, x.UserAgent,
            x.CorrelationId, x.Outcome, x.DetailJson, x.PreviousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
