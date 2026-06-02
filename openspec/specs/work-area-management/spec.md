# Work Area Management Specification

## Purpose

Reserve, restore, and query the Windows desktop work area via a dual-strategy pattern (SPI_SETWORKAREA primary, SHAppBarMessage fallback).

## Requirements

### Requirement: IWorkAreaManager Interface

The Domain layer MUST define `IWorkAreaManager` with `Apply(Rect area)`, `Restore()`, and `GetCurrent()` methods. The system MUST select strategy at composition time based on config + platform capability.

#### Scenario: Apply reserves bottom N pixels

- GIVEN a screen resolution of 1920×1080 and config setting `reservedBottomPixels: 80`
- WHEN `Apply` is called with a RECT of `{0, 0, 1920, 1000}`
- THEN `SPI_SETWORKAREA` is invoked with that RECT
- AND the original RECT `{0, 0, 1920, 1080}` is persisted before modification

#### Scenario: Restore returns to full screen

- GIVEN a previously persisted original RECT of `{0, 0, 1920, 1080}`
- WHEN `Restore` is called
- THEN `SPI_SETWORKAREA` is invoked with that exact RECT
- AND the stored RECT is removed from config

#### Scenario: Fallback strategy activates on Windows 10+

- GIVEN the platform reports Windows 10 and config sets `strategy: auto`
- WHEN `Apply` is called
- THEN the system MUST use SHAppBarMessage(ABM_SETPOS) as the execution strategy

#### Scenario: Non-Windows platform returns error

- GIVEN the OS is Linux
- WHEN `GetCurrent` is called
- THEN the system MUST throw `PlatformNotSupportedException`

### Requirement: Strategy Selection via Config + Capability

The system MUST resolve strategy by: (1) explicit config override, (2) platform capability detection via `IPlatformInfoProvider`, and (3) use SPI_SETWORKAREA for Windows ≤ 8.1 or SHAppBarMessage for Windows ≥ 10.

#### Scenario: Config explicit override

- GIVEN config has `strategy: shappbarmessage` and platform is Windows 8.1
- WHEN `IWorkAreaManager` is resolved
- THEN the SHAppBarMessage strategy is injected regardless of detected capability
