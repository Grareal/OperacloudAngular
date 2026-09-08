using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Shared.Options;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Infrastructure.Opera;

public sealed class OperaRegistrationCardService : IOperaRegistrationCardService
{
    private readonly HttpClient _httpClient;
    private readonly IOperaTokenService _tokenService;
    private readonly OperaCloudOptions _options;

    private static readonly (string Prefix, string Template)[] TemplateRules =
    [
        ("BON", "bon_registration_card"),
        ("MP", "regcard_mp_nv"),
        ("GMA", "regcard_tgm_nv"),
        ("SG", "regcard_sg_nv"),
        ("GBL", "regcard_tgb_nv"),
        ("EST", "regcard_estates_nv"),
        ("LVI", "regcard_tgl_nv"),
        ("LC", "regcard_tgl_nv"),
        ("CP", "regcard_cp_nv"),
        ("EP", "regcard_ep_nv"),
        ("KOT", "regcard_kots_nv"),
        ("DLX", "regcard_deluxxe_nv")
    ];

    public OperaRegistrationCardService(
        HttpClient httpClient,
        IOperaTokenService tokenService,
        IOptions<OperaCloudOptions> options)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public string ResolveTemplate(Reservation reservation, string? requestedTemplate = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedTemplate))
        {
            return requestedTemplate.Trim();
        }

        var roomType = reservation.RoomStay.RoomType?.Trim() ?? string.Empty;
        var match = TemplateRules.FirstOrDefault(rule =>
            roomType.StartsWith(rule.Prefix, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(match.Template))
        {
            return match.Template;
        }

        throw new InvalidOperationException(
            $"No existe un mapeo de Registration Card para el room type '{roomType}'. Seleccione una plantilla.");
    }

    public async Task<byte[]> GetOfficialPdfAsync(
        string hotelId,
        string reservationId,
        string template,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"regenerate=false&signedOnly=false&reservationIdContext=OPERA&reservationIdType=Reservation" +
                    $"&language={Uri.EscapeDataString(string.IsNullOrWhiteSpace(language) ? "EN" : language)}" +
                    $"&template={Uri.EscapeDataString(template)}";
        var path = $"/med/config/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations/" +
                   $"{Uri.EscapeDataString(reservationId)}/registrationCard?{query}";

        using var response = await SendAsync(HttpMethod.Get, path, hotelId, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Generar Registration Card en OPERA");

        using var document = JsonDocument.Parse(json);
        var base64 = document.RootElement
            .GetProperty("registrationCard")
            .GetProperty("registrationCard")
            .GetString();

        var bytes = string.IsNullOrWhiteSpace(base64) ? [] : Convert.FromBase64String(base64);
        if (bytes.Length < 4 || bytes[0] != (byte)'%' || bytes[1] != (byte)'P' || bytes[2] != (byte)'D' || bytes[3] != (byte)'F')
        {
            throw new InvalidDataException("OPERA no devolvió un PDF válido para la Registration Card seleccionada.");
        }

        return bytes;
    }

    public async Task<IReadOnlyList<OperaAttachmentResult>> GetAttachmentsAsync(
        string hotelId,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        var path = $"/rsv/v1/hotels/{Uri.EscapeDataString(hotelId)}/reservations/" +
                   $"{Uri.EscapeDataString(reservationId)}/attachments";
        using var response = await SendAsync(HttpMethod.Get, path, hotelId, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Consultar adjuntos de OPERA");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("reservationAttachments", out var attachments) ||
            attachments.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return attachments.EnumerateArray().Select(item => new OperaAttachmentResult(
            item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("fileName", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("fileSize", out var size) ? size.GetInt32() : 0,
            item.TryGetProperty("description", out var description) ? description.GetString() : null)).ToList();
    }

    public async Task<OperaAttachmentResult> UploadPdfAsync(
        string hotelId,
        string reservationId,
        string confirmationNumber,
        byte[] pdf,
        string userName,
        CancellationToken cancellationToken = default,
        string? documentVersion = null)
    {
        var safeConfirmation = new string(confirmationNumber.Where(char.IsLetterOrDigit).ToArray());
        var safeVersion = string.IsNullOrWhiteSpace(documentVersion)
            ? string.Empty
            : "-" + new string(documentVersion.Where(char.IsLetterOrDigit).ToArray());
        var fileName = $"REGCARD{safeConfirmation}SIGNED{safeVersion}.pdf";
        var existing = (await GetAttachmentsAsync(hotelId, reservationId, cancellationToken))
            .FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var payload = new
        {
            fileName,
            linkId = reservationId,
            overwriteExistingFileYN = "N",
            description = "Registration Card firmada electrónicamente desde Firma Opera Cloud",
            linkType = "Reservation",
            hotelId,
            userName,
            globalYN = "N",
            fileAttachment = Convert.ToBase64String(pdf)
        };

        // The official OHIP Postman request for "post File Attachments -> eRegCard"
        // sends application/json without a charset. JsonContent adds charset=utf-8,
        // which OPERA Cloud 26.1 rejects with 415 for this particular operation.
        var jsonPayload = JsonSerializer.Serialize(payload);
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(jsonPayload));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        using var response = await SendAsync(HttpMethod.Post, "/med/config/v1/fileAttachments", hotelId, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Subir Registration Card a OPERA");

        var uploaded = (await GetAttachmentsAsync(hotelId, reservationId, cancellationToken))
            .FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return uploaded ?? throw new InvalidOperationException("OPERA aceptó el archivo, pero no fue posible recuperarlo desde la reservación.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string hotelId,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, $"{_options.GatewayUrl}{path}") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-app-key", _options.AppKey);
        request.Headers.Add("x-hotelid", hotelId);
        request.Headers.Add("x-originating-application", "FirmaOperaCloud");
        request.Headers.Add("x-request-id", Guid.NewGuid().ToString());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{operation}: {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }
}
