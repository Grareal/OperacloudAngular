namespace FirmaOperaCloud.Domain.Entities;

public static class CommunicationDocumentTypes
{
    public const string PrivacyNotice = "PrivacyNotice";
    public const string Regulations = "Regulations";
    public const string Promotion = "Promotion";
    public const string Other = "Other";
}

public static class CommunicationDocumentSources
{
    public const string Upload = "Upload";
    public const string RemoteUrl = "RemoteUrl";
}

/// <summary>Documento complementario administrado localmente y seleccionado al enviar el paquete al huésped.</summary>
public sealed class CommunicationDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HotelId { get; set; } = string.Empty;
    public string Type { get; set; } = CommunicationDocumentTypes.Other;
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "EN";
    public int Version { get; set; } = 1;
    public string Source { get; set; } = CommunicationDocumentSources.Upload;
    public string? SourceUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public byte[]? FileData { get; set; }
    public string? DocumentHash { get; set; }
    public bool IsPublished { get; set; }
    public bool IsRequired { get; set; }
    public bool RequiresMarketingConsent { get; set; }
    public int SortOrder { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

public sealed class GuestEmailDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocalDocumentId { get; set; }
    public string HotelId { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Language { get; set; } = "EN";
    public bool MarketingConsent { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public LocalDocument? LocalDocument { get; set; }
    public ICollection<GuestEmailDeliveryItem> Items { get; set; } = [];
}

public sealed class GuestEmailDeliveryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuestEmailDeliveryId { get; set; }
    public Guid? CommunicationDocumentId { get; set; }
    public Guid? PdfTemplateId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DocumentHash { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public byte[]? GeneratedFileData { get; set; }
    public GuestEmailDelivery? Delivery { get; set; }
    public CommunicationDocument? CommunicationDocument { get; set; }
    public PdfTemplate? PdfTemplate { get; set; }
}

public sealed class GuestEmailSetting
{
    public string HotelId { get; set; } = "VINV";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Vidanta";
    public string Subject { get; set; } = "Documentos de su registro Vidanta - Reserva {confirmation}";
    public string BodyHtml { get; set; } = "<p>Estimado(a) {guest},</p><p>Adjuntamos los documentos relacionados con su registro y estancia.</p><p>Reserva: <strong>{confirmation}</strong></p>";
    public int MaxAttempts { get; set; } = 5;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
