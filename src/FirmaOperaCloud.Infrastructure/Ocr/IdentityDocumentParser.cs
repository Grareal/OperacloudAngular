using System.Text.RegularExpressions;

namespace FirmaOperaCloud.Infrastructure.Ocr;

/// <summary>Parser heurístico POC: MRZ pasaporte + CURP/clave elector/vigencia INE.</summary>
public sealed record IdentityFields(
    string? DocType,
    string? FullName,
    string? Curp,
    string? ClaveElector,
    string? Vigencia,
    string? MrzLine1,
    string? MrzLine2,
    string? PassportNumber,
    List<string> Warnings);

public static partial class IdentityDocumentParser
{
    [GeneratedRegex(@"[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d", RegexOptions.IgnoreCase)]
    private static partial Regex CurpRegex();

    [GeneratedRegex(@"P<[A-Z<]{3}[A-Z<]+")]
    private static partial Regex Mrz1Regex();

    [GeneratedRegex(@"[A-Z0-9<]{39,44}")]
    private static partial Regex Mrz2Regex();

    [GeneratedRegex(@"HASTA\s*(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VigenciaRegex();

    [GeneratedRegex(@"VIGENCIA\s*(\d{4})\s*[-–—/]\s*(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VigenciaRangoRegex();

    [GeneratedRegex(@"CLAVE\s*DE\s*ELECTOR\s*[:\-]?\s*([A-Z0-9]{16,18})", RegexOptions.IgnoreCase)]
    private static partial Regex ClaveRegex();

    public static IdentityFields Parse(string frontText, string? backText, string requestedType)
    {
        var all = $"{frontText}\n{backText ?? ""}".ToUpperInvariant().Replace('–', '-');
        var warnings = new List<string>();
        string? mrz1 = null, mrz2 = null, passport = null, curp = null, clave = null, vigencia = null, name = null;

        var lines = all.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var l in lines)
        {
            var compact = l.Replace(" ", "");
            if (mrz1 is null && compact.StartsWith("P<", StringComparison.Ordinal) && compact.Length >= 20) { mrz1 = compact; continue; }
            if (mrz1 is not null && mrz2 is null && Mrz2Regex().IsMatch(compact) && compact.Contains('<')) { mrz2 = compact; }
        }
        if (mrz1 is null) { var m = Mrz1Regex().Match(all); if (m.Success) mrz1 = m.Value.Replace(" ", ""); }
        if (mrz2 is null && mrz1 is not null)
        {
            var idx = all.IndexOf(mrz1, StringComparison.Ordinal);
            var after = all[(idx + mrz1.Length)..].Split('\n').Select(x => x.Replace(" ", "").Trim()).FirstOrDefault(x => x.Length >= 30 && x.Contains('<'));
            if (after is not null) mrz2 = after;
        }
        if (mrz2 is not null)
        {
            var doc = mrz2.Trim('<').Split('<')[0].Trim();
            if (doc.Length >= 6) passport = doc;
        }

        var mCurp = CurpRegex().Match(all);
        if (mCurp.Success) curp = mCurp.Value.ToUpperInvariant();
        else curp = FuzzyCurp(all); // tolera O/0, I/1, $/S típicos de foto
        var mClave = ClaveRegex().Match(all);
        if (mClave.Success) clave = OcrFix(mClave.Groups[1].Value.ToUpperInvariant());
        else clave = FuzzyClave(all);
        var mVig = VigenciaRegex().Match(all);
        if (mVig.Success) vigencia = mVig.Groups[1].Value;
        else
        {
            var mRango = VigenciaRangoRegex().Match(all);
            if (mRango.Success) vigencia = $"{mRango.Groups[1].Value}-{mRango.Groups[2].Value}";
        }

        // Nombre: anclado a etiqueta NOMBRE si existe, si no heurística anterior.
        name = NameAfterLabel(lines, "NOMBRE") ?? NameAfterLabel(lines, "NOMBRE / NAME")
            ?? lines.Where(l => l.Length >= 8 && !l.Contains('<') && !l.Any(char.IsDigit) && l.Count(char.IsLetter) >= 6)
            .OrderByDescending(l => l.Length).FirstOrDefault();

        var docType = requestedType.ToUpperInvariant() switch
        {
            var t when t.Contains("PAS") => "Pasaporte",
            var t when t.Contains("INE") => "INE",
            _ => mrz1 is not null ? "Pasaporte" : curp is not null || clave is not null ? "INE" : "Desconocido"
        };

        if (docType == "Pasaporte" && mrz1 is null) warnings.Add("No se detectó MRZ. Re-capture con buena luz, sin mica ni recorte.");
        if (docType == "Pasaporte" && mrz2 is not null && !VerifyPassportMrz(mrz2))
            warnings.Add("La MRZ no supera todos los dígitos verificadores; confirme los datos manualmente.");
        if (docType == "INE" && curp is null && clave is null) warnings.Add("No se detectó CURP ni clave de elector. Verifique enfoque e iluminación.");
        if (curp is not null && !VerifyCurpChecksum(curp)) warnings.Add("CURP con dígito verificador inválido: capture de nuevo o corrija manual.");
        if (string.IsNullOrWhiteSpace(frontText) || frontText.Length < 20) warnings.Add("Texto OCR muy corto: imagen probablemente borrosa u oscura.");

        return new IdentityFields(docType, name, curp, clave, vigencia, mrz1, mrz2, passport, warnings);
    }

    /// <summary>Corrige confusiones OCR en zonas alfanuméricas: $→S, espacios, minúsculas.</summary>
    private static string OcrFix(string s) => s.Replace("$", "S").Replace(" ", "").Replace("'", "").Trim();

    /// <summary>
    /// CURP flexible: busca tokens de 18 alfanuméricos con forma CURP permitiendo
    /// O en posiciones de dígito y dígitos en posiciones de letra, luego normaliza.
    /// Si hay varios candidatos, el dígito verificador decide.
    /// </summary>
    private static string? FuzzyCurp(string all)
    {
        var candidates = new List<string>();
        foreach (var raw in all.Split('\n', ' ', '\t', '|', '«', '»', '.', ',', ':', ';'))
        {
            var t = OcrFix(raw.ToUpperInvariant());
            if (t.Length != 18) continue;
            // Estructura: 4 letras + 6 fecha + H/M + 5 letras + 2 verif
            if (!char.IsLetter(t[0]) || !char.IsLetter(t[1]) || !char.IsLetter(t[2]) || !char.IsLetter(t[3])) continue;
            if (t[10] != 'H' && t[10] != 'M') continue;
            var fixed_ = FixCurpDigits(t);
            if (CurpRegex().IsMatch(fixed_)) candidates.Add(fixed_);
        }
        // Etiqueta CURP: el token siguiente a la palabra CURP
        var lines = all.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("CURP")) continue;
            foreach (var cand in (lines[i] + " " + (i + 1 < lines.Length ? lines[i + 1] : "")).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = OcrFix(cand.ToUpperInvariant());
                if (t.Length is >= 16 and <= 20)
                {
                    var f = FixCurpDigits(t.PadRight(18).Substring(0, Math.Min(18, t.Length)));
                    if (f.Length == 18 && CurpRegex().IsMatch(f) && !candidates.Contains(f)) candidates.Add(f);
                }
            }
        }
        // El dígito verificador (pos 18) decide entre candidatos.
        return candidates.OrderByDescending(VerifyCurpChecksum).ThenBy(c => c).FirstOrDefault();
    }

    /// <summary>Dígito verificador CURP: A-N=10-23, Ñ=24, O-Z=25-36, pesos 18..2.</summary>
    public static bool VerifyCurpChecksum(string curp)
    {
        if (curp.Length != 18 || !char.IsDigit(curp[17])) return false;
        var sum = 0;
        for (var i = 0; i < 17; i++)
        {
            var c = curp[i];
            int v = char.IsDigit(c) ? c - '0'
                : c is >= 'A' and <= 'N' ? c - 'A' + 10
                : c == 'Ñ' ? 24
                : c is >= 'O' and <= 'Z' ? c - 'A' + 11 : -1;
            if (v < 0) return false;
            sum += v * (18 - i);
        }
        return (10 - sum % 10) % 10 == curp[17] - '0';
    }

    public static bool VerifyPassportMrz(string line)
    {
        var value = line.Replace(" ", string.Empty).ToUpperInvariant();
        if (value.Length < 44) return false;
        value = value[..44];
        return CheckMrz(value[..9], value[9]) &&
               CheckMrz(value.Substring(13, 6), value[19]) &&
               CheckMrz(value.Substring(21, 6), value[27]);
    }

    private static bool CheckMrz(string value, char expected)
    {
        if (!char.IsDigit(expected)) return false;
        int[] weights = [7, 3, 1];
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var number = c == '<' ? 0 : char.IsDigit(c) ? c - '0' :
                c is >= 'A' and <= 'Z' ? c - 'A' + 10 : -1;
            if (number < 0) return false;
            sum += number * weights[i % weights.Length];
        }
        return sum % 10 == expected - '0';
    }

    private static string FixCurpDigits(string t)
    {
        var c = t.ToCharArray();
        // Posiciones de fecha 4-9 y verificador 17: O→0, I/L→1, B→8, S→5
        foreach (var i in new[] { 4, 5, 6, 7, 8, 9, 17 })
        {
            if (i >= c.Length) break;
            c[i] = c[i] switch { 'O' => '0', 'I' or 'L' => '1', 'B' => '8', 'S' => '5', 'A' => '4', _ => c[i] };
        }
        return new string(c);
    }

    private static string? FuzzyClave(string all)
    {
        var lines = all.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("ELECTOR")) continue;
            var pool = (lines[i] + " " + (i + 1 < lines.Length ? lines[i + 1] : "")).ToUpperInvariant();
            // 1) token suelto de 16-20 (caso normal)
            foreach (var cand in pool.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = OcrFix(cand);
                if (t.Length is >= 16 and <= 20 && t.Any(char.IsLetter) && t.Any(char.IsDigit) && !t.Contains("ELECTOR") && !t.Contains("CLAVE"))
                    return t;
            }
            // 2) OCR partió la clave en 2 tokens ("MZB'" + "'L$0107..."): junta alfanuméricos tras ELECTOR
            var after = pool[(pool.IndexOf("ELECTOR", StringComparison.Ordinal) + 7)..];
            var joined = new string(after.Where(char.IsLetterOrDigit).ToArray()).Replace("$", "S");
            if (joined.Length >= 16)
                return joined.Length <= 18 ? joined : joined[..18];
        }
        return null;
    }

    private static string? NameAfterLabel(string[] lines, string label)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(label, StringComparison.Ordinal)) continue;
            // INE frente: NOMBRE + 1-3 líneas (paterno, materno, nombres)
            var parts = new List<string>();
            for (var j = i + 1; j < Math.Min(i + 4, lines.Length) && parts.Count < 3; j++)
            {
                var l = lines[j].Trim();
                if (l.Length < 2 || l.Any(char.IsDigit) || l.Contains('<') || l.Contains("DOMICILIO") || l.Contains("SEXO") || l.Contains("CURP") || l.Contains("CLAVE") || l.Contains("ENTIDAD") || l.Contains("REGISTRO") || l.Contains("GOBIERNO") || l.Contains("VIGENCIA") || l.Contains("FECHA") || l.Contains("MÉXICO") || l.Contains("MEXICO")) break;
                if (l.Count(char.IsLetter) >= 2) parts.Add(l);
            }
            if (parts.Count > 0) return string.Join(" ", parts);
        }
        return null;
    }
}
