using Azure.Identity;
using FirmaOperaCloud.Api.Services;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Auth;
using FirmaOperaCloud.Infrastructure.Ocr;
using FirmaOperaCloud.Infrastructure.Opera;
using FirmaOperaCloud.Infrastructure.Pdf;
using FirmaOperaCloud.Infrastructure.Persistence;
using FirmaOperaCloud.Shared.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PdfSharp.Fonts;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// KEY_VAULT_URI no es secreto. DefaultAzureCredential usa identidad administrada en
// Azure y Azure CLI/Visual Studio durante el desarrollo.
var keyVaultUri = builder.Configuration["KEY_VAULT_URI"];
if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri))
{
    builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
}

GlobalFontSettings.UseWindowsFontsUnderWindows = true;

builder.Services.Configure<OperaCloudOptions>(
    builder.Configuration.GetSection(OperaCloudOptions.SectionName));
builder.Services.Configure<GuestEmailOptions>(
    builder.Configuration.GetSection(GuestEmailOptions.SectionName));

builder.Services.AddHttpClient<IOperaTokenService, OperaTokenService>((sp, client) =>
    client.Timeout = OperaTimeout(sp));
builder.Services.AddHttpClient<IReservationService, OperaReservationService>((sp, client) =>
    client.Timeout = OperaTimeout(sp));
builder.Services.AddHttpClient<IOperaRegistrationCardService, OperaRegistrationCardService>((sp, client) =>
    client.Timeout = OperaTimeout(sp));
builder.Services.AddHttpClient<IOperaAccompanyingGuestService, OperaAccompanyingGuestService>((sp, client) =>
    client.Timeout = OperaTimeout(sp));

static TimeSpan OperaTimeout(IServiceProvider services)
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OperaCloudOptions>>().Value;
    return TimeSpan.FromSeconds(Math.Clamp(options.HttpTimeoutSeconds, 5, 180));
}

var connectionString = builder.Configuration.GetConnectionString("FirmaOperaCloud");
if (string.IsNullOrWhiteSpace(connectionString) && builder.Environment.IsDevelopment())
{
    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=FirmaOperaCloud_Development;Trusted_Connection=True;TrustServerCertificate=True";
}
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings__FirmaOperaCloud en variables de entorno o Key Vault.");
}
builder.Services.AddDbContext<FirmaOperaCloudDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

var sessionMinutes = Math.Clamp(builder.Configuration.GetValue("Security:SessionMinutes", 60), 10, 480);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("FirmaOperaCloud");
var dataProtectionPath = builder.Configuration["DATA_PROTECTION_KEYS_PATH"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FirmaOperaCloud.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageAccompanyingGuests", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") ||
        context.User.HasClaim("view", ViewPermissions.RegistrationCard) ||
        context.User.HasClaim("view", ViewPermissions.AccompanyingGuests)));
    options.AddPolicy("Documents.Read", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.Documents)));
    options.AddPolicy("Documents.Seal", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Signatures.Read", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.SignatureLookup)));
    options.AddPolicy("Audit.Read", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.Audit)));
    options.AddPolicy("Ocr.Use", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.OcrIdentity)));
    options.AddPolicy("RegistrationCard.Use", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") ||
        context.User.HasClaim("view", ViewPermissions.RegistrationCard) ||
        context.User.HasClaim("view", ViewPermissions.Operation)));
    options.AddPolicy("PdfTemplates.Manage", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.PdfTemplates)));
    options.AddPolicy("Communications.Manage", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.Communications)));
    options.AddPolicy("EmailSettings.Manage", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.EmailSettings)));
    options.AddPolicy("Promotions.Manage", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim("view", ViewPermissions.Promotions)));
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.HeaderName = "X-XSRF-TOKEN";
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRegistrationCardService, RegistrationCardService>();
builder.Services.AddScoped<IRegistrationCardRepository, RegistrationCardRepository>();
builder.Services.AddScoped<IRegistrationCardStoreService, RegistrationCardStoreService>();
builder.Services.AddScoped<IRegistrationCardPdfFiller, RegistrationCardPdfFiller>();
builder.Services.AddScoped<LocalDocumentService>();
builder.Services.AddScoped<ReservationFileService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IdentityEvidencePdfService>();
builder.Services.AddScoped<IOcrService, TesseractOcrService>();
builder.Services.AddScoped<PdfTemplateRenderService>();
builder.Services.AddScoped<GuestEmailQueueService>();
builder.Services.AddSingleton<IGuestEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient("GuestDocuments", client => client.Timeout = TimeSpan.FromSeconds(45));
if (builder.Configuration.GetValue("GuestEmail:Enabled", false))
{
    builder.Services.AddHostedService<GuestEmailWorker>();
}

builder.Services.AddControllers(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Cookie", new OpenApiSecurityScheme
    {
        Name = "FirmaOperaCloud.Session",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Sesión HttpOnly emitida por /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Cookie" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
ShowEnvironmentBanner(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// En UAT/producción se recomienda ejecutar migraciones como paso separado del despliegue.
if (builder.Configuration.GetValue("Database:ApplyMigrations", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FirmaOperaCloudDbContext>();
    db.Database.Migrate();
    SeedUserGroups(db);
    SeedAdminUser(db, builder.Configuration);
    SeedCommunicationDocuments(db);
    SeedPdfTemplates(db);
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<AuditMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

static void SeedAdminUser(FirmaOperaCloudDbContext db, IConfiguration configuration)
{
    var adminGroup = db.UserGroups.First(x => x.Name == "Administradores");
    var existing = db.AppUsers.FirstOrDefault(x => x.Username == "admin");
    if (existing is not null)
    {
        if (existing.UserGroupId is null)
        {
            existing.UserGroupId = adminGroup.Id;
            db.SaveChanges();
        }
        return;
    }

    var initialPassword = configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(initialPassword))
    {
        Console.WriteLine(
            "No se creó el administrador inicial. Defina BootstrapAdmin__Password para una base nueva.");
        return;
    }

    db.AppUsers.Add(new AppUser
    {
        Username = "admin",
        PasswordHash = AuthService.HashPassword(initialPassword),
        DisplayName = "Administrador",
        Role = "Admin",
        UserGroupId = adminGroup.Id,
        IsActive = true
    });
    db.SaveChanges();
}

static void SeedUserGroups(FirmaOperaCloudDbContext db)
{
    var all = System.Text.Json.JsonSerializer.Serialize(ViewPermissions.All);
    var admins = db.UserGroups.FirstOrDefault(x => x.Name == "Administradores");
    if (admins is null)
    {
        db.UserGroups.Add(new UserGroup
        {
            Name = "Administradores",
            Description = "Acceso completo al sistema.",
            PermissionsJson = all,
            IsSystem = true
        });
    }
    else if (admins.PermissionsJson != all)
    {
        admins.PermissionsJson = all;
    }

    if (!db.UserGroups.Any(x => x.Name == "Recepción"))
    {
        db.UserGroups.Add(new UserGroup
        {
            Name = "Recepción",
            Description = "Operación diaria, tarjetas y consulta de firmas.",
            IsSystem = true,
            PermissionsJson = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                ViewPermissions.Operation,
                ViewPermissions.RegistrationCard,
                ViewPermissions.SignatureLookup
            })
        });
    }
    db.SaveChanges();
}

static void SeedCommunicationDocuments(FirmaOperaCloudDbContext db)
{
    if (db.CommunicationDocuments.Any(x =>
        x.HotelId == "VINV" && x.Type == CommunicationDocumentTypes.PrivacyNotice)) return;

    db.CommunicationDocuments.Add(new CommunicationDocument
    {
        HotelId = "VINV",
        Type = CommunicationDocumentTypes.PrivacyNotice,
        Name = "Aviso integral de privacidad Vidanta",
        Language = "EN",
        Version = 1,
        Source = CommunicationDocumentSources.RemoteUrl,
        SourceUrl = "https://avisos.vidanta.com/en/oth.pdf",
        FileName = "vidanta-privacy-notice-en.pdf",
        ContentType = "application/pdf",
        IsPublished = true,
        IsRequired = true,
        SortOrder = 10,
        CreatedBy = "System"
    });
    db.SaveChanges();
}

static void SeedPdfTemplates(FirmaOperaCloudDbContext db)
{
    if (db.PdfTemplates.Any(x =>
        x.HotelId == "VINV" && x.TemplateType == PdfTemplateTypes.Promotion &&
        x.Name == "Beneficios Vidanta")) return;

    var seed = PromotionTemplateSeed.Create();
    var template = new PdfTemplate
    {
        HotelId = "VINV",
        Name = "Beneficios Vidanta",
        TemplateType = PdfTemplateTypes.Promotion,
        Version = 1,
        FileName = "beneficios-vidanta.pdf",
        PdfData = seed.Pdf,
        Language = "ES",
        IsPublished = false,
        IsDefault = true,
        CreatedBy = "System"
    };
    foreach (var field in seed.Fields) template.Fields.Add(field);
    db.PdfTemplates.Add(template);
    db.SaveChanges();
}

static void ShowEnvironmentBanner(WebApplication app)
{
    var environment = app.Environment.EnvironmentName;
    var opera = app.Configuration.GetSection(OperaCloudOptions.SectionName)
        .Get<OperaCloudOptions>() ?? new OperaCloudOptions();
    var connection = app.Configuration.GetConnectionString("FirmaOperaCloud") ?? string.Empty;
    var database = connection.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
        .FirstOrDefault(x => x.Length == 2 &&
            (x[0].Equals("Database", StringComparison.OrdinalIgnoreCase) ||
             x[0].Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)))?[1] ?? "No identificada";
    var gateway = Uri.TryCreate(opera.GatewayUrl, UriKind.Absolute, out var uri)
        ? uri.Host
        : opera.GatewayUrl;
    var isUatGateway = gateway.Contains("oc-test.com", StringComparison.OrdinalIgnoreCase);
    var isUatDatabase = database.EndsWith("_UAT", StringComparison.OrdinalIgnoreCase);
    var isUat = isUatGateway && isUatDatabase;
    var hasEnvironmentTargets = !string.IsNullOrWhiteSpace(gateway) && database != "No identificada";
    var isProduction = hasEnvironmentTargets && !isUatGateway && !isUatDatabase;

    Console.ForegroundColor = isProduction ? ConsoleColor.Red : isUat ? ConsoleColor.Yellow : ConsoleColor.Magenta;
    Console.WriteLine("============================================================");
    Console.WriteLine($" FIRMA OPERA CLOUD -> {environment.ToUpperInvariant()}");
    Console.WriteLine($" OHIP:  {gateway}");
    Console.WriteLine($" HOTEL: {opera.DefaultHotelId}");
    Console.WriteLine($" DB:    {database}");
    Console.WriteLine(isProduction ? " ATENCIÓN: configuración de producción" :
        isUat ? " UAT CONFIRMADO: OHIP UAT + BASE UAT" :
        " CONFIGURACIÓN INCOMPLETA O MEZCLADA; no realice escrituras");
    Console.WriteLine("============================================================");
    Console.ResetColor();
}
