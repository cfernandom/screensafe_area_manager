# Logging Specification

## Purpose

File-based logging with size-based rotation, configurable retention, and fixed log path under `%LOCALAPPDATA%`.

## Requirements

### Requirement: Log Path and Directory

The system MUST write log files to `%LOCALAPPDATA%\ScreenSafe\Logs\` (fixed path, not relative to executable). The directory MUST be auto-created if it does not exist.

#### Scenario: Directory created automatically

- GIVEN `%LOCALAPPDATA%\ScreenSafe\Logs\` does not exist
- WHEN the logger initializes
- THEN the directory is created

### Requirement: Log File Format and Rotation

The system MUST write logs in format `screensafe-{yyyy-MM-dd}-{n}.log`. When a file exceeds 1 MB, the system MUST create a new file with incremented `{n}`. The system MUST keep the last 3 rotated files and delete older ones.

#### Scenario: Rotation on size threshold

- GIVEN the current log file is 1 MB
- WHEN a new log line is written
- THEN a new file `screensafe-{yyyy-MM-dd}-{n+1}.log` is created

#### Scenario: Retention enforces 3-file limit

- GIVEN 3 rotated files exist plus the current file
- WHEN a new rotation creates a 4th file
- THEN the oldest rotated file is deleted

### Requirement: Log Levels

The system MUST support Info, Warning, and Error log levels. Info covers lifecycle and events. Warning covers detected changes and reapplies. Error covers failures.

#### Scenario: Log levels produce correct entries

- GIVEN an event is received
- WHEN the daemon logs it at Info level
- THEN the log entry contains `[INFO]` and the event description
