# Coverage Comparison — Baseline vs Post-Change (Issue #128, P5-T6)

Timestamp: 2026-07-07T04-01
Command: comparison of P0-T5 baseline Cobertura vs P5-T5 post-change Cobertura for the OpenClaw.Core (T1) module
EXIT_CODE: 0

## 1. Baseline coverage (OpenClaw.Core package)

- Line: 0.9925 (99.25%)
- Branch: 0.9221 (92.21%)

Source: `artifacts/csharp/baseline/2b48c865-.../coverage.cobertura.xml`.

## 2. Post-change coverage (OpenClaw.Core package)

- Line: 0.9927 (99.27%)
- Branch: 0.9224 (92.24%)

Source: `artifacts/csharp/final/45d141d3-.../coverage.cobertura.xml`.

Delta: line +0.02 pp, branch +0.03 pp. No overall regression; both metrics increased slightly.

## 3. New / changed-code coverage (per file)

| File | Line | Branch | Note |
|---|---|---|---|
| GraphHostAdapterClient.RescheduleEvent.cs (add) | 100% | 100% | fully covered by the contract suite |
| SchedulingWorker.Reschedule.cs (add) | 100% | 92.85% | one guard-arm combination not distinctly hit; well above 75% |
| SchedulingWorker.cs (mod) | 100% | 100% | |
| SchedulingWorker.Pipeline.cs (mod) | 100% | 50% | UNCHANGED from baseline (50% pre-existing); changed lines (added param + call) carry no branch |
| HostAdapterHttpClient.cs (mod) | 100% | 100% | both requestId arms of the new method covered |
| HostAdapterSchedulingService.cs (mod) | 100% | 100% | |
| SentActionKey.cs (mod) | 100% | 100% | |
| ActionAuditResultCode.cs (mod) | n/a | n/a | const-only, no executable branches |
| ISchedulingService.cs (mod) | n/a | n/a | interface-only |

Changed-line no-regression check: no changed file's coverage decreased versus baseline. HostAdapterHttpClient.cs briefly dropped to 50% branch when the new method's requestId ternary was half-covered; a null-requestId test restored it to 100% before this comparison.

## Threshold verdict

- Line coverage >= 85%: PASS (99.27% overall; 100% on every changed production file).
- Branch coverage >= 75%: PASS (92.24% overall; changed files 100% except SchedulingWorker.Reschedule.cs at 92.85% and the pre-existing/unchanged Pipeline.cs 50% artifact).
- No regression on changed lines: PASS.

Overall verdict: PASS.
