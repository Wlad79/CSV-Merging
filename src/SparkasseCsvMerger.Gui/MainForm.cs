using System.ComponentModel;
using System.Diagnostics;
using SparkasseCsvMerger.Core;

namespace SparkasseCsvMerger.Gui;

internal sealed partial class MainForm : Form
{
    private CancellationTokenSource? _cancellation;
    private string? _lastOutput;
    private MergeSummary? _lastSummary;
    private bool _running;

    public MainForm()
    {
        InitializeComponent();

        // Der WinForms-Designer von Visual Studio führt diesen Konstruktor nicht aus.
        // Die Prüfung schützt nur davor, dass beim Entwurf Einstellungen geladen werden.
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            return;

        ApplySettings(GuiSettings.Load());
        UpdatePreview();
    }

    // ---------------------------------------------------------------- settings

    private void ApplySettings(GuiSettings settings)
    {
        _sourceList.Items.Clear();

        foreach (string source in settings.Sources)
            AddSource(source, select: false);

        _targetFolderBox.Text = settings.TargetFolder;
        _fileNameBox.Text = string.IsNullOrWhiteSpace(settings.FileName) ? "merged.csv" : settings.FileName;
        _dedupeCheck.Checked = settings.RemoveDuplicates;
        _sortCheck.Checked = settings.Sort;
        _recursiveCheck.Checked = settings.Recursive;
        _searchBox.Text = settings.SearchText ?? string.Empty;
    }

    private GuiSettings CollectSettings()
    {
        var settings = new GuiSettings
        {
            TargetFolder = _targetFolderBox.Text.Trim(),
            FileName = _fileNameBox.Text.Trim(),
            RemoveDuplicates = _dedupeCheck.Checked,
            Sort = _sortCheck.Checked,
            Recursive = _recursiveCheck.Checked,
            SearchText = _searchBox.Text.Trim()
        };

        settings.Sources.AddRange(_sourceList.Items.Cast<string>());
        return settings;
    }

    private void SaveSettings()
    {
        GuiSettings settings = CollectSettings();
        settings.Save();
    }

    // ------------------------------------------------------------------ sources

    private void OnAddFolderClicked(object? sender, EventArgs e) => AddFolder();

    private void OnAddFilesClicked(object? sender, EventArgs e) => AddFiles();

    private void OnRemoveClicked(object? sender, EventArgs e) => RemoveSelectedSources();

    private void OnSourceListDoubleClick(object? sender, EventArgs e) => OpenSelectedSource();

    private void AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Ordner mit CSV- oder ZIP-Dateien auswählen",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        string target = _targetFolderBox.Text.Trim();
        if (Directory.Exists(target))
            dialog.SelectedPath = target;
        else if (_sourceList.Items.Count > 0 && _sourceList.Items[0] is string first && Directory.Exists(first))
            dialog.SelectedPath = first;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddSource(dialog.SelectedPath);
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "CSV- oder ZIP-Dateien auswählen",
            Filter = "CSV- und ZIP-Dateien|*.csv;*.zip|Alle Dateien|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (string file in dialog.FileNames)
            AddSource(file);
    }

    private void AddSource(string path, bool select = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        foreach (string existing in _sourceList.Items)
        {
            if (string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                if (select)
                    _sourceList.SelectedItem = existing;

                return;
            }
        }

        int index = _sourceList.Items.Add(fullPath);

        if (select)
            _sourceList.SelectedIndex = index;

        UpdateStartState();
    }

    private void RemoveSelectedSources()
    {
        if (_sourceList.SelectedIndices.Count == 0)
            return;

        var indices = _sourceList.SelectedIndices.Cast<int>().OrderByDescending(index => index).ToList();

        foreach (int index in indices)
            _sourceList.Items.RemoveAt(index);

        UpdateStartState();
    }

    private void OnSourceListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Delete)
            return;

        RemoveSelectedSources();
        e.Handled = true;
    }

    private void OpenSelectedSource()
    {
        if (_sourceList.SelectedItem is not string path)
            return;

        try
        {
            if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path))
                OpenInShell(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Öffnen fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (string path in paths)
                AddSource(path);
        }
    }

    // ------------------------------------------------------------------- target

    private void OnBrowseTargetClicked(object? sender, EventArgs e) => BrowseTargetFolder();

    private void BrowseTargetFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Zielordner für die zusammengeführte CSV-Datei auswählen",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        string current = _targetFolderBox.Text.Trim();
        if (Directory.Exists(current))
            dialog.SelectedPath = current;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _targetFolderBox.Text = dialog.SelectedPath;
            UpdatePreview();
        }
    }

    private void OnOutputPathChanged(object? sender, EventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        string folder = _targetFolderBox.Text.Trim();
        string fileName = _fileNameBox.Text.Trim();

        if (fileName.Length > 0 && !fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            fileName += ".csv";

        if (folder.Length == 0 || fileName.Length == 0)
        {
            _previewLabel.Text = string.Empty;
            return;
        }

        try
        {
            _previewLabel.Text = "→ " + Path.Combine(folder, fileName);
        }
        catch (ArgumentException)
        {
            _previewLabel.Text = string.Empty;
        }
    }

    // --------------------------------------------------------------------- run

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (!TryBuildOptions(out MergeOptions? options, out string error))
        {
            MessageBox.Show(this, error, "Eingabe prüfen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (File.Exists(options.OutputPath))
        {
            DialogResult answer = MessageBox.Show(
                this,
                $"Die Datei existiert bereits:\n\n{options.OutputPath}\n\nÜberschreiben?",
                "Datei überschreiben",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return;
        }

        SaveSettings();

        _lastOutput = null;
        _lastSummary = null;
        SetRunning(true);
        _cancellation = new CancellationTokenSource();

        AppendLog(string.Empty);
        AppendLog($"=== Start {DateTime.Now:dd.MM.yyyy HH:mm:ss} ===");
        AppendLog($"Ziel: {options.OutputPath}");

        try
        {
            var progress = new Progress<MergeProgress>(OnProgress);
            CancellationToken token = _cancellation.Token;

            MergeSummary summary = await Task.Run(() => new MergerService().Run(options, progress, token), token);

            OnFinished(summary);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Abgebrochen.");
            _statusLabel.Text = "Abgebrochen.";
        }
        catch (MergeException ex)
        {
            AppendLog("FEHLER: " + ex.Message);
            _statusLabel.Text = "Fehler: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER: " + ex);
            _statusLabel.Text = "Unerwarteter Fehler.";
            MessageBox.Show(this, ex.Message, "Unerwarteter Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            SetRunning(false);
        }
    }

    private bool TryBuildOptions(out MergeOptions options, out string error)
    {
        options = new MergeOptions();

        var sources = _sourceList.Items.Cast<string>().ToList();
        if (sources.Count == 0)
        {
            error = "Bitte mindestens einen Quellordner oder eine Datei hinzufügen.";
            return false;
        }

        string targetFolder = _targetFolderBox.Text.Trim();
        if (targetFolder.Length == 0)
        {
            error = "Bitte einen Zielordner auswählen.";
            return false;
        }

        string fileName = _fileNameBox.Text.Trim();
        if (fileName.Length == 0)
        {
            error = "Bitte einen Dateinamen angeben.";
            return false;
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Der Dateiname enthält ungültige Zeichen.";
            return false;
        }

        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            fileName += ".csv";

        string outputPath;

        try
        {
            outputPath = Path.GetFullPath(Path.Combine(targetFolder, fileName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "Zielpfad ist ungültig: " + ex.Message;
            return false;
        }

        options = new MergeOptions
        {
            Inputs = sources,
            OutputPath = outputPath,
            Recursive = _recursiveCheck.Checked,
            Sort = _sortCheck.Checked,
            RemoveDuplicates = _dedupeCheck.Checked,
            QuoteAll = true,
            SearchText = _searchBox.Text.Trim().Length == 0 ? null : _searchBox.Text.Trim()
        };

        error = string.Empty;
        return true;
    }

    private void OnProgress(MergeProgress progress)
    {
        if (_running)
        {
            switch (progress.Stage)
            {
                case MergeStage.Scanning when progress.Source is null:
                    AppendLog("Hinweis: " + progress.Message);
                    break;

                case MergeStage.Reading when progress.Source is { } source:
                    if (source.Error is null)
                    {
                        AppendLog($"+ {source.DisplayName}  ({source.Rows} Zeilen, {source.Encoding}, '{Describe(source.Delimiter)}')");
                        _statusLabel.Text = $"Lese: {source.DisplayName}";
                    }
                    else
                    {
                        AppendLog($"! {source.DisplayName}: {source.Error}");
                    }

                    break;

                case MergeStage.Merging:
                    _statusLabel.Text = "Zeilen werden zusammengeführt…";
                    break;

                case MergeStage.Searching:
                    AppendLog(progress.Message);
                    _statusLabel.Text = "Suche wird angewendet…";
                    break;

                case MergeStage.Writing:
                    _statusLabel.Text = "Zieldatei wird geschrieben…";
                    break;
            }
        }
    }

    private void OnFinished(MergeSummary summary)
    {
        _lastOutput = summary.OutputPath;
        _lastSummary = summary;

        AppendLog(string.Empty);
        AppendLog($"Fertig in {summary.Elapsed.TotalSeconds:0.0} s");
        AppendLog($"  Quellen   : {summary.MergedSourceCount} zusammengeführt, {summary.SkippedSourceCount} übersprungen");
        AppendLog($"  Zeilen    : {summary.RowsRead} gelesen, {summary.DuplicatesRemoved} Duplikate entfernt, " +
                  $"{summary.MergedRowCount} zusammengeführt");

        if (summary.SearchText is not null)
            AppendLog($"  Suche     : \"{summary.SearchText}\" → {summary.RowCount} Treffer gespeichert " +
                      "(Groß-/Kleinschreibung egal)");
        else
            AppendLog($"  Zeilen    : {summary.RowCount} geschrieben");

        AppendLog($"  Spalten   : {summary.Columns.Count}");
        AppendLog($"  Spaltenfolge: {string.Join(" | ", summary.Columns)}");
        AppendLog($"  Datei     : {summary.OutputPath}");

        _statusLabel.Text = summary.SearchText is null
            ? $"Fertig: {summary.RowCount:N0} Zeilen geschrieben · {summary.DuplicatesRemoved:N0} Duplikate entfernt · " +
              $"{summary.Columns.Count} Spalten · {summary.Elapsed.TotalSeconds:0.0} s"
            : $"Fertig: {summary.RowCount:N0} Treffer für \"{summary.SearchText}\" gespeichert · " +
              $"{summary.MergedRowCount:N0} Zeilen durchsucht · {summary.Elapsed.TotalSeconds:0.0} s";

        if (_searchBox.Text.Trim().Length > 0)
            ShowMatches();
    }

    private void SetRunning(bool running)
    {
        _running = running;
        _sourceGroup.Enabled = !running;
        _targetGroup.Enabled = !running;
        _optionsPanel.Enabled = !running;
        _startButton.Enabled = !running;
        _cancelButton.Enabled = running;
        _statusProgress.Visible = running;
        _searchBox.Enabled = !running;
        _searchButton.Enabled = !running;

        if (running)
            _statusLabel.Text = "Läuft…";

        _openFileButton.Enabled = !running && _lastOutput is not null && File.Exists(_lastOutput);
        _openFolderButton.Enabled = !running && _lastOutput is not null
                                   && Directory.Exists(Path.GetDirectoryName(_lastOutput));
    }

    private void UpdateStartState()
    {
        if (!_running)
            _startButton.Enabled = true;
    }

    // ------------------------------------------------------------------- helper

    private void OnCancelClicked(object? sender, EventArgs e) => _cancellation?.Cancel();

    private void OnOpenFileClicked(object? sender, EventArgs e) => OpenInShell(_lastOutput);

    private void OnOpenFolderClicked(object? sender, EventArgs e) => OpenInShell(
        _lastOutput is null ? null : Path.GetDirectoryName(_lastOutput));

    private void OnSearchButtonClicked(object? sender, EventArgs e) => ShowMatches();

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        ShowMatches();
    }

    private void ShowMatches()
    {
        if (_lastSummary is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Zusammenführung starten.", "Suche",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string term = _searchBox.Text.Trim();

        if (term.Length == 0)
        {
            MessageBox.Show(this, "Bitte einen Suchbegriff eingeben.", "Suche",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _searchBox.Focus();
            return;
        }

        IReadOnlyList<SearchHit> hits = MergeSearch.Find(_lastSummary.Rows, term);

        AppendLog(string.Empty);
        AppendLog($"=== Suche \"{term}\" (Groß-/Kleinschreibung egal): " +
                  $"{hits.Count} Treffer von {_lastSummary.Rows.Count} Zeilen ===");

        if (hits.Count == 0)
        {
            AppendLog("(keine Treffer)");
        }
        else
        {
            foreach (SearchHit hit in hits)
                AppendLog(FormatHit(hit, _lastSummary.Columns));
        }

        _statusLabel.Text = $"Suche \"{term}\": {hits.Count} Treffer";
    }

    private static string FormatHit(SearchHit hit, IReadOnlyList<string> columns)
    {
        var parts = new List<string>(hit.Values.Length);

        for (int i = 0; i < hit.Values.Length && i < columns.Count; i++)
        {
            string value = hit.Values[i];
            if (value.Length > 0)
                parts.Add($"{columns[i]}={value}");
        }

        return $"#{hit.Number,5}  {string.Join(" | ", parts)}";
    }

    private void AppendLog(string line)
    {
        _logBox.AppendText(line + Environment.NewLine);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private static string Describe(char delimiter) => delimiter switch
    {
        '\t' => "\\t",
        _ => delimiter.ToString()
    };

    private void OpenInShell(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Öffnen fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_running)
        {
            DialogResult answer = MessageBox.Show(
                this,
                "Der Vorgang läuft noch. Abbrechen und schließen?",
                "Beenden",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _cancellation?.Cancel();
        }

        SaveSettings();
        base.OnFormClosing(e);
    }
}
