using FirmaOperaCloud.Domain.Entities;

namespace FirmaOperaCloud.Application.Contracts;

/// <summary>
/// Consulta de reservaciones en OPERA Cloud (exclusivamente operaciones GET / solo lectura).
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Busca reservas por número de confirmación.
    /// </summary>
    /// <param name="hotelId">Hotel ID (x-hotelid).</param>
    /// <param name="confirmationNumber">Número de confirmación (confirmationNumberList).</param>
    Task<IReadOnlyList<Reservation>> GetByConfirmationNumberAsync(string hotelId, string confirmationNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca reservas por rango de fechas de llegada (arrivalStartDate/arrivalEndDate).
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetByArrivalDateRangeAsync(string hotelId, DateTime start, DateTime end, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca reservas por rango de fechas de salida (departureStartDate/departureEndDate).
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetByDepartureDateRangeAsync(string hotelId, DateTime start, DateTime end, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca reservas por apellido del huésped (surname).
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetBySurnameAsync(string hotelId, string surname, int limit = 50, CancellationToken cancellationToken = default);
}
