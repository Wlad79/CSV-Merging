using System.Text;

namespace SparkasseCsvMerger.Core;

internal static class CsvOutputWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Writes the merged table as UTF-8 with BOM (Excel friendly), CRLF line endings.</summary>
    public static void Write(string path, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, char delimiter, bool quoteAll)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Utf8WithBom) { NewLine = "\r\n" };

        writer.WriteLine(FormatRecord(headers, delimiter, quoteAll));

        var builder = new StringBuilder(256);

        foreach (string[] row in rows)
        {
            builder.Clear();
            writer.WriteLine(FormatRecord(row, delimiter, quoteAll, builder));
        }
    }

    private static string FormatRecord(IReadOnlyList<string> fields, char delimiter, bool quoteAll, StringBuilder? builder = null)
    {
        builder ??= new StringBuilder(256);
        builder.Clear();

        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0)
                builder.Append(delimiter);

            AppendField(builder, fields[i], delimiter, quoteAll);
        }

        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string value, char delimiter, bool quoteAll)
    {
        value ??= string.Empty;

        bool needsQuotes = quoteAll
                           || value.IndexOf(delimiter) >= 0
                           || value.Contains('"')
                           || value.Contains('\r')
                           || value.Contains('\n')
                           || value.StartsWith(' ')
                           || value.EndsWith(' ');

        if (!needsQuotes)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\""));
        builder.Append('"');
    }
}
