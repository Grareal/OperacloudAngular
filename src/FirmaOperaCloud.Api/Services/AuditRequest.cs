using System.Security.Claims;
using FirmaOperaCloud.Application.Contracts;

namespace FirmaOperaCloud.Api.Services;

public static class AuditRequest
{
    public const string ReasonHeader = "X-Access-Reason";

    public static string? ReadReason(HttpContext context) =>
        context.Request.Headers.TryGetValue(ReasonHeader, out var value) &&
        !string.IsNullOrWhiteSpace(value)
            ? value.ToString().Trim()
            : null;

    public static AuditRecord Create(
        HttpContext context,
        string action,
        string resourceType,
        string? resourceId = null,
        string? hotelId = null,
        string? confirmationNumber = null,
        string? reason = null,
        string outcome = "Success",
        string? detailJson = null) => new(
            long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null,
            context.User.FindFirstValue("username") ?? context.User.Identity?.Name ?? "anonymous",
            action,
            resourceType,
            resourceId,
            hotelId,
            confirmationNumber,
            reason ?? ReadReason(context),
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            context.TraceIdentifier,
            outcome,
            detailJson);
}
