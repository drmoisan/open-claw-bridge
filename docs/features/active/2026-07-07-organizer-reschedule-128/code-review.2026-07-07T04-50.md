# Code Review — organizer-reschedule (Issue #128)

- **Feature folder:** `docs/features/active/2026-07-07-organizer-reschedule-128/`
- **Branch:** `feature/organizer-reschedule-128` (HEAD `294d976`)
- **Base:** `epic/openclaw-vision-integration` (merge-base `68a516f`)
- **Reviewed:** 2026-07-07T04-50 UTC

## Executive Summary

The branch delivers the first real calendar-write RPC (organizer reschedule as Graph
`PATCH /users/{p}/events/{id}`) in a single commit: two new production files, eight additive
modifications, six new test files, and four mechanical test-builder updates. The implementation is
disciplined: the worker orchestration follows the spec's exact six-step order with the move-guard
consulted before the flag gate; the adapter body structurally cannot carry any property other than
`start`/`end` (the online-meeting-blob guardrail); the local Stage-0 adapter fails closed with no
I/O; and every no-write path is asserted with explicit `Times.Never` verifications. The service
seam deliberately mirrors the existing `SendMailAsync` shape, and the send path's persisted
`ActingFlags` string is proven byte-identical to pre-F18 both by an unchanged-file diff and by a
directed dual-path test. Toolchain is clean end to end (CSharpier, zero analyzer/nullable
warnings, 1340 tests green), and independent dual-mode coverage measurement confirms the new
orchestration body at 100% line / 95.83% branch under full instrumentation.

Zero blocking findings. Two Minor findings (an untested error-detail fallback in the new service
method, replicated from the send path's existing gap; a zero-margin per-file branch figure) and
two Info observations are recorded below. None require remediation before merge.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs | RescheduleEventAsync, lines 168-176 | The failure-envelope fallback arms are measured-and-uncovered: line 168 pattern (3/4 conditions), lines 172-173 `Error?.Code ?? "UNKNOWN_ERROR"` / `Error?.Message ?? "...no error detail."` (2/4 each). Tests exercise Ok:true and Ok:false-with-populated-ApiError only; a failure envelope carrying a null `Error` (or null `Message`) never executes the fallback strings. | Add one directed test returning `new ApiEnvelope<EventDto>(false, null, Meta, null)` and assert the thrown message contains `UNKNOWN_ERROR` and the no-error-detail text. | Uncovered branches usually point at a real untested scenario; here the scenario is a malformed adapter envelope, which is exactly the condition the fallback exists for. The identical gap pre-exists in `SendMailAsync` (lines 136/140/141, same partial pattern at baseline), so this is a replicated convention rather than a regression — hence Minor, not Blocking. | Plain-mode cobertura: partial-branch lines [(168,(3,4)),(172,(2,4)),(173,(2,4))]; `HostAdapterSchedulingServiceRescheduleTests.cs` (no null-Error case) |
| Minor | src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs | whole file | Plain-mode per-file branch coverage lands at 75.93% (41/54) — above the 75% gate with under one percentage point of margin. All misses/partials are the pre-existing send-path/lookup arms plus the Finding-1 fallback arms. | The Finding-1 test alone lifts the file to ~81%; consider also covering the pre-existing `SendMailAsync` null-Error arms in the same change. | Zero-margin files deserve a named warning (established practice on the #115 review): any future branch touching this file can silently drop it below the gate. | This review's plain-mode cobertura parse |
| Info | src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs | ComputeRescheduleIntent, line 52 | One condition arm of `meetingEvent.Start is not { } originalStart \|\| meetingEvent.End is not { } originalEnd` is not distinctly hit (5/6): both the edge-test `missing-times` row and the property test null Start and End together, so the Start-present/End-null (mixed) combination never takes its own path. File branch is 95.83% plain / 92.86% settings — well above the gate. | Optional: split the `missing-times` DataRow into `start-only-null` and `end-only-null` rows. | The fail-closed outcome is identical for every combination, so behavioral risk is negligible; recorded for branch-arm completeness. | Plain-mode cobertura: partial-branch line [(52,(5,6))]; `SchedulingWorkerRescheduleEdgeTests.NoIntentFixture` |
| Info | mailbridge.runsettings (pre-existing, unchanged) | ExcludeByAttribute | Settings-mode coverage excludes compiler-generated async state machines, so the executor's committed per-file settings-mode figures (e.g., HostAdapterSchedulingService.cs "100% branch") under-count the new async bodies. The executor's evidence is accurate for the mode it names; this review's plain-mode run provides the full-instrumentation attestation (new file 100% line / 95.83% branch). | None for this branch. Longstanding recommendation stands: note the dual-mode practice in the runsettings comment or evidence template. | Known repository behavior (accepted disposition since the #99 review; dual-mode practice established on #120). Not a finding against this branch. | Settings-mode vs plain-mode instrumented-line counts (38 vs 191 for SchedulingWorker.Reschedule.cs) |

## Detailed Review Notes

### Design and separation of concerns

- The pure decision surface (`ComputeRescheduleIntent`, `BuildRescheduleActingFlags`) is extracted
  as internal static helpers, property-tested, and kept free of I/O, clock reads, and randomness —
  matching the simplicity-first and pure-logic-separation priorities in
  `general-code-change.md`.
- The `SchedulingWorker.Reschedule.cs` partial holds the entire orchestration; the pipeline change
  in `SchedulingWorker.Pipeline.cs` is a minimal delegation (thread `meetingEvent`, replace the
  old `!CalendarWriteEnabled` stub with the `EvaluateRescheduleAsync` call). Comments map each
  step to the spec's numbered order.
- `HostAdapterSchedulingService.RescheduleEventAsync` deliberately mirrors `SendMailAsync`
  (guard-clause, delegate, throw on non-Ok envelope, return `Task` not the DTO) — consistent with
  the documented D6 seam decision and the rejected-alternatives list in the spec.
- `BuildRescheduleActingFlags` is a separate snapshot builder; the existing `BuildActingFlags` is
  untouched (`SchedulingWorker.Audit.cs` has an empty diff), and edge test (h) proves the send
  path persists the pre-F18 string while a reschedule dry-run runs in the same evaluation.

### Error handling and fail-closed behavior

- The write failure path audits `reschedule_failed` durably before rethrowing
  (`catch ... when (exception is not OperationCanceledException)`), records no `series_moves` or
  dedupe row, and relies on the pre-existing per-message isolation to keep the cycle alive —
  mirroring the send-failure ordering. OperationCanceledException is correctly excluded from the
  audit-and-rethrow filter.
- The local adapter's synthesized `NOT_SUPPORTED` envelope is non-retryable and performs provably
  zero I/O and zero token acquisition (throwing `TokenReader` + counting handler in tests) —
  correctly distinguishing a permanent capability gap from a transient transport failure.
- The Graph adapter inherits retry/backoff and the D5 error matrix from the shared
  `GraphRequestExecutor` without duplication; the contract suite samples
  400/403(+`error.code` passthrough)/404/429-exhaustion/unparseable-2xx/mapping-gap.

### Determinism

- All time flows through the injected `TimeProvider`; tests use `FakeTimeProvider` exclusively.
  The 429-exhaustion test advances simulated time in a bounded loop (safety counter, no real
  waits). Zero banned-API matches (`Task.Delay`, `Thread.Sleep`, `DateTime.UtcNow`,
  `Random.Shared`) in added code. The correlation id is a GUID per evaluation, forwarded verbatim
  to the adapter as `client-request-id` (asserted at the service seam and at the wire).

### Naming, docs, and conventions

- XML docs on every new public/internal member, including the rationale-bearing remarks on
  `IHostAdapterClient.UpdateEventTimesAsync` (narrow update scope, fail-closed local backend).
- File-scoped namespaces, `PascalCase`/`camelCase` conventions, `Async` suffixes, and the
  MSTest + FluentAssertions + Moq stack all match the repository's established convention (the
  repo-wide convention differs from the xUnit/NSubstitute wording in `csharp.md`; this branch
  follows the actual codebase convention, consistent with every prior accepted feature).
- All 16 touched code files are under the 500-line cap (max 412); test files were deliberately
  split (`SchedulingWorkerRescheduleEdgeTests`, `HostAdapterHttpClientRescheduleTests`) to stay
  under it.

### Test quality

- Scenario completeness for the worker path: four-row gate truth table, guard block with flags on,
  dry-run detail (four time columns, duration preservation, reschedule ActingFlags snapshot),
  success ordering (audit -> move -> dedupe, pre-move occurrence start recorded), failure
  fail-closed, dedupe skip, five distinct no-intent rows, and send-path isolation.
- The contract suite asserts the absent-property guardrail structurally via `JsonDocument`
  (`body`/`subject`/`location`/`attendees` absent), the exact seconds-precision UTC rendering, and
  the escaped principal route.
- Property tests print failing seeds via CsCheck `Sample`, satisfying the seeded-RNG requirement.
- Typed-Python review: N/A — no Python files changed on this branch.

## Verdict

No blocking findings. Ready to merge from a code-quality standpoint; the two Minor findings are
recommended follow-ups, not gates.
