using System;
using System.Reflection;
using Microsoft.Win32;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Manages Windows auto-start registration via the HKCU\...\Run registry key.
    /// Defaults to HKCU\Software\Microsoft\Windows\CurrentVersion\Run with key name "ScreenSafe".
    /// Accepts a custom registry path for testability.
    /// </summary>
    public class WindowsStartupManager : IWindowsStartupManager
    {
        /// <summary>
        /// The well-known Windows Run key path under HKEY_CURRENT_USER.
        /// </summary>
        public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// The registry value name used for the ScreenSafe entry.
        /// </summary>
        public const string KeyName = "ScreenSafe";

        private readonly string _registryPath;

        /// <summary>
        /// Creates a WindowsStartupManager that operates on the default Run key path.
        /// </summary>
        public WindowsStartupManager()
            : this(DefaultRunKeyPath)
        {
        }

        /// <summary>
        /// Creates a WindowsStartupManager that operates on the specified registry path.
        /// Primarily intended for testing with a custom registry location.
        /// </summary>
        /// <param name="registryPath">
        /// Path under HKEY_CURRENT_USER for the Run key.
        /// For tests, use a test-specific path and clean up afterwards.
        /// </param>
        public WindowsStartupManager(string registryPath)
        {
            _registryPath = registryPath ?? throw new ArgumentNullException(nameof(registryPath));
        }

        /// <summary>
        /// Installs the ScreenSafe daemon for automatic startup with Windows.
        /// Writes a Run key entry pointing to the current executable with the --daemon flag.
        /// Idempotent: overwrites any existing entry.
        /// </summary>
        public void Install()
        {
            var command = GetDefaultCommand();
            using var key = Registry.CurrentUser.CreateSubKey(_registryPath);
            key.SetValue(KeyName, command);
        }

        /// <summary>
        /// Removes the ScreenSafe daemon from Windows automatic startup.
        /// Does not throw if the entry does not exist.
        /// </summary>
        public void Uninstall()
        {
            using var key = Registry.CurrentUser.CreateSubKey(_registryPath);
            key.DeleteValue(KeyName, throwOnMissingValue: false);
        }

        /// <summary>
        /// Checks whether ScreenSafe is registered for automatic startup.
        /// Returns true only if the value exists and matches the expected command.
        /// </summary>
        public bool IsInstalled()
        {
            var expected = GetDefaultCommand();
            if (expected == null)
                return false;

            using var key = Registry.CurrentUser.OpenSubKey(_registryPath);
            if (key == null)
                return false;

            var value = key.GetValue(KeyName) as string;
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the registered command string, or null if not installed.
        /// </summary>
        public string? GetRegisteredCommand()
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryPath);
            if (key == null)
                return null;

            return key.GetValue(KeyName) as string;
        }

        /// <summary>
        /// Builds the default command string: the current executable's full path with --daemon flag.
        /// </summary>
        private static string? GetDefaultCommand()
        {
            var exePath = Assembly.GetEntryAssembly()?.Location;
            return exePath != null ? $"{exePath} --daemon" : null;
        }
    }
}
