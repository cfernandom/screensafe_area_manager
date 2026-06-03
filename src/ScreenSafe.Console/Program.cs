using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ScreenSafe.Application;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;
using ScreenSafe.Infrastructure.Logging;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Console;

/// <summary>
/// Application entry point and DI composition root.
/// Supports dual mode: --daemon (resident background monitor) or CLI commands.
/// </summary>
static class Program
{
    /// <summary>
    /// Handle to the daemon singleton mutex, held for the lifetime of the daemon process.
    /// </summary>
    private static IntPtr _daemonMutex = IntPtr.Zero;

    /// <summary>
    /// Named mutex that identifies a running daemon instance.
    /// </summary>
    private const string DaemonMutexName = "Global\\ScreenSafeDaemon";

    /// <summary>
    /// Determines whether the application should run in daemon mode based on CLI arguments.
    /// Extracted for testability.
    /// </summary>
    internal static bool IsDaemonMode(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "--daemon", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Entry point. Routes to daemon mode or CLI mode based on the first argument.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>0 on success, 1 on error.</returns>
    static int Main(string[] args)
    {
        try
        {
            // Platform guard — only runs on Windows
            PlatformGuard.EnsureWindows();

            if (IsDaemonMode(args))
            {
                RunDaemon();
                return 0;
            }
            else
            {
                return RunCli(args);
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            System.Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Runs the resident daemon: creates a singleton mutex, hides the console,
    /// starts the work area watcher, debouncer, and auto-apply service.
    /// Blocks until Ctrl+C or WM_CLOSE is received.
    /// </summary>
    private static void RunDaemon()
    {
        // 1. Create named mutex — if already exists, another instance is running
        _daemonMutex = User32.CreateMutexW(IntPtr.Zero, true, DaemonMutexName);
        int error = Marshal.GetLastWin32Error();
        if (_daemonMutex == IntPtr.Zero || error == 183) // ERROR_ALREADY_EXISTS
        {
            if (_daemonMutex != IntPtr.Zero)
            {
                User32.CloseHandle(_daemonMutex);
                _daemonMutex = IntPtr.Zero;
            }
            System.Console.Error.WriteLine("ScreenSafe daemon is already running.");
            Environment.Exit(1);
            return;
        }

        // 2. Hide the console window (keep it attached for Ctrl+C support)
        var consoleHwnd = User32.GetConsoleWindow();
        if (consoleHwnd != IntPtr.Zero)
        {
            User32.ShowWindow(consoleHwnd, User32.SW_HIDE);
        }

        // 3. Build service provider via shared configuration
        var serviceProvider = ConfigureServices(true);

        // 4. Resolve core services
        var logger = serviceProvider.GetRequiredService<ILogger>();
        var watcher = serviceProvider.GetRequiredService<IWorkAreaWatcher>();
        var autoApplyService = serviceProvider.GetRequiredService<AutoApplyService>();
        var logRotator = serviceProvider.GetRequiredService<LogRotator>();
        var settingsRepo = serviceProvider.GetRequiredService<ISettingsRepository>();
        var workAreaManager = serviceProvider.GetRequiredService<IWorkAreaManager>();

        // 5. Load settings and apply desired work area (no-op if already correct)
        try
        {
            var settings = settingsRepo.Load();
            if (settings.Enabled)
            {
                workAreaManager.Apply(settings.ReservedBottomPixels);
                logger.Info($"Initial work area applied ({settings.ReservedBottomPixels}px reserved).");
            }
            else
            {
                logger.Info("Daemon started but work area reservation is disabled.");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to apply initial work area: {ex.Message}");
        }

        // 6. Start auto-apply service (which starts watcher + debouncer)
        autoApplyService.Start();
        logger.Info("ScreenSafe daemon started successfully.");

        // 7. Setup Ctrl+C / Ctrl+Break for clean shutdown
        var shutdownEvent = new ManualResetEvent(false);
        System.Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.Info("Shutdown signal received (Ctrl+C). Stopping daemon...");
            shutdownEvent.Set();
        };

        // 8. Block until shutdown signal
        shutdownEvent.WaitOne();

        // 9. Clean shutdown
        logger.Info("Shutting down daemon...");
        autoApplyService.Stop();
        watcher.Stop();

        if (_daemonMutex != IntPtr.Zero)
        {
            User32.CloseHandle(_daemonMutex);
            _daemonMutex = IntPtr.Zero;
        }

        // Dispose the event AFTER cleanup is complete.
        // Must NOT be `using` — ProcessExit fires during AppDomain unload
        // and would crash with ObjectDisposedException on the closed handle.
        shutdownEvent.Dispose();

        logger.Info("Daemon stopped.");
    }

    /// <summary>
    /// Runs CLI mode: builds DI container, resolves dispatcher, and executes the command.
    /// </summary>
    /// <param name="args">Command-line arguments (first is the command name).</param>
    /// <returns>0 on success, 1 on error.</returns>
    private static int RunCli(string[] args)
    {
        var serviceProvider = ConfigureServices(false);
        var dispatcher = serviceProvider.GetRequiredService<CliDispatcher>();
        return dispatcher.Execute(args);
    }

    /// <summary>
    /// Configures the shared DI service collection for both daemon and CLI modes.
    /// </summary>
    /// <param name="daemonMode">True if configuring for daemon mode; false for CLI mode.</param>
    /// <returns>Configured IServiceProvider.</returns>
    private static IServiceProvider ConfigureServices(bool daemonMode)
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var settingsPath = Path.Combine(exeDir, "appsettings.json");
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenSafe",
            "Logs");

        var services = new ServiceCollection();

        // Infrastructure services
        services.AddSingleton<IScreenInfoProvider, ScreenInfoProvider>();
        services.AddSingleton<IPlatformInfoProvider, PlatformInfoProvider>();
        services.AddSingleton<ISettingsRepository>(
            _ => new JsonSettingsRepository(settingsPath));
        services.AddSingleton<IWorkAreaWatcher>(sp => new WorkAreaWatcher(sp.GetRequiredService<ILogger>()));
        services.AddSingleton<IEventDebouncer>(sp =>
        {
            var settingsRepo = sp.GetRequiredService<ISettingsRepository>();
            var settings = settingsRepo.Load();
            return new EventDebouncer(settings.EventDebounceMs);
        });
        services.AddSingleton<IWindowsStartupManager, WindowsStartupManager>();
        services.AddSingleton<IDaemonStatusProvider, DaemonStatusProvider>();
        services.AddSingleton<LogRotator>(sp =>
        {
            var settingsRepo = sp.GetRequiredService<ISettingsRepository>();
            var settings = settingsRepo.Load();
            var dir = string.IsNullOrEmpty(settings.LogPath)
                ? logDirectory
                : settings.LogPath;
            return new LogRotator(dir);
        });

        // Logger: FileLogger in daemon mode, ConsoleLogger in CLI mode
        if (daemonMode)
        {
            var logFilePath = Path.Combine(logDirectory, "screensafe-daemon.log");
            services.AddSingleton<ILogger>(_ => new FileLogger(logFilePath));
        }
        else
        {
            services.AddSingleton<ILogger, ConsoleLogger>();
        }

        // Strategy: read config to determine which strategy to use
        services.AddSingleton<IWorkAreaManager>(sp =>
        {
            var settingsRepo = sp.GetRequiredService<ISettingsRepository>();
            var screenInfo = sp.GetRequiredService<IScreenInfoProvider>();
            var settings = settingsRepo.Load();

            if (string.Equals(settings.Strategy, "ShAppBarMessage", StringComparison.OrdinalIgnoreCase))
            {
                return new ShAppBarMessageStrategy(screenInfo);
            }

            return new SpSetWorkAreaStrategy(screenInfo);
        });

        // Application services
        services.AddTransient<ApplyUseCase>();
        services.AddTransient<RestoreUseCase>();
        services.AddTransient<StatusUseCase>();
        services.AddTransient<HealthUseCase>();
        services.AddSingleton<AutoApplyService>();

        // Console services
        services.AddSingleton<CliDispatcher>();

        return services.BuildServiceProvider();
    }
}
