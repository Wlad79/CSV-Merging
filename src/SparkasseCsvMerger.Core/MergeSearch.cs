namespace SparkasseCsvMerger.Core;

/// <summary>One row that contains the search term.</summary>
public sealed record SearchHit(int Number, string[] Values);

/// <summary>Case-insensitive full-text search over merged rows.</summary>
public static class MergeSearch
{
    /// <summary>
    /// Returns every row in which at least one cell contains <paramref name="term"/>.
    /// The comparison ignores case (ordinal, so umlauts work as expected).
    /// </summary>
    public static IReadOnlyList<SearchHit> Find(IReadOnlyList<string[]> rows, string? term)
    {
        if (string.IsNullOrEmpty(term) || rows.Count == 0)
            return Array.Empty<SearchHit>();

        var hits = new List<SearchHit>();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            string[] row = rows[rowIndex];

            foreach (string value in row)
            {
                if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(new SearchHit(rowIndex + 1, row));
                    break;
                }
            }
        }

        return hits;
    }
}
