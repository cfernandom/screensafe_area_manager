namespace ScreenSafe.Domain;

/// <summary>
/// Repository abstraction for loading and persisting <see cref="AppSettings"/>.
/// Enables testability of use cases via mock dependency injection.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Loads application settings from persistent storage.
    /// </summary>
    /// <returns>Deserialized <see cref="AppSettings"/> or defaults if unavailable.</returns>
    AppSettings Load();

    /// <summary>
    /// Persists the specified settings to storage.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    void Save(AppSettings settings);
}
