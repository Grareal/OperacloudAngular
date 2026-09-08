using System.Security.Cryptography;
using System.Text.Json;
using FirmaOperaCloud.Api.Services;
using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Ocr;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Controllers;

[ApiController, Authorize(Policy = "Ocr.Use"), Route("api/ocr")]
public sealed class OcrController(
    IOcrService ocr,
    IdentityEvidencePdfService pdfService,
    ReservationFileService reservationFiles,
    IAuditService audit,
    IConfiguration configuration,
    FirmaOperaCloudDbContext db) : ControllerBase
{
    private const long MaxBytes = 10_000_000;

    [HttpPost("parse")]
    [RequestSizeLimit(MaxBytes * 2)]
    public async Task<IActionResult> Parse(
        IFormFile front,
        IFormFile? back,
        [FromForm] string documentType = "Auto",
        [FromForm] string language = "spa+eng",
        CancellationToken ct = default)
    {
        var frontBytes = await ReadValidatedImageAsync(front, ct);
        var backBytes = back is null ? null : await ReadValidatedImageAsync(back, ct);
        var frontResult = await ocr.RecognizeAsync(frontBytes, language, ct);
        var backResult = backBytes is null
            ? null
            : await ocr.RecognizeAsync(backBytes, language, ct);
        var fields = IdentityDocumentParser.Parse(frontResult.Text, backResult?.Text, documentType);

        await audit.AppendAsync(AuditRequest.Create(HttpContext, "Ocr.Parsed", "IdentityDocument",
            reason: "Extracción OCR para revisión humana",
            detailJson: JsonSerializer.Serialize(new
            {
                fields.DocType,
                HasBack = backBytes is not null,
                WarningCount = fields.Warnings.Count
            })), ct);

        return Ok(new
        {
            docType = fields.DocType,
            frontConfidence = frontResult.MeanConfidence,
            backConfidence = backResult?.MeanConfidence,
            frontText = frontResult.Text,
            backText = backResult?.Text,
            fields = new
            {
                fields.DocType, fields.FullName, fields.Curp, fields.ClaveElector,
                fields.Vigencia, fields.PassportNumber, fields.MrzLine1, fields.MrzLine2
            },
            fieldConfidences = BuildFieldConfidences(fields, frontResult.MeanConfidence,
                backResult?.MeanConfidence),
            warnings = fields.Warnings,
            humanReviewRequired = true
        });
    }

    [HttpPost("identity-pdf")]
    [RequestSizeLimit(MaxBytes * 2)]
    public async Task<IActionResult> IdentityPdf(
        IFormFile front,
        IFormFile? back,
        [FromForm] string confirmationNumber,
        [FromForm] string hotelId,
        [FromForm] string? roomNumber,
        [FromForm] string documentType,
        [FromForm] string reviewedFieldsJson,
        [FromForm] bool reviewConfirmed,
        [FromForm] bool retentionAccepted,
        [FromForm] string language = "spa+eng",
        CancellationToken ct = default)
    {
        if (!reviewConfirmed || !retentionAccepted)
            return BadRequest(new { message = "Debe revisar los campos y aceptar la política de conservación." });
        var confirmation = confirmationNumber.Trim();
        if (confirmation.Length < 2)
            return BadRequest(new { message = "Confirmación inválida." });
        hotelId = hotelId.Trim().ToUpperInvariant();
        if (hotelId.Length is < 2 or > 20)
            return BadRequest(new { message = "Hotel inválido." });

        ReviewedIdentityFields? reviewed;
        try
        {
            reviewed = JsonSerializer.Deserialize<ReviewedIdentityFields>(reviewedFieldsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Los campos revisados no tienen un formato válido." });
        }
        if (reviewed is null || string.IsNullOrWhiteSpace(reviewed.FullName))
            return BadRequest(new { message = "El nombre revisado es obligatorio." });

        var frontBytes = await ReadValidatedImageAsync(front, ct);
        var backBytes = back is null ? null : await ReadValidatedImageAsync(back, ct);
        var frontResult = await ocr.RecognizeAsync(frontBytes, language, ct);
        var backResult = backBytes is null
            ? null
            : await ocr.RecognizeAsync(backBytes, language, ct);
        var parsed = IdentityDocumentParser.Parse(frontResult.Text, backResult?.Text, documentType);
        var fields = ApplyHumanReview(parsed, reviewed);
        var validationError = ValidateReviewed(fields);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var evidenceId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var user = User.FindFirst("username")?.Value ?? User.Identity?.Name ?? "unknown";
        var pdf = pdfService.Build(hotelId, confirmation, roomNumber, user, evidenceId, now,
            frontBytes, backBytes, fields, frontResult.Text, backResult?.Text,
            frontResult.MeanConfidence, backResult?.MeanConfidence ?? 0);
        var version = (await db.LocalDocuments
            .Where(x => x.HotelId == hotelId && x.ConfirmationNumber == confirmation)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var retentionDays = Math.Clamp(configuration.GetValue("Ocr:EvidenceRetentionDays", 365), 30, 3650);
        var row = new LocalDocument
        {
            HotelId = hotelId,
            ConfirmationNumber = confirmation,
            RoomNumber = string.IsNullOrWhiteSpace(roomNumber) ? null : roomNumber.Trim(),
            Version = version,
            FileName = $"ID{confirmation}-EVIDENCIA-V{version}.pdf",
            PdfData = pdf,
            DocumentHash = Convert.ToHexString(SHA256.HashData(pdf)),
            Status = "IdentityEvidence",
            CreatedBy = user,
            HumanReviewedAtUtc = now,
            HumanReviewedBy = user,
            RetentionUntilUtc = now.AddDays(retentionDays)
        };
        await reservationFiles.AddDocumentAsync(row, user, ct);
        await audit.AppendAsync(AuditRequest.Create(HttpContext, "IdentityEvidence.Created",
            "LocalDocument", row.Id.ToString(), hotelId, confirmation,
            "Creación confirmada después de revisión humana",
            detailJson: JsonSerializer.Serialize(new
            {
                EvidenceId = evidenceId,
                fields.DocType,
                row.DocumentHash,
                row.RetentionUntilUtc
            })), ct);

        Response.Headers["X-Document-Id"] = row.Id.ToString();
        Response.Headers["X-Document-Hash"] = row.DocumentHash;
        Response.Headers["X-Evidence-Id"] = evidenceId.ToString();
        return File(pdf, "application/pdf", row.FileName);
    }

    private static IdentityFields ApplyHumanReview(IdentityFields parsed, ReviewedIdentityFields reviewed) =>
        new(reviewed.DocType ?? parsed.DocType, reviewed.FullName, reviewed.Curp,
            reviewed.ClaveElector, reviewed.Vigencia, reviewed.MrzLine1,
            reviewed.MrzLine2, reviewed.PassportNumber, parsed.Warnings);

    private static string? ValidateReviewed(IdentityFields fields)
    {
        if (fields.DocType == "INE" && string.IsNullOrWhiteSpace(fields.Curp) &&
            string.IsNullOrWhiteSpace(fields.ClaveElector))
            return "Para INE confirme CURP o clave de elector.";
        if (!string.IsNullOrWhiteSpace(fields.Curp) &&
            !IdentityDocumentParser.VerifyCurpChecksum(fields.Curp.ToUpperInvariant()))
            return "La CURP revisada no tiene un dígito verificador válido.";
        if (fields.DocType == "Pasaporte" && string.IsNullOrWhiteSpace(fields.PassportNumber))
            return "Para pasaporte confirme el número de documento.";
        return null;
    }

    private static IReadOnlyDictionary<string, float> BuildFieldConfidences(
        IdentityFields fields, float front, float? back)
    {
        var reverse = back ?? front;
        return new Dictionary<string, float>
        {
            ["fullName"] = ValueConfidence(fields.FullName, front),
            ["curp"] = ValueConfidence(fields.Curp, reverse,
                fields.Curp is not null && IdentityDocumentParser.VerifyCurpChecksum(fields.Curp) ? 1f : .65f),
            ["claveElector"] = ValueConfidence(fields.ClaveElector, front),
            ["vigencia"] = ValueConfidence(fields.Vigencia, front),
            ["passportNumber"] = ValueConfidence(fields.PassportNumber, reverse),
            ["mrzLine1"] = ValueConfidence(fields.MrzLine1, reverse),
            ["mrzLine2"] = ValueConfidence(fields.MrzLine2, reverse,
                fields.MrzLine2 is not null && IdentityDocumentParser.VerifyPassportMrz(fields.MrzLine2) ? 1f : .65f)
        };
    }

    private static float ValueConfidence(string? value, float confidence, float validation = .9f) =>
        string.IsNullOrWhiteSpace(value) ? 0 : Math.Clamp(confidence * validation, 0, 1);

    private static async Task<byte[]> ReadValidatedImageAsync(IFormFile file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length == 0 || file.Length > MaxBytes)
            throw new InvalidOperationException("Archivo vacío o mayor a 10 MB.");
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream((int)file.Length);
        await input.CopyToAsync(output, ct);
        var bytes = output.ToArray();
        var jpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8)
            .SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        if (!jpeg && !png)
            throw new InvalidOperationException("Formato no permitido. Use una imagen JPEG o PNG real.");
        return bytes;
    }
}

public sealed record ReviewedIdentityFields(
    string? DocType,
    string FullName,
    string? Curp,
    string? ClaveElector,
    string? Vigencia,
    string? PassportNumber,
    string? MrzLine1,
    string? MrzLine2);
