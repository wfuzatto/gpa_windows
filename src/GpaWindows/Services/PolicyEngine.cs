using GpaWindows.Models;

namespace GpaWindows.Services;

public static class PolicyEngine
{
    public static void Apply(PolicyConfig config)
    {
        if (!config.Enabled)
            return;

        Validate(config);

        RegistryPolicyService.Apply(config);

        if (config.DnsAllowListEnabled)
        {
            NetworkPolicyService.ApplyStrictDns(config);
        }
        else if (config.HostsBackupBase64 is not null || config.AdapterDnsBackup.Count > 0)
        {
            NetworkPolicyService.RestoreNetwork(config);
        }

        config.LastAppliedUtc = DateTimeOffset.UtcNow;
        ConfigStore.Save(config);
    }

    public static void RevertAll(PolicyConfig config)
    {
        RegistryPolicyService.Revert();
        NetworkPolicyService.RestoreNetwork(config);

        config.Enabled = false;
        config.LockWallpaper = false;
        config.LockTheme = false;
        config.DnsAllowListEnabled = false;
        config.LastAppliedUtc = DateTimeOffset.UtcNow;

        ConfigStore.Save(config);
    }

    public static void Validate(PolicyConfig config)
    {
        if (!config.DnsAllowListEnabled)
            return;

        if (config.AllowedDomains.Count == 0)
            throw new InvalidOperationException(
                "Adicione pelo menos um domínio permitido antes de ativar o DNS Allowlist.");

        if (!System.Net.IPAddress.TryParse(config.UpstreamDns, out _))
            throw new InvalidOperationException("Informe um endereço IP válido para o DNS upstream.");
    }
}
