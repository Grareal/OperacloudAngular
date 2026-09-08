using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmaOperaCloud.Infrastructure.Opera.Models;

/// <summary>
/// DTOs para deserializar la respuesta de GET /rsv/v1/hotels/{hotelId}/reservations.
/// Solo se modelan los campos que necesita la aplicación (MVP).
/// </summary>
internal static class OperaReservationDto
{
    public sealed class Envelope
    {
        [JsonPropertyName("reservations")]
        public ReservationsContainer? Reservations { get; set; }
    }

    public sealed class ReservationsContainer
    {
        [JsonPropertyName("reservationInfo")]
        public List<ReservationInfo> ReservationInfo { get; set; } = new();
    }

    public sealed class ReservationInfo
    {
        [JsonPropertyName("reservationIdList")]
        public List<IdInfo> ReservationIdList { get; set; } = new();

        [JsonPropertyName("externalReferences")]
        public List<ExternalRef> ExternalReferences { get; set; } = new();

        [JsonPropertyName("roomStay")]
        public RoomStayInfo? RoomStay { get; set; }

        [JsonPropertyName("reservationGuest")]
        public GuestInfo? ReservationGuest { get; set; }

        [JsonPropertyName("attachedProfiles")]
        public List<AttachedProfileInfo> AttachedProfiles { get; set; } = new();

        [JsonPropertyName("reservationIndicators")]
        public List<IndicatorInfo> ReservationIndicators { get; set; } = new();

        [JsonPropertyName("userDefinedFields")]
        public UserDefinedFieldsInfo? UserDefinedFields { get; set; }

        [JsonPropertyName("hotelId")]
        public string HotelId { get; set; } = string.Empty;

        [JsonPropertyName("hotelName")]
        public string HotelName { get; set; } = string.Empty;

        [JsonPropertyName("reservationStatus")]
        public string ReservationStatus { get; set; } = string.Empty;

        [JsonPropertyName("computedReservationStatus")]
        public string ComputedReservationStatus { get; set; } = string.Empty;

        [JsonPropertyName("createDateTime")]
        public string CreateDateTime { get; set; } = string.Empty;

        [JsonPropertyName("lastModifyDateTime")]
        public string LastModifyDateTime { get; set; } = string.Empty;
    }

    public sealed class IdInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("idExtension")]
        public int? IdExtension { get; set; }
    }

    public sealed class ExternalRef
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("idExtension")]
        public int? IdExtension { get; set; }

        [JsonPropertyName("idContext")]
        public string IdContext { get; set; } = string.Empty;
    }

    public sealed class RoomStayInfo
    {
        [JsonPropertyName("arrivalDate")]
        public string ArrivalDate { get; set; } = string.Empty;

        [JsonPropertyName("departureDate")]
        public string DepartureDate { get; set; } = string.Empty;

        [JsonPropertyName("adultCount")]
        public int AdultCount { get; set; }

        [JsonPropertyName("childCount")]
        public int ChildCount { get; set; }

        [JsonPropertyName("roomClass")]
        public string RoomClass { get; set; } = string.Empty;

        [JsonPropertyName("roomType")]
        public string RoomType { get; set; } = string.Empty;

        [JsonPropertyName("roomId")]
        public string RoomId { get; set; } = string.Empty;

        [JsonPropertyName("ratePlanCode")]
        public string RatePlanCode { get; set; } = string.Empty;

        [JsonPropertyName("rateAmount")]
        public RateAmountInfo? RateAmount { get; set; }

        [JsonPropertyName("guarantee")]
        public GuaranteeInfo? Guarantee { get; set; }

        [JsonPropertyName("roomStatus")]
        public string RoomStatus { get; set; } = string.Empty;

        [JsonPropertyName("roomTypeCharged")]
        public string RoomTypeCharged { get; set; } = string.Empty;

        [JsonPropertyName("numberOfRooms")]
        public int? NumberOfRooms { get; set; }

        [JsonPropertyName("marketCode")]
        public string MarketCode { get; set; } = string.Empty;

        [JsonPropertyName("sourceCode")]
        public string SourceCode { get; set; } = string.Empty;

        [JsonPropertyName("sourceCodeDescription")]
        public string SourceCodeDescription { get; set; } = string.Empty;
    }

    public sealed class RateAmountInfo
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; } = string.Empty;
    }

    public sealed class GuaranteeInfo
    {
        [JsonPropertyName("guaranteeCode")]
        public string GuaranteeCode { get; set; } = string.Empty;

        [JsonPropertyName("shortDescription")]
        public string ShortDescription { get; set; } = string.Empty;
    }

    public sealed class GuestInfo
    {
        [JsonPropertyName("givenName")]
        public string GivenName { get; set; } = string.Empty;

        [JsonPropertyName("middleName")]
        public string MiddleName { get; set; } = string.Empty;

        [JsonPropertyName("surname")]
        public string Surname { get; set; } = string.Empty;

        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("nameType")]
        public string NameType { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public GuestAddressInfo? Address { get; set; }
    }

    public sealed class GuestAddressInfo
    {
        [JsonPropertyName("streetAddress")]
        public string StreetAddress { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("stateProvCode")]
        public string StateProvCode { get; set; } = string.Empty;

        [JsonPropertyName("postalCode")]
        public string PostalCode { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public CountryInfo? Country { get; set; }
    }

    public sealed class CountryInfo
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
    }

    public sealed class AttachedProfileInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("reservationProfileType")]
        public string ReservationProfileType { get; set; } = string.Empty;
    }

    public sealed class IndicatorInfo
    {
        [JsonPropertyName("indicatorName")]
        public string IndicatorName { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }

    public sealed class UserDefinedFieldsInfo
    {
        [JsonPropertyName("characterUDFs")]
        public List<UdfValue> CharacterUDFs { get; set; } = new();

        // Conserva colecciones UDF adicionales que OHIP pueda devolver según
        // la versión y configuración (por ejemplo, fechas y números).
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class UdfValue
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
