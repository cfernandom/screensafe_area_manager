# Auto-Start Specification

## Purpose

Register and unregister the ScreenSafe daemon for automatic startup with Windows via the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry key.

## Requirements

### Requirement: Install Run Key

The `install` command MUST write a Run key to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with value `"C:\path\to\ScreenSafe.Console.exe --daemon"`. The command MUST be idempotent — it overwrites any existing key.

#### Scenario: Install creates Run key

- GIVEN no existing ScreenSafe Run key
- WHEN `install` is executed
- THEN a Run key pointing to the executable with `--daemon` flag is created

#### Scenario: Install overwrites existing key

- GIVEN an existing Run key with an old executable path
- WHEN `install` is executed
- THEN the existing key is overwritten with the current path

### Requirement: Uninstall Run Key

The `uninstall` command MUST remove the ScreenSafe Run key. It MUST NOT fail if the key does not exist.

#### Scenario: Uninstall removes key

- GIVEN a ScreenSafe Run key exists
- WHEN `uninstall` is executed
- THEN the Run key is removed from the registry

#### Scenario: Uninstall on missing key

- GIVEN no ScreenSafe Run key exists
- WHEN `uninstall` is executed
- THEN the command succeeds without error
