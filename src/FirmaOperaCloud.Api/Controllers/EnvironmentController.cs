using FirmaOperaCloud.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/environment")]
public sealed class EnvironmentController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var opera = configuration.GetSection(OperaCloudOptions.SectionName).Get<OperaCloudOptions>() ?? new();
        var connection = configuration.GetConnectionString("FirmaOperaCloud") ?? "";
        var database = connection.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
            .FirstOrDefault(x => x.Length == 2 && (x[0].Equals("Database", StringComparison.OrdinalIgnoreCase) || x[0].Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)))?[1] ?? "";
        var gateway = Uri.TryCreate(opera.GatewayUrl, UriKind.Absolute, out var uri) ? uri.Host : opera.GatewayUrl;
        var isUat = gateway.Contains("oc-test.com", StringComparison.OrdinalIgnoreCase) && database.EndsWith("_UAT", StringComparison.OrdinalIgnoreCase);
        return Ok(new { environment = environment.EnvironmentName, isUat, hotelId = opera.DefaultHotelId, database, gateway });
    }
}
