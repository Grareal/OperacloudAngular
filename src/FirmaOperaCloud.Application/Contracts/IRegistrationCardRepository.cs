using FirmaOperaCloud.Domain.Entities;

namespace FirmaOperaCloud.Application.Contracts;

/// <summary>
/// Persistencia local de las tarjetas de registro, sus firmas y la auditoría.
/// Toda escritura ocurre en la base de datos local del sistema (nunca en OPERA).
/// </summary>
public interface IRegistrationCardRepository
{
    /// <summary>Guarda (inserta o actualiza) una tarjeta de registro en la BD local.</summary>
    Task<Guid> SaveAsync(RegistrationCard card, CancellationToken cancellationToken);

    /// <summary>Obtiene una tarjeta de registro por su Id.</summary>
    Task<RegistrationCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca tarjetas por número de confirmación.</summary>
    Task<IReadOnlyList<RegistrationCard>> GetByConfirmationNumberAsync(
        string confirmationNumber, CancellationToken cancellationToken);

    /// <summary>Lista las tarjetas recientes (máximo <paramref name="limit"/>), más nuevas primero.</summary>
    Task<IReadOnlyList<RegistrationCard>> GetRecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Búsqueda local sobre las tarjetas almacenadas: coincidencia en
    /// confirmación, nombre del huésped, correo, teléfono o habitación.
    /// </summary>
    Task<IReadOnlyList<RegistrationCard>> SearchAsync(string term, int limit, CancellationToken cancellationToken);

    /// <summary>Registra una entrada de auditoría local.</summary>
    Task AddAuditEntryAsync(SignatureAuditEntry entry, CancellationToken cancellationToken);
}
