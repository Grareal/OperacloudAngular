namespace FirmaOperaCloud.Domain.Entities;

/// <summary>
/// Usuario local del sistema para autenticación JWT.
/// Solo se almacena en la BD local; no tiene relación con OPERA.
/// </summary>
public sealed class AppUser
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 (Rfc2898DeriveBytes) en Base64: salt + hash.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Receptionist";
    public long? UserGroupId { get; set; }
    public UserGroup? UserGroup { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class UserGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "[]";
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<AppUser> Users { get; set; } = [];
}

public static class ViewPermissions
{
    public const string Operation = "operation";
    public const string RegistrationCard = "registration-card";
    public const string SignatureLookup = "signature-lookup";
    public const string History = "history";
    public const string Documents = "documents";
    public const string PdfTemplates = "pdf-templates";
    public const string Communications = "communications";
    public const string EmailSettings = "email-settings";
    public const string Promotions = "promotions";
    public const string AccompanyingGuests = "accompanying-guests";
    public const string UserAdministration = "user-administration";
    public const string Audit = "audit";
    public const string OcrIdentity = "ocr-identity";

    public static readonly IReadOnlyList<string> All =
    [Operation, RegistrationCard, SignatureLookup, History, Documents, PdfTemplates,
     Communications, EmailSettings, Promotions, AccompanyingGuests, UserAdministration, Audit, OcrIdentity];
}
