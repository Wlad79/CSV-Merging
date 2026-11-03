namespace SparkasseCsvMerger.Core;

/// <summary>One parsed CSV file: header row plus data rows.</summary>
internal sealed record CsvTable(
    string SourceName,
    string EncodingName,
    char Delimiter,
    string[] Headers,
    List<string[]> Rows,
    int SkippedRecords,
    int MalformedRecords);

internal static class CsvTableReader
{
    private static readonly string[] HeaderKeywords =
    {
        "buchungstag", "buchungsdatum", "verwendungszweck", "auftragskonto", "valutadatum",
        "buchungstext", "betrag", "empfaenger", "zahlungspflichtiger", "umsatz",
        "kategorie", "waehrung", "iban", "bic"
    };

    public static CsvTable Read(string text, string sourceName, string encodingName, char? forcedDelimiter)
    {
        char delimiter = forcedDelimiter ?? DelimiterDetector.Detect(text);
        List<string[]> records = DelimitedTextParser.Parse(text, delimiter);

        if (records.Count == 0)
            return new CsvTable(sourceName, encodingName, delimiter, Array.Empty<string>(), new(), 0, 0);

        int headerIndex = FindHeader(records);
        string[] headers = records[headerIndex].Select(Clean).ToArray();

        if (headers.Length < 2)
            return new CsvTable(sourceName, encodingName, delimiter, Array.Empty<string>(), new(), 0, 0);

        var rows = new List<string[]>();
        int skipped = 0;
        int malformed = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (i == headerIndex)
                continue;

            // Anything before the header row is a preamble (account info, advertising, ...).
            if (i < headerIndex)
            {
                skipped++;
                continue;
            }

            string[] record = records[i];

            if (record.All(string.IsNullOrWhiteSpace))
                continue;

            if (IsRepeatedHeader(record, headers))
            {
                skipped++;
                continue;
            }

            switch (FitWidth(record, headers.Length, out string[] fitted))
            {
                case RowFit.Unusable:
                    // Footer lines (balance, IBAN, "Auszug: 1") have fewer columns.
                    skipped++;
                    continue;
                case RowFit.Malformed:
                    malformed++;
                    break;
            }

            rows.Add(fitted);
        }

        return new CsvTable(sourceName, encodingName, delimiter, headers, rows, skipped, malformed);
    }

    private static int FindHeader(List<string[]> records)
    {
        int limit = Math.Min(records.Count, 60);

        for (int i = 0; i < limit; i++)
        {
            string[] record = records[i];
            if (record.Length < 2)
                continue;

            int hits = 0;

            foreach (string field in record)
            {
                string key = ColumnResolver.NormalizeKey(field);

                if (HeaderKeywords.Any(keyword => key.Contains(keyword, StringComparison.Ordinal)))
                {
                    hits++;
                    if (hits >= 2)
                        return i;
                }
            }
        }

        // No known header words: take the widest record of the first lines.
        int bestIndex = 0;
        int bestWidth = -1;

        for (int i = 0; i < Math.Min(records.Count, 30); i++)
        {
            if (records[i].Length > bestWidth)
            {
                bestWidth = records[i].Length;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool IsRepeatedHeader(string[] record, string[] headers)
    {
        if (record.Length < headers.Length)
            return false;

        for (int i = 0; i < headers.Length; i++)
        {
            if (!string.Equals(Clean(record[i]), headers[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private enum RowFit
    {
        Ok,
        Malformed,
        Unusable
    }

    private static RowFit FitWidth(string[] record, int width, out string[] fitted)
    {
        fitted = Array.Empty<string>();

        if (record.Length < width)
            return RowFit.Unusable;

        var normalized = new string[width];
        Array.Copy(record, normalized, width);
        fitted = normalized.Select(Clean).ToArray();

        if (record.Length == width)
            return RowFit.Ok;

        // A trailing separator (or trailing empty column) produces extra empty fields.
        for (int i = width; i < record.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(record[i]))
                return RowFit.Malformed;
        }

        return RowFit.Ok;
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace('\u00A0', ' ')
            .Replace("\uFEFF", string.Empty)
            .Trim();
    }
}
