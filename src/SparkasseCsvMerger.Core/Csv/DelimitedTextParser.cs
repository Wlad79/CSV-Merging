using System.Text;

namespace SparkasseCsvMerger.Core;

/// <summary>
/// Tolerant RFC 4180 style parser: handles quoted fields, escaped quotes ("") and line
/// breaks inside quoted fields. Records that are completely empty are dropped.
/// </summary>
internal static class DelimitedTextParser
{
    public static List<string[]> Parse(string text, char delimiter)
    {
        var records = new List<string[]>();
        if (string.IsNullOrEmpty(text))
            return records;

        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false;

        int i = 0;
        int length = text.Length;

        while (i < length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
                fieldStarted = true;
                i++;
                continue;
            }

            if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                fieldStarted = true;
                i++;
                continue;
            }

            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < length && text[i + 1] == '\n')
                    i++;

                i++;
                fields.Add(field.ToString());
                field.Clear();

                if (fieldStarted || fields.Count > 1 || fields[0].Length > 0)
                    AddRecord(records, fields);

                fields.Clear();
                fieldStarted = false;
                continue;
            }

            field.Append(c);
            fieldStarted = true;
            i++;
        }

        if (field.Length > 0 || fieldStarted)
        {
            fields.Add(field.ToString());
            AddRecord(records, fields);
        }

        return records;
    }

    private static void AddRecord(List<string[]> records, List<string> fields)
    {
        string[] record = fields.ToArray();

        foreach (string value in record)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                records.Add(record);
                return;
            }
        }
    }
}
