using System.Text.Json;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Auth;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/access")]
public sealed class UsersController(FirmaOperaCloudDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var groups = await db.UserGroups.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var users = await db.AppUsers.AsNoTracking().OrderBy(x => x.Username).ToListAsync(ct);
        return Ok(new
        {
            permissions = PermissionCatalog,
            groups = groups.Select(x => new UserGroupInfo(x.Id, x.Name, x.Description, Read(x.PermissionsJson), x.IsSystem, x.IsActive)),
            users = users.Select(x => new AppUserInfo(x.Id, x.Username, x.DisplayName, x.Role, x.UserGroupId, x.IsActive, x.CreatedAt))
        });
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(SaveUserGroupRequest request, CancellationToken ct)
    {
        var error = ValidateGroup(request); if (error is not null) return BadRequest(new { message = error });
        if (await db.UserGroups.AnyAsync(x => x.Name == request.Name.Trim(), ct)) return Conflict(new { message = "Ya existe un grupo con ese nombre." });
        var row = new UserGroup(); Apply(row, request); db.UserGroups.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id });
    }

    [HttpPut("groups/{id:long}")]
    public async Task<IActionResult> UpdateGroup(long id, SaveUserGroupRequest request, CancellationToken ct)
    {
        var row = await db.UserGroups.FirstOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
        var error = ValidateGroup(request); if (error is not null) return BadRequest(new { message = error });
        if (row.IsSystem && (!row.Name.Equals(request.Name.Trim(), StringComparison.Ordinal) || !request.IsActive))
            return BadRequest(new { message = "Los grupos del sistema no se pueden renombrar ni desactivar." });
        if (row.IsSystem && row.Name == "Administradores" && !ViewPermissions.All.All(request.Permissions.Contains))
            return BadRequest(new { message = "El grupo Administradores debe conservar acceso completo." });
        Apply(row, request); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(SaveAppUserRequest request, CancellationToken ct)
    {
        var error = await ValidateUser(request, true, ct); if (error is not null) return BadRequest(new { message = error });
        var row = new AppUser { Username = request.Username.Trim(), CreatedAt = DateTime.UtcNow };
        Apply(row, request); row.PasswordHash = AuthService.HashPassword(request.Password!);
        db.AppUsers.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id });
    }

    [HttpPut("users/{id:long}")]
    public async Task<IActionResult> UpdateUser(long id, SaveAppUserRequest request, CancellationToken ct)
    {
        var row = await db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
        var error = await ValidateUser(request, false, ct, id); if (error is not null) return BadRequest(new { message = error });
        if (row.Username == User.Identity!.Name && !request.IsActive) return BadRequest(new { message = "No puede desactivar su propia cuenta." });
        if (row.IsActive && row.Role == "Admin" && (!request.IsActive || request.Role != "Admin") &&
            await db.AppUsers.CountAsync(x => x.IsActive && x.Role == "Admin", ct) <= 1)
            return BadRequest(new { message = "Debe conservar al menos un administrador activo." });
        Apply(row, request); if (!string.IsNullOrWhiteSpace(request.Password)) row.PasswordHash = AuthService.HashPassword(request.Password);
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<string?> ValidateUser(SaveAppUserRequest x, bool requirePassword, CancellationToken ct, long? id = null)
    {
        if (string.IsNullOrWhiteSpace(x.Username) || x.Username.Trim().Length > 64) return "Usuario obligatorio; máximo 64 caracteres.";
        if (string.IsNullOrWhiteSpace(x.DisplayName)) return "Nombre visible obligatorio.";
        if (requirePassword && string.IsNullOrWhiteSpace(x.Password)) return "Contraseña obligatoria.";
        if (!string.IsNullOrEmpty(x.Password) && x.Password.Length < 8) return "La contraseña debe tener al menos 8 caracteres.";
        if (x.Role is not ("Admin" or "Receptionist")) return "Rol inválido.";
        if (!await db.UserGroups.AnyAsync(g => g.Id == x.UserGroupId && g.IsActive, ct)) return "Seleccione un grupo activo.";
        if (await db.AppUsers.AnyAsync(u => u.Username == x.Username.Trim() && u.Id != id, ct)) return "El usuario ya existe.";
        return null;
    }

    private static string? ValidateGroup(SaveUserGroupRequest x)
    {
        if (string.IsNullOrWhiteSpace(x.Name)) return "Nombre de grupo obligatorio.";
        if (x.Permissions.Except(ViewPermissions.All).Any()) return "El grupo contiene permisos inválidos.";
        return null;
    }
    private static void Apply(UserGroup row, SaveUserGroupRequest x) { row.Name = x.Name.Trim(); row.Description = x.Description?.Trim() ?? ""; row.PermissionsJson = JsonSerializer.Serialize(x.Permissions.Distinct()); row.IsActive = x.IsActive; }
    private static void Apply(AppUser row, SaveAppUserRequest x) { row.Username = x.Username.Trim(); row.DisplayName = x.DisplayName.Trim(); row.Role = x.Role; row.UserGroupId = x.UserGroupId; row.IsActive = x.IsActive; }
    private static List<string> Read(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch { return []; } }

    private static readonly object[] PermissionCatalog =
    [
        new { key=ViewPermissions.Operation, label="Operación y búsqueda" }, new { key=ViewPermissions.RegistrationCard, label="Registration Card y firma" },
        new { key=ViewPermissions.SignatureLookup, label="Consulta de firmas" }, new { key=ViewPermissions.History, label="Historial" },
        new { key=ViewPermissions.Documents, label="Expediente documental" }, new { key=ViewPermissions.PdfTemplates, label="Plantillas PDF" },
        new { key=ViewPermissions.Communications, label="Documentos por correo" }, new { key=ViewPermissions.EmailSettings, label="Configuración de correo" },
        new { key=ViewPermissions.Promotions, label="Promociones y códigos" }, new { key=ViewPermissions.AccompanyingGuests, label="Acompañantes OPERA" },
        new { key=ViewPermissions.UserAdministration, label="Usuarios y grupos" },
        new { key=ViewPermissions.Audit, label="Auditoría de accesos" },
        new { key=ViewPermissions.OcrIdentity, label="Captura OCR de identidad" }
    ];
}

public sealed record UserGroupInfo(long Id, string Name, string Description, List<string> Permissions, bool IsSystem, bool IsActive);
public sealed record AppUserInfo(long Id, string Username, string DisplayName, string Role, long? UserGroupId, bool IsActive, DateTime CreatedAt);
public sealed class SaveUserGroupRequest { public string Name { get; set; } = ""; public string? Description { get; set; } public List<string> Permissions { get; set; } = []; public bool IsActive { get; set; } = true; }
public sealed class SaveAppUserRequest { public string Username { get; set; } = ""; public string DisplayName { get; set; } = ""; public string Role { get; set; } = "Receptionist"; public long UserGroupId { get; set; } public string? Password { get; set; } public bool IsActive { get; set; } = true; }
