using System.Runtime.InteropServices;

namespace ScreenSafe.Infrastructure;

/// <summary>
/// Static guard that ensures the application only runs on Windows.
/// </summary>
public static class PlatformGuard
{
    /// <summary>
    /// Ensures the current OS is Windows.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current OS is not Windows.
    /// </exception>
    public static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException(
                "ScreenSafe Area Manager requires Windows 8.1 or later.");
    }
}
