namespace GpaWindows.Models;

public sealed class PolicyConfig
{
    public bool Enabled { get; set; } = true;
    public bool LockWallpaper { get; set; }
    public bool LockTheme { get; set; }

    public bool DnsAllowListEnabled { get; set; }
    public bool DisableBrowserDoH { get; set; } = true;
    public bool SanitizeHostsWhenDnsAllowListEnabled { get; set; } = true;
    public string UpstreamDns { get; set; } = "1.1.1.1";
    public List<string> AllowedDomains { get; set; } = [];

    public int EnforcementIntervalSeconds { get; set; } = 30;
    public DateTimeOffset? LastAppliedUtc { get; set; }

    // Backups usados para rollback das políticas de rede.
    public string? HostsBackupBase64 { get; set; }
    public List<AdapterDnsBackup> AdapterDnsBackup { get; set; } = [];
}

public sealed class AdapterDnsBackup
{
    public string InterfaceAlias { get; set; } = string.Empty;
    public List<string> ServerAddresses { get; set; } = [];
}
