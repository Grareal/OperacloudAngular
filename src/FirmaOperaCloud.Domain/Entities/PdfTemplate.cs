namespace FirmaOperaCloud.Domain.Entities;

/// <summary>Plantilla PDF y mapeo administrados únicamente por FirmaOperaCloud.</summary>
public sealed class PdfTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HotelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = PdfTemplateTypes.RegistrationCard;
    public int Version { get; set; } = 1;
    public string FileName { get; set; } = string.Empty;
    public byte[] PdfData { get; set; } = [];
    public bool IsPublished { get; set; }
    public bool IsDefault { get; set; }
    public string? RoomTypePrefix { get; set; }
    public string? Language { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public ICollection<PdfTemplateField> Fields { get; set; } = [];
}

public static class PdfTemplateTypes
{
    public const string RegistrationCard = "RegistrationCard";
    public const string Promotion = "Promotion";
    public const string PrivacyNotice = "PrivacyNotice";
    public const string Regulations = "Regulations";
    public const string Other = "Other";
}

public sealed class PdfTemplateField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PdfTemplateId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public double XPercent { get; set; }
    public double YPercent { get; set; }
    public double WidthPercent { get; set; } = 20;
    public double HeightPercent { get; set; } = 3;
    public double FontSize { get; set; } = 9;
    public string FieldType { get; set; } = "Text";
    public int? OccupantIndex { get; set; }
    public PdfTemplate? PdfTemplate { get; set; }
}

public static class DocumentSourceModes
{
    public const string Auto = "Auto";
    public const string Local = "Local";
    public const string Opera = "Opera";
}

public sealed class HotelDocumentSetting
{
    public string HotelId { get; set; } = string.Empty;
    public string SourceMode { get; set; } = DocumentSourceModes.Auto;
    public Guid? PreferredPdfTemplateId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public PdfTemplate? PreferredPdfTemplate { get; set; }
}
