using System.Net;
using FirmaOperaCloud.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Services;

public sealed class PdfTemplateRenderService(FirmaOperaCloudDbContext db)
{
    public byte[] Render(PdfTemplate template, Reservation reservation, IReadOnlyList<StoredSignature>? signatures = null)
    {
        signatures ??= [];
        using var source = new MemoryStream(template.PdfData, false);
        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        foreach (var group in template.Fields.GroupBy(x => x.PageNumber))
        {
            if (group.Key < 1 || group.Key > document.PageCount) continue;
            var page = document.Pages[group.Key - 1]; using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            foreach (var field in group)
            {
                var box = new XRect(page.Width.Point * field.XPercent / 100d, page.Height.Point * field.YPercent / 100d,
                    page.Width.Point * field.WidthPercent / 100d, page.Height.Point * field.HeightPercent / 100d);
                if (field.FieldType == "Signature")
                {
                    var signature = field.FieldKey == "PrimarySignature" ? signatures.LastOrDefault(x => x.SignerRole == "PrimaryGuest")
                        : signatures.Where(x => x.SignerRole == "Occupant").Skip(Math.Max(0, (field.OccupantIndex ?? 1) - 1)).FirstOrDefault();
                    if (signature is not null) DrawImage(graphics, signature.SignaturePng, box);
                }
                else
                {
                    var value = ResolveValue(field, reservation, signatures);
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var font = new XFont("Arial", Math.Clamp(field.FontSize, 5, 40));
                    if (field.FieldType == "Multiline") new XTextFormatter(graphics).DrawString(value, font, XBrushes.Black, box, XStringFormats.TopLeft);
                    else graphics.DrawString(value, font, XBrushes.Black, box, XStringFormats.CenterLeft);
                }
            }
        }
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }

    public string ResolveValue(PdfTemplateField field, Reservation r, IReadOnlyList<StoredSignature> signatures)
    {
        var udf = r.UserDefinedFields.FirstOrDefault(x => x.Name.Equals(field.FieldKey, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        return field.FieldKey switch
        {
            "ConfirmationNumber" => r.ConfirmationNumber ?? "", "GuestFullName" => r.Guest.FullName,
            "ArrivalDate" => r.RoomStay.ArrivalDate, "DepartureDate" => r.RoomStay.DepartureDate,
            "RoomNumber" => r.RoomStay.RoomId, "RoomType" => r.RoomStay.RoomType,
            "Adults" => r.RoomStay.AdultCount.ToString(), "Children" => r.RoomStay.ChildCount.ToString(),
            "Email" => r.Guest.Email, "Phone" => r.Guest.PhoneNumber, "City" => r.Guest.Address.City,
            "State" => r.Guest.Address.StateProvCode, "Country" => r.Guest.Address.CountryCode,
            "RateAmount" => $"{r.RoomStay.RateAmount:0.00} {r.RoomStay.CurrencyCode}",
            "OccupantName" => signatures.Where(x => x.SignerRole == "Occupant").Skip(Math.Max(0, (field.OccupantIndex ?? 1) - 1)).Select(x => x.SignerName).FirstOrDefault()
                ?? r.AccompanyingGuestNames.Skip(Math.Max(0, (field.OccupantIndex ?? 1) - 1)).FirstOrDefault() ?? "",
            "UDFC02" => udf, "UDFC16" => FormatBenefits(udf), "UDFC20" => udf,
            "PromotionCampaigns" => ResolveApprovedPromotions(r),
            "PromotionBenefits" => FormatBenefits(GetUdf(r, "UDFC16")),
            "PromotionTechnicalContext" => GetUdf(r, "UDFC20"),
            "PromotionSource" => GetUdf(r, "UDFC24"),
            "PromotionClassification" => GetUdf(r, "UDFC09"),
            _ => udf
        };
    }

    private string ResolveApprovedPromotions(Reservation reservation)
    {
        var codes = Split(GetUdf(reservation, "UDFC02"), [',', ';']).Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (codes.Length == 0) return string.Empty;
        var now = DateTime.UtcNow; var hotel = string.IsNullOrWhiteSpace(reservation.HotelId) ? "VINV" : reservation.HotelId.ToUpperInvariant();
        var entries = db.PromotionCatalogEntries.AsNoTracking().Where(x => x.HotelId == hotel && x.Language == "ES" &&
            x.IsActive && x.IsApprovedForGuest && codes.Contains(x.OperaCode) &&
            (!x.EffectiveFromUtc.HasValue || x.EffectiveFromUtc <= now) && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc >= now))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.OperaCode).ToList();
        return string.Join(Environment.NewLine, entries.Select(x => $"• {(string.IsNullOrWhiteSpace(x.GuestTitle) ? x.GuestDescription : x.GuestTitle + (string.IsNullOrWhiteSpace(x.GuestDescription) ? "" : ": " + x.GuestDescription))}"));
    }

    private static string GetUdf(Reservation r, string name) => r.UserDefinedFields.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
    public static string FormatCampaigns(string value) => string.Join(" · ", Split(value, [',', ';']).Select(HumanizeCampaign));
    public static string FormatBenefits(string value) => string.Join(Environment.NewLine, Split(WebUtility.HtmlDecode(value), [';']).Select(x => $"• {HumanizeBenefit(x)}"));

    private static IEnumerable<string> Split(string value, char[] separators) => value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !x.Equals("Room", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase);
    private static string HumanizeCampaign(string value) => value.Replace('_', ' ') switch
    {
        "FAMILIARDIRECTO" => "Familiar directo", "INTERCONEXION" => "Interconexión", "NAC PROMOCIONPARQUE" => "Promoción Parque",
        "NAC LAND&SEAPARQUE" => "Land & Sea Parque", "PREPAGO35K" => "Prepago $35,000", "MOMENTOSVIDANTA" => "Momentos Vidanta", var x => ToTitle(x)
    };
    private static string HumanizeBenefit(string value)
    {
        if (value.Equals("Internet", StringComparison.OrdinalIgnoreCase)) return "Acceso a internet sin costo";
        if (value.Equals("Gym/Spa", StringComparison.OrdinalIgnoreCase)) return "Acceso a gimnasio y zonas húmedas del spa";
        if (value.Equals("Tennis", StringComparison.OrdinalIgnoreCase)) return "Canchas de tenis en horario diurno";
        if (value.Contains("Golf Fixed Fee", StringComparison.OrdinalIgnoreCase)) return "Golf con tarifa preferencial";
        if (value.Contains("2X1", StringComparison.OrdinalIgnoreCase) && value.Contains("Golf", StringComparison.OrdinalIgnoreCase)) return "Rondas de golf 2×1";
        if (value.Contains("MAPFRE", StringComparison.OrdinalIgnoreCase)) return "Asistencia carretera MAPFRE por una semana";
        return value.Trim();
    }
    private static string ToTitle(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Length == 0 ? x : char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
    private static void DrawImage(XGraphics graphics, byte[] bytes, XRect box)
    {
        using var stream = new MemoryStream(bytes, false); using var image = XImage.FromStream(stream);
        var scale = Math.Min(box.Width / image.PointWidth, box.Height / image.PointHeight); var width = image.PointWidth * scale; var height = image.PointHeight * scale;
        graphics.DrawImage(image, box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
    }
}

public static class PromotionTemplateSeed
{
    public static (byte[] Pdf, List<PdfTemplateField> Fields) Create()
    {
        using var document = new PdfDocument(); var page = document.AddPage(); page.Size = PdfSharp.PageSize.Letter;
        using var g = XGraphics.FromPdfPage(page); var teal = XColor.FromArgb(9, 75, 70); var gold = XColor.FromArgb(187, 151, 73); var sand = XColor.FromArgb(247, 244, 237);
        g.DrawRectangle(new XSolidBrush(teal), 0, 0, page.Width.Point, 142); g.DrawRectangle(new XSolidBrush(gold), 0, 142, page.Width.Point, 7);
        g.DrawString("VIDANTA", new XFont("Arial", 25, XFontStyleEx.Bold), XBrushes.White, new XPoint(48, 58));
        g.DrawString("BENEFICIOS DE SU ESTANCIA", new XFont("Arial", 12, XFontStyleEx.Bold), new XSolidBrush(gold), new XPoint(48, 90));
        g.DrawString("Una experiencia creada especialmente para usted", new XFont("Arial", 10), XBrushes.White, new XPoint(48, 116));
        g.DrawRectangle(new XSolidBrush(sand), 38, 172, page.Width.Point - 76, 94); g.DrawString("INFORMACIÓN DE LA RESERVA", new XFont("Arial", 9, XFontStyleEx.Bold), new XSolidBrush(teal), new XPoint(56, 198));
        g.DrawString("Huésped", new XFont("Arial", 8), XBrushes.Gray, new XPoint(56, 222)); g.DrawString("Confirmación", new XFont("Arial", 8), XBrushes.Gray, new XPoint(340, 222));
        g.DrawString("SUS BENEFICIOS", new XFont("Arial", 15, XFontStyleEx.Bold), new XSolidBrush(teal), new XPoint(48, 310));
        g.DrawLine(new XPen(gold, 2), 48, 322, page.Width.Point - 48, 322);
        g.DrawString("Promociones aplicables", new XFont("Arial", 9, XFontStyleEx.Bold), new XSolidBrush(teal), new XPoint(48, 535));
        g.DrawRectangle(new XPen(gold, 1), 48, 548, page.Width.Point - 96, 74);
        g.DrawString("Los beneficios están sujetos a los términos, vigencia y condiciones de su reservación.", new XFont("Arial", 8), XBrushes.Gray, new XPoint(48, 665));
        g.DrawString("Gracias por elegir Vidanta", new XFont("Arial", 11, XFontStyleEx.Bold), new XSolidBrush(teal), new XPoint(48, 704));
        using var output = new MemoryStream(); document.Save(output, false);
        List<PdfTemplateField> fields =
        [
            new() { FieldKey="GuestFullName", Label="Nombre del huésped", FieldType="Text", PageNumber=1, XPercent=9.2, YPercent=28.4, WidthPercent=43, HeightPercent=3, FontSize=10 },
            new() { FieldKey="ConfirmationNumber", Label="Confirmación", FieldType="Text", PageNumber=1, XPercent=56, YPercent=28.4, WidthPercent=30, HeightPercent=3, FontSize=10 },
            new() { FieldKey="PromotionBenefits", Label="Beneficios descritos (UDFC16)", FieldType="Multiline", PageNumber=1, XPercent=8, YPercent=42, WidthPercent=84, HeightPercent=23, FontSize=11 },
            new() { FieldKey="PromotionCampaigns", Label="Promociones interpretadas (UDFC02)", FieldType="Multiline", PageNumber=1, XPercent=9, YPercent=70.2, WidthPercent=82, HeightPercent=7, FontSize=9 }
        ];
        return (output.ToArray(), fields);
    }
}
