using System.ServiceProcess;

namespace GpaWindows.Services;

public static class WindowsServiceManager
{
    public const string ServiceName = "GPAWindowsPolicy";
    public const string DisplayName = "GPA Windows Policy Service";

    public static bool IsInstalled()
    {
        try
        {
            return ServiceController.GetServices()
                .Any(s => string.Equals(s.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static string GetStatus()
    {
        if (!IsInstalled())
            return "Não instalado";

        try
        {
            using var controller = new ServiceController(ServiceName);
            return controller.Status switch
            {
                ServiceControllerStatus.Running => "Em execução",
                ServiceControllerStatus.Stopped => "Parado",
                ServiceControllerStatus.Paused => "Pausado",
                ServiceControllerStatus.StartPending => "Iniciando",
                ServiceControllerStatus.StopPending => "Parando",
                _ => controller.Status.ToString()
            };
        }
        catch
        {
            return "Status indisponível";
        }
    }

    public static void EnsureInstalledAndRunning()
    {
        InstallOrUpdate();
        Start();
    }

    public static void InstallOrUpdate()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");

        ProcessResult result;

        if (IsInstalled())
        {
            result = ProcessRunner.Run(
                "sc.exe",
                "config",
                ServiceName,
                "binPath=",
                $"\"{exePath}\" --service",
                "start=",
                "auto");
        }
        else
        {
            result = ProcessRunner.Run(
                "sc.exe",
                "create",
                ServiceName,
                "binPath=",
                $"\"{exePath}\" --service",
                "start=",
                "auto",
                "DisplayName=",
                DisplayName);
        }

        if (!result.Success)
            throw new InvalidOperationException("Falha ao instalar/configurar serviço: " + result.StdErr.Trim());

        ProcessRunner.Run(
            "sc.exe",
            "description",
            ServiceName,
            "Fiscaliza e reaplica as diretivas locais configuradas pelo GPA Windows.");

        ProcessRunner.Run(
            "sc.exe",
            "failure",
            ServiceName,
            "reset=",
            "86400",
            "actions=",
            "restart/5000/restart/10000/restart/30000");
    }

    public static void Start()
    {
        using var controller = new ServiceController(ServiceName);
        controller.Refresh();

        if (controller.Status == ServiceControllerStatus.Running)
            return;

        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
    }

    public static void Stop()
    {
        if (!IsInstalled())
            return;

        using var controller = new ServiceController(ServiceName);
        controller.Refresh();

        if (controller.Status == ServiceControllerStatus.Stopped)
            return;

        if (controller.CanStop)
        {
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
        }
    }

    public static void Remove()
    {
        if (!IsInstalled())
            return;

        Stop();

        var result = ProcessRunner.Run("sc.exe", "delete", ServiceName);
        if (!result.Success)
            throw new InvalidOperationException("Falha ao remover serviço: " + result.StdErr.Trim());
    }
}
