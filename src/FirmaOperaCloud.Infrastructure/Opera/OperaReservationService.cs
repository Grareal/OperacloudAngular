using System.Net.Http.Headers;
using System.Text.Json;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Opera.Models;
using FirmaOperaCloud.Shared.Options;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Infrastructure.Opera;

/// <summary>
/// Consulta de reservaciones en OPERA Cloud mediante GET (solo lectura).
/// No existe ninguna operación de escritura sobre OPERA en esta clase.
/// </summary>
public sealed class OperaReservationService : IReservationService
{
    private readonly OperaCloudOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IOperaTokenService _tokenService;

    public OperaReservationService(HttpClient httpClient, IOptions<OperaCloudOptions> options, IOperaTokenService tokenService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenService = tokenService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reservation>> GetByConfirmationNumberAsync(
        string hotelId, string confirmationNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(confirmationNumber))
        {
            throw new ArgumentException("Debe indicar un número de confirmación.", nameof(confirmationNumber));
        }

        var query = new Dictionary<string, string>
        {
            ["confirmationNumberList"] = confirmationNumber,
            ["limit"] = "10"
        };

        var reservations = await SearchAsync(hotelId, query, cancellationToken);
        foreach (var reservation in reservations)
        {
            await EnrichAccompanyingGuestsAsync(hotelId, reservation, cancellationToken);
        }

        return reservations;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reservation>> GetByArrivalDateRangeAsync(
        string hotelId, DateTime start, DateTime end, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["arrivalStartDate"] = start.ToString("yyyy-MM-dd"),
            ["arrivalEndDate"] = end.ToString("yyyy-MM-dd"),
            ["limit"] = limit.ToString()
        };

        return await SearchAsync(hotelId, query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reservation>> GetBySurnameAsync(
        string hotelId, string surname, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["surname"] = surname,
            ["limit"] = limit.ToString()
        };

        return await SearchAsync(hotelId, query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reservation>> GetByDepartureDateRangeAsync(
        string hotelId, DateTime start, DateTime end, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["departureStartDate"] = start.ToString("yyyy-MM-dd"),
            ["departureEndDate"] = end.ToString("yyyy-MM-dd"),
            ["limit"] = limit.ToString()
        };

        return await SearchAsync(hotelId, query, cancellationToken);
    }

    private async Task<IReadOnlyList<Reservation>> SearchAsync(
        string hotelId, Dictionary<string, string> query, CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

        var url = $"{_options.GatewayUrl}/rsv/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations";
        var uri = new UriBuilder(url);
        var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);
        foreach (var (key, value) in query)
        {
            qs[key] = value;
        }
        uri.Query = qs.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, uri.ToString());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-hotelid", hotelId);
        request.Headers.Add("x-app-key", _options.AppKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Consulta reservas: {(int)response.StatusCode} {response.StatusCode}: {json}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var envelope = JsonSerializer.Deserialize<OperaReservationDto.Envelope>(json, options);

        if (envelope?.Reservations?.ReservationInfo == null)
        {
            return Array.Empty<Reservation>();
        }

        return envelope.Reservations.ReservationInfo.Select(Map).ToList();
    }

    private async Task EnrichAccompanyingGuestsAsync(
        string hotelId,
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var reservationId = reservation.ReservationIdList
            .FirstOrDefault(item => item.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id;
        if (string.IsNullOrWhiteSpace(reservationId)) return;

        var path = $"{_options.GatewayUrl}/rsv/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations/" +
                   $"{Uri.EscapeDataString(reservationId)}?fetchInstructions=Reservation&fetchInstructions=AccompanyingGuestProfile";
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-hotelid", hotelId);
        request.Headers.Add("x-app-key", _options.AppKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detailedGuests = new Dictionary<string, AccompanyingGuest>(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("reservations", out var reservations) ||
            !reservations.TryGetProperty("reservation", out var reservationItems) ||
            reservationItems.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in reservationItems.EnumerateArray())
        {
            if (!item.TryGetProperty("reservationGuests", out var guests) || guests.ValueKind != JsonValueKind.Array) continue;
            foreach (var guest in guests.EnumerateArray())
            {
                if (!guest.TryGetProperty("profileInfo", out var profileInfo) ||
                    !profileInfo.TryGetProperty("profile", out var profile) ||
                    !profile.TryGetProperty("customer", out var customer))
                    continue;

                if (customer.TryGetProperty("accompanyGuests", out var accompanying) && accompanying.ValueKind == JsonValueKind.Array)
                {
                    foreach (var accompanyingGuest in accompanying.EnumerateArray())
                    {
                        var accompanyingName = accompanyingGuest.TryGetProperty("fullName", out var value) ? value.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(accompanyingName))
                        {
                            var clean = accompanyingName.Trim(); names.Add(clean);
                            detailedGuests[clean] = new AccompanyingGuest { FullName = clean,
                                ProfileId = ReadId(accompanyingGuest, "profileId"), ReservationGuestId = ReadId(accompanyingGuest, "reservationGuestId") };
                        }
                    }
                }

                if (guest.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True) continue;
                if (!customer.TryGetProperty("personName", out var personNames) ||
                    personNames.ValueKind != JsonValueKind.Array || personNames.GetArrayLength() == 0) continue;

                var personName = personNames.EnumerateArray().FirstOrDefault();
                var fullName = personName.TryGetProperty("fullName", out var fullNameElement)
                    ? fullNameElement.GetString()
                    : string.Join(" ", new[]
                    {
                        personName.TryGetProperty("givenName", out var given) ? given.GetString() : null,
                        personName.TryGetProperty("middleName", out var middle) ? middle.GetString() : null,
                        personName.TryGetProperty("surname", out var surname) ? surname.GetString() : null
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));

                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    var clean = fullName.Trim(); names.Add(clean);
                    detailedGuests[clean] = new AccompanyingGuest { FullName = clean,
                        ProfileId = ReadId(profileInfo, "profileId"),
                        ReservationGuestId = ReadId(profileInfo, "reservationId") ?? ReadId(guest, "reservationGuestId") };
                }
            }
        }

        reservation.AccompanyingGuestNames = names.Take(8).ToArray();
        reservation.AccompanyingGuests = reservation.AccompanyingGuestNames.Select(name => detailedGuests.TryGetValue(name, out var item) ? item : new AccompanyingGuest { FullName = name }).ToArray();
    }

    private static string? ReadId(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var direct)) return direct.ValueKind == JsonValueKind.String ? direct.GetString() : direct.ToString();
        if (element.TryGetProperty(property + "List", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray()) if (item.TryGetProperty("id", out var id)) return id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
        return null;
    }

    private static Reservation Map(OperaReservationDto.ReservationInfo dto) => new()
    {
        ReservationIdList = dto.ReservationIdList.Select(x => new ReservationIdInfo
        {
            Id = x.Id,
            Type = x.Type,
            IdExtension = x.IdExtension
        }).ToList(),

        ExternalReferences = dto.ExternalReferences.Select(x => new ExternalReference
        {
            Id = x.Id,
            IdExtension = x.IdExtension,
            IdContext = x.IdContext
        }).ToList(),

        RoomStay = new RoomStay
        {
            ArrivalDate = dto.RoomStay?.ArrivalDate ?? string.Empty,
            DepartureDate = dto.RoomStay?.DepartureDate ?? string.Empty,
            AdultCount = dto.RoomStay?.AdultCount ?? 0,
            ChildCount = dto.RoomStay?.ChildCount ?? 0,
            RoomClass = dto.RoomStay?.RoomClass ?? string.Empty,
            RoomType = dto.RoomStay?.RoomType ?? string.Empty,
            RoomId = dto.RoomStay?.RoomId ?? string.Empty,
            RatePlanCode = dto.RoomStay?.RatePlanCode ?? string.Empty,
            RateAmount = dto.RoomStay?.RateAmount?.Amount ?? 0,
            CurrencyCode = dto.RoomStay?.RateAmount?.CurrencyCode ?? string.Empty,
            RoomStatus = dto.RoomStay?.RoomStatus ?? string.Empty,
            RoomTypeCharged = dto.RoomStay?.RoomTypeCharged ?? string.Empty,
            NumberOfRooms = (dto.RoomStay?.NumberOfRooms ?? 1).ToString(),
            MarketCode = dto.RoomStay?.MarketCode ?? string.Empty,
            SourceCode = dto.RoomStay?.SourceCode ?? string.Empty,
            SourceCodeDescription = dto.RoomStay?.SourceCodeDescription ?? string.Empty,
            GuaranteeCode = dto.RoomStay?.Guarantee?.GuaranteeCode ?? string.Empty,
            GuaranteeDescription = dto.RoomStay?.Guarantee?.ShortDescription ?? string.Empty
        },

        Guest = new ReservationGuest
        {
            GivenName = dto.ReservationGuest?.GivenName ?? string.Empty,
            MiddleName = dto.ReservationGuest?.MiddleName ?? string.Empty,
            Surname = dto.ReservationGuest?.Surname ?? string.Empty,
            PhoneNumber = dto.ReservationGuest?.PhoneNumber ?? string.Empty,
            Email = dto.ReservationGuest?.Email ?? string.Empty,
            Language = dto.ReservationGuest?.Language ?? string.Empty,
            Id = dto.ReservationGuest?.Id ?? string.Empty,
            NameType = dto.ReservationGuest?.NameType ?? string.Empty,
            Address = new GuestAddress
            {
                StreetAddress = dto.ReservationGuest?.Address?.StreetAddress ?? string.Empty,
                City = dto.ReservationGuest?.Address?.City ?? string.Empty,
                StateProvCode = dto.ReservationGuest?.Address?.StateProvCode ?? string.Empty,
                PostalCode = dto.ReservationGuest?.Address?.PostalCode ?? string.Empty,
                CountryCode = dto.ReservationGuest?.Address?.Country?.Code ?? string.Empty
            }
        },

        AttachedProfiles = dto.AttachedProfiles.Select(x => new AttachedProfile
        {
            Name = x.Name,
            ReservationProfileType = x.ReservationProfileType
        }).ToList(),

        Indicators = dto.ReservationIndicators.Select(x => new ReservationIndicator
        {
            IndicatorName = x.IndicatorName,
            Count = x.Count
        }).ToList(),

        UserDefinedFields = MapUserDefinedFields(dto.UserDefinedFields),

        HotelId = dto.HotelId,
        HotelName = dto.HotelName,
        ReservationStatus = dto.ReservationStatus,
        ComputedReservationStatus = dto.ComputedReservationStatus,
        CreateDateTime = dto.CreateDateTime,
        LastModifyDateTime = dto.LastModifyDateTime
    };

    private static IReadOnlyList<UserDefinedField> MapUserDefinedFields(OperaReservationDto.UserDefinedFieldsInfo? source)
    {
        if (source is null) return Array.Empty<UserDefinedField>();

        var fields = source.CharacterUDFs.Select(x => new UserDefinedField
        {
            Name = x.Name,
            Value = x.Value,
            DataType = "Character"
        }).ToList();

        foreach (var collection in source.AdditionalFields)
        {
            if (collection.Value.ValueKind != JsonValueKind.Array ||
                !collection.Key.Contains("udf", StringComparison.OrdinalIgnoreCase)) continue;

            var dataType = collection.Key.Contains("date", StringComparison.OrdinalIgnoreCase)
                ? "Date"
                : collection.Key.Contains("num", StringComparison.OrdinalIgnoreCase) || collection.Key.Contains("number", StringComparison.OrdinalIgnoreCase)
                    ? "Number"
                    : "Other";

            foreach (var item in collection.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("name", out var name) ||
                    !item.TryGetProperty("value", out var value)) continue;

                fields.Add(new UserDefinedField
                {
                    Name = name.GetString() ?? string.Empty,
                    Value = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString(),
                    DataType = dataType
                });
            }
        }

        return fields;
    }
}
