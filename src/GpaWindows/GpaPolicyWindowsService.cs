using System.ServiceProcess;
using GpaWindows.Services;

namespace GpaWindows;

public sealed class GpaPolicyWindowsService : ServiceBase
{
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private Task? _appWorker;
    private DnsFilterService? _dnsFilter;
    private string? _dnsSignature;

    public GpaPolicyWindowsService()
    {
        ServiceName = WindowsServiceManager.ServiceName;
        CanStop = true;
        CanPauseAndContinue = false;
        AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => RunLoopAsync(_cts.Token));
        _appWorker = Task.Run(() => RunApplicationGuardLoopAsync(_cts.Token));
    }

    protected override void OnStop()
    {
        try
        {
            _cts?.Cancel();
            _dnsFilter?.Dispose();
            _dnsFilter = null;
            _worker?.Wait(TimeSpan.FromSeconds(5));
            _appWorker?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _worker = null;
            _appWorker = null;
        }
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var delaySeconds = 30;

            try
            {
                var config = ConfigStore.Load();
                delaySeconds = Math.Clamp(config.EnforcementIntervalSeconds, 10, 3600);

                if (config.Enabled)
                {
                    PolicyEngine.Validate(config);
                    EnsureDnsFilter(config);
                    PolicyEngine.Apply(config);
                }
                else
                {
                    StopDnsFilter();
                }
            }
            catch (Exception ex)
            {
                WriteServiceLog(ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task RunApplicationGuardLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var config = ConfigStore.Load();

                if (config.Enabled && config.ApplicationControlEnabled)
                    AppControlService.Enforce(config);
            }
            catch (Exception ex)
            {
                WriteServiceLog(ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void EnsureDnsFilter(GpaWindows.Models.PolicyConfig config)
    {
        if (!config.DnsAllowListEnabled)
        {
            StopDnsFilter();
            return;
        }

        var signature = string.Join(
            "|",
            config.UpstreamDns,
            config.AllowedDomains
                .Select(d => d.Trim().ToLowerInvariant())
                .OrderBy(d => d));

        if (_dnsFilter is not null && string.Equals(_dnsSignature, signature, StringComparison.Ordinal))
            return;

        StopDnsFilter();

        _dnsFilter = new DnsFilterService(config);
        _dnsFilter.Start();
        _dnsSignature = signature;
    }

    private void StopDnsFilter()
    {
        _dnsFilter?.Dispose();
        _dnsFilter = null;
        _dnsSignature = null;
    }

    private static void WriteServiceLog(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.BaseDirectory);
            var logPath = Path.Combine(ConfigStore.BaseDirectory, "service.log");
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {ex}\r\n");
        }
        catch
        {
        }
    }
}
