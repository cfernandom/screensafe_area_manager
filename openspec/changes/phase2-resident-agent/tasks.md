# Tasks: Phase 2 — Resident Protection Agent

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~950–1450 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes (resolved: size:exception)
Chained PRs recommended: Yes (overridden: single PR, maintainer accepted size:exception)
Chain strategy: single-pr (size:exception accepted)
400-line budget risk: High (mitigated: exception approved)

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Foundation: Domain interfaces, P/Invoke, Logger infra | PR 1 | base=feature/phase2-resident-agent; compiles standalone |
| 2 | Infrastructure: EventDebouncer, WorkAreaWatcher, WindowsStartupManager, LogRotator + unit tests | PR 2 | base=PR_1 branch; TDD per component |
| 3 | Application + Console: AutoApplyService, HealthUseCase, CLI integration, full tests | PR 3 | base=PR_2 branch; final integration |

## Phase 1: Foundation — Interfaces + P/Invoke

- [x] 1.1 Create `Domain/IWorkAreaWatcher.cs`, `IWindowsStartupManager.cs`, `IEventDebouncer.cs`, `HealthReport.cs`
- [x] 1.2 Add P/Invoke to `Infrastructure/NativeMethods/User32.cs`
- [x] 1.3 Create `Domain/ILogger.cs`, `Infrastructure/Logging/ConsoleLogger.cs`, `Infrastructure/Logging/FileLogger.cs`
- [x] 1.4 Verify build: `dotnet build -f net48` compiles clean

## Phase 2: Infrastructure — TDD

- [x] 2.1 **[RED]** Write `EventDebouncerTests` (single-fire, multi-event collapse) → **[GREEN]** Implement `EventDebouncer.cs`
- [x] 2.2 **[RED]** Write `WindowsStartupManagerTests` (registry CRUD on test path) → **[GREEN]** Implement `WindowsStartupManager.cs`
- [x] 2.3 **[RED]** Write `LogRotatorTests` (1MB rotation, 3-file retention) → **[GREEN]** Implement `LogRotator.cs`
- [x] 2.4 Create `WorkAreaWatcher.cs`: hidden window, message pump, C# events + `[Fact(Skip)]` integration test via `PostMessage`
- [x] 2.5 `dotnet test -f net48` — all infra tests green

## Phase 3: Application — TDD

- [x] 3.1 **[RED]** Write `AutoApplyServiceTests` (orchestration, circuit breaker threshold/suspension) → **[GREEN]** Implement `AutoApplyService.cs`
- [x] 3.2 **[RED]** Write `HealthUseCaseTests` (aggregation, exit codes per spec) → **[GREEN]** Implement `HealthUseCase.cs`
- [x] 3.3 `dotnet test -f net48` — all app tests green

## Phase 4: Console Integration

- [x] 4.1 Modify `Program.cs`: `--daemon` routing, shared `ConfigureServices()`, named mutex, `FreeConsole()`
- [x] 4.2 Modify `CliDispatcher.cs`: `install`, `uninstall`, `health` commands + health exit codes
- [x] 4.3 `dotnet build -f net48` + smoke test: CLI commands dispatch, daemon starts without visible window

## Phase 5: Build & Verify

- [ ] 5.1 `dotnet build -f net48` — entire solution compiles without warnings
- [ ] 5.2 `dotnet test -f net48` — all unit + integration tests pass
