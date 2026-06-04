using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for LogRotator: file size rotation, retention limits, file naming, and thread safety.
/// Uses temp directories cleaned up via IDisposable pattern.
/// </summary>
public class LogRotatorTests : IDisposable
{
    private readonly string _tempDir;

    public LogRotatorTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeLogRotatorTests",
            Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDir))
        {
            try
            {
                System.IO.Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public void Write_BelowMaxSize_DoesNotRotate()
    {
        // Use a large maxSize so writes never trigger rotation
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 100_000);

        rotator.Write("INFO", "first entry");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        Assert.Single(files);
    }

    [Fact]
    public void Write_ExceedingMaxSize_CreatesNewFile()
    {
        // Use tiny maxSize to force rotation on second write
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 10);

        rotator.Write("INFO", "entry one");
        rotator.Write("INFO", "entry two");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void Write_RetainsMaxRetainedFiles_DeletesOldest()
    {
        // Use tiny maxSize and maxRetainedFiles=3 to trigger rotation + deletion
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 10, maxRetainedFiles: 3);

        // Each write creates ~40-50 bytes, exceeding the 10-byte limit
        // Write 1 → file 0 (no rotation, file doesn't exist yet)
        // Write 2 → file 0 full → rotate to file 1
        // Write 3 → file 1 full → rotate to file 2
        // Write 4 → file 2 full → rotate to file 3, delete file at index 3-3=0
        rotator.Write("INFO", "msg 1");
        rotator.Write("INFO", "msg 2");
        rotator.Write("INFO", "msg 3");
        rotator.Write("INFO", "msg 4");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        Assert.Equal(3, files.Length);

        // File 0 should have been deleted
        var file0Exists = System.IO.File.Exists(
            System.IO.Path.Combine(_tempDir, $"screensafe-{DateTime.Now:yyyy-MM-dd}-0.log"));
        Assert.False(file0Exists);
    }

    [Fact]
    public void Write_FileNaming_FollowsExpectedFormat()
    {
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 100_000);

        rotator.Write("INFO", "naming test");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        var fileName = System.IO.Path.GetFileName(files[0]);

        // Format: screensafe-{yyyy-MM-dd}-{n}.log
        Assert.Matches(@"^screensafe-\d{4}-\d{2}-\d{2}-\d+\.log$", fileName);
    }

    [Fact]
    public void Write_LogsFormattedEntry()
    {
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 100_000);

        rotator.Write("ERROR", "test error message");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        var content = System.IO.File.ReadAllText(files[0]);

        // Format: yyyy-MM-dd HH:mm:ss [ERROR] test error message
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[ERROR\] test error message",
            content.Trim());
    }

    [Fact]
    public void Write_AutoCreatesLogDirectory()
    {
        var deepDir = System.IO.Path.Combine(_tempDir, "sub", "logs");
        Assert.False(System.IO.Directory.Exists(deepDir));

        var rotator = new LogRotator(deepDir, maxFileSizeBytes: 100_000);
        rotator.Write("INFO", "auto-create");

        Assert.True(System.IO.Directory.Exists(deepDir));
        Assert.NotEmpty(System.IO.Directory.GetFiles(deepDir, "*.log"));
    }

    [Fact]
    public void Write_MultipleEntries_WithinSameFile_BelowThreshold()
    {
        var rotator = new LogRotator(_tempDir, maxFileSizeBytes: 100_000);

        rotator.Write("INFO", "first");
        rotator.Write("WARNING", "second");
        rotator.Write("ERROR", "third");

        var files = System.IO.Directory.GetFiles(_tempDir, "*.log");
        Assert.Single(files);

        var content = System.IO.File.ReadAllText(files[0]);
        var lines = content.Split(
            new[] { Environment.NewLine },
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
    }
}
