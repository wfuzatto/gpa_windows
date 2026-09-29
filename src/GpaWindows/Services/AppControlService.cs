using System.Diagnostics;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class AppControlService
{
    public static void Enforce(PolicyConfig config)
    {
        if (!config.ApplicationControlEnabled)
            return;

        var denied = config.ManagedApplications
            .Where(x => !x.Allowed && !string.IsNullOrWhiteSpace(x.ExecutablePath))
            .Select(x => NormalizePath(x.ExecutablePath))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowed = config.ManagedApplications
            .Where(x => x.Allowed && !string.IsNullOrWhiteSpace(x.ExecutablePath))
            .Select(x => NormalizePath(x.ExecutablePath))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selfPath = NormalizePath(Environment.ProcessPath);
        var windowsPath = NormalizeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId || process.SessionId <= 0)
                    continue;

                var path = NormalizePath(process.MainModule?.FileName);
                if (path is null)
                    continue;

                if (selfPath is not null &&
                    path.Equals(selfPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (windowsPath is not null && IsUnderDirectory(path, windowsPath))
                    continue;

                if (IsAlwaysProtected(path))
                    continue;

                var mustBlock = denied.Contains(path);

                if (!mustBlock && config.BlockUnknownApplications)
                    mustBlock = !allowed.Contains(path);

                if (!mustBlock)
                    continue;

                var username = TryGetUserName(process);
                process.Kill(entireProcessTree: true);
                AppLogger.Write(
                    "app-block.log",
                    $"user={username}; executable={Path.GetFileName(path)}; path={path}; pid={process.Id}; reason={(config.BlockUnknownApplications && !denied.Contains(path) ? "strict-mode" : "explicit-deny")}");
            }
            catch
            {
                // Processos protegidos ou encerrados entre a enumeração e a leitura são ignorados.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return Path.GetFullPath(value.Trim().Trim('"'));
        }
        catch
        {
            return null;
        }
    }

    public static string? NormalizePathForPolicy(string? value) => NormalizePath(value);

    public static bool IsProtectedExecutable(string? value) =>
        value is not null && IsAlwaysProtected(NormalizePath(value));

    private static bool IsAlwaysProtected(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var name = Path.GetFileName(path);
        return name.Equals("GpaWindows.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("services.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("winlogon.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("csrss.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("wininit.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("lsass.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("smss.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("conhost.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryGetUserName(Process process)
    {
        try { return process.StartInfo.UserName ?? Environment.UserName; }
        catch { return Environment.UserName; }
    }
}
