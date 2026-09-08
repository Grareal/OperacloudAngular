using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FirmaOperaCloud.Application.Contracts;
using Tesseract;

namespace FirmaOperaCloud.Infrastructure.Ocr;

/// <summary>
/// OCR on-prem con Tesseract 5 + preproceso (escala grises, borde blanco).
/// Crea un engine por llamada (POC, thread-safe).
/// tessdata esperado en: AppContext.BaseDirectory/tessdata (spa+eng+osd).
/// Solo Windows (System.Drawing + host IIS Windows).
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private static string TessDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "FirmaOperaCloud.Api", "tessdata"),
        };
        foreach (var p in candidates)
            if (Directory.Exists(p) && File.Exists(Path.Combine(p, "spa.traineddata")))
                return p;
        return candidates[0];
    }

    public Task<OcrReadResult> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0 || imageBytes.Length > 20_000_000)
            throw new ArgumentException("Imagen vacía o mayor a 20 MB.", nameof(imageBytes));

        var lang = string.IsNullOrWhiteSpace(language) ? "spa+eng" : language.Trim();
        var tessPath = TessDataPath();
        if (!File.Exists(Path.Combine(tessPath, "spa.traineddata")))
            throw new FileNotFoundException($"Falta tessdata en {tessPath}. Copie spa/eng/osd.traineddata.");

        return Task.Run(() =>
        {
            var prepared = Preprocess(imageBytes);
            // Voto multi-PSM: pasada Auto + pasada bloque uniforme, gana mayor confidence.
            var best = RunPass(tessPath, lang, prepared, PageSegMode.Auto);
            var alt = RunPass(tessPath, lang, prepared, PageSegMode.SingleBlock);
            return alt.MeanConfidence > best.MeanConfidence ? alt : best;
        }, ct);
    }

    private static OcrReadResult RunPass(string tessPath, string lang, byte[] png, PageSegMode psm)
    {
        using var engine = new TesseractEngine(tessPath, lang, EngineMode.Default);
        engine.DefaultPageSegMode = psm;
        using var pix = Pix.LoadFromMemory(png);
        using var page = engine.Process(pix);
        var text = page.GetText() ?? string.Empty;
        return new OcrReadResult(text.Trim(), page.GetMeanConfidence(), lang);
    }

    /// <summary>Escala 2x si es chica, grises, borde blanco: +10-15% en fotos INE.</summary>
    internal static byte[] Preprocess(byte[] src)
    {
        try
        {
            using var ms = new MemoryStream(src, writable: false);
            using var img = Image.FromStream(ms);
            var scale = img.Width < 1800 ? 2.0f : 1.0f;
            var w = (int)(img.Width * scale) + 60;
            var h = (int)(img.Height * scale) + 60;
            using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, 30, 30, w - 60, h - 60);
            // Grises
            var cm = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 },
            });
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(cm);
            using var gray = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g2 = Graphics.FromImage(gray))
                g2.DrawImage(bmp, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attrs);
            using var outMs = new MemoryStream();
            gray.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            return outMs.ToArray();
        }
        catch
        {
            return src; // si falla el preproceso, OCR sobre original
        }
    }
}
