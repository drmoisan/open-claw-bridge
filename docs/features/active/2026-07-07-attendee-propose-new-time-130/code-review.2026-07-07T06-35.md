# Code Review — attendee-propose-new-time (Issue #130)

- **Feature folder:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/`
- **Branch:** `feature/attendee-propose-new-time-130` (HEAD `1633a6c`)
- **Base:** `epic/openclaw-vision-integration` (merge-base `273c7df`)
- **Reviewed:** 2026-07-07T06-35 UTC

## Executive Summary

The branch delivers the attendee-side calendar-write RPC (propose-new-time as Graph
`POST /users/{p}/events/{id}/tentativelyAccept`) in a single commit — the mirror of the F18
organizer path — as two new production files, seven additive modifications, and six new test files.
The implementation is disciplined and matches the spec precisely: the worker orchestration follows
the spec's exact five-step order (intent -> flag gate -> dedupe -> write -> audit-first
bookkeeping) with **no** move-guard step, **no** blocked result code, and **no**
`series_moves`/`ISeriesMoveHistory` interaction on any branch; the adapter request body carries
exactly `sendResponse` and `proposedNewTime` and structurally cannot rewrite the event; the local
Stage-0 adapter fails closed with a synthesized non-retryable `NOT_SUPPORTED` envelope and zero
I/O; and every no-write path is asserted with explicit `Times.Never` verifications on the write,
`RecordMoveAsync`, and the dedupe record. Mutual exclusivity with F18 is enforced by the intent
predicates (`IsOrganizer` true vs false) rather than pipeline branching and is asserted in both
directions. The propose path uses its own `BuildProposeNewTimeActingFlags` snapshot, leaving the
send path's and the F18 reschedule path's persisted `ActingFlags` strings byte-identical to
pre-F19 (proven by a dedicated multi-path test). Notably, no pre-existing test file required
modification — the loose Moq mocks absorbed the new interface members automatically.

Toolchain is clean end to end (CSharpier 1.3.0, zero analyzer/nullable warnings under
`TreatWarningsAsErrors`, 930 Core tests green in this review). Independent dual-mode coverage
measurement confirms the new async orchestration body at 100% line / 93.75% branch under full
(plain-mode) instrumentation — the two new Graph/HTTP members and the new service-seam method are
synchronous `Task`-returning methods and are fully instrumented in both modes.

Zero blocking findings. One Minor finding (a coverage-evidence disclosure gap that this review
resolved independently) and two Info observations are recorded below. None require remediation
before merge.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | docs/features/active/2026-07-07-attendee-propose-new-time-130/evidence/qa-gates/coverage-comparison.2026-07-07T05-56.md | new/changed-code table | The executor's committed coverage evidence reports `SchedulingWorker.ProposeNewTime.cs` at 100% line / 93.75% branch and `HostAdapterSchedulingService.cs` (new method) at 100% without disclosing that under `--settings mailbridge.runsettings` the async `EvaluateProposeNewTimeAsync` (124-254) and the async `ProposeNewMeetingTimeAsync` bodies are excluded from instrumentation, so the settings-mode figure attests only the sync sub-portion. | State the async-exclusion explicitly in the coverage evidence and/or run dual-mode (settings for baseline comparability, plain for new-code attestation), as established on the #120/#128 reviews. | Non-disclosure of the CompilerGenerated async-exclusion recurs (pattern seen on #113/#115). The underlying coverage is genuinely complete — this review's plain-mode run measures the full async body at 100% line — so this is an evidence-hygiene gap, not a coverage gap; hence Minor, not Blocking. | This review's settings-mode parse (worker file instruments only 47-115) vs plain-mode parse (24-254 fully hit, 145/145) |
| Info | src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs | ComputeProposeNewTimeIntent, line 56 | One condition arm of `meetingEvent.Start is not { } originalStart \|\| meetingEvent.End is not { } originalEnd` is not distinctly hit (file branch 93.75%, 15/16): both the `missing-times` edge row and the property test null `Start` and `End` together, so the Start-present/End-null (mixed) combination never takes its own path. | Optional: split the `missing-times` case into `start-only-null` and `end-only-null`. | The fail-closed outcome is identical for every combination, so behavioral risk is negligible; recorded for branch-arm completeness. File branch (93.75% plain / 93.75% settings) is well above the 75% gate. This mirrors the accepted Info finding on the F18 sibling (#128, line 52). | Plain-mode cobertura branch-rate 0.9375 for the file; `SchedulingWorkerProposeNewTimeIntentPropertyTests` / edge `NoIntent` matrix |
| Info | mailbridge.runsettings (pre-existing, unchanged) | ExcludeByAttribute | Settings-mode coverage excludes compiler-generated async state machines, so the settings-mode per-file figures under-count new async bodies repository-wide. Not a finding against this branch's code; the pre-existing runsettings behavior is the root cause of the Minor above. | Longstanding recommendation: note the dual-mode practice in the runsettings comment or the evidence template so executors disclose it by default. | Known repository behavior (accepted disposition since the #99 review; dual-mode practice established on #120). | Settings-mode vs plain-mode instrumented-line counts (sync-only 47-115 vs full 24-254 for the worker file) |

## Detailed Review Notes

### Design and separation of concerns

- The pure decision surface (`ComputeProposeNewTimeIntent`, `BuildProposeNewTimeActingFlags`) is
  extracted as internal static helpers, property-tested, and kept free of I/O, clock reads, and
  randomness — matching the simplicity-first and pure-logic-separation priorities in
  `general-code-change.md`. `ComputeProposeNewTimeIntent` guards its two reference arguments with
  `ArgumentNullException.ThrowIfNull` and returns `null` for every missing precondition, so the
  caller's "no intent -> silent return" is unambiguous.
- The `SchedulingWorker.ProposeNewTime.cs` partial holds the entire orchestration; the pipeline
  change in `SchedulingWorker.Pipeline.cs` is a minimal single-call delegation immediately after
  the F18 `EvaluateRescheduleAsync` call, with a comment mapping exclusivity to the intent
  predicates rather than pipeline branching. Comments map each step to the spec's numbered order.
- `HostAdapterSchedulingService.ProposeNewMeetingTimeAsync` deliberately mirrors
  `RescheduleEventAsync`/`SendMailAsync` (guard-clause the id with
  `ArgumentException.ThrowIfNullOrWhiteSpace`, delegate, throw `InvalidOperationException` on a
  non-`Ok` envelope, return `Task` not a DTO) — consistent with the documented D6 seam decision.
- `BuildProposeNewTimeActingFlags` is a separate snapshot builder; neither `BuildActingFlags` nor
  `BuildRescheduleActingFlags` is widened, and edge test
  `SendAndReschedulePaths_PersistUnmodifiedActingFlags_AfterF19` proves both the send path and the
  F18 reschedule path persist their pre-F19 flag strings while a propose dry-run runs.
- The `IHostAdapterClient` member 11 and the local `NOT_SUPPORTED` fail-closed choice are
  well-documented in XML remarks, including the rationale that a real local POST would 404 and
  misreport a permanent capability gap as a transient `TRANSPORT_FAILURE`.

### Error handling and fail-closed behavior

- The write failure path audits `propose_new_time_failed` durably before rethrowing
  (`catch (Exception exception) when (exception is not OperationCanceledException)`), records no
  dedupe row and makes no `RecordMoveAsync` call, and relies on the pre-existing per-message
  isolation to keep the cycle alive. `OperationCanceledException` is correctly excluded from the
  audit-and-rethrow filter so cancellation propagates cleanly without a spurious failure audit.
- The local adapter's synthesized `NOT_SUPPORTED` envelope is non-retryable and performs provably
  zero I/O and zero token acquisition (a counting handler and `handlerInvocations == 0` assertion
  in `HostAdapterHttpClientProposeNewTimeTests`), correctly distinguishing a permanent capability
  gap from a transient transport failure. A null `requestId` self-generates a GUID while still
  returning `NOT_SUPPORTED`.
- The Graph adapter inherits bearer auth, `client-request-id` propagation, retry/backoff, and the
  D5 error matrix from the shared `GraphRequestExecutor` without duplication; the contract suite
  samples the D5 non-retryable codes and 429 exhaustion under `FakeTimeProvider`.

### Determinism

- All time flows through the injected `TimeProvider`; tests use `FakeTimeProvider` exclusively.
  The 429-exhaustion contract test advances simulated time (three attempts to exhaustion,
  Retry-After precedence) with no real waits. Zero banned-API matches (`Task.Delay`,
  `Thread.Sleep`, `DateTime.UtcNow`, `Random.Shared`) in added code. One correlation-id GUID per
  evaluation is forwarded verbatim to the adapter as `client-request-id` (asserted at the service
  seam by `ProposeNewMeetingTimeAsync_ForwardsCorrelationIdAsRequestId` and at the wire).

### Naming, docs, and conventions

- XML docs on every new public/internal member, including the rationale-bearing remarks on
  `IHostAdapterClient.ProposeNewMeetingTimeAsync` (two-property body, 202-no-body semantics,
  fail-closed local backend).
- File-scoped namespaces, `PascalCase`/`camelCase` conventions, `Async` suffixes, and the
  MSTest + FluentAssertions + Moq stack all match the repository's established convention (which
  differs from the xUnit/NSubstitute wording in `csharp.md`; this branch follows the actual
  codebase convention, consistent with every prior accepted feature).
- All 15 touched code files are under the 500-line cap (max production 255, max test 352). The
  worker tests were deliberately split into `...Tests.cs` and `...EdgeTests.cs` siblings to stay
  under the cap, matching the planner's pre-existing sibling-add convention. No pre-existing test
  file was modified.

### Test quality

- Scenario completeness for the worker path: four-row gate truth table (three disabled DataRows +
  both-on writes exactly once), dry-run detail (four time columns, duration preservation, propose
  ActingFlags snapshot), success ordering (audit `proposed_new_time` -> dedupe record, no move),
  failure fail-closed, dedupe skip, a six-case eligibility fail-closed matrix (null-event,
  organizer-owned, proposals-disallowed, missing-times, empty-id, zero-slots), and mutual
  exclusivity asserted in both directions.
- The contract suite asserts the absent-property guardrail structurally via `JsonDocument` (top
  level exactly `sendResponse` + `proposedNewTime`, no `comment`, no top-level
  `start`/`end`/`body`/`subject`/`attendees`), the exact seconds-precision UTC rendering, the
  escaped principal/event route, and the 202-empty-body -> `ok: true, data: null` mapping.
- Property tests print failing seeds via CsCheck `Sample`, satisfying the seeded-RNG determinism
  requirement.
- Typed-Python review: N/A — no Python files changed on this branch.

## Verdict

No blocking findings. Ready to merge from a code-quality standpoint; the single Minor finding is an
evidence-disclosure recommendation (already resolved by this review's independent plain-mode
attestation), not a gate.
