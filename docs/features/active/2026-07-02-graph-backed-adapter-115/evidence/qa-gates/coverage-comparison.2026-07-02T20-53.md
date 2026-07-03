# Coverage Comparison — Baseline vs Post-Change vs New-Code (P8-T6)

Timestamp: 2026-07-02T20-53
Command: comparison of `evidence/baseline/csharp-test-coverage.2026-07-02T20-04.md` (P0-T5) against `evidence/qa-gates/csharp-test-coverage.2026-07-02T20-53.md` (P8-T5); new-code slice computed from the Core Cobertura XML (`artifacts/csharp/final-2026-07-02T20-53/coverage.core.cobertura.xml`) filtered to `CloudGraph` filenames and `OpenClaw.Core/Program.cs`
EXIT_CODE: 0

## 1. Baseline coverage (P0-T5, 2026-07-02T20-04)

- Pooled: line 4540/4975 = 91.26%; branch 1040/1278 = 81.38%
- OpenClaw.Core package: line 1894/2068 = 91.59%; branch 453/552 = 82.07%

## 2. Post-change coverage (P8-T5, 2026-07-02T20-53)

- Pooled: line 5234/5668 = 92.34% (delta +1.08 pp); branch 1269/1526 = 83.16% (delta +1.78 pp)
- OpenClaw.Core package: line 2588/2761 = 93.73% (delta +2.14 pp); branch 682/800 = 85.25% (delta +3.18 pp)
- Untouched packages byte-identical to baseline: HostAdapter 1113/1269 and 170/253; MailBridge 1533/1638 and 417/473.

## 3. New/changed-code coverage

- `src/OpenClaw.Core/CloudGraph/**` (all new production code): line 1370/1374 = 99.71%; branch 446/492 = 90.65%
- `src/OpenClaw.Core/Program.cs` (contains the only changed production lines — the backend-selection conditional block): line 492/492 = 100.00%; branch 4/4 = 100.00%. The changed lines are exercised by both `GraphBackendSelectionTests` paths (flag absent and flag true), so no changed line lost coverage.

## Threshold verdict

| Gate | Threshold | Value | Verdict |
|---|---|---|---|
| Line coverage (pooled) | >= 85% | 92.34% | PASS |
| Branch coverage (pooled) | >= 75% | 83.16% | PASS |
| Line coverage (Core package) | >= 85% | 93.73% | PASS |
| Branch coverage (Core package) | >= 75% | 85.25% | PASS |
| New-code line coverage (CloudGraph) | >= 85% | 99.71% | PASS |
| New-code branch coverage (CloudGraph) | >= 75% | 90.65% | PASS |
| No regression on changed lines | no decrease | Program.cs 100%/100%; pooled +1.08 pp line, +1.78 pp branch | PASS |

**Verdict: PASS** — all thresholds hold; coverage improved relative to baseline and no changed line regressed.
