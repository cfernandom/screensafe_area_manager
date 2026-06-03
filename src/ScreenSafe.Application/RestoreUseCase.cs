using ScreenSafe.Domain;

namespace ScreenSafe.Application;

/// <summary>
/// Use case for restoring the original full-screen work area.
/// Clears the stored original after successful restoration.
/// </summary>
public class RestoreUseCase
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IWorkAreaManager _workAreaManager;

    public RestoreUseCase(ISettingsRepository settingsRepo, IWorkAreaManager workAreaManager)
    {
        _settingsRepo = settingsRepo;
        _workAreaManager = workAreaManager;
    }

    /// <summary>
    /// Executes the restore workflow: restores the original work area and clears the stored value.
    /// </summary>
    /// <returns>0 on success, non-zero on failure.</returns>
    public int Execute()
    {
        var settings = _settingsRepo.Load();
        if (settings.OriginalWorkArea == null) return 1;

        var result = _workAreaManager.Restore(settings.OriginalWorkArea!.Value);
        if (!result) return 1;

        settings.OriginalWorkArea = null;
        _settingsRepo.Save(settings);
        return 0;
    }
}
