namespace FirmaOperaCloud.Application.Contracts;

/// <summary>POC OCR on-prem (Tesseract) para INE / pasaporte. Sin nube, sin OPERA.</summary>
public sealed record OcrReadResult(string Text, float MeanConfidence, string Language);

public interface IOcrService
{
    Task<OcrReadResult> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct);
}
