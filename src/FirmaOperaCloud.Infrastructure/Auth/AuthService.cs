using System.Security.Cryptography;
using System.Text.Json;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Infrastructure.Auth;

/// <summary>Autenticación local. La API conserva la sesión en una cookie HttpOnly.</summary>
public sealed class AuthService(FirmaOperaCloudDbContext db) : IAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;

        var user = await db.AppUsers.Include(x => x.UserGroup)
            .FirstOrDefaultAsync(x => x.Username == username.Trim() && x.IsActive, cancellationToken);
        if (user is null || !VerifyPassword(password, user.PasswordHash)) return null;

        return new AuthenticatedUser(
            user.Id, user.Username, user.DisplayName, user.Role, ReadPermissions(user));
    }

    private static IReadOnlyList<string> ReadPermissions(AppUser user)
    {
        if (user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && user.UserGroup is null)
            return ViewPermissions.All;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(user.UserGroup?.PermissionsJson ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = stored.Split('.');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            if (CryptographicOperations.FixedTimeEquals(actual, expected)) return true;

            // Compatibilidad con usuarios creados por la aplicación original.
            var legacy = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(legacy, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
