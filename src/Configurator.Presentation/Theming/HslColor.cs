namespace Configurator.Presentation.Theming;

using System.Globalization;

/// <summary>Immutable HSL color value supporting deterministic palette derivation and WCAG contrast checks.</summary>
public readonly record struct HslColor
{
    public double H { get; }
    public double S { get; }
    public double L { get; }
    public double A { get; }

    private HslColor(double h, double s, double l, double a = 1.0)
    {
        H = h;
        S = s;
        L = l;
        A = Math.Clamp(a, 0.0, 1.0);
    }

    public static bool TryNormalizeHex(string? hex, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith('#'))
            span = span[1..];

        if (span.Length == 3)
        {
            if (!IsHexDigit(span[0]) || !IsHexDigit(span[1]) || !IsHexDigit(span[2]))
                return false;

            Span<char> expanded = stackalloc char[7];
            expanded[0] = '#';
            expanded[1] = char.ToUpperInvariant(span[0]);
            expanded[2] = expanded[1];
            expanded[3] = char.ToUpperInvariant(span[1]);
            expanded[4] = expanded[3];
            expanded[5] = char.ToUpperInvariant(span[2]);
            expanded[6] = expanded[5];
            normalized = new string(expanded);
            return true;
        }

        if (span.Length != 6)
            return false;

        Span<char> output = stackalloc char[7];
        output[0] = '#';
        for (int index = 0; index < 6; index++)
        {
            if (!IsHexDigit(span[index]))
                return false;
            output[index + 1] = char.ToUpperInvariant(span[index]);
        }

        normalized = new string(output);
        return true;
    }

    public static bool TryParseHex(string? hex, out HslColor color)
    {
        color = default;
        if (!TryNormalizeHex(hex, out string normalized))
            return false;

        ReadOnlySpan<char> digits = normalized.AsSpan(1);
        double r = int.Parse(digits[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        double g = int.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        double b = int.Parse(digits[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        color = FromRgb(r, g, b);
        return true;
    }

    public static HslColor FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        if (!TryParseHex(hex, out HslColor color))
            throw new ArgumentException("Value must be a valid 3-digit or 6-digit hexadecimal color.", nameof(hex));
        return color;
    }

    public static HslColor FromRgb(double r, double g, double b)
    {
        r = Math.Clamp(r, 0.0, 1.0);
        g = Math.Clamp(g, 0.0, 1.0);
        b = Math.Clamp(b, 0.0, 1.0);

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 1e-12)
            return new HslColor(0.0, 0.0, l * 100.0);

        double d = max - min;
        double s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        double h;
        if (Math.Abs(max - r) < 1e-12)
            h = (g - b) / d + (g < b ? 6.0 : 0.0);
        else if (Math.Abs(max - g) < 1e-12)
            h = (b - r) / d + 2.0;
        else
            h = (r - g) / d + 4.0;

        h /= 6.0;
        return new HslColor(h * 360.0, s * 100.0, l * 100.0);
    }

    public static HslColor FromHsl(double h, double s, double l, double a = 1.0) =>
        new((h % 360.0 + 360.0) % 360.0, Math.Clamp(s, 0.0, 100.0), Math.Clamp(l, 0.0, 100.0), a);

    public HslColor AdjustHue(double newH) => new((newH % 360.0 + 360.0) % 360.0, S, L, A);

    public HslColor AdjustLightness(double newL) => new(H, S, Math.Clamp(newL, 0.0, 100.0), A);

    public HslColor AdjustSaturation(double newS) => new(H, Math.Clamp(newS, 0.0, 100.0), L, A);

    public HslColor WithAlpha(double alpha) => new(H, S, L, Math.Clamp(alpha, 0.0, 1.0));

    public (double R, double G, double B) ToRgb()
    {
        double h = H / 360.0;
        double s = S / 100.0;
        double l = L / 100.0;

        if (s < 1e-12)
            return (l, l, l);

        double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
        double p = 2.0 * l - q;

        return (HueToRgb(p, q, h + 1.0 / 3.0), HueToRgb(p, q, h), HueToRgb(p, q, h - 1.0 / 3.0));
    }

    public string ToHex()
    {
        (double r, double g, double b) = ToRgb();
        int red = Math.Clamp((int)Math.Round(r * 255.0), 0, 255);
        int green = Math.Clamp((int)Math.Round(g * 255.0), 0, 255);
        int blue = Math.Clamp((int)Math.Round(b * 255.0), 0, 255);
        if (A < 1.0)
            return string.Create(CultureInfo.InvariantCulture, $"rgba({red},{green},{blue},{A:F2})");

        return string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}");
    }

    public double RelativeLuminance()
    {
        (double r, double g, double b) = ToRgb();
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    /// <summary>Selects a high-contrast foreground color (#0F172A or #FFFFFF) using WCAG relative luminance.</summary>
    public string ContrastTextColor()
    {
        // At ~0.179 relative luminance, #0F172A and #FFFFFF yield equal (~4.2-4.6:1) WCAG contrast.
        return RelativeLuminance() > 0.179 ? "#0F172A" : "#FFFFFF";
    }

    public static double ContrastRatio(HslColor first, HslColor second)
    {
        double lumA = first.RelativeLuminance();
        double lumB = second.RelativeLuminance();
        double lighter = Math.Max(lumA, lumB);
        double darker = Math.Min(lumA, lumB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static double ContrastRatio(string hexA, string hexB) =>
        ContrastRatio(FromHex(hexA), FromHex(hexB));

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 0.5) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    private static double Linearize(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
