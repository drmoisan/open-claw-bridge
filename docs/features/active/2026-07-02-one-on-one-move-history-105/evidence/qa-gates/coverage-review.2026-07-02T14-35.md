# Reviewer Coverage Re-Run and Per-File Re-Measurement (Issue #105)

Timestamp: 2026-07-02T14-35
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/coverage-review"` (repo root, branch head `be38534`)
EXIT_CODE: 0

## Test results (reviewer run)

- 774 passed, 0 failed, 5 skipped (environment-gated, same as baseline), 779 total.
  - OpenClaw.HostAdapter.Tests: 100/100
  - OpenClaw.Core.Tests: 327/327 (includes the 12 SeriesMoves repository cases, 27 OneOnOneMoveGuard unit cases, 4 CsCheck properties, and the NetArchTest architecture-boundary suite)
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352

## Pooled coverage (reviewer-parsed from the three fresh cobertura reports)

- Line: 4353/4788 = 90.91% (executor evidence reports the same counts 4353/4788, rendered as 90.92% — a 0.01pp rounding difference only)
- Branch: 998/1236 = 80.74% (identical to executor evidence)
- Package `OpenClaw.Core`: line 1623/1643 = 98.78%, branch 411/447 = 91.95%

## Per-changed-file coverage (line AND branch, reviewer-parsed)

| File | Line | Branch | Notes |
|---|---|---|---|
| `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` (new) | 52/52 = 100.00% | 8/8 = 100.00% | zero uncovered lines, zero partial conditions |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (modified) | 37/37 = 100.00% | 6/6 = 100.00% | DDL constant + migration helpers |
| `src/OpenClaw.Core/Program.cs` (modified) | 239/239 = 100.00% | no branch points | DI registration line covered |
| `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` (new) | not instrumented | not instrumented | all members async; `mailbridge.runsettings` `ExcludeByAttribute` includes `CompilerGeneratedAttribute`, so async state-machine bodies carry no instrumented lines (known repo-wide instrumentation-scope pattern, same disposition as the #99/#103 reviews). Behaviorally verified by the 12 dedicated repository tests (both public methods, both flag states of the lazy schema-ensure guard, blank-key throw x3, duplicate `ON CONFLICT DO NOTHING`, non-UTC normalization, ordering, isolation, upgrade, restart). |
| `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` (new) | not instrumented | not instrumented | interface-only file; legitimately reports no executable coverage per the general-unit-test policy clarification |

## Baseline comparison

- Baseline (executor, `evidence/baseline/baseline-test-coverage.2026-07-02T14-04.md`): pooled line 90.81% (4298/4733), branch 80.62% (990/1228).
- Post-change (reviewer): pooled line 90.91% (4353/4788), branch 80.74% (998/1236). Change: +0.10pp line, +0.12pp branch.
- HostAdapter and MailBridge report counts are unchanged vs baseline; the delta is entirely in `OpenClaw.Core` (98.74% -> 98.78% line, 91.79% -> 91.95% branch).

## Verdicts

| Gate | Value | Verdict |
|---|---|---|
| Repo-wide line >= 85% | 90.91% | PASS |
| Repo-wide branch >= 75% | 80.74% | PASS |
| New instrumented code line/branch | 100.00% / 100.00% (`OneOnOneMoveGuard.cs`) | PASS |
| Modified files, no changed-line regression | `Schema.cs` and `Program.cs` at 100.00% line; pooled coverage improved | PASS |
