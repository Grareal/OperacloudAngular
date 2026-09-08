using FirmaOperaCloud.Infrastructure.Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FirmaOperaCloud.Api.Services;

/// <summary>POC: arma un PDF local de evidencia INE/pasaporte. NO sube a OPERA.</summary>
public sealed class IdentityEvidencePdfService
{
    public byte[] Build(
        string hotelId, string confirmationNumber, string? roomNumber,
        string? capturedBy, Guid evidenceId, DateTime capturedAtUtc,
        byte[]? frontImage, byte[]? backImage,
        IdentityFields fields, string frontText, string? backText,
        float frontConfidence, float backConfidence)
    {
        using var doc = new PdfDocument();
        doc.Info.Title = $"Evidencia identidad {confirmationNumber}";
        doc.Info.Creator = "FirmaOperaCloud OCR POC (Tesseract on-prem)";
        doc.Info.Subject = evidenceId.ToString();

        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var title = new XFont("Arial", 14, XFontStyleEx.Bold);
        var h2 = new XFont("Arial", 11, XFontStyleEx.Bold);
        var body = new XFont("Arial", 9, XFontStyleEx.Regular);
        var small = new XFont("Arial", 7.5, XFontStyleEx.Regular);
        double y = 40;
        const double L = 40, R = 555;

        gfx.DrawString("Evidencia de identidad — copia local (NO OPERA)", title, XBrushes.Black, new XRect(L, y, R - L, 20), XStringFormats.TopLeft);
        y += 22;
        gfx.DrawString($"Hotel {hotelId} · Reserva {confirmationNumber} · Hab {(string.IsNullOrWhiteSpace(roomNumber) ? "—" : roomNumber)}", body, XBrushes.Black, L, y); y += 14;
        gfx.DrawString($"GUID {evidenceId} · Capturado {capturedAtUtc:yyyy-MM-dd HH:mm} UTC por {(string.IsNullOrWhiteSpace(capturedBy) ? "—" : capturedBy)}", small, XBrushes.Black, L, y); y += 14;
        gfx.DrawString("POC on-prem Tesseract. Requiere revisión humana antes de cualquier uso operativo.", small, XBrushes.DarkRed, L, y); y += 18;

        gfx.DrawString($"Documento: {fields.DocType ?? "—"}   Conf. frente {frontConfidence:P0}   Conf. reverso {backConfidence:P0}", h2, XBrushes.Black, L, y); y += 16;
        foreach (var line in FieldLines(fields))
        {
            gfx.DrawString(line, body, XBrushes.Black, L, y); y += 13;
            if (y > 780) { page = doc.AddPage(); page.Size = PdfSharp.PageSize.A4; gfx = XGraphics.FromPdfPage(page); y = 40; }
        }
        y += 6;

        y = DrawImageBlock(gfx, doc, ref page, frontImage, "Frente / pasaporte", L, y, body, h2);
        y = DrawImageBlock(gfx, doc, ref page, backImage, "Reverso INE", L, y, body, h2);

        gfx.DrawString("Texto OCR (recorte, solo para auditoría):", h2, XBrushes.Black, L, y); y += 15;
        var snippet = (frontText + "\n" + (backText ?? "")).Trim();
        if (snippet.Length > 1200) snippet = snippet[..1200] + "…";
        foreach (var chunk in SplitLines(gfx, snippet, small, R - L))
        {
            gfx.DrawString(chunk, small, XBrushes.Black, L, y); y += 11;
            if (y > 800) { page = doc.AddPage(); page.Size = PdfSharp.PageSize.A4; gfx = XGraphics.FromPdfPage(page); y = 40; }
        }

        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static List<string> FieldLines(IdentityFields f) =>
    [
        $"Nombre detectado: {f.FullName ?? "—"}",
        $"CURP: {f.Curp ?? "—"}   Clave elector: {f.ClaveElector ?? "—"}   Vigencia: {f.Vigencia ?? "—"}",
        $"Pasaporte/MRZ doc: {f.PassportNumber ?? "—"}",
        $"MRZ1: {Trunc(f.MrzLine1, 60) ?? "—"}",
        $"MRZ2: {Trunc(f.MrzLine2, 60) ?? "—"}",
        f.Warnings.Count == 0 ? "Advertencias: ninguna" : $"Advertencias: {string.Join(" | ", f.Warnings)}",
    ];

    private static string? Trunc(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n] + "…";

    private static double DrawImageBlock(XGraphics gfx, PdfDocument doc, ref PdfSharp.Pdf.PdfPage page,
        byte[]? img, string label, double L, double y, XFont body, XFont h2)
    {
        gfx.DrawString(label + (img is null ? " (no capturado)" : ""), h2, XBrushes.Black, L, y); y += 14;
        if (img is null) return y + 4;
        try
        {
            using var ms = new MemoryStream(img, writable: false);
            using var ximg = XImage.FromStream(ms);
            const double maxW = 515, maxH = 300;
            var scale = Math.Min(maxW / ximg.PointWidth, maxH / ximg.PointHeight);
            var w = ximg.PointWidth * scale; var h = ximg.PointHeight * scale;
            if (y + h > 800) { page = doc.AddPage(); page.Size = PdfSharp.PageSize.A4; gfx = XGraphics.FromPdfPage(page); y = 40; }
            gfx.DrawImage(ximg, L, y, w, h);
            return y + h + 10;
        }
        catch
        {
            gfx.DrawString("(imagen no válida: use JPG/PNG)", body, XBrushes.DarkRed, L, y);
            return y + 18;
        }
    }

    private static IEnumerable<string> SplitLines(XGraphics gfx, string text, XFont font, double maxW)
    {
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            while (gfx.MeasureString(line, font).Width > maxW && line.Length > 20)
            {
                var cut = line.Length / 2;
                yield return line[..cut];
                line = line[cut..];
            }
            yield return line;
        }
    }
}
