using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;
using System.Text.Json;
using System.IO;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="JsonSettingsRepository"/> — file-based settings persistence.
/// All tests use temporary files that are cleaned up after execution.
/// </summary>
public class JsonSettingsRepositoryTests : System.IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    // ── Load — Existing file ─────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileExistsWithValidSettings_ReturnsDeserializedSettings()
    {
        var settings = new AppSettings
        {
            Enabled = false,
            ReservedBottomPixels = 50,
            Strategy = "SpSetWorkArea"
        };
        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(_tempFile, json);
        var repo = new JsonSettingsRepository(_tempFile);

        var result = repo.Load();

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Equal(50, result.ReservedBottomPixels);
        Assert.Equal("SpSetWorkArea", result.Strategy);
    }

    // ── Load — Missing file ──────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        var repo = new JsonSettingsRepository(missingPath);

        var result = repo.Load();

        Assert.NotNull(result);
        Assert.True(result.Enabled);            // default
        Assert.Equal(80, result.ReservedBottomPixels); // default
        Assert.Equal("auto", result.Strategy);  // default
        Assert.Null(result.OriginalWorkArea);   // default
    }

    // ── Load — Corrupt JSON ──────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileContainsCorruptJson_ReturnsDefaultSettings()
    {
        File.WriteAllText(_tempFile, "this is not valid json {{{");
        var repo = new JsonSettingsRepository(_tempFile);

        var result = repo.Load();

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.Equal(80, result.ReservedBottomPixels);
        Assert.Equal("auto", result.Strategy);
        Assert.Null(result.OriginalWorkArea);
    }

    // ── Save — OriginalWorkArea RECT ─────────────────────────────────────

    [Fact]
    public void Save_WithOriginalWorkArea_RoundTripsRectValues()
    {
        var settings = new AppSettings
        {
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080),
            Enabled = true,
            ReservedBottomPixels = 100,
            Strategy = "SpSetWorkArea"
        };
        var repo = new JsonSettingsRepository(_tempFile);
        repo.Save(settings);

        var result = repo.Load();

        Assert.NotNull(result);
        Assert.NotNull(result.OriginalWorkArea);
        var rect = result.OriginalWorkArea.Value;
        Assert.Equal(0, rect.Left);
        Assert.Equal(0, rect.Top);
        Assert.Equal(1920, rect.Right);
        Assert.Equal(1080, rect.Bottom);
        Assert.Equal(100, result.ReservedBottomPixels);
        Assert.Equal("SpSetWorkArea", result.Strategy);
    }

    // ── Round-trip all properties ────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_RoundTripsAllProperties()
    {
        var originalRect = new ScreenRect(100, 50, 1900, 1050);
        var settings = new AppSettings
        {
            Enabled = false,
            ReservedBottomPixels = 120,
            Strategy = "ShAppBarMessage",
            OriginalWorkArea = originalRect
        };
        var repo = new JsonSettingsRepository(_tempFile);
        repo.Save(settings);

        var result = repo.Load();

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Equal(120, result.ReservedBottomPixels);
        Assert.Equal("ShAppBarMessage", result.Strategy);
        Assert.NotNull(result.OriginalWorkArea);
        var rect = result.OriginalWorkArea.Value;
        Assert.Equal(100, rect.Left);
        Assert.Equal(50, rect.Top);
        Assert.Equal(1900, rect.Right);
        Assert.Equal(1050, rect.Bottom);
    }
}
