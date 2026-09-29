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

                var mustBlock = denied.Contains(path);

                if (!mustBlock && config.BlockUnknownApplications)
                    mustBlock = !allowed.Contains(path);

                if (!mustBlock)
                    continue;

                process.Kill(entireProcessTree: true);
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
}
