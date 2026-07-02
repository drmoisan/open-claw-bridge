# Coverage Comparison — Baseline vs Post-Change (Issue #105)

Timestamp: 2026-07-02T14-20
Baseline raw reports: `artifacts/csharp/baseline-105/` (captured 2026-07-02T14-04)
Final raw reports: `artifacts/csharp/final-105/` (captured 2026-07-02T14-20)

## (a) Baseline coverage

- Pooled (three Cobertura reports): line 90.81% (4298/4733), branch 80.62% (990/1228)
- Package `OpenClaw.Core`: line 98.74%, branch 91.79%
- Per-run roots: HostAdapter 87.70%/67.19%; Core 90.47%/80.27%; MailBridge 93.58%/88.16%

## (b) Post-change coverage

- Pooled: line 90.92% (4353/4788), branch 80.74% (998/1236)
- Package `OpenClaw.Core`: line 98.78%, branch 91.94%
- Per-run roots: HostAdapter 87.70%/67.19% (identical counts to baseline); Core 90.75%/80.59%; MailBridge 93.58%/88.16% (identical counts to baseline)

## (c) Per-file coverage — changed/new production files (Core run, final report)

| File | Line | Branch | Notes |
|---|---|---|---|
| `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` (`OneOnOneMoveGuard`) | 100% | 100% | fully covered by unit + property tests |
| `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` (`SeriesMoveHistoryAnswers`) | 100% | 100% | record fully covered |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` | 100% | 100% | DDL constant + migration helpers |
| `src/OpenClaw.Core/Program.cs` (`Program`) | 100% | 100% | as reported by the Core run |
| `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` | no coverable lines | no coverable lines | see note below |
| `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` | n/a | n/a | interface-only; legitimately excluded from executable coverage per policy |

Note on `CoreCacheRepository.SeriesMoves.cs`: every member is `async` (`RecordMoveAsync`, `GetMovedOccurrenceStartsAsync`, `EnsureSeriesMovesSchemaAsync`); `mailbridge.runsettings` sets `ExcludeByAttribute` including `CompilerGeneratedAttribute`, so async method bodies (compiled into compiler-generated state machines) carry no instrumented lines and the file reports zero coverable lines. This is the pre-existing measurement configuration (the async members of `CoreCacheRepository.SentActions.cs` are measured the same way); no coverage exclusion was added or modified for this feature. Behavioral coverage is demonstrated by the 12 dedicated tests in `CoreCacheRepositorySeriesMovesTests.cs`, which exercise both public methods and every branch: blank-key `ArgumentException` (null/empty/whitespace), duplicate-pair `ON CONFLICT DO NOTHING`, non-UTC normalization, descending order, series isolation, lazy schema-ensure (both flag states), migration idempotency, pre-existing-database upgrade, and restart persistence.

## (d) Verdicts

| Threshold | Value | Verdict |
|---|---|---|
| Line coverage >= 85% | 90.92% pooled (Core package 98.78%) | PASS |
| Branch coverage >= 75% | 80.74% pooled (Core package 91.94%) | PASS |
| No changed-line coverage regression | Pooled line +0.11 pp, branch +0.12 pp vs baseline; Core package line 98.74% -> 98.78%, branch 91.79% -> 91.94%; HostAdapter and MailBridge reports have counts identical to baseline; all measurable changed/new production code reports 100%/100% | PASS |
