using System.Net;
using System.Net.Mail;

namespace FirmaOperaCloud.Api.Services;

public sealed record EmailAttachmentData(string FileName, string ContentType, byte[] Content);

public interface IGuestEmailSender
{
    bool IsConfigured(GuestEmailOptions options);
    Task<string?> SendAsync(GuestEmailOptions options, string recipient, string subject, string htmlBody,
        IReadOnlyList<EmailAttachmentData> attachments, CancellationToken ct);
}

/// <summary>Envía los paquetes documentales mediante un servidor SMTP autenticado.</summary>
public sealed class SmtpEmailSender : IGuestEmailSender
{
    public bool IsConfigured(GuestEmailOptions options) =>
        !string.IsNullOrWhiteSpace(options.Host) && options.Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(options.FromAddress);

    public async Task<string?> SendAsync(GuestEmailOptions options, string recipient, string subject, string htmlBody,
        IReadOnlyList<EmailAttachmentData> attachments, CancellationToken ct)
    {
        if (!IsConfigured(options)) throw new InvalidOperationException("SMTP no está configurado. Indique servidor, puerto y remitente.");
        if (!MailAddress.TryCreate(options.FromAddress, out var from)) throw new InvalidOperationException("El remitente SMTP no es válido.");
        if (!MailAddress.TryCreate(recipient, out var to)) throw new InvalidOperationException("El destinatario no es válido.");

        using var message = new MailMessage
        {
            From = new MailAddress(from.Address, options.FromName), Subject = subject, Body = htmlBody, IsBodyHtml = true,
            SubjectEncoding = System.Text.Encoding.UTF8, BodyEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(to);
        foreach (var attachment in attachments)
        {
            var stream = new MemoryStream(attachment.Content, writable: false);
            message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
        }
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network, UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(options.Username) ? null : new NetworkCredential(options.Username, options.Password)
        };
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message).WaitAsync(ct);
        return null;
    }
}
