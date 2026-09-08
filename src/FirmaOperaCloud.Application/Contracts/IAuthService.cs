namespace FirmaOperaCloud.Application.Contracts;

/// <summary>Identidad validada que se convertirá en una sesión segura del servidor.</summary>
public sealed record AuthenticatedUser(
    long UserId,
    string Username,
    string DisplayName,
    string Role,
    IReadOnlyList<string> Permissions);

public interface IAuthService
{
    /// <summary>Valida credenciales locales. La contraseña y cualquier token nunca se devuelven al SPA.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);
}
