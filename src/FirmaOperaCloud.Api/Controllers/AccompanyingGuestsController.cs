using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController]
[Authorize(Policy = "ManageAccompanyingGuests")]
[Route("api/reservations/{confirmationNumber}/accompanying-guests")]
public sealed class AccompanyingGuestsController(
    IOperaAccompanyingGuestService service,
    IOptions<OperaCloudOptions> options) : ControllerBase
{
    [HttpGet("lookup-adult")]
    public async Task<IActionResult> LookupAdult(
        string confirmationNumber, [FromQuery] string givenName, [FromQuery] string surname, CancellationToken ct)
    {
        try
        {
            var result = await service.LookupAdultAsync(
                options.Value.DefaultHotelId, confirmationNumber, givenName, surname, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("preview-add-adult")]
    public async Task<IActionResult> PreviewAddAdult(
        string confirmationNumber, [FromQuery] string profileId, CancellationToken ct)
    {
        try
        {
            var preview = await service.PreviewAddAdultAsync(options.Value.DefaultHotelId, confirmationNumber, profileId, ct);
            return Ok(preview);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("add-adult")]
    public async Task<IActionResult> AddAdult(
        string confirmationNumber, AddAccompanyingAdultRequest request, CancellationToken ct)
    {
        if (!request.ConfirmationText.Equals("AGREGAR ACOMPAÑANTE", StringComparison.Ordinal))
            return BadRequest(new { message = "La confirmación escrita no coincide." });
        try
        {
            var result = await service.AddAdultAsync(
                options.Value.DefaultHotelId,
                confirmationNumber,
                request.ProfileId,
                request.ExpectedLastModifyDateTime,
                User.Identity?.Name ?? "Admin",
                ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("create-and-add-adult")]
    public async Task<IActionResult> CreateAndAddAdult(
        string confirmationNumber, CreateAndAddAccompanyingAdultRequest request, CancellationToken ct)
    {
        if (!request.ConfirmationText.Equals("AGREGAR ACOMPAÑANTE", StringComparison.Ordinal))
            return BadRequest(new { message = "La confirmación escrita no coincide." });
        try
        {
            var result = await service.CreateAndAddAdultAsync(
                options.Value.DefaultHotelId,
                confirmationNumber,
                request.GivenName,
                request.Surname,
                request.ExpectedLastModifyDateTime,
                User.Identity?.Name ?? "Admin",
                ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public sealed class AddAccompanyingAdultRequest
{
    public string ProfileId { get; set; } = string.Empty;
    public string ExpectedLastModifyDateTime { get; set; } = string.Empty;
    public string ConfirmationText { get; set; } = string.Empty;
}

public sealed class CreateAndAddAccompanyingAdultRequest
{
    public string GivenName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string ExpectedLastModifyDateTime { get; set; } = string.Empty;
    public string ConfirmationText { get; set; } = string.Empty;
}
