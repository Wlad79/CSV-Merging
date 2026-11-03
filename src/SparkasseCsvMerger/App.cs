using SparkasseCsvMerger.Core;

namespace SparkasseCsvMerger;

internal static class App
{
    public static int Run(string[] args)
    {
        TryEnableUtf8Console();

        CliOptions options;

        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        if (options.ShowHelp || args.Length == 0)
        {
            Console.WriteLine(CliOptions.HelpText);
            return 0;
        }

        var mergeOptions = new MergeOptions
        {
            Inputs = options.Inputs,
            OutputPath = options.OutputPath,
            Recursive = options.Recursive,
            Sort = options.Sort,
            RemoveDuplicates = !options.KeepDuplicates,
            QuoteAll = options.QuoteAll,
            ForcedEncoding = options.ForcedEncoding,
            ForcedDelimiter = options.ForcedDelimiter,
            SearchText = options.SearchText,
            WriteOutput = !options.DryRun
        };

        try
        {
            MergeSummary summary = new MergerService().Run(
                mergeOptions,
                new InlineProgress(progress => ReportProgress(progress, options)));

            PrintSummary(summary, options);
            return 0;
        }
        catch (MergeException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static void ReportProgress(MergeProgress progress, CliOptions options)
    {
        if (progress.Source is { } source)
        {
            if (source.Error is not null)
            {
                WriteWarning($"{source.DisplayName}: {source.Error}");
                return;
            }

            string details = options.Verbose
                ? $"  [sep '{Describe(source.Delimiter)}', {source.Encoding}, " +
                  $"{source.SkippedRows} footer/preamble rows skipped, {source.MalformedRows} malformed]"
                : string.Empty;

            Console.WriteLine($"  + {source.DisplayName}: {source.Rows} rows{details}");
            return;
        }

        if (progress.Stage == MergeStage.Scanning)
            WriteWarning(progress.Message);
    }

    private static void PrintSummary(MergeSummary summary, CliOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("Summary");
        Console.WriteLine($"  files merged : {summary.MergedSourceCount}" +
                          (summary.SkippedSourceCount > 0 ? $", {summary.SkippedSourceCount} skipped" : string.Empty));
        Console.WriteLine($"  columns      : {summary.Columns.Count}");
        Console.WriteLine($"  rows read    : {summary.RowsRead}");
        Console.WriteLine($"  duplicates   : {summary.DuplicatesRemoved} removed");
        Console.WriteLine($"  rows merged  : {summary.MergedRowCount}");

        if (summary.SearchText is not null)
            Console.WriteLine($"  search       : \"{summary.SearchText}\" -> {summary.RowCount} rows kept");

        Console.WriteLine($"  rows kept    : {summary.RowCount}");

        if (options.Verbose || summary.Columns.Count <= 16)
            Console.WriteLine($"  column order : {string.Join(" | ", summary.Columns)}");

        if (options.DryRun)
        {
            Console.WriteLine();
            Console.WriteLine("Dry run: nothing written.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Written: {summary.OutputPath}");
        Console.WriteLine($"         UTF-8 with BOM, separator '{Describe(summary.Delimiter)}', {summary.RowCount} data rows");
    }

    private static void TryEnableUtf8Console()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Redirected console without UTF-8 support - output stays ASCII compatible.
        }
    }

    private static string Describe(char delimiter) => delimiter switch
    {
        '\t' => "\\t",
        _ => delimiter.ToString()
    };

    private static void WriteWarning(string message) =>
        Console.Error.WriteLine($"warning: {message}");

    /// <summary>IProgress implementation that invokes the callback synchronously.</summary>
    private sealed class InlineProgress : IProgress<MergeProgress>
    {
        private readonly Action<MergeProgress> _callback;

        public InlineProgress(Action<MergeProgress> callback) => _callback = callback;

        public void Report(MergeProgress value) => _callback(value);
    }
}
