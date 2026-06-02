using System;
using System.Runtime.InteropServices;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Provides information about the current operating system platform.
    /// Wraps Environment.OSVersion and RuntimeInformation for testability.
    /// </summary>
    public class PlatformInfoProvider : IPlatformInfoProvider
    {
        /// <summary>
        /// Gets the operating system version from Environment.OSVersion.
        /// </summary>
        public Version OSVersion => Environment.OSVersion.Version;

        /// <summary>
        /// Gets the processor architecture as a string (e.g., "X64", "X86", "ARM64").
        /// </summary>
        public string Architecture => RuntimeInformation.OSArchitecture.ToString();

        /// <summary>
        /// Determines whether the current platform supports the given strategy.
        /// On Windows, both strategies ("SpSetWorkArea" and "ShAppBarMessage") are supported.
        /// On non-Windows platforms, no strategy is supported.
        /// </summary>
        /// <param name="strategy">The strategy name to check.</param>
        /// <returns>True if the strategy is supported on the current platform.</returns>
        public bool CanSupportStrategy(string strategy)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
    }
}
