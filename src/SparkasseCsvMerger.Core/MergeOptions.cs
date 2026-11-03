namespace SparkasseCsvMerger.Core;

/// <summary>Everything the merger needs to run one job.</summary>
public sealed class MergeOptions
{
    /// <summary>Folders, ZIP archives and/or CSV files.</summary>
    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();

    /// <summary>Target CSV file (folders are created automatically).</summary>
    public string OutputPath { get; init; } = "merged.csv";

    /// <summary>Scan sub folders of input folders.</summary>
    public bool Recursive { get; init; } = true;

    /// <summary>Sort the result by booking date.</summary>
    public bool Sort { get; init; } = true;

    /// <summary>Remove duplicate rows.</summary>
    public bool RemoveDuplicates { get; init; } = true;

    /// <summary>Quote every field in the output.</summary>
    public bool QuoteAll { get; init; } = true;

    /// <summary>Force the input encoding (e.g. "windows-1252") instead of detecting it.</summary>
    public string? ForcedEncoding { get; init; }

    /// <summary>Force the input separator instead of detecting it.</summary>
    public char? ForcedDelimiter { get; init; }

    /// <summary>When false the merge is only analysed, no file is written (dry run).</summary>
    public bool WriteOutput { get; init; } = true;
}
