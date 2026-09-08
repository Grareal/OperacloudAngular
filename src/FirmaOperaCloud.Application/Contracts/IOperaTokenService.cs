namespace FirmaOperaCloud.Application.Contracts;

/// <summary>
/// Gestiona el token OAuth de acceso a OPERA Cloud (flujo Client Credentials / OCIM).
/// Encapsula completamente la autenticación: reutiliza el token mientras sea válido
/// y lo renueva automáticamente antes de que expire.
/// </summary>
public interface IOperaTokenService
{
    /// <summary>
    /// Devuelve un token de acceso válido. Renueva automáticamente si expiró o está por expirar.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
