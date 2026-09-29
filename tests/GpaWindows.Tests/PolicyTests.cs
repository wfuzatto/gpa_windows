using System;
using System.Linq;
using System.Text.Json;
using GpaWindows.Models;
using GpaWindows.Services;
using Xunit;

namespace GpaWindows.Tests;

public sealed class PolicyTests
{
    [Fact]
    public void Configuration_RoundTrips_WithApplicationChoices()
    {
        var config = new PolicyConfig
        {
            Enabled = true,
            ManagedApplications =
            [
                new ManagedApplication { DisplayName = "Teste", ExecutablePath = @"C:\Tools\teste.exe", Allowed = false }
            ]
        };

        var json = JsonSerializer.Serialize(config);
        var restored = JsonSerializer.Deserialize<PolicyConfig>(json);

        Assert.NotNull(restored);
        Assert.False(restored!.ManagedApplications.Single().Allowed);
        Assert.Equal(@"C:\Tools\teste.exe", restored.ManagedApplications.Single().ExecutablePath);
    }

    [Theory]
    [InlineData("Example.COM.", "example.com")]
    [InlineData(" api.example.com ", "api.example.com")]
    public void Domain_Normalization_IsStable(string input, string expected) =>
        Assert.Equal(expected, DnsFilterService.NormalizeDomainForPolicy(input));

    [Fact]
    public void Allowlist_Validation_RejectsEmptyAllowlist()
    {
        var config = new PolicyConfig { DnsAllowListEnabled = true, UpstreamDns = "1.1.1.1" };
        Assert.Throws<InvalidOperationException>(() => PolicyEngine.Validate(config));
    }

    [Fact]
    public void StrictMode_RequiresAnAllowedExecutable()
    {
        var config = new PolicyConfig { ApplicationControlEnabled = true, BlockUnknownApplications = true };
        Assert.Throws<InvalidOperationException>(() => PolicyEngine.Validate(config));
    }

    [Fact]
    public void Paths_AreNormalized_AndCriticalExecutablesAreProtected()
    {
        Assert.Equal(@"C:\Tools\app.exe", AppControlService.NormalizePathForPolicy(@"C:\Tools\..\Tools\app.exe"));
        Assert.True(AppControlService.IsProtectedExecutable(@"C:\Windows\explorer.exe"));
        Assert.True(AppControlService.IsProtectedExecutable(@"C:\Tools\powershell.exe"));
        Assert.False(AppControlService.IsProtectedExecutable(@"C:\Tools\sample.exe"));
    }
}
