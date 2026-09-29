using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Net.Sockets;

namespace GpaWindows.Services;

public static class DiagnosticRunner
{
    public static string ReportPath => Path.Combine(ConfigStore.BaseDirectory, "diagnostic-report.txt");

    public static void Run()
    {
        var report = new StringBuilder();
        var config = ConfigStore.Load();
        var apps = Array.Empty<Models.ManagedApplication>();
        var audioCount = 0;
        var udp53 = DnsFilterService.IsPortAvailable(53);
        var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

        try { apps = AppInventoryService.Scan().ToArray(); }
        catch (Exception ex) { AppLogger.WriteException("startup.log", ex, "diagnostic app inventory"); }

        try
        {
            audioCount = ProcessRunner.PowerShell("Get-PnpDevice -Class AudioEndpoint | Measure-Object | Select-Object -ExpandProperty Count").StdOut
                .Trim() is { Length: > 0 } output && int.TryParse(output, out var parsed) ? parsed : 0;
        }
        catch (Exception ex) { AppLogger.WriteException("startup.log", ex, "diagnostic audio enumeration"); }

        report.AppendLine("GPA Windows diagnostic report")
            .AppendLine($"Version: {Assembly.GetEntryAssembly()?.GetName().Version}")
            .AppendLine($"Windows: {RuntimeInformation.OSDescription}")
            .AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}")
            .AppendLine($"Administrator: {OperatingSystem.IsWindows() && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)}")
            .AppendLine($"Executable: {Environment.ProcessPath}")
            .AppendLine($"Config loaded: {config is not null}")
            .AppendLine($"Service installed: {WindowsServiceManager.IsInstalled()}")
            .AppendLine($"Service status: {WindowsServiceManager.GetStatus()}")
            .AppendLine($"UDP 53: {(udp53 ? "available" : "occupied")}")
            .AppendLine($"Applications found: {apps.Length}")
            .AppendLine($"Applications with EXE: {apps.Count(x => !string.IsNullOrWhiteSpace(x.ExecutablePath))}")
            .AppendLine($"Audio devices found: {audioCount}")
            .AppendLine($"ProgramData writable: {CanWrite(ConfigStore.BaseDirectory)}")
            .AppendLine($"Hosts exists: {File.Exists(hostsPath)}")
            .AppendLine($"Hosts writable: {CanWrite(hostsPath)}")
            .AppendLine("Exceptions: see %ProgramData%\\GPAWindows\\logs\\startup.log");

        Directory.CreateDirectory(ConfigStore.BaseDirectory);
        File.WriteAllText(ReportPath, report.ToString());
    }

    private static bool CanWrite(string path)
    {
        try
        {
            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (directory is null) return false;
            Directory.CreateDirectory(directory);
            var test = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(test, "ok");
            File.Delete(test);
            return true;
        }
        catch { return false; }
    }
}
