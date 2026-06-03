using ScreenSafe.Domain;

namespace ScreenSafe.Application;

/// <summary>
/// Use case for displaying the current screen work area status to the console.
/// </summary>
public class StatusUseCase
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IWorkAreaManager _workAreaManager;
    private readonly IScreenInfoProvider _screenInfo;

    public StatusUseCase(ISettingsRepository settingsRepo, IWorkAreaManager workAreaManager, IScreenInfoProvider screenInfo)
    {
        _settingsRepo = settingsRepo;
        _workAreaManager = workAreaManager;
        _screenInfo = screenInfo;
    }

    /// <summary>
    /// Executes the status display workflow.
    /// </summary>
    /// <returns>0 always (display is informational).</returns>
    public int Execute()
    {
        var settings = _settingsRepo.Load();
        var status = _workAreaManager.GetStatus();

        Console.WriteLine($"ScreenSafe Area Manager — Status");
        Console.WriteLine($"  Enabled: {settings.Enabled}");
        Console.WriteLine($"  Reserved bottom pixels: {settings.ReservedBottomPixels}");
        Console.WriteLine($"  Strategy: {settings.Strategy}");
        Console.WriteLine($"  Screen: {_screenInfo.GetScreenWidth()}x{_screenInfo.GetScreenHeight()}");

        if (status.HasValue)
        {
            Console.WriteLine($"  Current work area: ({status.Value.left}, {status.Value.top}) → ({status.Value.right}, {status.Value.bottom})");
        }
        else
        {
            Console.WriteLine($"  Current work area: (unknown)");
        }

        if (settings.OriginalWorkArea.HasValue)
        {
            var owa = settings.OriginalWorkArea.Value;
            Console.WriteLine($"  Original work area: ({owa.Left}, {owa.Top}) → ({owa.Right}, {owa.Bottom})");
        }
        else
        {
            Console.WriteLine($"  Original work area: (not set)");
        }

        return 0;
    }
}
