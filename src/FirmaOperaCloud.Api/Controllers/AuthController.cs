using System.Security.Claims;
using FirmaOperaCloud.Application.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { headerName = tokens.HeaderName });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authService.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (user is null) return Unauthorized(new { message = "Credenciales inválidas." });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role),
            new("username", user.Username)
        };
        claims.AddRange(user.Permissions.Select(x => new Claim("view", x)));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        return Ok(ToResponse(UserFromClaims(principal)));
    }

    [Authorize]
    [HttpGet("session")]
    public ActionResult<AuthSessionResponse> Session() => Ok(ToResponse(UserFromClaims(User)));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private static AuthenticatedUser UserFromClaims(ClaimsPrincipal principal) => new(
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0,
        principal.FindFirstValue("username") ?? string.Empty,
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
        principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
        principal.FindAll("view").Select(x => x.Value).Distinct().ToArray());

    private static AuthSessionResponse ToResponse(AuthenticatedUser user) =>
        new(user.Username, user.DisplayName, user.Role, user.Permissions);
}

public sealed record LoginRequest(string Username, string Password);
public sealed record AuthSessionResponse(
    string Username,
    string DisplayName,
    string Role,
    IReadOnlyList<string> Permissions);
