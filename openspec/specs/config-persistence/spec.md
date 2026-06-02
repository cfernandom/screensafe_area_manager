# Config Persistence Specification

## Purpose

JSON-based settings read/write for app configuration (`appsettings.json`) and original work area state persistence.

## Requirements

### Requirement: Read Configuration on Startup

The system MUST read `appsettings.json` from the executable directory on startup. If the file does not exist, the system MUST return default settings (reservedBottomPixels: 80, strategy: auto).

#### Scenario: File exists with valid JSON

- GIVEN `appsettings.json` exists with `{"reservedBottomPixels": 120, "strategy": "spisetworkarea"}`
- WHEN `Load()` is called
- THEN it returns a `Settings` object with `ReservedBottomPixels = 120` and `Strategy = "spisetworkarea"`

#### Scenario: File does not exist

- GIVEN no `appsettings.json` exists in the executable directory
- WHEN `Load()` is called
- THEN it returns default settings

### Requirement: Persist Original Work Area State

The system MUST store the original desktop RECT before any modification is applied, and MUST read it back on restore.

#### Scenario: Apply stores original RECT

- GIVEN original work area is `{0, 0, 1920, 1080}` and `reservedBottomPixels: 80`
- WHEN `Apply` stores the original state
- THEN `appsettings.json` contains `"originalWorkArea": {"left": 0, "top": 0, "right": 1920, "bottom": 1080}`

#### Scenario: Restore reads stored RECT

- GIVEN `appsettings.json` has an `originalWorkArea` entry
- WHEN `Restore` reads settings
- THEN it returns the exact stored RECT without recalculation

### Requirement: Handle Corrupt JSON Gracefully

If `appsettings.json` contains invalid JSON, the system MUST log an error and fall back to default settings. It MUST NOT crash.

#### Scenario: Corrupt JSON falls back to defaults

- GIVEN `appsettings.json` contains `{invalid json`
- WHEN `Load()` is called
- THEN it returns default settings
- AND the corrupt file is NOT overwritten
