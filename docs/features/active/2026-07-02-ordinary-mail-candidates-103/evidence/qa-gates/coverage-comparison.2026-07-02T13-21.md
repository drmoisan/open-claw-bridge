# Coverage Comparison — Baseline vs Post-Change (#103)

Timestamp: 2026-07-02T13-21
Sources:
- Baseline (P0-T4): `artifacts/csharp/baseline-2026-07-02T13-02/coverage.{core,mailbridge,hostadapter}.cobertura.xml`
- Post-change (P5-T4): `artifacts/csharp/final-2026-07-02T13-21/coverage.{core,mailbridge,hostadapter}.cobertura.xml`

## Pooled (all three test projects)

| Metric | Baseline | Post-change | Delta |
|---|---|---|---|
| Line coverage | 90.63% (4207/4642) | 90.81% (4298/4733) | +0.18 pp |
| Branch coverage | 80.25% (947/1180) | 80.62% (990/1228) | +0.37 pp |

## Core package (OpenClaw.Core, the changed assembly)

| Metric | Baseline | Post-change | Delta |
|---|---|---|---|
| Line coverage | 89.97% (1561/1735) | 90.47% (1652/1826) | +0.50 pp |
| Branch coverage | 79.29% (360/454) | 80.27% (403/502) | +0.98 pp |

## Changed production files (post-change, Core Cobertura)

| File | Line coverage | Branch coverage | Notes |
|---|---|---|---|
| `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs` (new) | 100.00% (91/91) | 89.58% (43/48) | New pure matcher; every instrumented line covered by 14 unit + 4 property tests. |
| `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs` | 100.00% (5/5) | n/a (no branches) | Identical to baseline (100%); the changed kind-literal line is covered by 4 new tests. |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` | 100.00% (20/20 instrumented) | 50.00% (2/4 instrumented) | Identical instrumented figures to baseline (20/20, 2/4 — the residual branch miss is the pre-existing `MailboxUpn` ternary, untouched by this change). The new fallback code is `async` and therefore compiled into a CompilerGenerated state machine that the pre-existing runsettings instrumentation exclusion omits from the denominator (same treatment as every other async pipeline member at baseline). Its behavior is directly verified by 7 `SchedulingWorkerFallbackTests` (window bounds, match hydration, no-match, non-positive opt-out x2, sub-threshold, failure degradation). |
| `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` | not instrumented | n/a | Auto-property accessors are CompilerGenerated and excluded from instrumentation at baseline and post-change alike (file absent from both reports). The new `CalendarViewFallbackDays` default (14) and opt-out are verified behaviorally by `RunCycle_LookupMiss_FetchesCalendarViewFromNowToNowPlusFourteenDays` and `RunCycle_NonPositiveFallbackDays_NeverFetchesCalendarView`. |

## Threshold Verdicts

- Line coverage >= 85%: **PASS** (pooled 90.81%; Core package 90.47%)
- Branch coverage >= 75%: **PASS** (pooled 80.62%; Core package 80.27%)
- No coverage regression on changed lines: **PASS** — every changed file's instrumented coverage is equal to or better than baseline (candidate source 100% -> 100%; pipeline 100%/50% -> 100%/50% with the sole branch miss pre-existing and untouched; matcher is new at 100% line / 89.58% branch); pooled and Core-package coverage both increased.
