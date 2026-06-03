using ScreenSafe.Application;
using ScreenSafe.Domain;

namespace ScreenSafe.Console;

/// <summary>
/// Parses command-line arguments and dispatches to the appropriate use case.
/// Supports apply, restore, status, install, uninstall, and health commands.
/// </summary>
public class CliDispatcher
{
    private readonly ApplyUseCase _applyUseCase;
    private readonly RestoreUseCase _restoreUseCase;
    private readonly StatusUseCase _statusUseCase;
    private readonly HealthUseCase _healthUseCase;
    private readonly IWindowsStartupManager _startupManager;

    public CliDispatcher(
        ApplyUseCase applyUseCase,
        RestoreUseCase restoreUseCase,
        StatusUseCase statusUseCase,
        HealthUseCase healthUseCase,
        IWindowsStartupManager startupManager)
    {
        _applyUseCase = applyUseCase ?? throw new ArgumentNullException(nameof(applyUseCase));
        _restoreUseCase = restoreUseCase ?? throw new ArgumentNullException(nameof(restoreUseCase));
        _statusUseCase = statusUseCase ?? throw new ArgumentNullException(nameof(statusUseCase));
        _healthUseCase = healthUseCase ?? throw new ArgumentNullException(nameof(healthUseCase));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
    }

    /// <summary>
    /// Executes the command specified by the given arguments.
    /// </summary>
    /// <param name="args">Command-line arguments. First argument is the command name.</param>
    /// <returns>0 on success, 1 on error or unknown command.</returns>
    public int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "apply":
                return _applyUseCase.Execute();

            case "restore":
                return _restoreUseCase.Execute();

            case "status":
                return _statusUseCase.Execute();

            case "install":
                return ExecuteInstall();

            case "uninstall":
                return ExecuteUninstall();

            case "health":
                return ExecuteHealth();

            default:
                System.Console.Error.WriteLine($"Unknown command: {args[0]}");
                PrintUsage();
                return 1;
        }
    }

    private int ExecuteInstall()
    {
        try
        {
            _startupManager.Install();
            System.Console.WriteLine("Auto-start enabled");
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Failed to enable auto-start: {ex.Message}");
            return 1;
        }
    }

    private int ExecuteUninstall()
    {
        try
        {
            _startupManager.Uninstall();
            System.Console.WriteLine("Auto-start disabled");
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Failed to disable auto-start: {ex.Message}");
            return 1;
        }
    }

    private int ExecuteHealth()
    {
        try
        {
            var report = _healthUseCase.Execute();

            System.Console.WriteLine("ScreenSafe Health");
            System.Console.WriteLine();
            System.Console.WriteLine($"Current Resolution: {report.ScreenWidth}x{report.ScreenHeight}");
            System.Console.WriteLine($"Desired WorkArea:   {report.DesiredWorkArea.left},{report.DesiredWorkArea.top},{report.DesiredWorkArea.right},{report.DesiredWorkArea.bottom}");
            System.Console.WriteLine($"Current WorkArea:   {report.CurrentWorkArea?.left},{report.CurrentWorkArea?.top},{report.CurrentWorkArea?.right},{report.CurrentWorkArea?.bottom}");
            System.Console.WriteLine($"Strategy:           {report.Strategy}");
            System.Console.WriteLine($"Daemon:             {(report.DaemonRunning ? "Running" : "Stopped")}");
            System.Console.WriteLine($"AutoStart:          {(report.AutoStartEnabled ? "Enabled" : "Disabled")}");
            System.Console.WriteLine($"Last Reapply:       {report.LastReapply}");
            System.Console.WriteLine($"Status:             {report.Status}");

            Environment.ExitCode = report.ExitCode;
            return report.ExitCode;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Health check failed: {ex.Message}");
            Environment.ExitCode = 2;
            return 2;
        }
    }

    private static void PrintUsage()
    {
        System.Console.WriteLine("Usage: ScreenSafe.Console.exe <command>");
        System.Console.WriteLine("Commands: apply, restore, status, install, uninstall, health");
    }
}
