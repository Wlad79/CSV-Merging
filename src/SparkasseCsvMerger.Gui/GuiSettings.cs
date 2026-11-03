using System.Text.Json;

namespace SparkasseCsvMerger.Gui;

/// <summary>Last used values, stored in %APPDATA%\SparkasseCsvMerger\gui-settings.json.</summary>
internal sealed class GuiSettings
{
    public List<string> Sources { get; set; } = new();
    public string TargetFolder { get; set; } = string.Empty;
    public string FileName { get; set; } = "merged.csv";
    public bool RemoveDuplicates { get; set; } = true;
    public bool Sort { get; set; } = true;
    public bool Recursive { get; set; } = true;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SparkasseCsvMerger",
        "gui-settings.json");

    public static GuiSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                GuiSettings? settings = JsonSerializer.Deserialize<GuiSettings>(File.ReadAllText(SettingsPath));
                if (settings is not null)
                    return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Broken settings file: fall back to defaults.
        }

        return CreateDefault();
    }

    public void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are only a convenience.
        }
    }

    private static GuiSettings CreateDefault()
    {
        var settings = new GuiSettings();

        string desktopData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "data");

        if (Directory.Exists(desktopData))
        {
            settings.Sources.Add(desktopData);
            settings.TargetFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "merged");
        }

        return settings;
    }
}
