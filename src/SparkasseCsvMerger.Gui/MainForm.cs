using System.Diagnostics;
using SparkasseCsvMerger.Core;

namespace SparkasseCsvMerger.Gui;

internal sealed class MainForm : Form
{
    private readonly ListBox _sourceList = new();
    private readonly TextBox _targetFolderBox = new();
    private readonly TextBox _fileNameBox = new();
    private readonly Label _previewLabel = new();
    private readonly CheckBox _dedupeCheck = new();
    private readonly CheckBox _sortCheck = new();
    private readonly CheckBox _recursiveCheck = new();
    private readonly TextBox _searchBox = new();
    private readonly Button _searchButton = new();
    private readonly TextBox _logBox = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openFileButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _addFolderButton = new();
    private readonly Button _addFilesButton = new();
    private readonly Button _removeButton = new();
    private readonly GroupBox _sourceGroup = new();
    private readonly GroupBox _targetGroup = new();
    private readonly FlowLayoutPanel _optionsPanel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripProgressBar _statusProgress = new();

    private CancellationTokenSource? _cancellation;
    private string? _lastOutput;
    private MergeSummary? _lastSummary;
    private bool _running;

    public MainForm()
    {
        BuildUi();
        ApplySettings(GuiSettings.Load());
        UpdatePreview();
    }

    private void BuildUi()
    {
        SuspendLayout();

        Text = "Sparkasse CSV Merger";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 740);
        MinimumSize = new Size(820, 660);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12, 12, 12, 6)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));

        root.Controls.Add(BuildSourceGroup(), 0, 0);
        root.Controls.Add(BuildTargetGroup(), 0, 1);
        root.Controls.Add(BuildOptionsPanel(), 0, 2);
        root.Controls.Add(BuildSearchPanel(), 0, 3);
        root.Controls.Add(BuildButtonPanel(), 0, 4);
        root.Controls.Add(BuildLogGroup(), 0, 5);

        Controls.Add(root);
        Controls.Add(BuildStatusStrip());

        ResumeLayout(false);
        PerformLayout();
    }

    private GroupBox BuildSourceGroup()
    {
        _sourceGroup.Text = "1. Quellen (Ordner, ZIP-Archiv oder CSV-Datei)";
        _sourceGroup.Dock = DockStyle.Fill;
        _sourceGroup.Padding = new Padding(10, 6, 10, 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 182F));

        _sourceList.Dock = DockStyle.Fill;
        _sourceList.SelectionMode = SelectionMode.MultiExtended;
        _sourceList.IntegralHeight = false;
        _sourceList.HorizontalScrollbar = true;
        _sourceList.AccessibleName = "Quellen";
        _sourceList.AllowDrop = true;
        _sourceList.DragEnter += OnDragEnter;
        _sourceList.DragDrop += OnDragDrop;
        _sourceList.KeyDown += OnSourceListKeyDown;
        _sourceList.DoubleClick += (_, _) => OpenSelectedSource();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8, 0, 0, 0)
        };

        ConfigureSourceButton(_addFolderButton, "Ordner hinzufügen…");
        _addFolderButton.Click += (_, _) => AddFolder();

        ConfigureSourceButton(_addFilesButton, "Dateien hinzufügen…");
        _addFilesButton.Click += (_, _) => AddFiles();

        ConfigureSourceButton(_removeButton, "Entfernen");
        _removeButton.Click += (_, _) => RemoveSelectedSources();

        var hint = new Label
        {
            Text = "Tipp: Ordner und Dateien\nkönnen auch hierher\ngezogen werden.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(2, 12, 0, 0)
        };

        buttons.Controls.AddRange(new Control[] { _addFolderButton, _addFilesButton, _removeButton, hint });

        layout.Controls.Add(_sourceList, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        _sourceGroup.Controls.Add(layout);
        return _sourceGroup;
    }

    private static void ConfigureSourceButton(Button button, string text)
    {
        button.Text = text;
        button.Size = new Size(164, 30);
        button.Margin = new Padding(0, 0, 0, 6);
    }

    private GroupBox BuildTargetGroup()
    {
        _targetGroup.Text = "2. Zieldatei";
        _targetGroup.Dock = DockStyle.Fill;
        _targetGroup.Padding = new Padding(10, 6, 10, 8);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

        var folderLabel = new Label
        {
            Text = "Zielordner:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        };

        _targetFolderBox.Dock = DockStyle.Fill;
        _targetFolderBox.Margin = new Padding(0, 2, 8, 2);
        _targetFolderBox.AccessibleName = "Zielordner";
        _targetFolderBox.TextChanged += (_, _) => UpdatePreview();

        var browseButton = new Button
        {
            Text = "Durchsuchen…",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 1, 0, 3)
        };
        browseButton.Click += (_, _) => BrowseTargetFolder();

        var nameLabel = new Label
        {
            Text = "Dateiname:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        };

        _fileNameBox.Dock = DockStyle.Fill;
        _fileNameBox.Margin = new Padding(0, 2, 8, 2);
        _fileNameBox.AccessibleName = "Dateiname";
        _fileNameBox.TextChanged += (_, _) => UpdatePreview();

        _previewLabel.AutoEllipsis = true;
        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewLabel.ForeColor = SystemColors.GrayText;
        _previewLabel.Margin = new Padding(0);

        layout.Controls.Add(folderLabel, 0, 0);
        layout.Controls.Add(_targetFolderBox, 1, 0);
        layout.Controls.Add(browseButton, 2, 0);
        layout.Controls.Add(nameLabel, 0, 1);
        layout.Controls.Add(_fileNameBox, 1, 1);
        layout.Controls.Add(_previewLabel, 1, 2);
        layout.SetColumnSpan(_previewLabel, 2);

        _targetGroup.Controls.Add(layout);
        return _targetGroup;
    }

    private FlowLayoutPanel BuildOptionsPanel()
    {
        _optionsPanel.Dock = DockStyle.Fill;
        _optionsPanel.FlowDirection = FlowDirection.LeftToRight;
        _optionsPanel.WrapContents = false;
        _optionsPanel.Padding = new Padding(4, 10, 0, 0);

        _dedupeCheck.Text = "Duplikate entfernen";
        _dedupeCheck.AutoSize = true;
        _dedupeCheck.Checked = true;
        _dedupeCheck.Margin = new Padding(0, 0, 26, 0);

        _sortCheck.Text = "Nach Datum sortieren";
        _sortCheck.AutoSize = true;
        _sortCheck.Checked = true;
        _sortCheck.Margin = new Padding(0, 0, 26, 0);

        _recursiveCheck.Text = "Unterordner einbeziehen";
        _recursiveCheck.AutoSize = true;
        _recursiveCheck.Checked = true;

        _optionsPanel.Controls.AddRange(new Control[] { _dedupeCheck, _sortCheck, _recursiveCheck });
        return _optionsPanel;
    }

    private TableLayoutPanel BuildSearchPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(4, 6, 0, 0)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));

        var label = new Label
        {
            Text = "Suche im Ergebnis:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 0)
        };

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Margin = new Padding(0, 2, 8, 2);
        _searchBox.AccessibleName = "Suchbegriff";
        _searchBox.PlaceholderText = "Begriff eingeben – Groß-/Kleinschreibung wird ignoriert";
        _searchBox.KeyDown += OnSearchBoxKeyDown;

        _searchButton.Text = "Treffer anzeigen";
        _searchButton.Dock = DockStyle.Fill;
        _searchButton.Margin = new Padding(0, 1, 0, 1);
        _searchButton.Click += (_, _) => ShowMatches();

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(_searchBox, 1, 0);
        panel.Controls.Add(_searchButton, 2, 0);
        return panel;
    }

    private FlowLayoutPanel BuildButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 6)
        };

        _startButton.Text = "Zusammenführen";
        _startButton.Size = new Size(172, 34);
        _startButton.Font = new Font(Font, FontStyle.Bold);
        _startButton.Margin = new Padding(0, 0, 10, 0);
        _startButton.Click += OnStartClicked;

        _cancelButton.Text = "Abbrechen";
        _cancelButton.Size = new Size(110, 34);
        _cancelButton.Enabled = false;
        _cancelButton.Margin = new Padding(0, 0, 26, 0);
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();

        _openFileButton.Text = "Ergebnis öffnen";
        _openFileButton.Size = new Size(140, 34);
        _openFileButton.Enabled = false;
        _openFileButton.Margin = new Padding(0, 0, 10, 0);
        _openFileButton.Click += (_, _) => OpenInShell(_lastOutput);

        _openFolderButton.Text = "Zielordner öffnen";
        _openFolderButton.Size = new Size(160, 34);
        _openFolderButton.Enabled = false;
        _openFolderButton.Click += (_, _) => OpenInShell(
            _lastOutput is null ? null : Path.GetDirectoryName(_lastOutput));

        panel.Controls.AddRange(new Control[] { _startButton, _cancelButton, _openFileButton, _openFolderButton });
        return panel;
    }

    private GroupBox BuildLogGroup()
    {
        var group = new GroupBox
        {
            Text = "Protokoll",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 10)
        };

        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Both;
        _logBox.WordWrap = false;
        _logBox.BackColor = Color.FromArgb(252, 252, 252);
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.AccessibleName = "Protokoll";

        group.Controls.Add(_logBox);
        return group;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip();

        _statusLabel.Text = "Bereit.";
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _statusProgress.Style = ProgressBarStyle.Marquee;
        _statusProgress.Width = 140;
        _statusProgress.Visible = false;

        strip.Items.Add(_statusLabel);
        strip.Items.Add(_statusProgress);
        return strip;
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
            QuoteAll = true
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
        AppendLog($"  Zeilen    : {summary.RowsRead} gelesen, {summary.DuplicatesRemoved} Duplikate entfernt, {summary.RowCount} geschrieben");
        AppendLog($"  Spalten   : {summary.Columns.Count}");
        AppendLog($"  Spaltenfolge: {string.Join(" | ", summary.Columns)}");
        AppendLog($"  Datei     : {summary.OutputPath}");

        _statusLabel.Text =
            $"Fertig: {summary.RowCount:N0} Zeilen geschrieben · {summary.DuplicatesRemoved:N0} Duplikate entfernt · " +
            $"{summary.Columns.Count} Spalten · {summary.Elapsed.TotalSeconds:0.0} s";

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
