namespace SparkasseCsvMerger.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private static void Report(Exception? exception)
    {
        string message = exception?.ToString() ?? "Unbekannter Fehler.";

        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SparkasseCsvMerger",
                "error.log");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:u}]{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never hide the original error.
        }

        MessageBox.Show(message, "Unerwarteter Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
