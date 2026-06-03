using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using ScreenSafe.Application;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Console;

/// <summary>
/// Application entry point and DI composition root.
/// </summary>
static class Program
{
    /// <summary>
    /// Entry point. Validates platform, builds DI container, and dispatches the command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>0 on success, 1 on error.</returns>
    static int Main(string[] args)
    {
        try
        {
            // Platform guard — only runs on Windows
            PlatformGuard.EnsureWindows();

            // Determine the settings file path (same directory as the executable)
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var settingsPath = Path.Combine(exeDir, "appsettings.json");

            // Build DI container
            var services = new ServiceCollection();

            // Infrastructure services
            services.AddSingleton<IScreenInfoProvider, ScreenInfoProvider>();
            services.AddSingleton<IPlatformInfoProvider, PlatformInfoProvider>();
            services.AddSingleton<ISettingsRepository>(
                _ => new JsonSettingsRepository(settingsPath));

            // Strategy: read config to determine which strategy to use
            services.AddSingleton<IWorkAreaManager>(sp =>
            {
                var settingsRepo = sp.GetRequiredService<ISettingsRepository>();
                var screenInfo = sp.GetRequiredService<IScreenInfoProvider>();
                var settings = settingsRepo.Load();

                // Use configured strategy or auto-detect
                if (string.Equals(settings.Strategy, "ShAppBarMessage", StringComparison.OrdinalIgnoreCase))
                {
                    return new ShAppBarMessageStrategy(screenInfo);
                }

                // Default: SpSetWorkArea (also works for "auto")
                return new SpSetWorkAreaStrategy(screenInfo);
            });

            // Application services
            services.AddTransient<ApplyUseCase>();
            services.AddTransient<RestoreUseCase>();
            services.AddTransient<StatusUseCase>();

            // Console services
            services.AddSingleton<CliDispatcher>();

            var serviceProvider = services.BuildServiceProvider();

            // Dispatch
            var dispatcher = serviceProvider.GetRequiredService<CliDispatcher>();
            return dispatcher.Execute(args);
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
}
