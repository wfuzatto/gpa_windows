using System.Text;
using System.Text.Json;
using GpaWindows.Models;

namespace GpaWindows.Services;

public static class NetworkPolicyService
{
    private static string HostsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

    public static void EnsureBackups(PolicyConfig config)
    {
        if (config.HostsBackupBase64 is null && File.Exists(HostsPath))
            config.HostsBackupBase64 = Convert.ToBase64String(File.ReadAllBytes(HostsPath));

        if (config.AdapterDnsBackup.Count == 0)
            config.AdapterDnsBackup = ReadDnsBackup();
    }

    public static void ApplyStrictDns(PolicyConfig config)
    {
        if (!DnsFilterService.IsPortAvailable())
            throw new InvalidOperationException(
                "A porta UDP/53 já está ocupada. O DNS Allowlist não foi aplicado e a configuração de rede não foi alterada.");

        EnsureBackups(config);

        if (config.SanitizeHostsWhenDnsAllowListEnabled)
            WriteManagedHosts();

        var command =
            "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | " +
            "ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses @('127.0.0.1') }";

        var result = ProcessRunner.PowerShell(command);
        if (!result.Success)
            throw new InvalidOperationException("Falha ao configurar DNS local: " + result.StdErr.Trim());

        FlushDns();
    }

    public static void RestoreNetwork(PolicyConfig config)
    {
        RestoreHosts(config);
        RestoreDns(config);
        FlushDns();
    }

    public static void RestoreHosts(PolicyConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.HostsBackupBase64))
            return;

        var bytes = Convert.FromBase64String(config.HostsBackupBase64);
        File.WriteAllBytes(HostsPath, bytes);
        config.HostsBackupBase64 = null;
    }

    public static void RestoreDns(PolicyConfig config)
    {
        foreach (var adapter in config.AdapterDnsBackup)
        {
            var alias = EscapePowerShellSingleQuoted(adapter.InterfaceAlias);

            string command;
            if (adapter.ServerAddresses.Count == 0)
            {
                command =
                    $"Set-DnsClientServerAddress -InterfaceAlias '{alias}' -ResetServerAddresses -ErrorAction SilentlyContinue";
            }
            else
            {
                var addresses = string.Join(
                    ",",
                    adapter.ServerAddresses.Select(a => $"'{EscapePowerShellSingleQuoted(a)}'"));

                command =
                    $"Set-DnsClientServerAddress -InterfaceAlias '{alias}' -ServerAddresses @({addresses}) -ErrorAction SilentlyContinue";
            }

            ProcessRunner.PowerShell(command);
        }

        config.AdapterDnsBackup.Clear();
    }

    public static void FlushDns()
    {
        ProcessRunner.Run("ipconfig.exe", "/flushdns");
    }

    private static List<AdapterDnsBackup> ReadDnsBackup()
    {
        var command =
            "Get-DnsClientServerAddress -AddressFamily IPv4 | " +
            "Where-Object { $_.InterfaceAlias -notmatch 'Loopback' } | " +
            "Select-Object InterfaceAlias,ServerAddresses | ConvertTo-Json -Compress";

        var result = ProcessRunner.PowerShell(command);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            var list = new List<AdapterDnsBackup>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                    AddDnsBackup(item, list);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddDnsBackup(doc.RootElement, list);
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static void AddDnsBackup(JsonElement item, List<AdapterDnsBackup> list)
    {
        if (!item.TryGetProperty("InterfaceAlias", out var aliasProperty))
            return;

        var alias = aliasProperty.GetString();
        if (string.IsNullOrWhiteSpace(alias))
            return;

        var addresses = new List<string>();

        if (item.TryGetProperty("ServerAddresses", out var servers))
        {
            if (servers.ValueKind == JsonValueKind.Array)
            {
                foreach (var server in servers.EnumerateArray())
                {
                    var value = server.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        addresses.Add(value);
                }
            }
            else if (servers.ValueKind == JsonValueKind.String)
            {
                var value = servers.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    addresses.Add(value);
            }
        }

        list.Add(new AdapterDnsBackup
        {
            InterfaceAlias = alias,
            ServerAddresses = addresses
        });
    }

    private static void WriteManagedHosts()
    {
        var content = new StringBuilder()
            .AppendLine("# GPA Windows - arquivo hosts gerenciado")
            .AppendLine("# O modo DNS Allowlist usa o resolvedor local 127.0.0.1:53.")
            .AppendLine("127.0.0.1 localhost")
            .AppendLine("::1 localhost")
            .ToString();

        File.WriteAllText(HostsPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
