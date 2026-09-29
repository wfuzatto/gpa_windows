using Microsoft.Win32;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class AppInventoryService
{
    public static List<ManagedApplication> Scan()
    {
        var found = new Dictionary<string, ManagedApplication>(StringComparer.OrdinalIgnoreCase);

        ScanUninstallHive(RegistryHive.LocalMachine, RegistryView.Registry64, found);
        ScanUninstallHive(RegistryHive.LocalMachine, RegistryView.Registry32, found);
        ScanUninstallHive(RegistryHive.CurrentUser, RegistryView.Registry64, found);
        ScanUninstallHive(RegistryHive.CurrentUser, RegistryView.Registry32, found);

        ScanAppPaths(RegistryHive.LocalMachine, RegistryView.Registry64, found);
        ScanAppPaths(RegistryHive.LocalMachine, RegistryView.Registry32, found);
        ScanAppPaths(RegistryHive.CurrentUser, RegistryView.Registry64, found);
        ScanAppPaths(RegistryHive.CurrentUser, RegistryView.Registry32, found);

        return found.Values
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ScanUninstallHive(
        RegistryHive hive,
        RegistryView view,
        Dictionary<string, ManagedApplication> found)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall");

            if (uninstall is null)
                return;

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var appKey = uninstall.OpenSubKey(subKeyName);
                    if (appKey is null)
                        continue;

                    var displayName = Convert.ToString(appKey.GetValue("DisplayName"))?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    var publisher = Convert.ToString(appKey.GetValue("Publisher"))?.Trim() ?? string.Empty;
                    var displayIcon = Convert.ToString(appKey.GetValue("DisplayIcon"))?.Trim();
                    var installLocation = Convert.ToString(appKey.GetValue("InstallLocation"))?.Trim();

                    var executable = ResolveDisplayIcon(displayIcon);

                    if (string.IsNullOrWhiteSpace(executable) && !string.IsNullOrWhiteSpace(installLocation))
                        executable = GuessPrimaryExecutable(installLocation, displayName);

                    AddOrMerge(found, new ManagedApplication
                    {
                        DisplayName = displayName,
                        Publisher = publisher,
                        ExecutablePath = executable ?? string.Empty,
                        Allowed = true
                    });
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static void ScanAppPaths(
        RegistryHive hive,
        RegistryView view,
        Dictionary<string, ManagedApplication> found)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var appPaths = baseKey.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\App Paths");

            if (appPaths is null)
                return;

            foreach (var subKeyName in appPaths.GetSubKeyNames())
            {
                try
                {
                    using var appKey = appPaths.OpenSubKey(subKeyName);
                    var rawPath = Convert.ToString(appKey?.GetValue(null));
                    var executable = NormalizeExecutablePath(rawPath);

                    if (string.IsNullOrWhiteSpace(executable))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(executable);

                    AddOrMerge(found, new ManagedApplication
                    {
                        DisplayName = string.IsNullOrWhiteSpace(name) ? subKeyName : name,
                        Publisher = string.Empty,
                        ExecutablePath = executable,
                        Allowed = true
                    });
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static void AddOrMerge(
        Dictionary<string, ManagedApplication> found,
        ManagedApplication application)
    {
        application.ExecutablePath = NormalizeExecutablePath(application.ExecutablePath) ?? string.Empty;
        application.ProcessName = string.IsNullOrWhiteSpace(application.ExecutablePath)
            ? string.Empty
            : Path.GetFileName(application.ExecutablePath);

        var key = !string.IsNullOrWhiteSpace(application.ExecutablePath)
            ? application.ExecutablePath
            : $"{application.DisplayName}|{application.Publisher}";

        if (!found.TryGetValue(key, out var existing))
        {
            found[key] = application;
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.Publisher) && !string.IsNullOrWhiteSpace(application.Publisher))
            existing.Publisher = application.Publisher;

        if (string.IsNullOrWhiteSpace(existing.ExecutablePath) && !string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            existing.ExecutablePath = application.ExecutablePath;
            existing.ProcessName = application.ProcessName;
        }
    }

    private static string? ResolveDisplayIcon(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
        string candidate;

        if (expanded.StartsWith('"'))
        {
            var endQuote = expanded.IndexOf('"', 1);
            candidate = endQuote > 1
                ? expanded[1..endQuote]
                : expanded.Trim('"');
        }
        else
        {
            var comma = expanded.LastIndexOf(',');
            candidate = comma > 0 ? expanded[..comma] : expanded;
        }

        return NormalizeExecutablePath(candidate);
    }

    private static string? GuessPrimaryExecutable(string installLocation, string displayName)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(installLocation.Trim().Trim('"'));

            if (!Directory.Exists(expanded))
                return null;

            var executables = Directory
                .EnumerateFiles(expanded, "*.exe", SearchOption.TopDirectoryOnly)
                .Take(50)
                .ToList();

            if (executables.Count == 0)
                return null;

            var compactName = new string(
                displayName
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());

            var best = executables
                .Select(path => new
                {
                    Path = path,
                    Name = new string(
                        Path.GetFileNameWithoutExtension(path)
                            .Where(char.IsLetterOrDigit)
                            .Select(char.ToLowerInvariant)
                            .ToArray())
                })
                .OrderByDescending(x =>
                    compactName.Contains(x.Name, StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(compactName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.Path.Length)
                .FirstOrDefault();

            return NormalizeExecutablePath(best?.Path);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

            if (!expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return null;

            return Path.GetFullPath(expanded);
        }
        catch
        {
            return null;
        }
    }
}
