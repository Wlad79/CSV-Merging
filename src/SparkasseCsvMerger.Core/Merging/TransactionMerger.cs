using System.Text;

namespace SparkasseCsvMerger.Core;

internal sealed class MergeResult
{
    public required List<string> Columns { get; init; }
    public required List<string[]> Rows { get; init; }
    public int RowsRead { get; init; }
    public int DuplicatesRemoved { get; init; }
}

internal static class TransactionMerger
{
    /// <summary>
    /// Builds the union of all columns, projects every row onto it, removes duplicate
    /// rows and (optionally) sorts by booking date.
    /// </summary>
    public static MergeResult Merge(IReadOnlyList<CsvTable> tables, bool sort, bool removeDuplicates = true)
    {
        // Pass 1: resolve every header and remember the order in which columns appear.
        var resolved = new List<string[]>();
        var firstSeen = new List<string>();
        var present = new HashSet<string>(StringComparer.Ordinal);

        foreach (CsvTable table in tables)
        {
            var names = new string[table.Headers.Length];

            for (int i = 0; i < table.Headers.Length; i++)
            {
                names[i] = ColumnResolver.Resolve(table.Headers[i]);

                if (present.Add(names[i]))
                    firstSeen.Add(names[i]);
            }

            resolved.Add(names);
        }

        // Pass 2: canonical column order first (stable output), then any unknown columns.
        var columns = ColumnResolver.PreferredOrder.Where(present.Contains).ToList();
        columns.AddRange(firstSeen.Where(column => !columns.Contains(column)));

        var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < columns.Count; i++)
            columnIndex[columns[i]] = i;

        var layouts = new List<(CsvTable Table, int[] Map)>();

        for (int t = 0; t < tables.Count; t++)
        {
            var map = new int[resolved[t].Length];

            for (int i = 0; i < resolved[t].Length; i++)
                map[i] = columnIndex[resolved[t][i]];

            layouts.Add((tables[t], map));
        }

        int dateIndex = FindDateColumn(columns);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<(string[] Values, DateTime SortDate, int Order)>();

        int rowsRead = 0;
        int duplicates = 0;
        int order = 0;

        foreach ((CsvTable table, int[] map) in layouts)
        {
            foreach (string[] source in table.Rows)
            {
                rowsRead++;

                var values = new string[columns.Count];
                Array.Fill(values, string.Empty);

                int width = Math.Min(map.Length, source.Length);
                for (int i = 0; i < width; i++)
                {
                    if (values[map[i]].Length == 0)
                        values[map[i]] = source[i];
                }

                if (removeDuplicates)
                {
                    string key = BuildKey(values, columns);
                    if (!seen.Add(key))
                    {
                        duplicates++;
                        continue;
                    }
                }

                DateTime sortDate = dateIndex >= 0 && ValueNormalizer.TryParseDate(values[dateIndex], out DateTime parsed)
                    ? parsed
                    : DateTime.MinValue;

                rows.Add((values, sortDate, order++));
            }
        }

        if (sort)
        {
            rows = rows
                .OrderBy(r => r.SortDate == DateTime.MinValue ? DateTime.MaxValue : r.SortDate)
                .ThenBy(r => r.Order)
                .ToList();
        }

        return new MergeResult
        {
            Columns = columns,
            Rows = rows.Select(r => r.Values).ToList(),
            RowsRead = rowsRead,
            DuplicatesRemoved = duplicates
        };
    }

    private static int FindDateColumn(IReadOnlyList<string> columns)
    {
        int index = columns.ToList().IndexOf("Buchungstag");
        if (index >= 0)
            return index;

        index = columns.ToList().IndexOf("Valutadatum");
        if (index >= 0)
            return index;

        for (int i = 0; i < columns.Count; i++)
        {
            if (ColumnResolver.NormalizeKey(columns[i]).Contains("datum", StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static string BuildKey(IReadOnlyList<string> values, IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0)
                builder.Append('\u001F');

            builder.Append(columns[i] switch
            {
                "Betrag" => ValueNormalizer.NormalizeAmount(values[i]),
                "Buchungstag" or "Valutadatum" => ValueNormalizer.NormalizeDate(values[i]),
                _ => ValueNormalizer.CollapseWhitespace(values[i])
            });
        }

        return builder.ToString();
    }
}
