using System.Security.Cryptography;
using FirmaOperaCloud.Api.Controllers;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace FirmaOperaCloud.Api.Services;

public sealed class LocalDocumentService(
    FirmaOperaCloudDbContext db,
    ReservationFileService reservationFiles)
{
    public async Task<PdfTemplate?> ResolveTemplateAsync(Reservation reservation, Guid? requestedId, CancellationToken ct)
    {
        var setting = await db.HotelDocumentSettings.AsNoTracking().FirstOrDefaultAsync(x => x.HotelId == reservation.HotelId, ct);
        if (setting?.SourceMode == DocumentSourceModes.Opera) return null;
        if (requestedId.HasValue)
            return await db.PdfTemplates.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == requestedId && x.IsPublished && x.HotelId == reservation.HotelId && x.TemplateType == PdfTemplateTypes.RegistrationCard, ct)
                ?? throw new InvalidOperationException("La plantilla local solicitada no está publicada para este hotel.");
        var candidates = await db.PdfTemplates.Include(x => x.Fields).Where(x => x.HotelId == reservation.HotelId && x.IsPublished && x.TemplateType == PdfTemplateTypes.RegistrationCard).ToListAsync(ct);
        var preferred = setting?.PreferredPdfTemplateId is Guid preferredId
            ? candidates.FirstOrDefault(x => x.Id == preferredId)
            : null;
        var selected = preferred
            ?? candidates.Where(x => !string.IsNullOrWhiteSpace(x.RoomTypePrefix) && reservation.RoomStay.RoomType.StartsWith(x.RoomTypePrefix, StringComparison.OrdinalIgnoreCase)
                && (x.Language == null || x.Language.Equals(reservation.Guest.Language, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.RoomTypePrefix!.Length)
            .ThenByDescending(x => x.Version).FirstOrDefault()
            ?? candidates.Where(x => x.IsDefault && (x.Language == null || x.Language.Equals(reservation.Guest.Language, StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.Version).FirstOrDefault();
        if (setting?.SourceMode == DocumentSourceModes.Local && selected is null)
            throw new InvalidOperationException("El hotel está configurado para usar plantillas locales, pero no existe una plantilla publicada compatible.");
        return selected;
    }

    public byte[] Fill(PdfTemplate template, Reservation r, FillOfficialRegistrationCardRequest input)
    {
        using var source = new MemoryStream(template.PdfData, false); using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        foreach (var group in template.Fields.GroupBy(x => x.PageNumber))
        {
            if (group.Key < 1 || group.Key > document.PageCount) continue;
            var page = document.Pages[group.Key - 1]; using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            foreach (var f in group)
            {
                var box = new XRect(page.Width.Point * f.XPercent / 100, page.Height.Point * f.YPercent / 100, page.Width.Point * f.WidthPercent / 100, page.Height.Point * f.HeightPercent / 100);
                if (f.FieldType == "Signature") DrawSignature(graphics, f.FieldKey == "PrimarySignature" ? input.PrimarySignaturePngBase64 : input.Occupants.ElementAtOrDefault((f.OccupantIndex ?? 1) - 1)?.SignaturePngBase64, box);
                else { var value = Value(f, r, input); if (!string.IsNullOrWhiteSpace(value)) graphics.DrawString(value, new XFont("Arial", Math.Clamp(f.FontSize, 5, 40)), XBrushes.Black, box, XStringFormats.CenterLeft); }
            }
        }
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }

    public async Task<(LocalDocument Document, List<StoredSignature> Signatures)> StoreAsync(Reservation r, PdfTemplate? template, FillOfficialRegistrationCardRequest input, byte[] pdf, string? user, CancellationToken ct)
    {
        var confirmation = r.ConfirmationNumber ?? throw new InvalidOperationException("Reserva sin confirmación.");
        var reservationId = r.ReservationIdList.FirstOrDefault(x => x.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id;
        var signatures = new List<StoredSignature>
        {
            CreateSignature(r, confirmation, reservationId, input.PrimaryGuestName ?? r.Guest.FullName,
                input.PrimarySignerId ?? r.Guest.Id, "PrimaryGuest", input.PrimarySignaturePngBase64, user)
        };
        signatures.AddRange(input.Occupants.Select(x => CreateSignature(
            r, confirmation, reservationId, x.Name, x.SignerId, "Occupant", x.SignaturePngBase64, user)));
        db.StoredSignatures.AddRange(signatures);
        var version = (await db.LocalDocuments.Where(x => x.HotelId == r.HotelId && x.ConfirmationNumber == confirmation).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var document = new LocalDocument { HotelId = r.HotelId, ConfirmationNumber = confirmation, ReservationId = reservationId, RoomNumber = r.RoomStay.RoomId,
            PdfTemplateId = template?.Id, Version = version, FileName = $"REGCARD{confirmation}-V{version}.pdf", PdfData = pdf,
            DocumentHash = Convert.ToHexString(SHA256.HashData(pdf)), CreatedBy = user };
        for (var i = 0; i < signatures.Count; i++) document.Signatures.Add(new LocalDocumentSignature { StoredSignature = signatures[i], Role = signatures[i].SignerRole, Position = i });
        await reservationFiles.AddDocumentAsync(document, user, ct);
        return (document, signatures);
    }

    private static StoredSignature CreateSignature(Reservation r, string confirmation, string? reservationId, string name, string? externalId, string role, string? base64, string? user)
    {
        if (string.IsNullOrWhiteSpace(base64)) throw new InvalidOperationException($"Falta la firma de {name}."); var comma = base64.IndexOf(','); var bytes = Convert.FromBase64String(comma >= 0 ? base64[(comma + 1)..] : base64);
        var normalized = string.Join(' ', name.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return new StoredSignature { HotelId = r.HotelId, ConfirmationNumber = confirmation, ReservationId = reservationId, RoomNumber = r.RoomStay.RoomId,
            SignerName = name.Trim(), OperaProfileId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim(), SignerKey = string.IsNullOrWhiteSpace(externalId) ? $"LOCAL:{r.HotelId}:{confirmation}:{normalized}" : $"OPERA:{externalId.Trim()}",
            SignerRole = role, SignaturePng = bytes, SignatureHash = Convert.ToHexString(SHA256.HashData(bytes)), CapturedBy = user };
    }
    private static string Value(PdfTemplateField f, Reservation r, FillOfficialRegistrationCardRequest x) => f.FieldKey switch { "ConfirmationNumber" => r.ConfirmationNumber ?? "", "GuestFullName" => x.PrimaryGuestName ?? r.Guest.FullName, "ArrivalDate" => r.RoomStay.ArrivalDate, "DepartureDate" => r.RoomStay.DepartureDate, "RoomNumber" => r.RoomStay.RoomId, "RoomType" => r.RoomStay.RoomType, "Adults" => r.RoomStay.AdultCount.ToString(), "Children" => r.RoomStay.ChildCount.ToString(), "Email" => x.Email ?? r.Guest.Email, "Phone" => x.CellPhone ?? r.Guest.PhoneNumber, "City" => x.City ?? r.Guest.Address.City, "State" => x.State ?? r.Guest.Address.StateProvCode, "Country" => x.Country ?? r.Guest.Address.CountryCode, "Citizenship" => x.Citizenship ?? "", "RateAmount" => $"{r.RoomStay.RateAmount:0.00} {r.RoomStay.CurrencyCode}", "OccupantName" => x.Occupants.ElementAtOrDefault((f.OccupantIndex ?? 1) - 1)?.Name ?? "", _ => "" };
    private static void DrawSignature(XGraphics g, string? base64, XRect box) { if (string.IsNullOrWhiteSpace(base64)) return; var comma=base64.IndexOf(','); var bytes=Convert.FromBase64String(comma>=0?base64[(comma+1)..]:base64); using var s=new MemoryStream(bytes); using var image=XImage.FromStream(s); var scale=Math.Min(box.Width/image.PointWidth,box.Height/image.PointHeight); var w=image.PointWidth*scale; var h=image.PointHeight*scale; g.DrawImage(image,box.X+(box.Width-w)/2,box.Y+(box.Height-h)/2,w,h); }
}
