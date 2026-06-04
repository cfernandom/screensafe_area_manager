using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for WindowsStartupManager registry CRUD operations.
/// Uses the real HKCU registry on a well-known path and cleans up after each test.
/// All tests are skipped by default — run manually on Windows only.
/// </summary>
public class WindowsStartupManagerTests
{
    private const string TestRunKeyPath = @"Software\ScreenSafeTests\Windows\CurrentVersion\Run";

    /// <summary>
    /// Cleans up the test registry key after each test.
    /// </summary>
    private void CleanupTestKey()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\ScreenSafeTests", writable: true);
            if (key != null)
            {
                key.DeleteSubKeyTree(@"Windows\CurrentVersion\Run");
            }
        }
        catch
        {
            // Best-effort cleanup — key may not exist
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void Install_CreatesRunKey_WithExpectedCommand()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);
            manager.Install();

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
            Assert.NotNull(key);

            var value = key.GetValue("ScreenSafe") as string;
            Assert.NotNull(value);
            Assert.Contains("--daemon", value);
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void Install_IsIdempotent_OverwritesExisting()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);

            // Install twice
            manager.Install();
            manager.Install();

            // Verify only one value exists
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
            Assert.NotNull(key);
            var value = key.GetValue("ScreenSafe") as string;
            Assert.NotNull(value);
            Assert.Contains("--daemon", value);
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void Uninstall_RemovesRunKey()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);

            // Install first
            manager.Install();
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(TestRunKeyPath))
            {
                Assert.NotNull(key);
            }

            // Then uninstall
            manager.Uninstall();
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(TestRunKeyPath))
            {
                // Key may still exist (CreateSubKey creates it), but value should be gone
                var value = key?.GetValue("ScreenSafe");
                Assert.Null(value);
            }
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void Uninstall_DoesNotThrow_WhenNotInstalled()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);

            // Should not throw
            var exception = Record.Exception(() => manager.Uninstall());
            Assert.Null(exception);
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void IsInstalled_ReturnsTrue_WhenInstalled()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);
            Assert.False(manager.IsInstalled());

            manager.Install();
            Assert.True(manager.IsInstalled());
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void IsInstalled_ReturnsFalse_AfterUninstall()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);
            manager.Install();
            Assert.True(manager.IsInstalled());

            manager.Uninstall();
            Assert.False(manager.IsInstalled());
        }
        finally
        {
            CleanupTestKey();
        }
    }

    [Fact(Skip = "Requires Windows Registry — run on Windows only")]
    public void GetRegisteredCommand_ReturnsStoredCommand()
    {
        CleanupTestKey();

        try
        {
            var manager = new WindowsStartupManager(TestRunKeyPath);

            var before = manager.GetRegisteredCommand();
            Assert.Null(before);

            manager.Install();
            var command = manager.GetRegisteredCommand();
            Assert.NotNull(command);
            Assert.Contains("--daemon", command);
        }
        finally
        {
            CleanupTestKey();
        }
    }
}
