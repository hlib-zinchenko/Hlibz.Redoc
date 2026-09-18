using System.Globalization;

namespace Hlibz.Redoc.AspNetCore.Extensions;

/// <summary>
/// Minimal HSL-based lighten/darken helpers for deriving accent color shades from a single hex
/// color, used when the caller supplies a custom <see cref="DarkThemeOptions.PrimaryColor"/>
/// without also specifying explicit light/dark shades.
/// </summary>
internal static class ColorUtils
{
    public static string Lighten(string hex, double amount) => AdjustLightness(hex, amount);

    public static string Darken(string hex, double amount) => AdjustLightness(hex, -amount);

    private static string AdjustLightness(string hex, double delta)
    {
        (double h, double s, double l) = ToHsl(hex);
        l = Math.Clamp(l + delta, 0, 1);
        return FromHsl(h, s, l);
    }

    private static (double H, double S, double L) ToHsl(string hex)
    {
        (int r, int g, int b) = ParseHex(hex);
        double rN = r / 255.0;
        double gN = g / 255.0;
        double bN = b / 255.0;

        double max = Math.Max(rN, Math.Max(gN, bN));
        double min = Math.Min(rN, Math.Min(gN, bN));
        double l = (max + min) / 2;

        if (max == min)
        {
            return (0, 0, l);
        }

        double delta = max - min;
        double s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

        double h;
        if (max == rN)
        {
            h = ((gN - bN) / delta) % 6;
        }
        else if (max == gN)
        {
            h = ((bN - rN) / delta) + 2;
        }
        else
        {
            h = ((rN - gN) / delta) + 4;
        }

        h *= 60;
        if (h < 0)
        {
            h += 360;
        }

        return (h, s, l);
    }

    private static string FromHsl(double h, double s, double l)
    {
        if (s == 0)
        {
            int gray = (int)Math.Round(l * 255);
            return ToHex(gray, gray, gray);
        }

        double q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        double p = (2 * l) - q;
        double hNorm = h / 360;

        double r = HueToRgb(p, q, hNorm + (1.0 / 3));
        double g = HueToRgb(p, q, hNorm);
        double b = HueToRgb(p, q, hNorm - (1.0 / 3));

        return ToHex((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6)
        {
            return p + ((q - p) * 6 * t);
        }

        if (t < 1.0 / 2)
        {
            return q;
        }

        if (t < 2.0 / 3)
        {
            return p + ((q - p) * ((2.0 / 3) - t) * 6);
        }

        return p;
    }

    private static (int R, int G, int B) ParseHex(string hex)
    {
        string value = hex.TrimStart('#');
        int r = int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int g = int.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int b = int.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }

    private static string ToHex(int r, int g, int b) =>
        $"#{Math.Clamp(r, 0, 255):x2}{Math.Clamp(g, 0, 255):x2}{Math.Clamp(b, 0, 255):x2}";
}
