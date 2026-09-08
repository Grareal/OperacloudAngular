namespace FirmaOperaCloud.Domain.Entities;

/// <summary>
/// Tarjeta de registro digital generada por el sistema.
/// Agrupa los datos de la reserva (desde OPERA, solo lectura) y la información
/// adicional capturada localmente (firma, PDF, metadatos).
/// Toda esta información se almacena en la base de datos del sistema,
/// NUNCA se escribe de vuelta en OPERA.
/// </summary>
public sealed class RegistrationCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? Version { get; set; }
    public string HotelId { get; set; } = string.Empty;
    public string? ReservationId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string HotelAddress { get; set; } = string.Empty;

    // Datos de la reserva (fuente: OPERA)
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string? TswNumber { get; set; }
    public string GuestFullName { get; set; } = string.Empty;
    public string ArrivalDate { get; set; } = string.Empty;
    public string DepartureDate { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string RoomClass { get; set; } = string.Empty;
    public int Adults { get; set; }
    public int Children { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Citizenship { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string RatePlanCode { get; set; } = string.Empty;
    public string RateAmount { get; set; } = string.Empty;
    public string Guarantee { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;

    // Metadatos locales
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public string? SignatureBase64Png { get; set; }
    public string? SignatureBase64Svg { get; set; }
    public string? PdfBase64 { get; set; }
    public string? DocumentHash { get; set; }
    public string? Receptionist { get; set; }
    public string? DeviceIp { get; set; }
    public string? DeviceInfo { get; set; }
    public string Status { get; set; } = "Generated";
}
