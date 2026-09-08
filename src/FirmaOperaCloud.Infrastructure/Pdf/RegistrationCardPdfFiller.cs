using FirmaOperaCloud.Application.Contracts;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;

namespace FirmaOperaCloud.Infrastructure.Pdf;

public sealed class RegistrationCardPdfFiller : IRegistrationCardPdfFiller
{
    static RegistrationCardPdfFiller()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private sealed record Field(double X, double Y, double Width, double Height);
    private sealed record OccupantField(Field Name, Field Signature);

    private static readonly OccupantField[] OccupantFields =
    [
        new(new(146, 296, 76, 14), new(224, 296, 67, 14)),
        new(new(146, 323, 76, 14), new(224, 323, 67, 14)),
        new(new(146, 343, 76, 14), new(224, 343, 67, 14)),
        new(new(146, 363, 76, 14), new(224, 363, 67, 14)),
        new(new(406, 296, 92, 14), new(500, 296, 67, 14)),
        new(new(406, 323, 92, 14), new(500, 323, 67, 14)),
        new(new(406, 343, 92, 14), new(500, 343, 67, 14)),
        new(new(406, 363, 92, 14), new(500, 363, 67, 14))
    ];

    public byte[] Fill(byte[] officialPdf, RegistrationCardFillInput input)
    {
        ArgumentNullException.ThrowIfNull(officialPdf);
        ArgumentNullException.ThrowIfNull(input);

        if (input.Occupants.Count > OccupantFields.Length)
        {
            throw new ArgumentException("La Registration Card permite un máximo de ocho ocupantes.", nameof(input));
        }

        using var source = new MemoryStream(officialPdf, writable: false);
        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        if (document.PageCount == 0)
        {
            throw new InvalidDataException("El PDF oficial no contiene páginas.");
        }

        var page = document.Pages[0];
        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var font = new XFont("Arial", 7.5, XFontStyleEx.Regular);
        var smallFont = new XFont("Arial", 6.5, XFontStyleEx.Regular);

        DrawText(graphics, font, input.Citizenship, new(410, 158, 148, 14));
        DrawText(graphics, font, input.City, new(112, 175, 130, 14));
        DrawText(graphics, font, input.State, new(294, 175, 142, 14));
        DrawText(graphics, font, input.Country, new(476, 175, 82, 14));
        DrawText(graphics, font, input.Email, new(93, 232, 190, 14));
        DrawText(graphics, font, input.CellPhone, new(442, 232, 116, 14));

        for (var index = 0; index < input.Occupants.Count; index++)
        {
            var occupant = input.Occupants[index];
            var field = OccupantFields[index];
            DrawText(graphics, smallFont, occupant.Name, field.Name);
            DrawSignature(graphics, occupant.SignaturePngBase64, field.Signature);
        }

        DrawText(graphics, font, input.PrimaryGuestName, new(260, 670, 95, 18));
        DrawSignature(graphics, input.PrimarySignaturePngBase64, new(330, 667, 82, 18));

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void DrawText(XGraphics graphics, XFont font, string? value, Field field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        graphics.DrawString(
            value.Trim(),
            font,
            XBrushes.Black,
            new XRect(field.X, field.Y, field.Width, field.Height),
            XStringFormats.CenterLeft);
    }

    private static void DrawSignature(XGraphics graphics, string? base64, Field field)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return;
        }

        var separator = base64.IndexOf(',');
        var raw = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && separator >= 0
            ? base64[(separator + 1)..]
            : base64;
        var bytes = Convert.FromBase64String(raw);
        using var stream = new MemoryStream(bytes, writable: false);
        using var image = XImage.FromStream(stream);

        var scale = Math.Min(field.Width / image.PointWidth, field.Height / image.PointHeight);
        var width = image.PointWidth * scale;
        var height = image.PointHeight * scale;
        var x = field.X + (field.Width - width) / 2;
        var y = field.Y + (field.Height - height) / 2;
        graphics.DrawImage(image, x, y, width, height);
    }
}
