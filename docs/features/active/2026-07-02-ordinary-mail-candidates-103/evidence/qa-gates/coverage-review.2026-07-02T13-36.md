# Reviewer Coverage Re-Run — Feature Review (#103)

Timestamp: 2026-07-02T13-36
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/coverage-review"`
EXIT_CODE: 0
Branch head: `0f346d5e74a5543526ba8e642fe684b73475dba3`; merge base: `3dae644d98dcb564767002a51503e6b9944e4eab` (origin/main)

## Test Results

- OpenClaw.HostAdapter.Tests: 100 passed, 0 failed, 0 skipped
- OpenClaw.Core.Tests: 284 passed, 0 failed, 0 skipped
- OpenClaw.MailBridge.Tests: 347 passed, 0 failed, 5 skipped (environment-gated COM/publish tests, same as baseline)
- Suite total: 731 passed, 0 failed, 5 skipped (736 total)

## Independently Parsed Coverage (fresh cobertura, this run)

Reports: three `coverage.cobertura.xml` files under `evidence/qa-gates/coverage-review/{2300629f...,4f1a1449...,75d17a89...}/`.

Pooled (per-line max across reports, deduped duplicate class entries per partial-class file):

- Line: 4298/4733 = 90.81% (baseline 4207/4642 = 90.63%; +0.18 pp)
- Branch: 990/1228 = 80.62% (baseline 947/1180 = 80.25%; +0.37 pp)

These pooled figures are identical to the executor evidence in `evidence/qa-gates/coverage-comparison.2026-07-02T13-21.md` — the match is the verification.

## Per-Changed-File (line AND branch, reviewer-parsed)

| File | Line | Branch | Notes |
|---|---|---|---|
| `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs` (new) | 100.00% (91/91) | 89.58% (43/48) | Partial conditions at lines 180, 186, 189 are nullable-lifted compiler branches in the tie-break (`Start` lifted comparison, `Id ?? string.Empty` null arms). Gate: PASS (>= 85 / >= 75). |
| `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs` | 100.00% (5/5) | n/a (no branch points) | Identical to baseline (100%). No regression. |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` | 100.00% (20/20 instrumented) | 50.00% (2/4 instrumented) | Identical to baseline (20/20, 2/4). The two partial conditions (post-change lines 233, 240) are the pre-existing `MailboxUpn` and `BuildProposalReply` ternaries — verified by inspecting baseline lines 170/177, which are the same members shifted by the +63 added lines. The new fallback code is `async` and excluded from instrumentation by the pre-existing runsettings `CompilerGeneratedAttribute` filter; it is behaviorally verified by the 7 `SchedulingWorkerFallbackTests` cases plus fail-before evidence (`expect-fail-worker-fallback.2026-07-02T13-11.md`, EXIT 1). |
| `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` | not instrumented | n/a | Auto-property accessors are CompilerGenerated; file absent from baseline and post-change reports alike. The new `CalendarViewFallbackDays` default and opt-out are behaviorally verified by `RunCycle_LookupMiss_FetchesCalendarViewFromNowToNowPlusFourteenDays` and `RunCycle_NonPositiveFallbackDays_NeverFetchesCalendarView(0,-1)`. |

## Baseline Cross-Check

Executor baseline cobertura at `artifacts/csharp/baseline-2026-07-02T13-02/` parsed with the same tool: pooled 90.63% line (4207/4642), 80.25% branch (947/1180); `SchedulingWorker.Pipeline.cs` 20/20 line, 2/4 branch; `RelatedEventMatcher.cs` not present (new file). Confirms no regression on changed lines.
