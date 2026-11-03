namespace SparkasseCsvMerger;

/// <summary>Parsed command line arguments.</summary>
internal sealed class CliOptions
{
    public List<string> Inputs { get; } = new();
    public string OutputPath { get; set; } = "merged.csv";
    public bool Recursive { get; set; } = true;
    public bool Sort { get; set; } = true;
    public bool Verbose { get; set; }
    public bool DryRun { get; set; }
    public bool QuoteAll { get; set; } = true;
    public bool KeepDuplicates { get; set; }
    public string? ForcedEncoding { get; set; }
    public char? ForcedDelimiter { get; set; }
    public bool ShowHelp { get; set; }

    public const string HelpText = """
        SparkasseCsvMerger - merges Sparkasse CSV exports (plain files or inside ZIP archives)
        into a single CSV file without duplicate rows.

        Usage:
          SparkasseCsvMerger <input>... [options]

        Inputs may be CSV files, ZIP archives or directories. Directories are scanned for
        *.csv and *.zip files. ZIP archives may contain any number of CSV files.

        Options:
          -o, --output <path>     Output CSV file (default: merged.csv)
              --no-recursive      Do not scan directories recursively
              --no-sort           Keep the original row order (default: sort by booking date)
              --plain-quotes      Only quote fields that really need quoting
              --keep-duplicates   Do not remove duplicate rows
              --dry-run           Analyse and report, but write nothing
              --encoding <name>   Force input encoding (e.g. windows-1252, utf-8)
              --delimiter <char>  Force input delimiter: ; , | or tab
          -v, --verbose           Show per-file details (encoding, delimiter, skipped rows)
          -h, --help              Show this help

        Examples:
          SparkasseCsvMerger "C:\Users\me\Desktop\Umsaetze" -o merged.csv
          SparkasseCsvMerger umsaetze.zip -o alle-umsaetze.csv
          SparkasseCsvMerger export1.zip export2.zip konto.csv --no-sort -v
        """;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                case "/?":
                    options.ShowHelp = true;
                    continue;
                case "--no-recursive":
                    options.Recursive = false;
                    continue;
                case "--no-sort":
                    options.Sort = false;
                    continue;
                case "--dry-run":
                    options.DryRun = true;
                    continue;
                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    continue;
                case "--quote-all":
                    options.QuoteAll = true;
                    continue;
                case "--plain-quotes":
                    options.QuoteAll = false;
                    continue;
                case "--keep-duplicates":
                    options.KeepDuplicates = true;
                    continue;
            }

            if (TryReadValue(args, ref i, arg, "-o", "--output", out string output))
            {
                options.OutputPath = output;
                continue;
            }

            if (TryReadValue(args, ref i, arg, null, "--encoding", out string encoding))
            {
                options.ForcedEncoding = encoding;
                continue;
            }

            if (TryReadValue(args, ref i, arg, null, "--delimiter", out string delimiter))
            {
                options.ForcedDelimiter = ParseDelimiter(delimiter);
                continue;
            }

            if (arg.Length > 1 && (arg[0] == '-' || arg[0] == '/'))
                throw new ArgumentException($"unknown option '{arg}'");

            options.Inputs.Add(arg);
        }

        if (options.Inputs.Count == 0 && !options.ShowHelp)
            options.Inputs.Add(".");

        return options;
    }

    private static bool TryReadValue(string[] args, ref int index, string arg, string? shortName, string longName, out string value)
    {
        value = string.Empty;

        if (arg == longName || (shortName is not null && arg == shortName))
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException($"{arg} requires a value");

            value = args[++index];
            return true;
        }

        string prefix = longName + "=";
        if (arg.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = arg[prefix.Length..];
            return true;
        }

        return false;
    }

    private static char ParseDelimiter(string raw)
    {
        return raw.ToLowerInvariant() switch
        {
            ";" or "semicolon" or "semikolon" => ';',
            "," or "comma" or "komma" => ',',
            "|" or "pipe" => '|',
            "tab" or "\\t" or "\t" => '\t',
            _ when raw.Length == 1 => raw[0],
            _ => throw new ArgumentException($"invalid delimiter '{raw}' (use ; , | or tab)")
        };
    }
}
