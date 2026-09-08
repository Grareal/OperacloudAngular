using FirmaOperaCloud.Domain.Entities;

namespace FirmaOperaCloud.Application.Contracts;

/// <summary>
/// Genera la tarjeta de registro digital (PDF) a partir de una reserva.
/// </summary>
public interface IRegistrationCardService
{
    /// <summary>
    /// Construye el modelo de la tarjeta de registro a partir de una reserva de OPERA.
    /// </summary>
    RegistrationCard BuildCard(Reservation reservation);

    /// <summary>
    /// Genera el PDF de la tarjeta de registro y lo devuelve como bytes.
    /// </summary>
    byte[] GeneratePdf(RegistrationCard card);
}
