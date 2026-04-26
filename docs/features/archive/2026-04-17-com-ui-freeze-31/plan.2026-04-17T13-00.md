# 2026-04-17-com-ui-freeze (Plan)

- **Issue:** #31
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-17T13-00
- **Status:** Approved
- **Version:** 1.0
- **Work Mode:** minor-audit
- **Requirements Source:** `docs/features/active/2026-04-17-com-ui-freeze-31/issue.md` (`## Acceptance Criteria` section)

## Overview

Outlook COM interaction in the MailBridge scanner freezes the Outlook UI because `OutlookScanner` iterates through up to 500 inbox/calendar items in a tight loop, making thousands of cross-apartment COM calls without yielding. This plan adds configurable COM yield settings to `BridgeSettings`, inserts periodic `Thread.Sleep` yields during scan loops, adds validation rules, and covers the change with unit tests.

## Acceptance Criteria (from issue.md)

- [x] AC-1: `BridgeSettings` includes `ComYieldBatchSize` (default 25) and `ComYieldMilliseconds` (default 15)
- [x] AC-2: `OutlookScanner` yields control (via `Thread.Sleep`) after every `ComYieldBatchSize` items during inbox and calendar scans
- [x] AC-3: Existing unit tests continue to pass with no regressions
- [x] AC-4: New unit tests verify that yield logic is invoked at correct batch boundaries
- [x] AC-5: `BridgeSettingsValidator` rejects invalid yield settings (batch size < 1, yield ms < 0)

## Affected Files

| File | Change |
|------|--------|
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | Add `ComYieldBatchSize` and `ComYieldMilliseconds` to `BridgeSettings` record and `Default` |
| `src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs` | Add validation rules in `BridgeSettingsValidator.Validate` |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | Modify `EnumerateItems` to yield via `Thread.Sleep` at batch boundaries |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs` | Add yield-boundary unit tests |

---

## Implementation Plan (Atomic Tasks)

### Phase 0 — Context & Baseline Capture

- [x] [P0-T1] Read required policy files in order: (1) `general-code-change.instructions.md`, (2) `general-unit-test.instructions.md`, (3) `csharp-code-change.instructions.md`, (4) `csharp-unit-test.instructions.md`. Record completion in `docs/features/active/2026-04-17-com-ui-freeze-31/phase0-instructions-read.md` with fields: `Timestamp:`, `Policy Order:`, explicit list of files read.
  - Acceptance: artifact exists at path with all four required fields populated.

- [x] [P0-T2] Capture baseline build state by running `dotnet build OpenClaw.MailBridge.sln -c Debug`. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/baseline-build.md` with fields: `Timestamp:` (ISO-8601), `Command:` (exact command), `EXIT_CODE:` (integer), `Output Summary:` (pass/fail, warning count, error count).
  - Acceptance: artifact exists at path with all four required fields populated and `EXIT_CODE: 0`.

- [x] [P0-T3] Capture baseline test state by running `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"`. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/baseline-test.md` with fields: `Timestamp:` (ISO-8601), `Command:` (exact command), `EXIT_CODE:` (integer), `Output Summary:` (total passed/failed/skipped counts, numeric line coverage percentage from coverage report).
  - Acceptance: artifact exists at path with all four required fields populated, `EXIT_CODE: 0`, and numeric coverage headline value present in `Output Summary`.

### Phase 1 — Implementation (Small-Path Handoff)

- [x] [P1-T1] **Handoff to csharp-typed-engineer.** Implement the following changes as a single constrained small-path unit of work:

  **1. Add yield settings to `BridgeSettings`** (`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`):
  - Add `int ComYieldBatchSize` parameter to the `BridgeSettings` record (after `BodyPreviewMaxChars`).
  - Add `int ComYieldMilliseconds` parameter to the `BridgeSettings` record (after `ComYieldBatchSize`).
  - Update `BridgeSettings.Default` to include `ComYieldBatchSize: 25` and `ComYieldMilliseconds: 15`.
  - Update all existing call sites that construct `BridgeSettings` (including test helpers) to include the two new parameters.

  **2. Add validation rules** (`src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs`):
  - In `BridgeSettingsValidator.Validate`, add: if `ComYieldBatchSize < 1`, add error `"comYieldBatchSize must be >= 1"`.
  - In `BridgeSettingsValidator.Validate`, add: if `ComYieldMilliseconds < 0`, add error `"comYieldMilliseconds must be >= 0"`.

  **3. Add yield logic to scan loops** (`src/OpenClaw.MailBridge/OutlookScanner.cs`):
  - Modify `EnumerateItems` (or introduce a new helper) to call `Thread.Sleep(_settings.ComYieldMilliseconds)` after every `_settings.ComYieldBatchSize` items yielded (i.e., when `count % ComYieldBatchSize == 0` and `count > 0`).
  - The yield must occur during both inbox and calendar scan iteration since both call `EnumerateItems`.

  **4. Add unit tests** (`tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs`):
  - Add test: `EnumerateItems_should_yield_after_ComYieldBatchSize_items` — verify that when iterating over items where count exceeds `ComYieldBatchSize`, the yield point is reached at the correct boundary (e.g., after item 25 with default batch size 25).
  - Add test: `EnumerateItems_should_not_yield_when_count_is_below_batch_size` — verify that when iterating fewer items than `ComYieldBatchSize`, no yield delay occurs.
  - Add test: `EnumerateItems_should_yield_multiple_times_for_large_item_sets` — verify that iterating 75 items with `ComYieldBatchSize=25` produces yields at items 25, 50, and 75.
  - Add test: `BridgeSettingsValidator_should_reject_ComYieldBatchSize_less_than_1` — verify that `Validate` returns an error containing `"comYieldBatchSize"` when `ComYieldBatchSize` is 0 or negative.
  - Add test: `BridgeSettingsValidator_should_reject_negative_ComYieldMilliseconds` — verify that `Validate` returns an error containing `"comYieldMilliseconds"` when `ComYieldMilliseconds` is -1.
  - Add test: `BridgeSettingsValidator_should_accept_valid_yield_settings` — verify that `Validate` returns no yield-related errors when `ComYieldBatchSize=25` and `ComYieldMilliseconds=15`.

  **Acceptance criteria for implementation completion:**
  - All four production files are modified as described above.
  - All six named unit tests exist, are syntactically correct, and pass when run with `dotnet test`.
  - No existing tests are broken (regression-free).
  - `BridgeSettings.Default` includes `ComYieldBatchSize: 25` and `ComYieldMilliseconds: 15`.
  - The solution builds without errors or nullable warnings.

### Phase 2 — Final QC Loop

- [x] [P2-T1] Run CSharpier formatter: `dotnet tool run csharpier .` from solution root. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-format.md` with fields: `Timestamp:` (ISO-8601), `Command: dotnet tool run csharpier .`, `EXIT_CODE:` (integer), `Output Summary:` (files changed or "no changes"). If any files are changed, restart the QC loop from P2-T1.
  - Acceptance: artifact exists and `EXIT_CODE: 0` with no files changed.

- [x] [P2-T2] Run build with analyzers enabled: `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-analyzers.md` with fields: `Timestamp:` (ISO-8601), `Command:` (exact command), `EXIT_CODE:` (integer), `Output Summary:` (error/warning counts). If build fails, fix issues and restart from P2-T1.
  - Acceptance: artifact exists and `EXIT_CODE: 0` with zero errors.

- [x] [P2-T3] Run build with nullable analysis and warnings-as-errors: `dotnet build OpenClaw.MailBridge.sln -c Debug /p:Nullable=enable /p:TreatWarningsAsErrors=true`. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-nullable.md` with fields: `Timestamp:` (ISO-8601), `Command:` (exact command), `EXIT_CODE:` (integer), `Output Summary:` (error/warning counts). If build fails, fix issues and restart from P2-T1.
  - Acceptance: artifact exists and `EXIT_CODE: 0` with zero errors and zero warnings.

- [x] [P2-T4] Run tests with code coverage: `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"`. Record result in `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-test.md` with fields: `Timestamp:` (ISO-8601), `Command:` (exact command), `EXIT_CODE:` (integer), `Output Summary:` (total passed/failed/skipped counts, numeric line coverage percentage post-change, comparison to baseline from P0-T3). If any tests fail, fix issues and restart from P2-T1.
  - Acceptance: artifact exists, `EXIT_CODE: 0`, all tests pass, numeric coverage is present and does not regress below baseline.

- [x] [P2-T5] Verify all five acceptance criteria from `issue.md` are satisfied. Record a brief AC traceability check in `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-ac-check.md` mapping each AC to its evidence (test name, code location, or QA artifact).
  - Acceptance: artifact exists with all five ACs marked as satisfied with specific evidence references.
