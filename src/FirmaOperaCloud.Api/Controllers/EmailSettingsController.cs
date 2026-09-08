using System.Net.Mail;
using FirmaOperaCloud.Api.Services;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "EmailSettings.Manage"), Route("api/email-settings")]
public sealed class EmailSettingsController(FirmaOperaCloudDbContext db, IGuestEmailSender sender) : ControllerBase
{
    [HttpGet("{hotelId}")]
    public async Task<IActionResult> Get(string hotelId, CancellationToken ct)
    {
        var key = hotelId.Trim().ToUpperInvariant();
        var row = await db.GuestEmailSettings.AsNoTracking().FirstOrDefaultAsync(x => x.HotelId == key, ct)
            ?? new GuestEmailSetting { HotelId = key };
        return Ok(ToOutput(row));
    }

    [HttpPut("{hotelId}")]
    public async Task<IActionResult> Save(string hotelId, GuestEmailSettingInput input, CancellationToken ct)
    {
        if (input.Enabled && (string.IsNullOrWhiteSpace(input.FromAddress) || string.IsNullOrWhiteSpace(input.Host) || input.Port is < 1 or > 65535))
            return BadRequest(new { message = "Para activar el correo indique servidor SMTP, puerto y remitente." });
        if (!string.IsNullOrWhiteSpace(input.FromAddress) && !MailAddress.TryCreate(input.FromAddress, out _))
            return BadRequest(new { message = "El buzón remitente no es válido." });

        var key = hotelId.Trim().ToUpperInvariant();
        var row = await db.GuestEmailSettings.FindAsync([key], ct);
        if (row is null) { row = new GuestEmailSetting { HotelId = key }; db.Add(row); }
        Apply(row, input);
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = User.Identity?.Name;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{hotelId}/test")]
    public async Task<IActionResult> Test(string hotelId, EmailTestInput input, CancellationToken ct)
    {
        if (!MailAddress.TryCreate(input.Recipient, out _)) return BadRequest(new { message = "El destinatario no es válido." });
        var row = await db.GuestEmailSettings.AsNoTracking().FirstOrDefaultAsync(x => x.HotelId == hotelId.ToUpper(), ct);
        if (row is null || string.IsNullOrWhiteSpace(row.FromAddress)) return BadRequest(new { message = "Guarde primero la configuración SMTP." });
        var cfg = ToOptions(row);
        if (!sender.IsConfigured(cfg)) return BadRequest(new { message = "SMTP no está configurado en el servidor." });

        try
        {
            var requestId = await sender.SendAsync(cfg, input.Recipient, $"Prueba SMTP Firma Vidanta - {row.HotelId}",
                $"<p>La integración SMTP para {row.HotelId} aceptó este correo.</p><p>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>", [], ct);
            return Ok(new { message = $"SMTP aceptó el correo de prueba para {input.Recipient}.", requestId });
        }
        catch (Exception ex) { return BadRequest(new { message = $"No fue posible enviar mediante SMTP: {ex.Message}" }); }
    }

    private EmailSettingOutput ToOutput(GuestEmailSetting row) => new(row.HotelId, row.Enabled, row.FromAddress,
        row.FromName, row.Subject, row.BodyHtml, row.MaxAttempts, row.Host, row.Port, row.EnableSsl, row.Username,
        !string.IsNullOrWhiteSpace(row.Password), sender.IsConfigured(ToOptions(row)));

    private static void Apply(GuestEmailSetting row, GuestEmailSettingInput input)
    {
        row.Enabled = input.Enabled; row.Host = input.Host.Trim(); row.Port = Math.Clamp(input.Port, 1, 65535);
        row.EnableSsl = input.EnableSsl; row.Username = input.Username.Trim();
        if (!string.IsNullOrWhiteSpace(input.Password)) row.Password = input.Password;
        row.FromAddress = input.FromAddress.Trim(); row.FromName = input.FromName.Trim(); row.Subject = input.Subject.Trim();
        row.BodyHtml = input.BodyHtml; row.MaxAttempts = Math.Clamp(input.MaxAttempts, 1, 20);
    }

    private static GuestEmailOptions ToOptions(GuestEmailSetting row) => new()
    {
        Enabled = row.Enabled, Host = row.Host, Port = row.Port, EnableSsl = row.EnableSsl, Username = row.Username, Password = row.Password,
        FromAddress = row.FromAddress, FromName = row.FromName, Subject = row.Subject, BodyHtml = row.BodyHtml, MaxAttempts = row.MaxAttempts
    };
}

public sealed class GuestEmailSettingInput
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Vidanta";
    public string Subject { get; set; } = "Documentos de su registro Vidanta - Reserva {confirmation}";
    public string BodyHtml { get; set; } = "";
    public int MaxAttempts { get; set; } = 5;
}

public sealed record EmailSettingOutput(string HotelId, bool Enabled, string FromAddress, string FromName,
    string Subject, string BodyHtml, int MaxAttempts, string Host, int Port, bool EnableSsl, string Username,
    bool PasswordConfigured, bool SmtpConfigured);
public sealed class EmailTestInput { public string Recipient { get; set; } = ""; }
