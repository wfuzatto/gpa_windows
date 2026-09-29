using Microsoft.Win32;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class RegistryPolicyService
{
    private const string WallpaperPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop";
    private const string ThemePolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

    public static void Apply(PolicyConfig config)
    {
        ApplyToUserHive(Registry.CurrentUser, config);

        foreach (var sid in Registry.Users.GetSubKeyNames())
        {
            if (!IsUserSid(sid))
                continue;

            try
            {
                using var userHive = Registry.Users.OpenSubKey(sid, writable: true);
                if (userHive is not null)
                    ApplyToUserHive(userHive, config);
            }
            catch
            {
                // Alguns hives podem estar sendo descarregados durante logon/logoff.
            }
        }

        ApplyBrowserDnsPolicies(config.DnsAllowListEnabled && config.DisableBrowserDoH);
    }

    public static void Revert()
    {
        RevertUserHive(Registry.CurrentUser);

        foreach (var sid in Registry.Users.GetSubKeyNames())
        {
            if (!IsUserSid(sid))
                continue;

            try
            {
                using var userHive = Registry.Users.OpenSubKey(sid, writable: true);
                if (userHive is not null)
                    RevertUserHive(userHive);
            }
            catch
            {
            }
        }

        ApplyBrowserDnsPolicies(false);
    }

    private static void ApplyToUserHive(RegistryKey hive, PolicyConfig config)
    {
        SetOrDeleteDword(hive, WallpaperPolicyPath, "NoChangingWallPaper", config.LockWallpaper);
        SetOrDeleteDword(hive, ThemePolicyPath, "NoThemesTab", config.LockTheme);
    }

    private static void RevertUserHive(RegistryKey hive)
    {
        SetOrDeleteDword(hive, WallpaperPolicyPath, "NoChangingWallPaper", false);
        SetOrDeleteDword(hive, ThemePolicyPath, "NoThemesTab", false);
    }

    private static void SetOrDeleteDword(
        RegistryKey hive,
        string path,
        string valueName,
        bool enabled)
    {
        using var key = hive.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"Não foi possível abrir/criar {path}.");

        if (enabled)
            key.SetValue(valueName, 1, RegistryValueKind.DWord);
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static void ApplyBrowserDnsPolicies(bool disableDoH)
    {
        SetOrDeleteString(
            Registry.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Edge",
            "DnsOverHttpsMode",
            disableDoH ? "off" : null);

        SetOrDeleteString(
            Registry.LocalMachine,
            @"SOFTWARE\Policies\Google\Chrome",
            "DnsOverHttpsMode",
            disableDoH ? "off" : null);
    }

    private static void SetOrDeleteString(
        RegistryKey hive,
        string path,
        string valueName,
        string? value)
    {
        using var key = hive.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"Não foi possível abrir/criar {path}.");

        if (value is not null)
            key.SetValue(valueName, value, RegistryValueKind.String);
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static bool IsUserSid(string value) =>
        value.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("S-1-12-1-", StringComparison.OrdinalIgnoreCase);
}
