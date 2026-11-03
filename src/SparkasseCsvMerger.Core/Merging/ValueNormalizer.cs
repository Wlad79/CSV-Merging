using System.Globalization;
using System.Text;

namespace SparkasseCsvMerger.Core;

/// <summary>Value normalization used for duplicate detection and sorting.</summary>
internal static class ValueNormalizer
{
    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy", "d.M.yyyy", "dd.M.yyyy", "d.MM.yyyy",
        "dd.MM.yy", "d.M.yy",
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy",
        "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss"
    };

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        bool lastWasSpace = false;

        foreach (char c in value)
        {
            char normalized = c == '\u00A0' ? ' ' : c;

            if (char.IsWhiteSpace(normalized))
            {
                if (!lastWasSpace)
                    builder.Append(' ');

                lastWasSpace = true;
                continue;
            }

            builder.Append(normalized);
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    /// <summary>Normalizes "1.234,56", "-1,234.56" and "+12" to "1234.56" style output.</summary>
    public static string NormalizeAmount(string value)
    {
        string raw = CollapseWhitespace(value)
            .Replace("€", string.Empty)
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (raw.Length == 0)
            return string.Empty;

        raw = raw.Replace(" ", string.Empty);

        bool negative = raw.StartsWith('-');
        raw = raw.TrimStart('+', '-');

        int lastComma = raw.LastIndexOf(',');
        int lastDot = raw.LastIndexOf('.');

        string normalized;

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
                normalized = raw.Replace(".", string.Empty).Replace(',', '.');
            else
                normalized = raw.Replace(",", string.Empty);
        }
        else if (lastComma >= 0)
        {
            normalized = raw.Replace(',', '.');
        }
        else
        {
            normalized = raw;
        }

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            return value.Trim();

        return (negative ? -amount : amount).ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string NormalizeDate(string value)
    {
        return TryParseDate(value, out DateTime date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : CollapseWhitespace(value);
    }

    public static bool TryParseDate(string value, out DateTime date)
    {
        string raw = CollapseWhitespace(value);
        if (raw.Length == 0)
        {
            date = default;
            return false;
        }

        if (DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParse(raw, German, DateTimeStyles.None, out date)
               || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
