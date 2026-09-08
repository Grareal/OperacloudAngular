using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using FirmaOperaCloud.Shared.Options;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Infrastructure.Opera;

/// <summary>
/// Escritura deliberadamente limitada para agregar un perfil adulto a una reserva UAT.
/// Siempre reconstruye la colección completa desde un GET fresco y verifica con otro GET.
/// </summary>
public sealed class OperaAccompanyingGuestService : IOperaAccompanyingGuestService
{
    private readonly HttpClient _httpClient;
    private readonly IOperaTokenService _tokenService;
    private readonly IReservationService _reservations;
    private readonly FirmaOperaCloudDbContext _db;
    private readonly OperaCloudOptions _options;

    public OperaAccompanyingGuestService(
        HttpClient httpClient,
        IOperaTokenService tokenService,
        IReservationService reservations,
        FirmaOperaCloudDbContext db,
        IOptions<OperaCloudOptions> options)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _reservations = reservations;
        _db = db;
        _options = options.Value;
    }

    public async Task<AccompanyingGuestProfileLookup> LookupAdultAsync(
        string hotelId,
        string confirmationNumber,
        string givenName,
        string surname,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationInput(hotelId, confirmationNumber);
        (givenName, surname) = ValidateAndNormalizeName(givenName, surname);

        var detail = await GetDetailAsync(hotelId, confirmationNumber, cancellationToken);
        var guests = ReadGuests(detail.Reservation);
        var matches = await SearchProfilesAsync(hotelId, givenName, surname, cancellationToken);
        AccompanyingGuestChangePreview? newProfilePreview = null;
        if (matches.Count == 0)
        {
            var proposed = new OperaGuestProfileInfo(string.Empty, $"{givenName} {surname}", false);
            newProfilePreview = CreatePreview(detail, proposed, guests);
        }

        return new AccompanyingGuestProfileLookup(givenName, surname, matches, newProfilePreview);
    }

    public async Task<AccompanyingGuestChangePreview> PreviewAddAdultAsync(
        string hotelId, string confirmationNumber, string profileId, CancellationToken cancellationToken = default)
    {
        ValidateInput(hotelId, confirmationNumber, profileId);
        var detail = await GetDetailAsync(hotelId, confirmationNumber, cancellationToken);
        var guests = ReadGuests(detail.Reservation);
        var existing = guests.FirstOrDefault(x => x.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        var requested = existing ?? await GetProfileAsync(hotelId, profileId, cancellationToken);
        return CreatePreview(detail, requested, guests);
    }

    public async Task<AccompanyingGuestChangeResult> AddAdultAsync(
        string hotelId,
        string confirmationNumber,
        string profileId,
        string expectedLastModifyDateTime,
        string userName,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(hotelId, confirmationNumber, profileId);
        EnsureWritesAllowed(hotelId);

        var detail = await GetDetailAsync(hotelId, confirmationNumber, cancellationToken);
        if (!string.Equals(detail.LastModifyDateTime, expectedLastModifyDateTime, StringComparison.Ordinal))
            throw new InvalidOperationException("La reserva cambió después de la vista previa. Actualice la información antes de confirmar.");

        var currentGuests = ReadGuests(detail.Reservation);
        var existing = currentGuests.FirstOrDefault(x => x.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        var requested = existing ?? await GetProfileAsync(hotelId, profileId, cancellationToken);
        var before = CreatePreview(detail, requested, currentGuests);
        if (!before.CanApply) throw new InvalidOperationException(before.ValidationMessage);

        var correlationId = Guid.NewGuid().ToString();
        var payload = BuildPayload(detail.Reservation, profileId, before.ProposedAdults);
        var requestJson = payload.ToJsonString(JsonOptions);
        var audit = new ReservationGuestChangeAudit
        {
            HotelId = hotelId,
            ConfirmationNumber = confirmationNumber,
            ReservationId = detail.ReservationId,
            RequestedProfileId = profileId,
            RequestedProfileName = requested.FullName,
            UserName = userName,
            CorrelationId = correlationId,
            BeforeJson = JsonSerializer.Serialize(before, JsonOptions),
            RequestJson = requestJson
        };
        _db.ReservationGuestChangeAudits.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var path = $"/rsv/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations/{Uri.EscapeDataString(detail.ReservationId)}";
            using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestJson));
            content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            using var response = await SendAsync(HttpMethod.Put, path, hotelId, correlationId, content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            audit.ResponseJson = responseJson;
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"OPERA rechazó la actualización: {(int)response.StatusCode} {response.ReasonPhrase}: {responseJson}");

            var verifiedDetail = await GetDetailAsync(hotelId, confirmationNumber, cancellationToken);
            var verifiedGuests = ReadGuests(verifiedDetail.Reservation);
            var after = CreatePreview(verifiedDetail, requested, verifiedGuests);
            var linked = verifiedGuests.Any(x => x.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase) && !x.Primary);
            if (!linked || after.CurrentAdults != before.ProposedAdults || verifiedGuests.Count(x => x.Primary) != 1)
                throw new InvalidOperationException("OPERA respondió correctamente, pero la verificación posterior no coincide con el cambio solicitado.");

            audit.Status = "Verified";
            audit.AfterJson = JsonSerializer.Serialize(after, JsonOptions);
            audit.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new AccompanyingGuestChangeResult(audit.Id, correlationId, before, after, responseJson);
        }
        catch (Exception ex)
        {
            audit.Status = "Failed";
            audit.ErrorMessage = ex.Message;
            audit.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AccompanyingGuestChangeResult> CreateAndAddAdultAsync(
        string hotelId,
        string confirmationNumber,
        string givenName,
        string surname,
        string expectedLastModifyDateTime,
        string userName,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationInput(hotelId, confirmationNumber);
        (givenName, surname) = ValidateAndNormalizeName(givenName, surname);
        EnsureWritesAllowed(hotelId);

        var detail = await GetDetailAsync(hotelId, confirmationNumber, cancellationToken);
        if (!string.Equals(detail.LastModifyDateTime, expectedLastModifyDateTime, StringComparison.Ordinal))
            throw new InvalidOperationException("La reserva cambió después de la vista previa. Actualice la información antes de confirmar.");

        var guests = ReadGuests(detail.Reservation);
        var proposed = new OperaGuestProfileInfo(string.Empty, $"{givenName} {surname}", false);
        var preview = CreatePreview(detail, proposed, guests);
        if (!preview.CanApply) throw new InvalidOperationException(preview.ValidationMessage);

        // La búsqueda se repite inmediatamente antes de crear para reducir al mínimo
        // la posibilidad de duplicar un perfil creado por recepción en otra pantalla.
        var matches = await SearchProfilesAsync(hotelId, givenName, surname, cancellationToken);
        if (matches.Count > 0)
            throw new InvalidOperationException(
                "OPERA encontró un perfil existente con ese nombre. Vuelva a revisar y seleccione su Profile ID antes de crear otro.");

        var created = await CreateProfileAsync(hotelId, givenName, surname, cancellationToken);
        try
        {
            return await AddAdultAsync(
                hotelId, confirmationNumber, created.ProfileId, expectedLastModifyDateTime, userName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Se creó el Profile ID {created.ProfileId}, pero no pudo vincularse a la reserva. Puede reintentarlo usando ese ID. Detalle: {ex.Message}", ex);
        }
    }

    private async Task<ReservationDetail> GetDetailAsync(string hotelId, string confirmationNumber, CancellationToken ct)
    {
        var summary = (await _reservations.GetByConfirmationNumberAsync(hotelId, confirmationNumber, ct)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"No se encontró la reserva {confirmationNumber}.");
        var reservationId = summary.ReservationIdList.FirstOrDefault(x => x.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new InvalidOperationException("La reserva no devolvió el Reservation ID interno de OPERA.");
        var path = $"/rsv/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations/{Uri.EscapeDataString(reservationId)}" +
                   "?fetchInstructions=Reservation&fetchInstructions=AccompanyingGuestProfile&fetchInstructions=RateInfoDetails";
        using var response = await SendAsync(HttpMethod.Get, path, hotelId, Guid.NewGuid().ToString(), null, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Detalle de reserva OPERA: {(int)response.StatusCode} {response.ReasonPhrase}: {json}");
        var root = JsonNode.Parse(json) ?? throw new InvalidOperationException("OPERA devolvió una respuesta vacía.");
        var reservation = root["reservations"]?["reservation"]?.AsArray().FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("OPERA no devolvió el detalle esperado de la reserva.");
        return new ReservationDetail(reservationId, reservation["lastModifyDateTime"]?.GetValue<string>() ?? string.Empty, reservation);
    }

    private async Task<OperaGuestProfileInfo> GetProfileAsync(string hotelId, string profileId, CancellationToken ct)
    {
        var path = $"/crm/v1/profiles/{Uri.EscapeDataString(profileId)}";
        using var response = await SendAsync(HttpMethod.Get, path, hotelId, Guid.NewGuid().ToString(), null, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json))
            throw new KeyNotFoundException($"No fue posible validar el Profile ID {profileId}: {(int)response.StatusCode} {response.ReasonPhrase}.");
        var root = JsonNode.Parse(json);
        var details = root?["profileDetails"];
        var type = details?["profileType"]?.GetValue<string>() ?? details?["profile"]?["profileType"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(type) && !type.Equals("Guest", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"El Profile ID {profileId} no es de tipo Guest.");
        var customer = details?["customer"] ?? details?["profile"]?["customer"];
        var name = ReadPersonName(customer?["personName"]?.AsArray().FirstOrDefault());
        return new OperaGuestProfileInfo(profileId, string.IsNullOrWhiteSpace(name) ? $"Profile {profileId}" : name, false);
    }

    private async Task<IReadOnlyList<OperaGuestProfileInfo>> SearchProfilesAsync(
        string hotelId, string givenName, string surname, CancellationToken ct)
    {
        var path = "/crm/v1/profiles" +
                   $"?profileType=Guest&givenName={Uri.EscapeDataString(givenName)}" +
                   $"&profileName={Uri.EscapeDataString(surname)}&excludeInactive=true&summaryInfo=true&searchType=Any&limit=10&hotelId={Uri.EscapeDataString(hotelId)}";
        using var response = await SendAsync(HttpMethod.Get, path, hotelId, Guid.NewGuid().ToString(), null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return [];
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Búsqueda de perfiles OPERA: {(int)response.StatusCode} {response.ReasonPhrase}: {json}");

        var root = JsonNode.Parse(json);
        var result = new List<OperaGuestProfileInfo>();
        var ids = root?["profileSummaries"]?["profileInfo"]?.AsArray()
            .SelectMany(x => x?["profileIdList"]?.AsArray() ?? [])
            .Select(ReadId)
            .Where(x => x.Type.Equals("Profile", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => x.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray() ?? [];

        foreach (var id in ids)
            result.Add(await GetProfileAsync(hotelId, id, ct));
        return result;
    }

    private async Task<OperaGuestProfileInfo> CreateProfileAsync(
        string hotelId, string givenName, string surname, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["profileDetails"] = new JsonObject
            {
                ["customer"] = new JsonObject
                {
                    ["personName"] = new JsonArray(
                        new JsonObject { ["givenName"] = givenName, ["surname"] = surname, ["nameType"] = "Primary" },
                        new JsonObject { ["nameType"] = "Alternate" },
                        new JsonObject { ["nameType"] = "Incognito" }),
                    ["alienInfo"] = new JsonObject(),
                    ["birthCountry"] = new JsonObject { ["code"] = string.Empty }
                },
                ["mailingActions"] = new JsonObject { ["active"] = true },
                ["taxInfo"] = new JsonObject(),
                ["statusCode"] = "Active",
                ["requestForHotel"] = hotelId,
                ["markAsRecentlyAccessed"] = true,
                ["profileType"] = "Guest"
            },
            ["profileIdList"] = new JsonArray()
        };
        var requestJson = payload.ToJsonString(JsonOptions);
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestJson));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        using var response = await SendAsync(HttpMethod.Post, "/crm/v1/profiles", hotelId, Guid.NewGuid().ToString(), content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OPERA rechazó la creación del perfil: {(int)response.StatusCode} {response.ReasonPhrase}: {responseJson}");

        var profileId = ReadCreatedProfileId(response, responseJson);
        if (string.IsNullOrWhiteSpace(profileId) || !profileId.All(char.IsDigit))
            throw new InvalidOperationException("OPERA creó el perfil, pero no devolvió un Profile ID válido.");
        return await GetProfileAsync(hotelId, profileId, ct);
    }

    private static string ReadCreatedProfileId(HttpResponseMessage response, string responseJson)
    {
        var location = response.Headers.Location?.ToString();
        if (!string.IsNullOrWhiteSpace(location))
        {
            var fromLocation = location.TrimEnd('/').Split('/').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(fromLocation) && fromLocation.All(char.IsDigit)) return fromLocation;
        }

        if (!string.IsNullOrWhiteSpace(responseJson))
        {
            var root = JsonNode.Parse(responseJson);
            var fromBody = FindProfileId(root);
            if (!string.IsNullOrWhiteSpace(fromBody)) return fromBody;
        }
        return string.Empty;
    }

    private static string? FindProfileId(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj["type"]?.ToString().Equals("Profile", StringComparison.OrdinalIgnoreCase) == true &&
                obj["id"] is JsonNode id && id.ToString().All(char.IsDigit))
                return id.ToString();
            foreach (var property in obj)
                if (FindProfileId(property.Value) is { Length: > 0 } found) return found;
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                if (FindProfileId(item) is { Length: > 0 } found) return found;
        }
        return null;
    }

    private AccompanyingGuestChangePreview CreatePreview(
        ReservationDetail detail, OperaGuestProfileInfo requested, IReadOnlyList<OperaGuestProfileInfo> guests)
    {
        var status = detail.Reservation["reservationStatus"]?.GetValue<string>() ?? string.Empty;
        var counts = detail.Reservation["roomStay"]?["guestCounts"];
        var adults = counts?["adults"]?.GetValue<int>() ?? 0;
        var children = counts?["children"]?.GetValue<int>() ?? 0;
        var alreadyLinked = guests.Any(x => x.ProfileId.Equals(requested.ProfileId, StringComparison.OrdinalIgnoreCase));
        var proposed = alreadyLinked ? guests : guests.Append(requested with { Primary = false }).ToArray();
        var message = ValidateChange(status, guests, alreadyLinked, adults);
        return new AccompanyingGuestChangePreview(
            detail.Reservation["hotelId"]?.GetValue<string>() ?? _options.DefaultHotelId,
            detail.Reservation["reservationIdList"]?.AsArray()
                .Select(ReadId).FirstOrDefault(x => x.Type.Equals("Confirmation", StringComparison.OrdinalIgnoreCase)).Id ?? string.Empty,
            detail.ReservationId,
            status,
            detail.LastModifyDateTime,
            adults,
            alreadyLinked ? adults : adults + 1,
            children,
            requested,
            guests,
            proposed,
            alreadyLinked,
            message.Length == 0,
            message.Length == 0 ? "Cambio listo para confirmar." : message);
    }

    private string ValidateChange(string status, IReadOnlyList<OperaGuestProfileInfo> guests, bool alreadyLinked, int adults)
    {
        if (!_options.AllowAccompanyingGuestWrites || !IsUatGateway()) return "Las escrituras de acompañantes no están habilitadas en este ambiente.";
        if (!status.Equals("Reserved", StringComparison.OrdinalIgnoreCase)) return "La primera versión solo permite reservas con estado Reserved.";
        if (guests.Count(x => x.Primary) != 1) return "La reserva no tiene exactamente un titular; no se puede modificar de forma segura.";
        if (alreadyLinked) return "El perfil ya está vinculado a la reserva; no se ejecutará un PUT duplicado.";
        if (adults >= 8) return "La primera versión admite como máximo ocho adultos.";
        return string.Empty;
    }

    private static IReadOnlyList<OperaGuestProfileInfo> ReadGuests(JsonObject reservation)
    {
        var result = new List<OperaGuestProfileInfo>();
        foreach (var node in reservation["reservationGuests"]?.AsArray() ?? [])
        {
            var profileInfo = node?["profileInfo"];
            var id = profileInfo?["profileIdList"]?.AsArray().Select(ReadId)
                .FirstOrDefault(x => x.Type.Equals("Profile", StringComparison.OrdinalIgnoreCase)).Id;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var name = ReadPersonName(profileInfo?["profile"]?["customer"]?["personName"]?.AsArray().FirstOrDefault());
            var primary = node?["primary"]?.GetValue<bool>() == true;
            result.Add(new OperaGuestProfileInfo(id, string.IsNullOrWhiteSpace(name) ? $"Profile {id}" : name, primary));
        }
        return result;
    }

    private static JsonObject BuildPayload(JsonObject source, string newProfileId, int adults)
    {
        var roomStay = source["roomStay"]?.AsObject() ?? throw new InvalidOperationException("La reserva no contiene roomStay.");
        var guestCounts = roomStay["guestCounts"]?.DeepClone()?.AsObject() ?? new JsonObject();
        guestCounts["adults"] = adults;

        var roomRates = new JsonArray();
        foreach (var rateNode in roomStay["roomRates"]?.AsArray() ?? [])
        {
            var sourceRate = rateNode!.AsObject();
            var rate = CopyProperties(sourceRate,
                "rates", "roomType", "ratePlanCode", "start", "end", "marketCode", "sourceCode",
                "numberOfUnits", "pseudoRoom", "roomTypeCharged", "suppressRate", "fixedRate");
            var rateCounts = sourceRate["guestCounts"]?.DeepClone()?.AsObject() ?? new JsonObject();
            rateCounts["adults"] = adults;
            rate["guestCounts"] = rateCounts;
            roomRates.Add(rate);
        }

        var safeRoomStay = CopyProperties(roomStay, "arrivalDate", "departureDate", "roomNumberLocked", "printRate");
        safeRoomStay["guestCounts"] = guestCounts;
        safeRoomStay["roomRates"] = roomRates;
        if (roomStay["guarantee"]?["guaranteeCode"] is JsonNode guaranteeCode)
            safeRoomStay["guarantee"] = new JsonObject { ["guaranteeCode"] = guaranteeCode.DeepClone() };

        var reservationGuests = new JsonArray();
        foreach (var guest in ReadGuests(source)) reservationGuests.Add(BuildGuest(guest.ProfileId, guest.Primary));
        reservationGuests.Add(BuildGuest(newProfileId, false));

        var reservation = new JsonObject
        {
            ["reservationIdList"] = new JsonArray(new JsonObject { ["type"] = "Reservation", ["id"] = ReadReservationId(source) }),
            ["roomStay"] = safeRoomStay,
            ["reservationGuests"] = reservationGuests,
            ["hotelId"] = source["hotelId"]?.DeepClone()
        };
        return new JsonObject { ["reservations"] = new JsonArray(reservation) };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string hotelId, string correlationId, HttpContent? content, CancellationToken ct)
    {
        var token = await _tokenService.GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, $"{_options.GatewayUrl}{path}") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-app-key", _options.AppKey);
        request.Headers.Add("x-hotelid", hotelId);
        request.Headers.Add("x-originating-application", "FirmaOperaCloud");
        request.Headers.Add("x-request-id", correlationId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _httpClient.SendAsync(request, ct);
    }

    private void EnsureWritesAllowed(string hotelId)
    {
        if (!_options.AllowAccompanyingGuestWrites || !IsUatGateway())
            throw new InvalidOperationException("Las escrituras de acompañantes están deshabilitadas fuera de UAT.");
        if (!hotelId.Equals("VINV", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La primera versión solo está autorizada para el hotel VINV.");
    }

    private bool IsUatGateway() => Uri.TryCreate(_options.GatewayUrl, UriKind.Absolute, out var uri) &&
                                   uri.Host.EndsWith("oc-test.com", StringComparison.OrdinalIgnoreCase);

    private static void ValidateInput(string hotelId, string confirmationNumber, string profileId)
    {
        ValidateReservationInput(hotelId, confirmationNumber);
        if (string.IsNullOrWhiteSpace(profileId) || !profileId.All(char.IsDigit))
            throw new ArgumentException("El Profile ID debe contener solo números.");
    }

    private static void ValidateReservationInput(string hotelId, string confirmationNumber)
    {
        if (string.IsNullOrWhiteSpace(hotelId)) throw new ArgumentException("Debe indicar el hotel.");
        if (string.IsNullOrWhiteSpace(confirmationNumber) || !confirmationNumber.All(char.IsDigit))
            throw new ArgumentException("La confirmación debe contener solo números.");
    }

    private static (string GivenName, string Surname) ValidateAndNormalizeName(string givenName, string surname)
    {
        givenName = string.Join(' ', (givenName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        surname = string.Join(' ', (surname ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (givenName.Length < 2 || surname.Length < 2)
            throw new ArgumentException("Capture nombre y apellido del acompañante (mínimo dos caracteres cada uno).");
        if (givenName.Length > 40 || surname.Length > 40)
            throw new ArgumentException("Nombre y apellido admiten un máximo de 40 caracteres cada uno en OPERA.");
        return (givenName, surname);
    }

    private static JsonObject BuildGuest(string profileId, bool primary) => new()
    {
        ["profileInfo"] = new JsonObject
        {
            ["profileIdList"] = new JsonArray(new JsonObject { ["type"] = "Profile", ["id"] = profileId }),
            ["profile"] = new JsonObject()
        },
        ["primary"] = primary
    };

    private static JsonObject CopyProperties(JsonObject source, params string[] names)
    {
        var result = new JsonObject();
        foreach (var name in names) if (source[name] is JsonNode value) result[name] = value.DeepClone();
        return result;
    }

    private static string ReadReservationId(JsonObject reservation) => reservation["reservationIdList"]?.AsArray()
        .Select(ReadId).FirstOrDefault(x => x.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase)).Id
        ?? throw new InvalidOperationException("No se encontró Reservation ID.");

    private static (string Id, string Type) ReadId(JsonNode? node) =>
        (node?["id"]?.ToString() ?? string.Empty, node?["type"]?.ToString() ?? string.Empty);

    private static string ReadPersonName(JsonNode? node) => string.Join(" ", new[]
    {
        node?["givenName"]?.GetValue<string>(), node?["middleName"]?.GetValue<string>(), node?["surname"]?.GetValue<string>()
    }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private sealed record ReservationDetail(string ReservationId, string LastModifyDateTime, JsonObject Reservation);
}
