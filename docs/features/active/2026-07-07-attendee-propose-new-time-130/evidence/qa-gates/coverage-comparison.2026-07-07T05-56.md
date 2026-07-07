# QA Gate — Coverage Comparison (baseline vs post-change vs new/changed code) (F19, #130)

Timestamp: 2026-07-07T05-56
Command: compare `evidence/baseline/csharp-test-coverage.2026-07-07T05-56.md` (P0-T5) against `evidence/qa-gates/csharp-test-coverage.2026-07-07T05-56.md` (P5-T5); per-file rates from the OpenClaw.Core Cobertura report
EXIT_CODE: 0

## Baseline coverage (P0-T5)

- OpenClaw.Core package: line 99.27%, branch 92.24%.

## Post-change coverage (P5-T5)

- OpenClaw.Core package: line 99.29%, branch 92.28%.
- Delta: line +0.02 pts, branch +0.04 pts. No regression at the package level (coverage increased).

## New / changed-code coverage

| File | Kind | Line | Branch |
|---|---|---|---|
| src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs | new | 100% | 100% |
| src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs | new | 100% | 93.75% |
| src/OpenClaw.Core/HostAdapterHttpClient.cs (new method) | modified | 100% | 100% |
| src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs (new method) | modified | 100% | 100% |
| src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs (1 added await) | modified | 100% (added line) | n/a (no new branch) |
| src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs | modified | interface-only (XML docs + signature; no executable body) | n/a |
| src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs | modified | interface-only (XML docs + signature; no executable body) | n/a |
| src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs | modified | const strings (no executable body) | n/a |
| src/OpenClaw.Core/Agent/SentActionKey.cs | modified | const string (no executable body) | n/a |

## Threshold verdict

- Line coverage >= 85%: PASS (package 99.29%; every new/changed executable file 100% on changed lines).
- Branch coverage >= 75%: PASS (package 92.28%; new files 100% and 93.75%).
- No regression on changed lines: PASS (every added executable statement is exercised; package coverage increased vs baseline).

Verdict: PASS.
