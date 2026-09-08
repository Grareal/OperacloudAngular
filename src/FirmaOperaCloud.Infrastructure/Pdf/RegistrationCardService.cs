using System.Security.Cryptography;
using System.Text;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;

namespace FirmaOperaCloud.Infrastructure.Pdf;

/// <summary>
/// Construye la tarjeta de registro digital a partir de una reserva de OPERA
/// y delega la generación del PDF a <see cref="RegistrationCardPdfGenerator"/>.
/// </summary>
public sealed class RegistrationCardService : IRegistrationCardService
{
    /// <inheritdoc />
    public RegistrationCard BuildCard(Reservation reservation)
    {
        if (reservation == null)
        {
            throw new ArgumentNullException(nameof(reservation));
        }

        var company = reservation.AttachedProfiles.FirstOrDefault(p =>
            p.ReservationProfileType.Equals("Company", StringComparison.OrdinalIgnoreCase))?.Name ?? string.Empty;

        var observations = reservation.Indicators
            .Where(i => i.IndicatorName.Equals("COMMENT", StringComparison.OrdinalIgnoreCase))
            .Select(i => $"{i.IndicatorName} (x{i.Count})")
            .ToList();

        var card = new RegistrationCard
        {
            HotelName = string.IsNullOrWhiteSpace(reservation.HotelName) ? "Vidanta" : reservation.HotelName,
            HotelAddress = RegistrationCardContent.HotelAddress,
            ConfirmationNumber = reservation.ConfirmationNumber ?? string.Empty,
            TswNumber = reservation.TswNumber,
            GuestFullName = reservation.Guest.FullName,
            ArrivalDate = reservation.RoomStay.ArrivalDate,
            DepartureDate = reservation.RoomStay.DepartureDate,
            RoomNumber = reservation.RoomStay.RoomId,
            RoomType = reservation.RoomStay.RoomType,
            RoomClass = reservation.RoomStay.RoomClass,
            Adults = reservation.RoomStay.AdultCount,
            Children = reservation.RoomStay.ChildCount,
            Email = reservation.Guest.Email,
            Phone = reservation.Guest.PhoneNumber,
            Citizenship = reservation.Guest.Address.CountryCode,
            City = reservation.Guest.Address.City,
            State = reservation.Guest.Address.StateProvCode,
            Country = reservation.Guest.Address.CountryCode,
            Company = company,
            RatePlanCode = reservation.RoomStay.RatePlanCode,
            RateAmount = reservation.RoomStay.RateAmount > 0
                ? $"{reservation.RoomStay.RateAmount:0.00} {reservation.RoomStay.CurrencyCode}"
                : string.Empty,
            Guarantee = string.IsNullOrWhiteSpace(reservation.RoomStay.GuaranteeDescription)
                ? reservation.RoomStay.GuaranteeCode
                : reservation.RoomStay.GuaranteeDescription,
            Observations = string.Join(" | ", observations)
        };

        return card;
    }

    /// <inheritdoc />
    public byte[] GeneratePdf(RegistrationCard card)
    {
        if (card == null)
        {
            throw new ArgumentNullException(nameof(card));
        }

        return RegistrationCardPdfGenerator.Generate(card);
    }

    /// <summary>
    /// Calcula el hash SHA-256 del PDF (para auditoría e integridad del documento).
    /// </summary>
    public static string ComputeHash(byte[] pdfBytes)
    {
        var sha = SHA256.HashData(pdfBytes);
        return Convert.ToHexString(sha);
    }
}
