using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Api.Controllers;

/// <summary>Pruebas UAT explícitas y de solo lectura; uelve el token.</summary>
[ApiController, Authorize(Roles = "Admin"), Route("api/opera-uat-validation")]
public sealed class OperaUatValidationController(
    IOperaTokenService tokens,
    IReservationService reservations,
    IOptions<OperaCloudOptions> options) : ControllerBase
{
    [HttpGet("authentication")]
    public async Task<IActionResult> Authentication(CancellationToken ct)
    {
        EnsureUat();
        var token = await tokens.GetAccessTokenAsync(ct);
        return Ok(new
        {
            success = !string.IsNullOrWhiteSpace(token),
            environment = "UAT",
            options.Value.DefaultHotelId,
            checkedAtUtc = DateTime.UtcNow
        });
    }

    [HttpGet("reservation/{confirmationNumber}")]
    public async Task<IActionResult> Reservation(string confirmationNumber, CancellationToken ct)
    {
        EnsureUat();
        var rows = await reservations.GetByConfirmationNumberAsync(
            options.Value.DefaultHotelId, confirmationNumber.Trim(), ct);
        return Ok(new
        {
            success = true,
            count = rows.Count,
            confirmations = rows.Select(x => x.ConfirmationNumber).ToArray(),
            checkedAtUtc = DateTime.UtcNow
        });
    }

    private void EnsureUat()
    {
        if (!Uri.TryCreate(options.Value.GatewayUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Contains("oc-test.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La validación fue bloqueada: el gateway no corresponde a OPERA UAT.");
    }
}
