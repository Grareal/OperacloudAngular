using System.Net;
using System.Security.Cryptography;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Api.Services;

public sealed class GuestEmailOptions
{
    public const string SectionName = "GuestEmail";
    public bool Enabled { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Vidanta";
    public string Subject { get; set; } = "Documentos de su registro Vidanta - Reserva {confirmation}";
    public string BodyHtml { get; set; } = "<p>Estimado(a) {guest},</p><p>Adjuntamos los documentos relacionados con su registro y estancia.</p><p>Reserva: <strong>{confirmation}</strong></p>";
    public int MaxAttempts { get; set; } = 5;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class GuestEmailQueueService(FirmaOperaCloudDbContext db, PdfTemplateRenderService renderer)
{
    public async Task<GuestEmailDelivery?> QueueAsync(LocalDocument document, Reservation reservation, string? recipient,
        bool marketingConsent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return null;
        if (!System.Net.Mail.MailAddress.TryCreate(recipient.Trim(), out _)) return null;
        var normalizedLanguage = NormalizeLanguage(reservation.Guest.Language);
        var now = DateTime.UtcNow;
        var documents = await db.CommunicationDocuments.Where(x => x.HotelId == document.HotelId && x.IsPublished
            && (x.Language == normalizedLanguage || x.Language == "ALL")
            && (!x.EffectiveFromUtc.HasValue || x.EffectiveFromUtc <= now)
            && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc >= now)
            && (!x.RequiresMarketingConsent || marketingConsent)).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
        var delivery = new GuestEmailDelivery
        {
            LocalDocumentId = document.Id, HotelId = document.HotelId, ConfirmationNumber = document.ConfirmationNumber,
            GuestName = reservation.Guest.FullName, RecipientEmail = recipient.Trim(), Language = normalizedLanguage, MarketingConsent = marketingConsent
        };
        delivery.Items.Add(new GuestEmailDeliveryItem { DocumentType = "RegistrationCard", Name = "Tarjeta de registro firmada",
            Version = document.Version, FileName = document.FileName, DocumentHash = document.DocumentHash });
        foreach (var item in documents) delivery.Items.Add(new GuestEmailDeliveryItem { CommunicationDocument = item,
            DocumentType = item.Type, Name = item.Name, Version = item.Version, FileName = item.FileName, DocumentHash = item.DocumentHash ?? string.Empty });
        if (marketingConsent)
        {
            var promotionalTemplates = await db.PdfTemplates.Include(x => x.Fields).Where(x => x.HotelId == document.HotelId && x.IsPublished
                && x.TemplateType == PdfTemplateTypes.Promotion && (x.Language == null || x.Language == normalizedLanguage || x.Language == "ALL"))
                .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.Version).ToListAsync(ct);
            foreach (var template in promotionalTemplates.GroupBy(x => x.Name).Select(x => x.First()))
            {
                var generated = renderer.Render(template, reservation);
                delivery.Items.Add(new GuestEmailDeliveryItem { PdfTemplate = template, DocumentType = CommunicationDocumentTypes.Promotion,
                    Name = template.Name, Version = template.Version, FileName = $"{Safe(template.Name)}-{document.ConfirmationNumber}.pdf",
                    ContentType = "application/pdf", GeneratedFileData = generated, DocumentHash = Convert.ToHexString(SHA256.HashData(generated)) });
            }
        }
        db.GuestEmailDeliveries.Add(delivery); await db.SaveChangesAsync(ct); return delivery;
    }

    private static string NormalizeLanguage(string? language) => language?.Trim().ToUpperInvariant() switch
    {
        "ES" or "ESP" or "S" or "SPANISH" or "ESPANOL" or "ESPAÑOL" => "ES",
        _ => "EN"
    };
    private static string Safe(string value) => string.Concat(value.Select(x => char.IsLetterOrDigit(x) ? x : '-')).Trim('-');
}

public sealed class GuestEmailWorker(IServiceScopeFactory scopes, IOptions<GuestEmailOptions> options,
    IHttpClientFactory clients, IGuestEmailSender sender, ILogger<GuestEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Error procesando la cola de correos del huésped."); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FirmaOperaCloudDbContext>();
        var candidates = await db.GuestEmailDeliveries.Where(x => x.Status == "Pending" || x.Status == "Retry")
            .OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Id, x.HotelId, x.Status, x.AttemptCount, x.LastAttemptAtUtc })
            .Take(100).ToListAsync(ct);
        var pending = candidates.Where(x => IsDue(x.Status, x.LastAttemptAtUtc, x.AttemptCount)).Take(20);
        foreach (var item in pending)
        {
            var saved = await db.GuestEmailSettings.AsNoTracking().FirstOrDefaultAsync(x => x.HotelId == item.HotelId, ct);
            var cfg = saved is null ? options.Value : From(saved);
            if (!cfg.Enabled || !sender.IsConfigured(cfg) || item.AttemptCount >= cfg.MaxAttempts) continue;
            await SendAsync(db, item.Id, cfg, ct);
        }
    }

    private static bool IsDue(string status, DateTime? lastAttemptAtUtc, int attemptCount)
    {
        if (status == "Pending" || lastAttemptAtUtc is null) return true;
        var seconds = Math.Min(900, 15 * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return lastAttemptAtUtc <= DateTime.UtcNow.AddSeconds(-seconds);
    }

    private static GuestEmailOptions From(GuestEmailSetting x) => new()
    {
        Enabled=x.Enabled, Host=x.Host, Port=x.Port, EnableSsl=x.EnableSsl, Username=x.Username, Password=x.Password,
        FromAddress=x.FromAddress, FromName=x.FromName, Subject=x.Subject, BodyHtml=x.BodyHtml, MaxAttempts=x.MaxAttempts
    };

    private async Task SendAsync(FirmaOperaCloudDbContext db, Guid id, GuestEmailOptions cfg, CancellationToken ct)
    {
        var row = await db.GuestEmailDeliveries.Include(x => x.LocalDocument).Include(x => x.Items).ThenInclude(x => x.CommunicationDocument)
            .FirstAsync(x => x.Id == id, ct);
        row.Status = "Sending"; row.AttemptCount++; row.LastAttemptAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        try
        {
            var attachments = new List<EmailAttachmentData>();
            foreach (var item in row.Items.OrderBy(x => x.DocumentType == "RegistrationCard" ? 0 : 1))
            {
                byte[] bytes; string contentType;
                if (item.DocumentType == "RegistrationCard") { bytes = row.LocalDocument!.PdfData; contentType = "application/pdf"; }
                else if (item.GeneratedFileData is not null) { bytes = item.GeneratedFileData; contentType = item.ContentType; }
                else
                {
                    var document = item.CommunicationDocument ?? throw new InvalidOperationException($"No se encontró {item.Name} v{item.Version}.");
                    contentType = document.ContentType;
                    if (document.FileData is not null) bytes = document.FileData;
                    else
                    {
                        using var response = await clients.CreateClient("GuestDocuments").GetAsync(document.SourceUrl, ct);
                        response.EnsureSuccessStatusCode(); bytes = await response.Content.ReadAsByteArrayAsync(ct);
                        if (bytes.Length > 20_000_000) throw new InvalidDataException($"El documento remoto {document.Name} supera 20 MB.");
                        contentType = response.Content.Headers.ContentType?.MediaType ?? contentType;
                    }
                }
                item.DocumentHash = Convert.ToHexString(SHA256.HashData(bytes));
                attachments.Add(new EmailAttachmentData(item.FileName, contentType, bytes));
            }
            var subject = cfg.Subject.Replace("{confirmation}", row.ConfirmationNumber, StringComparison.OrdinalIgnoreCase);
            var body = cfg.BodyHtml.Replace("{guest}", WebUtility.HtmlEncode(row.GuestName), StringComparison.OrdinalIgnoreCase)
                .Replace("{confirmation}", WebUtility.HtmlEncode(row.ConfirmationNumber), StringComparison.OrdinalIgnoreCase);
            row.ProviderMessageId = await sender.SendAsync(cfg, row.RecipientEmail, subject, body, attachments, ct);
            row.Status = "Sent"; row.SentAtUtc = DateTime.UtcNow;
            row.LastError = null;
            logger.LogInformation("SMTP aceptó el correo de la reserva {Confirmation}; intento {Attempt}.", row.ConfirmationNumber, row.AttemptCount);
        }
        catch (Exception ex)
        {
            row.Status = row.AttemptCount >= cfg.MaxAttempts ? "Failed" : "Retry";
            row.LastError = ex.Message[..Math.Min(2000, ex.Message.Length)];
            logger.LogWarning(ex, "Falló el correo SMTP de la reserva {Confirmation}; estado {Status}, intento {Attempt}/{MaxAttempts}.",
                row.ConfirmationNumber, row.Status, row.AttemptCount, cfg.MaxAttempts);
        }
        await db.SaveChangesAsync(ct);
    }
}
