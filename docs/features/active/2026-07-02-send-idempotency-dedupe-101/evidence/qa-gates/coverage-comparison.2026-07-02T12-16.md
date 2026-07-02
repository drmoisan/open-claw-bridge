# Coverage Comparison — Baseline vs Post-Change (P5-T4)

Timestamp: 2026-07-02T12-16
Baseline source: `artifacts/csharp/baseline-101/` (three Cobertura reports, run 2026-07-02T11-59)
Final source: `artifacts/csharp/final-101/` (three Cobertura reports, run 2026-07-02T12-16)

## (a) Baseline coverage

- Pooled line coverage: 90.56% (4174/4609)
- Pooled branch coverage: 80.05% (935/1168)
- `OpenClaw.Core` package (Core.Tests report): line 98.63%, branch 91.82%

## (b) Post-change coverage

- Pooled line coverage: 90.63% (4207/4642) — up 0.07 points from baseline
- Pooled branch coverage: 80.25% (947/1180) — up 0.20 points from baseline
- `OpenClaw.Core` package (Core.Tests report): line 98.66%, branch 92.07% — both up from baseline

## (c) Per-file line coverage for changed/new production files (Core.Tests report)

| File | Status | Baseline line / branch | Final line / branch |
|---|---|---|---|
| `src/OpenClaw.Core/Agent/SentActionKey.cs` | new | n/a | 100% / 100% |
| `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs` | new | n/a | 100% / 100% |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` | modified | 100% / 100% | 100% / 100% |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` | modified | 100% / 50% | 100% / 50% |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` | modified | 100% / 100% | 100% / 100% |
| `src/OpenClaw.Core/Program.cs` | modified | 100% / 100% | 100% / 100% |
| `src/OpenClaw.Core/Agent/Contracts/ISentActionStore.cs` | new | n/a | interface-only; no executable lines — legitimately excluded from executable coverage per policy |

Note on `SchedulingWorker.Pipeline.cs` branch rate: the 50% branch rate is identical at baseline and final. Verified via line-level Cobertura data in the final report: the file has zero uncovered lines and exactly two partial branches, both on pre-existing lines untouched by this feature (line 170, the `MailboxUpn()` internal-domain ternary; line 177, the `BuildProposalReply` empty-slots ternary). The consult/record logic added by this feature (lines 129–157) is inside the async `ProposeAndActAsync` body, which the runsettings' CompilerGenerated exclusion leaves uninstrumented; its behavior is directly verified by the five `SchedulingWorkerDedupeTests` (hit-skip, miss-send-record ordering, failure no-record, kill-switch, restart persistence), including both outcomes of the new `IsRecordedAsync` branch. No changed-line regression.

## (d) Verdict per threshold

| Threshold | Value | Verdict |
|---|---|---|
| Line coverage >= 85% | 90.63% pooled (98.66% Core package) | PASS |
| Branch coverage >= 75% | 80.25% pooled (92.07% Core package) | PASS |
| Changed-line coverage no regression | all new/changed production lines 100% line-covered; pooled and Core-package figures both improved vs baseline | PASS |

Overall: PASS.
