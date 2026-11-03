using System.Diagnostics;

namespace SparkasseCsvMerger.Core;

/// <summary>
/// High level entry point used by the console application and the GUI.
/// </summary>
public sealed class MergerService
{
    public MergeSummary Run(MergeOptions options, IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        string outputPath = Path.GetFullPath(options.OutputPath);

        (List<CsvSource> sources, List<string> scanWarnings) = InputScanner.Scan(options.Inputs, options.Recursive, outputPath);

        foreach (string warning in scanWarnings)
            progress?.Report(new MergeProgress(MergeStage.Scanning, warning));

        if (sources.Count == 0)
            throw new MergeException("Keine CSV-Eingabe gefunden. Bitte einen Ordner, ein ZIP-Archiv oder eine CSV-Datei auswählen.");

        var tables = new List<CsvTable>();
        var reports = new List<SourceReport>();

        foreach (CsvSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                (string text, string encodingName) = TextDecoding.Decode(source.Content, options.ForcedEncoding);
                CsvTable table = CsvTableReader.Read(text, source.DisplayName, encodingName, options.ForcedDelimiter);

                if (table.Headers.Length == 0 || table.Rows.Count == 0)
                {
                    var empty = new SourceReport(source.DisplayName, 0, table.SkippedRecords, table.MalformedRecords,
                        encodingName, table.Delimiter, "keine Datenzeilen gefunden");
                    reports.Add(empty);
                    progress?.Report(new MergeProgress(MergeStage.Reading, empty.Error!, empty));
                    continue;
                }

                tables.Add(table);

                var report = new SourceReport(source.DisplayName, table.Rows.Count, table.SkippedRecords,
                    table.MalformedRecords, encodingName, table.Delimiter, null);
                reports.Add(report);
                progress?.Report(new MergeProgress(MergeStage.Reading, $"{source.DisplayName}: {table.Rows.Count} Zeilen", report));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var failed = new SourceReport(source.DisplayName, 0, 0, 0, string.Empty, ';', ex.Message);
                reports.Add(failed);
                progress?.Report(new MergeProgress(MergeStage.Reading, failed.Error!, failed));
            }
        }

        if (tables.Count == 0)
            throw new MergeException("Keine lesbaren CSV-Daten gefunden.");

        progress?.Report(new MergeProgress(MergeStage.Merging, "Zeilen werden zusammengeführt..."));
        MergeResult result = TransactionMerger.Merge(tables, options.Sort, options.RemoveDuplicates);

        char delimiter = tables.All(table => table.Delimiter == ',') ? ',' : ';';

        if (options.WriteOutput)
        {
            if (result.Rows.Count == 0)
                throw new MergeException("Nach dem Zusammenführen sind keine Datenzeilen übrig.");

            progress?.Report(new MergeProgress(MergeStage.Writing, $"Schreibe {outputPath}..."));

            CsvOutputWriter.Write(outputPath, result.Columns, result.Rows, delimiter, options.QuoteAll);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new MergeSummary
        {
            OutputPath = outputPath,
            Columns = result.Columns,
            Rows = result.Rows,
            Sources = reports,
            RowsRead = result.RowsRead,
            DuplicatesRemoved = result.DuplicatesRemoved,
            RowCount = result.Rows.Count,
            Delimiter = delimiter,
            Elapsed = stopwatch.Elapsed
        };
    }
}
