using System;

namespace ScreenSafe.Domain
{
    /// <summary>
    /// Provides information about the current operating system platform.
    /// Used to determine which strategy is supported at runtime.
    /// </summary>
    public interface IPlatformInfoProvider
    {
        /// <summary>
        /// Gets the operating system version.
        /// </summary>
        Version OSVersion { get; }

        /// <summary>
        /// Gets the processor architecture (e.g., "X64", "X86", "ARM64").
        /// </summary>
        string Architecture { get; }

        /// <summary>
        /// Determines whether the current platform supports the given strategy.
        /// </summary>
        /// <param name="strategy">Strategy name ("SpSetWorkArea" or "ShAppBarMessage").</param>
        /// <returns>True if the strategy is supported on this platform.</returns>
        bool CanSupportStrategy(string strategy);
    }
}
