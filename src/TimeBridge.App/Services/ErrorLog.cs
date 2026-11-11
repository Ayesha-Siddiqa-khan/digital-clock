using System.IO;

namespace TimeBridge.App.Services;

public static class ErrorLog
{
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeBridge", "logs");

    public static void Write(Exception ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.AppendAllText(Path.Combine(Directory, "error.log"), $"[{DateTime.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* logging must never throw */ }
    }
}
