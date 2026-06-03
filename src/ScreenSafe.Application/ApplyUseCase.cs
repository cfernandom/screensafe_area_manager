using ScreenSafe.Domain;

namespace ScreenSafe.Application;

/// <summary>
/// Use case for reserving bottom pixels on the screen.
/// Stores the original work area before modification and saves settings.
/// </summary>
public class ApplyUseCase
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IWorkAreaManager _workAreaManager;
    private readonly IScreenInfoProvider _screenInfo;

    public ApplyUseCase(ISettingsRepository settingsRepo, IWorkAreaManager workAreaManager, IScreenInfoProvider screenInfo)
    {
        _settingsRepo = settingsRepo;
        _workAreaManager = workAreaManager;
        _screenInfo = screenInfo;
    }

    /// <summary>
    /// Executes the apply workflow: stores the original work area, then reserves the bottom pixels.
    /// </summary>
    /// <returns>0 on success, non-zero on failure.</returns>
    public int Execute()
    {
        var settings = _settingsRepo.Load();
        if (!settings.Enabled) return 1;

        var status = _workAreaManager.GetStatus();
        if (status == null) return 1;

        // Store the current full-screen work area before modifying
        settings.OriginalWorkArea = new ScreenRect(
            status.Value.left,
            status.Value.top,
            status.Value.right,
            status.Value.bottom
        );

        var result = _workAreaManager.Apply(settings.ReservedBottomPixels);
        if (!result) return 1;

        _settingsRepo.Save(settings);
        return 0;
    }
}
