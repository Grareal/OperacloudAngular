using FirmaOperaCloud.Application.Contracts;

namespace FirmaOperaCloud.Api.Services;

/// <summary>Registra transversalmente cada operación autenticada de la API.</summary>
public sealed class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        var started = DateTime.UtcNow;
        Exception? failure = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            if (ShouldAudit(context))
            {
                try
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    var outcome = failure is not null ? "Exception" :
                        context.Response.StatusCode >= 400 ? $"Http{context.Response.StatusCode}" : "Success";
                    await audit.AppendAsync(AuditRequest.Create(context,
                        $"Http.{context.Request.Method}", "ApiEndpoint", path,
                        reason: AuditRequest.ReadReason(context), outcome: outcome,
                        detailJson: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            Method = context.Request.Method,
                            Path = path,
                            StatusCode = context.Response.StatusCode,
                            DurationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds
                        })), context.RequestAborted);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No fue posible registrar la auditoría de {Method} {Path}",
                        context.Request.Method, context.Request.Path);
                }
            }
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.Request.Path.StartsWithSegments("/api")) return false;
        return !context.Request.Path.StartsWithSegments("/api/audit") &&
               !context.Request.Path.StartsWithSegments("/api/auth") &&
               !context.Request.Path.StartsWithSegments("/api/environment");
    }
}
