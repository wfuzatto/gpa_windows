using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GpaWindows.Services;

public static class AppLogger
{
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GPAWindows", "logs");

    public static string StartupLogPath => Path.Combine(LogDirectory, "startup.log");

    public static void Write(string fileName, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                Path.Combine(LogDirectory, fileName),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Logging must never become the reason the application cannot start.
        }
    }

    public static void WriteException(string fileName, Exception exception, string context)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var details = new StringBuilder()
            .AppendLine($"Context: {context}")
            .AppendLine($"Version: {version}")
            .AppendLine($"OS: {RuntimeInformation.OSDescription}")
            .AppendLine($"Executable: {Environment.ProcessPath ?? "unknown"}")
            .AppendLine($"Exception type: {exception.GetType().FullName}")
            .AppendLine($"Message: {exception.Message}")
            .AppendLine($"Stack trace: {exception.StackTrace}")
            .AppendLine($"Inner exception: {exception.InnerException}");
        Write(fileName, details.ToString());
    }
}
