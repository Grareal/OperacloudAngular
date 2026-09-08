using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Shared.Options;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Infrastructure.Opera;

/// <summary>
/// Gestiona el token OAuth de OPERA Cloud usando el flujo Client Credentials (OCIM).
/// Reutiliza el token mientras sea válido y lo renueva automáticamente antes de expirar.
/// </summary>
public sealed class OperaTokenService : IOperaTokenService
{
    private readonly OperaCloudOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _token;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public OperaTokenService(HttpClient httpClient, IOptions<OperaCloudOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Si ya hay un token vigente (con margen de seguridad), se reutiliza sin llamar a OPERA.
        if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _token;
        }

        // Bloqueo para evitar solicitudes duplicadas cuando varios hilos piden token a la vez.
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Revalidar dentro del bloqueo (otro hilo pudo renovar mientras esperábamos).
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _expiresAtUtc)
            {
                return _token;
            }

            var request = BuildTokenRequest();

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Token: {(int)response.StatusCode} {response.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenElement) || tokenElement.GetString() is not { Length: > 0 } token)
            {
                throw new InvalidOperationException("No se pudo obtener el token de OPERA.");
            }

            _token = token;

            // Tiempo de expiración con margen de seguridad para renovar antes de que venza.
            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 28800;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn - _options.TokenRenewBufferSeconds);

            return _token;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private HttpRequestMessage BuildTokenRequest()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.GatewayUrl}{_options.TokenPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("x-app-key", _options.AppKey);
        request.Headers.Add("enterpriseId", _options.EnterpriseId);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = _options.Scope
        };
        request.Content = new FormUrlEncodedContent(form);

        return request;
    }
}
