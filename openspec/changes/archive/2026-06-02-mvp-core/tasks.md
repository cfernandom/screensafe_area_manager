# Tasks: ScreenSafe Area Manager — MVP Core

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1100–1300 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Single PR (size:exception approved) |
| Delivery strategy | ask-always |
| Chain strategy | size-exception |

Decision needed before apply: No (exception granted)
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Solution scaffolding + Domain layer (models, interfaces, domain tests) | PR 1 | base=main; pure C#, no P/Invoke, compiles standalone |
| 2 | Infrastructure (NativeMethods, strategies, providers, persistence + tests) | PR 2 | base=main; all Win32 calls, depends on Domain only |
| 3 | Application use cases + Console CLI + DI + integration tests | PR 3 | base=main; depends on PR 1+2, final integration |

## Phase 1: Foundation — Solution + Domain (TDD)

- [x] 1.1 **[RED]** Write `ScreenRectTests` for Width, Height, equality in `Tests/Domain/`
- [x] 1.2 Create `ScreenSafe.sln` + 5 `.csproj` files targeting .NET Framework 4.8
- [x] 1.3 Create `ScreenRect.cs`, `AppSettings.cs`, `IWorkAreaManager.cs`, `IPlatformInfoProvider.cs`, `IScreenInfoProvider.cs` in `Domain/`
- [x] 1.4 **[GREEN]** Domain compiles, `ScreenRectTests` pass

## Phase 2: Infrastructure (TDD)

- [x] 2.1 **[RED]** Write `JsonSettingsRepositoryTests`: load existing, load missing, corrupt JSON fallback, save RECT
- [x] 2.2 Create `NativeMethods/User32.cs` (SPI_GETWORKAREA, SPI_SETWORKAREA) + `NativeMethods/Shell32.cs` (SHAppBarMessage constants)
- [x] 2.3 Create `PlatformInfoProvider` (OS version, capability check) + `ScreenInfoProvider` (GetSystemMetrics)
- [x] 2.4 Create `SpSetWorkAreaStrategy` (primary SPI path) + `ShAppBarMessageStrategy` (fallback)
- [x] 2.5 Create `JsonSettingsRepository` (Load/Save, corrupt-JSON fallback) + `PlatformGuard` (EnsureWindows)
- [x] 2.6 **[GREEN]** Infrastructure tests pass

## Phase 3: Application + Console + Integration (TDD)

- [x] 3.1 **[RED]** Write `ApplyUseCaseTests`, `RestoreUseCaseTests`, `StatusUseCaseTests` with Moq mocks
- [x] 3.2 Create `ApplyUseCase`, `RestoreUseCase`, `StatusUseCase` in `Application/`
- [x] 3.3 Create `CliDispatcher` — arg parse, dispatch, unknown command → exit code 1
- [x] 3.4 Create `Program.cs` — DI composition root via `ServiceCollection`, platform guard, command dispatch
- [x] 3.5 Create `app.manifest` (Win 8.1 detection compat) + `appsettings.json` (defaults: 80px, auto strategy)
- [x] 3.6 **[GREEN]** Use case tests pass, `Program.cs` resolves all DI services without throwing
- [x] 3.7 Build validation: compile on Linux, verify `PlatformNotSupportedException` at runtime
