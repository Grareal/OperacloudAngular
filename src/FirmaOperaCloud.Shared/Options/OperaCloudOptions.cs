namespace FirmaOperaCloud.Shared.Options;

/// <summary>
/// Opciones de configuración para conectarse a la API de Oracle OPERA Cloud (OHIP).
/// Se cargan desde appsettings.json mediante el patrón IOptions.
/// </summary>
public sealed class OperaCloudOptions
{
    public const string SectionName = "OperaCloud";

    /// <summary>App Key (x-app-key) de la aplicación en el Developer Portal.</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>URL base de la pasarela (gateway) del entorno.</summary>
    public string GatewayUrl { get; set; } = string.Empty;

    /// <summary>Ruta completa del endpoint de tokens (ej. /oauth/v1/tokens).</summary>
    public string TokenPath { get; set; } = "/oauth/v1/tokens";

    /// <summary>Client ID OCIM del entorno.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret OCIM del entorno.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Enterprise ID (ej. EVIDA).</summary>
    public string EnterpriseId { get; set; } = string.Empty;

    /// <summary>Scope de OAuth (ej. urn:opc:hgbu:ws:__myscopes__).</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Hotel ID por defecto (x-hotelid).</summary>
    public string DefaultHotelId { get; set; } = string.Empty;

    /// <summary>Margen en segundos antes de la expiración para renovar el token.</summary>
    public int TokenRenewBufferSeconds { get; set; } = 300;

    /// <summary>Timeout HTTP en segundos para llamadas a OPERA.</summary>
    public int HttpTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Interruptor de seguridad. Las escrituras de acompañantes permanecen apagadas
    /// salvo que el ambiente las habilite explícitamente (actualmente solo UAT).
    /// </summary>
    public bool AllowAccompanyingGuestWrites { get; set; }
}
