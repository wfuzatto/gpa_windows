using System.Text.Json;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class AudioPolicyService
{
    public static void Apply(PolicyConfig config)
    {
        if (!config.BlockAudio)
        {
            if (config.AudioDeviceBackup.Count > 0)
                Restore(config);

            return;
        }

        var enabledIds = ReadEnabledAudioEndpointIds();

        foreach (var id in enabledIds)
        {
            if (!config.AudioDeviceBackup.Contains(id, StringComparer.OrdinalIgnoreCase))
                config.AudioDeviceBackup.Add(id);
        }

        if (enabledIds.Count == 0)
            return;

        var command =
            "Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue | " +
            "Where-Object { $_.Status -eq 'OK' } | " +
            "Disable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue";

        ProcessRunner.PowerShell(command);
    }

    public static void Restore(PolicyConfig config)
    {
        foreach (var instanceId in config.AudioDeviceBackup.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var escaped = EscapePowerShellSingleQuoted(instanceId);
            ProcessRunner.PowerShell(
                $"Get-PnpDevice -InstanceId '{escaped}' -ErrorAction SilentlyContinue | " +
                "Enable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue");
        }

        config.AudioDeviceBackup.Clear();
    }

    private static List<string> ReadEnabledAudioEndpointIds()
    {
        var result = ProcessRunner.PowerShell(
            "Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue | " +
            "Where-Object { $_.Status -eq 'OK' } | " +
            "Select-Object -ExpandProperty InstanceId | ConvertTo-Json -Compress");

        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
            return [];

        try
        {
            using var document = JsonDocument.Parse(result.StdOut);
            var values = new List<string>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value);
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                var value = document.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }

            return values;
        }
        catch
        {
            return [];
        }
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
