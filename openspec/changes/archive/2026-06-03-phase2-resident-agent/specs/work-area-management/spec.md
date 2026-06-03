# Delta for Work Area Management

## ADDED Requirements

### Requirement: Comparison Before Reapply

Before any automatic reapply, the system MUST read `SPI_GETWORKAREA`, compare against the desired area from config, and call `SPI_SETWORKAREA` ONLY if they differ. This applies to daemon-triggered reapplies, not initial `apply`.

#### Scenario: Comparison prevents no-op call

- GIVEN the current WorkArea matches the desired area
- WHEN the daemon triggers a reapply evaluation
- THEN no `SPI_SETWORKAREA` call is made

#### Scenario: Comparison allows necessary call

- GIVEN the current WorkArea differs from the desired area
- WHEN the daemon triggers a reapply evaluation
- THEN `SPI_SETWORKAREA` is called with the desired RECT

### Requirement: OriginalWorkArea Immutability

The daemon MUST capture `OriginalWorkArea` during the first manual `apply` and MUST NOT rewrite it during monitoring or automatic reapplies. Only the `restore` command clears `OriginalWorkArea` from config.

#### Scenario: Daemon does not rewrite OriginalWorkArea

- GIVEN `OriginalWorkArea` is stored in `appsettings.json`
- WHEN the daemon performs automatic reapplies
- THEN `OriginalWorkArea` in config remains unchanged

#### Scenario: Restore clears OriginalWorkArea

- GIVEN `OriginalWorkArea` is stored in config
- WHEN `restore` is executed
- THEN `OriginalWorkArea` is removed from config after restoration
