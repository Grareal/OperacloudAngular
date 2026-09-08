using FirmaOperaCloud.Domain.Entities;

namespace FirmaOperaCloud.Application.Contracts;

/// <summary>
/// Orquesta la generación, persistencia y firma de las tarjetas de registro
/// en la base de datos local, registrando auditoría. No escribe en OPERA.
/// </summary>
public interface IRegistrationCardStoreService
{
    /// <summary>Genera el PDF, lo persiste en la BD local y registra auditoría.</summary>
    Task<RegistrationCard> GenerateAndStoreAsync(
        Reservation reservation,
        string? receptionist,
        string? deviceIp,
        string? deviceInfo,
        CancellationToken cancellationToken);

    /// <summary>Firma una tarjeta existente (almacena la firma, actualiza estado y audita).</summary>
    Task<RegistrationCard> SignAsync(
        Guid cardId,
        string signaturePngBase64,
        string? signatureSvgBase64,
        string? receptionist,
        string? deviceIp,
        string? deviceInfo,
        CancellationToken cancellationToken);

    /// <summary>Obtiene una tarjeta por Id.</summary>
    Task<RegistrationCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca tarjetas por número de confirmación.</summary>
    Task<IReadOnlyList<RegistrationCard>> GetByConfirmationNumberAsync(
        string confirmationNumber, CancellationToken cancellationToken);

    /// <summary>Lista las tarjetas recientes.</summary>
    Task<IReadOnlyList<RegistrationCard>> GetRecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Búsqueda local en las tarjetas almacenadas (confirmación, huésped,
    /// correo, teléfono o habitación).
    /// </summary>
    Task<IReadOnlyList<RegistrationCard>> SearchAsync(string term, int limit, CancellationToken cancellationToken);

    /// <summary>Registra una acción de auditoría (ej. descarga del PDF firmado).</summary>
    Task AuditAsync(Guid cardId, string action, string? performedBy, string? deviceIp, string? deviceInfo, string? detail, CancellationToken cancellationToken);
}
