using System.ServiceProcess;
using GpaWindows.Services;

namespace GpaWindows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.ThreadException += (_, eventArgs) => HandleFatal(eventArgs.Exception, "Application.ThreadException");
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                HandleFatal(exception, "AppDomain.CurrentDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLogger.WriteException("startup.log", eventArgs.Exception, "TaskScheduler.UnobservedTaskException");
            eventArgs.SetObserved();
        };

        if (args.Any(a => string.Equals(a, "--service", StringComparison.OrdinalIgnoreCase)))
        {
            ServiceBase.Run(new GpaPolicyWindowsService());
            return;
        }

        if (args.Any(a => string.Equals(a, "--diagnostic", StringComparison.OrdinalIgnoreCase)))
        {
            DiagnosticRunner.Run();
            return;
        }

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            HandleFatal(exception, "Program.Main startup");
        }
    }

    private static void HandleFatal(Exception exception, string context)
    {
        AppLogger.WriteException("startup.log", exception, context);
        try
        {
            MessageBox.Show(
                $"O GPA Windows não conseguiu iniciar.\n\nDetalhes gravados em:\n{AppLogger.StartupLogPath}\n\n{exception.Message}",
                "GPA Windows",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
