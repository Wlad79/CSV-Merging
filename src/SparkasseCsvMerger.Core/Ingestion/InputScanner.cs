using System.IO.Compression;

namespace SparkasseCsvMerger.Core;

internal sealed record CsvSource(string DisplayName, byte[] Content);

/// <summary>
/// Turns the user supplied paths into a list of in-memory CSV sources. ZIP archives are
/// opened and every CSV/TXT entry inside is read; loose CSV files are read directly.
/// </summary>
internal static class InputScanner
{
    private const long MaxEntryBytes = 512L * 1024 * 1024;

    public static (List<CsvSource> Sources, List<string> Warnings) Scan(
        IEnumerable<string> inputs,
        bool recursive,
        string? excludedFullPath)
    {
        var sources = new List<CsvSource>();
        var warnings = new List<string>();

        foreach (string input in inputs)
        {
            string fullPath = Path.GetFullPath(input);

            if (Directory.Exists(fullPath))
            {
                SearchOption search = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                List<string> files;

                try
                {
                    files = Directory.EnumerateFiles(fullPath, "*", search)
                        .Where(IsArchiveOrCsv)
                        .Where(path => !IsExcluded(path, excludedFullPath))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"cannot scan '{fullPath}': {ex.Message}");
                    continue;
                }

                if (files.Count == 0)
                    warnings.Add($"no .csv or .zip files found in '{fullPath}'");

                foreach (string file in files)
                    ReadFile(file, sources, warnings);

                continue;
            }

            if (File.Exists(fullPath))
            {
                if (IsExcluded(fullPath, excludedFullPath))
                {
                    warnings.Add($"'{fullPath}' is the output file and was skipped");
                    continue;
                }

                ReadFile(fullPath, sources, warnings);
                continue;
            }

            warnings.Add($"input not found: '{fullPath}'");
        }

        return (sources, warnings);
    }

    private static void ReadFile(string path, List<CsvSource> sources, List<string> warnings)
    {
        string extension = Path.GetExtension(path);

        bool isZip = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                     || extension.Equals(".zipx", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isZip)
            {
                ReadZip(path, sources, warnings);
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            sources.Add(new CsvSource(Shorten(path), bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"cannot read '{path}': {ex.Message}");
        }
    }

    private static void ReadZip(string path, List<CsvSource> sources, List<string> warnings)
    {
        int before = sources.Count;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                string entryPath = entry.FullName.Replace('\\', '/');

                if (entryPath.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
                    || entryPath.Split('/').Any(part => part.StartsWith('.')))
                    continue;

                if (!Path.GetExtension(entry.Name).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.Length > MaxEntryBytes)
                {
                    warnings.Add($"{Shorten(path)} > {entryPath}: skipped, larger than {MaxEntryBytes / (1024 * 1024)} MB");
                    continue;
                }

                using Stream entryStream = entry.Open();
                using var buffer = new MemoryStream(entry.Length > 0 && entry.Length < int.MaxValue ? (int)entry.Length : 0);
                entryStream.CopyTo(buffer);

                sources.Add(new CsvSource($"{Shorten(path)} > {entryPath}", buffer.ToArray()));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            warnings.Add($"cannot open archive '{path}': {ex.Message}");
        }

        if (sources.Count == before && !warnings.Any(w => w.Contains(path, StringComparison.Ordinal)))
            warnings.Add($"{Shorten(path)}: archive contains no CSV files");
    }

    private static bool IsArchiveOrCsv(string path)
    {
        string extension = Path.GetExtension(path);

        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcluded(string path, string? excludedFullPath)
    {
        if (string.IsNullOrEmpty(excludedFullPath))
            return false;

        if (!Path.IsPathRooted(path))
            path = Path.GetFullPath(path);

        return string.Equals(path, excludedFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shortens a path to be relative to the working directory when possible.</summary>
    private static string Shorten(string path)
    {
        string full = Path.GetFullPath(path);
        string cwd = Directory.GetCurrentDirectory();

        if (full.StartsWith(cwd + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return full[(cwd.Length + 1)..];

        return full;
    }
}
