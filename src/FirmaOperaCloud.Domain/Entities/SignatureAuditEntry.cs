namespace FirmaOperaCloud.Domain.Entities;

/// <summary>
/// Registro de auditoría local de una tarjeta de registro:
/// cuándo se generó, se firmó, se descargó o se consultó.
/// Solo se almacena localmente, nunca se envía a OPERA.
/// </summary>
public sealed class SignatureAuditEntry
{
    public long Id { get; set; }
    public Guid RegistrationCardId { get; set; }
    public RegistrationCard? RegistrationCard { get; set; }

    /// <summary>Acción realizada: Generated, Signed, Downloaded, Viewed, etc.</summary>
    public string Action { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Usuario/empleado que realizó la acción (receptionist).</summary>
    public string? PerformedBy { get; set; }

    public string? DeviceIp { get; set; }
    public string? DeviceInfo { get; set; }

    /// <summary>Detalle adicional (ej. hash del documento, observaciones).</summary>
    public string? Detail { get; set; }
}
