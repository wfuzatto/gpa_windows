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
        AudioPolicyService.Apply(config);
        AppControlService.Enforce(config);

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
        AudioPolicyService.Restore(config);

        config.Enabled = false;
        config.LockWallpaper = false;
        config.LockTheme = false;
        config.BlockAudio = false;
        config.ApplicationControlEnabled = false;
        config.BlockUnknownApplications = false;
        config.DnsAllowListEnabled = false;
        config.LastAppliedUtc = DateTimeOffset.UtcNow;

        ConfigStore.Save(config);
    }

    public static void Validate(PolicyConfig config)
    {
        if (config.DnsAllowListEnabled)
        {
            if (config.AllowedDomains.Count == 0)
            {
                throw new InvalidOperationException(
                    "Adicione pelo menos um domínio permitido antes de ativar o DNS Allowlist.");
            }

            if (!System.Net.IPAddress.TryParse(config.UpstreamDns, out _))
                throw new InvalidOperationException("Informe um endereço IP válido para o DNS upstream.");
        }

        if (config.ApplicationControlEnabled &&
            config.BlockUnknownApplications &&
            !config.ManagedApplications.Any(x =>
                x.Allowed && !string.IsNullOrWhiteSpace(x.ExecutablePath)))
        {
            throw new InvalidOperationException(
                "O modo estrito de aplicativos precisa de pelo menos um executável permitido.");
        }
    }
}
