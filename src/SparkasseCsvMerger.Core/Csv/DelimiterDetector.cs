namespace SparkasseCsvMerger.Core;

/// <summary>Guesses the field separator of a CSV file by looking at the first lines.</summary>
internal static class DelimiterDetector
{
    private static readonly char[] Candidates = { ';', ',', '\t', '|' };
    private const int SampleLines = 40;
    private const int SampleChars = 32_000;

    public static char Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ';';

        string sample = TakeSample(text);
        char best = ';';
        int bestScore = -1;

        foreach (char candidate in Candidates)
        {
            List<string[]> records = DelimitedTextParser.Parse(sample, candidate);
            if (records.Count == 0)
                continue;

            List<int> counts = records.Where(r => r.Length >= 2).Select(r => r.Length).ToList();
            if (counts.Count == 0)
                continue;

            IGrouping<int, int>? mode = counts
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .FirstOrDefault();

            if (mode is null)
                continue;

            // Prefer a separator that yields the same field count on many lines.
            int score = (mode.Count() * 100) + (mode.Key * 3);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static string TakeSample(string text)
    {
        int end = 0;
        int lines = 0;

        while (end < text.Length && lines < SampleLines && end < SampleChars)
        {
            char c = text[end];
            end++;
            if (c == '\n')
                lines++;
        }

        return text[..end];
    }
}
