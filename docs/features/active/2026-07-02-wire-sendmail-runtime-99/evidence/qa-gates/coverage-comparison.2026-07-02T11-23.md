# Coverage Comparison — Baseline vs Post-Change vs Changed Lines (P5-T7)

Timestamp: 2026-07-02T11-23
Command: Cobertura analysis of `artifacts/csharp/baseline/**/coverage.cobertura.xml` (P0-T4) and `artifacts/csharp/final/**/coverage.cobertura.xml` (P5-T5); per-line hit extraction for the two changed runtime files.
EXIT_CODE: 0
Output Summary: All three numeric sections pass — thresholds met, no regression, changed instrumented lines 100% covered. Verdict: PASS.

## 1. Baseline coverage (P0-T4, `baseline-test-coverage.2026-07-02T10-54.md`)

| Scope | Line | Branch |
|---|---|---|
| Solution-wide pooled | 4149/4584 = 90.51% | 929/1162 = 79.95% |
| OpenClaw.Core package | 1419/1439 = 98.61% | 342/373 = 91.69% |

## 2. Post-change coverage (P5-T5, `final-qa-test-coverage.2026-07-02T11-20.md`)

| Scope | Line | Branch | Threshold verdict |
|---|---|---|---|
| Solution-wide pooled | 4174/4609 = 90.56% | 935/1168 = 80.05% | line >= 85% PASS; branch >= 75% PASS |
| OpenClaw.Core package | 1444/1464 = 98.63% | 348/379 = 91.82% | line >= 85% PASS; branch >= 75% PASS |

No-regression verdict: pooled line +0.05pp, pooled branch +0.10pp; Core line +0.02pp, Core branch +0.13pp — no regression relative to baseline. PASS.

## 3. Changed-line coverage

- `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`: 130/135 instrumented file lines covered. The 5 uncovered lines (184, 185, 211, 213, 236) are pre-existing untouched paths (JSON null-parse return, `MapSensitivity` "personal"/"confidential" arms, `MapImportance` "high" arm) that were equally uncovered at baseline. Every changed/new line (`MapSendMailRequest`, `MapRecipients`, alias and doc lines) is covered — changed-line coverage 100%. PASS.
- `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`: 4/4 instrumented lines covered (100%). The new `SendMailAsync` body is compiled into a `[CompilerGenerated]` async state machine, which the repo's pre-existing `mailbridge.runsettings` setting `ExcludeByAttribute=CompilerGeneratedAttribute` excludes from instrumentation — uniformly for every async method in the solution (at baseline the same class reported 9/9 because the throwing `SendMailAsync` was non-async). Behavioral coverage of the new body is evidenced by the five delegation tests (success, envelope failure, exception propagation, cancellation, request mapping) in `pass-after-sendmail-delegation.2026-07-02T11-08.md`. PASS.
- `src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs`: doc-comment-only change; no executable lines changed. Not applicable.

Changed-line verdict: all instrumented changed production lines covered — PASS.

## Overall verdict

PASS — all required numeric values present; line >= 85% and branch >= 75% (uniform T1-T4) hold solution-wide and for the T1 `OpenClaw.Core` package; no regression against baseline; changed-line coverage complete.
