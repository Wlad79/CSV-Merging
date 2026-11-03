namespace SparkasseCsvMerger.Core;

public enum MergeStage
{
    Scanning,
    Reading,
    Merging,
    Writing
}

/// <summary>Status of one single CSV file.</summary>
public sealed record SourceReport(
    string DisplayName,
    int Rows,
    int SkippedRows,
    int MalformedRows,
    string Encoding,
    char Delimiter,
    string? Error)
{
    public bool IsMerged => Error is null && Rows > 0;
}

/// <summary>Progress notification. A <see cref="Source"/> is set when one file was processed.</summary>
public sealed record MergeProgress(MergeStage Stage, string Message, SourceReport? Source = null);

/// <summary>Expected failure (no input, unreadable data, ...) - message is meant for the user.</summary>
public sealed class MergeException : Exception
{
    public MergeException(string message) : base(message)
    {
    }
}

public sealed class MergeSummary
{
    public required string OutputPath { get; init; }
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<string[]> Rows { get; init; }
    public required IReadOnlyList<SourceReport> Sources { get; init; }
    public required int RowsRead { get; init; }
    public required int DuplicatesRemoved { get; init; }
    public required int RowCount { get; init; }
    public required char Delimiter { get; init; }
    public required TimeSpan Elapsed { get; init; }

    public int MergedSourceCount => Sources.Count(source => source.IsMerged);
    public int SkippedSourceCount => Sources.Count - MergedSourceCount;
}
