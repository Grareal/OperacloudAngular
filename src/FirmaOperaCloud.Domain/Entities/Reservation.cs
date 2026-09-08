namespace FirmaOperaCloud.Domain.Entities;

/// <summary>
/// Identificador de una reserva dentro de OPERA Cloud
/// (Reservation, Confirmation, ExternalReference, etc.).
/// </summary>
public sealed class ReservationIdInfo
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? IdExtension { get; set; }
}

/// <summary>
/// Referencia externa asociada a una reserva
/// (idContext: OPERA, TIMESHAREWARE, etc.).
/// </summary>
public sealed class ExternalReference
{
    public string Id { get; set; } = string.Empty;
    public int? IdExtension { get; set; }
    public string IdContext { get; set; } = string.Empty;
}

/// <summary>
/// Datos del huésped principal de la reserva.
/// </summary>
public sealed class ReservationGuest
{
    public string GivenName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string FullName => string.Join(" ", new[] { GivenName, MiddleName, Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string NameType { get; set; } = string.Empty;
    public GuestAddress Address { get; set; } = new();
}
public sealed class AccompanyingGuest
{
    public string FullName { get; set; } = string.Empty;
    public string? ProfileId { get; set; }
    public string? ReservationGuestId { get; set; }
}

/// <summary>
/// Dirección del huésped (país, ciudad, estado, código postal).
/// </summary>
public sealed class GuestAddress
{
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateProvCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

/// <summary>
/// Estancia (room stay) de la reserva: fechas, habitación, tarifa, garantía.
/// </summary>
public sealed class RoomStay
{
    public string ArrivalDate { get; set; } = string.Empty;
    public string DepartureDate { get; set; } = string.Empty;
    public int AdultCount { get; set; }
    public int ChildCount { get; set; }
    public string RoomClass { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string RatePlanCode { get; set; } = string.Empty;
    public decimal RateAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string RoomStatus { get; set; } = string.Empty;
    public string RoomTypeCharged { get; set; } = string.Empty;
    public string NumberOfRooms { get; set; } = "1";
    public string MarketCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string SourceCodeDescription { get; set; } = string.Empty;
    public string GuaranteeCode { get; set; } = string.Empty;
    public string GuaranteeDescription { get; set; } = string.Empty;
}

/// <summary>
/// Indicador de la reserva (COMMENT, EXTERNALREFERENCES, etc.).
/// </summary>
public sealed class ReservationIndicator
{
    public string IndicatorName { get; set; } = string.Empty;
    public int? Count { get; set; }
}

/// <summary>
/// Perfil asociado a la reserva (Source, Company, Contact, etc.).
/// </summary>
public sealed class AttachedProfile
{
    public string Name { get; set; } = string.Empty;
    public string ReservationProfileType { get; set; } = string.Empty;
}

/// <summary>
/// Campo definido por el usuario (UDF).
/// </summary>
public sealed class UserDefinedField
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DataType { get; set; } = "Character";
}

/// <summary>
/// Agregado que representa una reserva consultada en OPERA Cloud (solo lectura).
/// </summary>
public sealed class Reservation
{
    public IReadOnlyList<ReservationIdInfo> ReservationIdList { get; set; } = Array.Empty<ReservationIdInfo>();
    public IReadOnlyList<ExternalReference> ExternalReferences { get; set; } = Array.Empty<ExternalReference>();
    public RoomStay RoomStay { get; set; } = new();
    public ReservationGuest Guest { get; set; } = new();
    public IReadOnlyList<string> AccompanyingGuestNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AccompanyingGuest> AccompanyingGuests { get; set; } = Array.Empty<AccompanyingGuest>();
    public IReadOnlyList<AttachedProfile> AttachedProfiles { get; set; } = Array.Empty<AttachedProfile>();
    public IReadOnlyList<ReservationIndicator> Indicators { get; set; } = Array.Empty<ReservationIndicator>();
    public IReadOnlyList<UserDefinedField> UserDefinedFields { get; set; } = Array.Empty<UserDefinedField>();
    public string HotelId { get; set; } = string.Empty;
    public string HotelName { get; set; } = string.Empty;
    public string ReservationStatus { get; set; } = string.Empty;
    public string ComputedReservationStatus { get; set; } = string.Empty;
    public string CreateDateTime { get; set; } = string.Empty;
    public string LastModifyDateTime { get; set; } = string.Empty;

    /// <summary>Número de confirmación (búsqueda por confirmationNumberList).</summary>
    public string? ConfirmationNumber =>
        ReservationIdList.FirstOrDefault(r => r.Type == "Confirmation")?.Id
        ?? ExternalReferences.FirstOrDefault(e => e.IdContext == "OPERA")?.Id;

    /// <summary>Referencia TSW (Timeshareware) si existe.</summary>
    public string? TswNumber =>
        ExternalReferences.FirstOrDefault(e => e.IdContext == "TIMESHAREWARE")?.Id;
}
