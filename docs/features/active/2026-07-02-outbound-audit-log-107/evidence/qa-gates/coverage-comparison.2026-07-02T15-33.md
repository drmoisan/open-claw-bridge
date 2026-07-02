# Final QA — Coverage Comparison (baseline vs. post-change)

Timestamp: 2026-07-02T15-33
Command: Python comparison of `artifacts/csharp/baseline-107/*/coverage.cobertura.xml` (baseline artifact `evidence/baseline/dotnet-test-coverage.2026-07-02T15-04.md`) against `artifacts/csharp/final-107/*/coverage.cobertura.xml` (P5-T3)
EXIT_CODE: 0

## Overall (pooled, per-package best across reports)

| Metric | Baseline | Post-change | Delta | Threshold | Status |
|---|---|---|---|---|---|
| Pooled line | 8392/8670 = 96.79% | 8484/8762 = 96.83% | +0.04 pp | >= 85% | PASS |
| Pooled branch | 1996/2220 = 89.91% | 2008/2232 = 89.96% | +0.05 pp | >= 75% | PASS |
| OpenClaw.Core line | 3246/3286 = 98.78% | 3338/3378 = 98.82% | +0.04 pp | >= 85% | PASS |
| OpenClaw.Core branch | 822/894 = 91.95% | 834/906 = 92.05% | +0.10 pp | >= 75% | PASS |

Other packages unchanged (MailBridge 93.10%/86.36%; HostAdapter 98.64%/89.47%; MailBridge.Client 90.48%/93.10%; MailBridge.Contracts 98.14%/93.65%; HostAdapter.Contracts 100%/n-a).

## Changed/new production files (Core test report; instrumented lines per mailbridge.runsettings, which excludes CompilerGenerated — async method bodies compile to excluded state machines)

| File | Line coverage | Branch coverage |
|---|---|---|
| ActionAuditRecord.cs (new) | 30/30 = 100% | n/a (no branches) |
| ActionAuditResultCode.cs (new) | const-only; no instrumented lines (no executable code) | n/a |
| CoreCacheRepository.AuditLog.cs (new) | 26/26 = 100% | 12/12 = 100% |
| CoreCacheRepository.Schema.cs | 74/74 = 100% | 12/12 = 100% |
| SchedulingWorker.cs | 20/20 = 100% | n/a |
| SchedulingWorker.Audit.cs (new) | 32/32 = 100% | n/a |
| SchedulingWorker.Pipeline.cs | 40/40 = 100% | 4/8 = 50% (see note) |
| HostAdapterSchedulingService.cs | 8/8 = 100% | n/a |
| Program.cs | 726/726 = 100% | 28/28 = 100% |

`IActionAuditLog.cs` and `ISchedulingService.cs` are interface-only files omitted per policy (`.claude/rules/csharp.md` interface-only clarification).

Note on `SchedulingWorker.Pipeline.cs` branch 4/8: the four uncovered branch outcomes sit on two pre-existing, unchanged lines — the `MailboxUpn()` ternary (line 298) and the `BuildProposalReply` slot-count ternary (line 305). The baseline reports show the identical two lines at 50% (1/2) condition coverage (old line numbers 233 and 240), so there is no coverage regression on changed lines: every instrumented line and branch introduced or modified by this feature is covered. Behavioral coverage of the async emission paths (not instrumented due to the CompilerGenerated exclusion) is demonstrated by the 8 `SchedulingWorkerAuditTests` exercising all four decision points, both resilience paths, and the time/correlation properties (red in the expect-fail run, green after implementation).

## Verdict

- Line >= 85%: PASS (96.83% pooled, 98.82% Core)
- Branch >= 75%: PASS (89.96% pooled, 92.05% Core)
- No coverage regression on changed lines: PASS (all changed/new instrumented lines 100%; only partially covered branches are pre-existing unchanged lines, identical at baseline)
