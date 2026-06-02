using ScreenSafe.Domain;
using System.IO;
using System.Text.Json;

namespace ScreenSafe.Infrastructure;

/// <summary>
/// Repository for reading and writing <see cref="AppSettings"/> to a JSON file.
/// Provides graceful fallback on missing or corrupt files.
/// </summary>
public class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ScreenRectJsonConverter() }
    };

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance bound to the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the JSON settings file.</param>
    public JsonSettingsRepository(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Loads settings from the JSON file.
    /// </summary>
    /// <returns>
    /// Deserialized <see cref="AppSettings"/> if the file exists and is valid;
    /// otherwise, a new <see cref="AppSettings"/> with default values.
    /// </returns>
    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves the specified settings to the JSON file.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
