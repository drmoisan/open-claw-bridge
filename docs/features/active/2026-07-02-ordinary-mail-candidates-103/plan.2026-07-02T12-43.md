# ordinary-mail-candidates - Plan

- **Issue:** #103
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T12-43
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature (per `issue.md` metadata)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- C# Standards: `.claude/rules/csharp.md`
- Architecture Boundaries: `.claude/rules/architecture-boundaries.md`
- Quality Tiers: `.claude/rules/quality-tiers.md`
- Authoritative requirements: `docs/features/active/2026-07-02-ordinary-mail-candidates-103/spec.md` (7 acceptance criteria)

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used Throughout This Plan

- `<FEATURE>` = `docs/features/active/2026-07-02-ordinary-mail-candidates-103`
- `<ISO>` = the actual run timestamp in `yyyy-MM-ddTHH-mm` format at artifact creation time.
- Evidence artifacts go under `<FEATURE>/evidence/<kind>/` only. Raw command intermediates (TRX, Cobertura XML, build logs) go under `artifacts/csharp/`. Writing evidence to `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or `artifacts/evidence/` is a policy violation.
- Every command-step evidence artifact MUST contain: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (1-20 lines). Test-step artifacts MUST include numeric line and branch coverage in `Output Summary:`.
- Toolchain commands (C#): `csharpier format .` / `csharpier check .` (global tool 1.3.0; not `dotnet csharpier` — no local tool manifest), `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
- Diff scope is confined to: `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs` (new), `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`, `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`, and the test files named in Phases 1-4. No HostAdapter, MailBridge, or wire-contract changes.
- Test stack: MSTest + FluentAssertions + Moq + CsCheck (established convention of `tests/OpenClaw.Core.Tests`); clock via injected `TimeProvider` / `FakeTimeProvider`; no temp files (in-memory shared-cache SQLite per `CoreCacheRepositoryMessageFieldsTests` convention); 500-line cap on all touched files.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Compliance and Baseline Capture

- [x] [P0-T1] Read repository policies in the required order — `CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md` — and write `<FEATURE>/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read.
  - Acceptance: artifact exists at the named path with all three required fields populated.
- [x] [P0-T2] Capture the formatting baseline by running `csharpier check .` from the repo root and writing `<FEATURE>/evidence/baseline/baseline-csharpier-check.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all four fields; `EXIT_CODE:` records the actual exit code (expected 0 on a clean tree).
- [x] [P0-T3] Capture the build/lint/type baseline by running `dotnet build OpenClaw.MailBridge.sln` and writing `<FEATURE>/evidence/baseline/baseline-dotnet-build.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warnings-as-errors count, analyzer status).
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0`.
- [x] [P0-T4] Capture the test-and-coverage baseline by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, copying raw TRX/Cobertura outputs under `artifacts/csharp/`, and writing `<FEATURE>/evidence/baseline/baseline-dotnet-test.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that includes pass/fail counts and numeric baseline line-coverage and branch-coverage percentages (no placeholders).
  - Acceptance: artifact exists with all four fields; `Output Summary:` contains numeric line % and branch % values; raw coverage files are under `artifacts/csharp/`.

### Phase 1 — Pure Matcher (RelatedEventMatcher)

- [x] [P1-T1] Create `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs`: a pure static class porting master §9.2 `chooseMostLikelyRelatedEvent` per spec D1 — public API exactly `public static SchedulingEventDto? ChooseMostLikelyRelatedEvent(SchedulingMessageDto message, IReadOnlyList<SchedulingEventDto> events)`; `ArgumentNullException` on null arguments; subject tokenization via a `GeneratedRegex` (lowercase, split on `[^a-z0-9]+`, keep tokens length >= 4, distinct) following the `MeetingContextNormalizer.Helpers.cs` pattern; participant emails from `From`, `Sender`, `ToRecipients`, `CcRecipients` normalized via the existing `MeetingContextNormalizer.NormalizeEmail`/`EmailOf`, empties dropped, distinct; event attendee set = union of `RequiredAttendees` + `OptionalAttendees` + `ResourceAttendees` (organizer excluded); score +2 per shared subject token, +3 per shared participant email; return the best event only when score >= 4; tie-break among max-scoring qualifiers by earliest `Start` (null `Start` last) then ordinal `Id` (null treated as empty string). Also expose an `internal` overload or helper returning the selected event together with its score so the Phase 3 fallback can log the score without recomputing.
  - Acceptance: file compiles under `dotnet build OpenClaw.MailBridge.sln` with zero warnings; file <= 500 lines; no I/O, clock, or randomness APIs referenced.
- [x] [P1-T2] Create `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherTests.cs` (MSTest + FluentAssertions) covering, as individually named tests: subject-only match (two tokens, score 4, accepted), attendee-only match (two participants, score 6, accepted), combined token+participant (score 5, accepted), sub-threshold (score 3, returns null), exact-threshold (score 4, accepted), tie-break by earliest `Start` with null-`Start` sorting last, tie-break by ordinal `Id` when `Start` ties, empty event list returns null, null/short-token subject scores 0 from subject, case-insensitive token and email matching, organizer email not counted, and `ArgumentNullException` for null `message` and null `events`.
  - Acceptance: file <= 500 lines; every listed scenario has a dedicated test; all tests in the file pass via `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~RelatedEventMatcherTests"`.
- [x] [P1-T3] Create `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherPropertyTests.cs` (CsCheck, mirroring `MeetingContextNormalizerPropertyTests` conventions) with properties: (a) result is null or the selected event's score >= 4; (b) the selected event is invariant under permutation of the input event list; (c) scoring is case-insensitive (uppercasing subjects/emails does not change the selection); (d) duplicated subject tokens and duplicated participant/attendee emails do not change the score (set semantics).
  - Acceptance: file <= 500 lines; all four properties present and passing via `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~RelatedEventMatcherPropertyTests"`.

### Phase 2 — Candidate Widening (Test-First)

- [x] [P2-T1] [expect-fail] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/CacheSchedulingCandidateSourceTests.cs` (new file; none exists) using an in-memory shared-cache SQLite `CoreCacheRepository` (`Data Source=candidate-source-{Guid.NewGuid():N};Mode=Memory;Cache=Shared`, per `CoreCacheRepositoryMessageFieldsTests` convention; no temp files): seed one `item_kind = 'meeting'` row and one `item_kind = 'mail'` row inside the lookback window plus one row outside it; tests assert (a) both in-window bridge ids are returned (mail alongside meeting), (b) the lookback window excludes the stale row, (c) `Defaults.Limit` caps the result, (d) recency ordering is preserved. Run the file with `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CacheSchedulingCandidateSourceTests"` and record the observed failure of the mail-inclusion test in `<FEATURE>/evidence/regression-testing/expect-fail-candidate-widening.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` naming the failing test(s).
  - Acceptance: file compiles; the mail-inclusion assertion fails against the current `"meeting"` literal; evidence artifact exists with all four fields.
- [x] [P2-T2] Update `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`: change the `ListMessagesAsync` kind literal from `"meeting"` to `"all"` and update the XML summary (currently "meeting-message identifiers") to describe the widened candidate set (meeting plus ordinary mail); make no other changes to lookback, limit, or ordering.
  - Acceptance: diff touches only the kind literal and XML doc text in this file; file <= 500 lines.
- [x] [P2-T3] Rerun `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CacheSchedulingCandidateSourceTests"` and confirm all Phase 2 tests pass, including the previously failing mail-inclusion test.
  - Acceptance: exit code 0 for the filtered run; zero failed tests.

### Phase 3 — CalendarView Fallback (Option + Worker Pipeline)

- [x] [P3-T1] [expect-fail] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs` (new file — `SchedulingWorkerTests.cs` is ~330 lines and adding fallback tests there risks the 500-line cap) with tests that compile against existing types only (mocked `ISchedulingService`, `FakeTimeProvider` pinned to a fixed instant, Moq, MSTest, FluentAssertions): (a) when `GetEventForMessageAsync` returns null, the worker calls `GetCalendarViewAsync(now, now + 14 days)` with bounds exactly equal to `FakeTimeProvider` now and now+14d (default option value); (b) a window event that clears the matcher threshold hydrates the meeting context — observable because the pipeline behaves as event-linked (assert via mock-verified downstream interaction, e.g. the send path receiving event-derived context under `SendEnabled = true`, following existing `SchedulingWorkerTests` observation patterns); (c) an empty window proceeds to message-only triage with no exception thrown. Run the file with `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerFallbackTests"` and record the failures in `<FEATURE>/evidence/regression-testing/expect-fail-worker-fallback.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` naming the failing tests.
  - Acceptance: file compiles against the current production types; tests (a)-(c) fail (fallback not yet implemented); evidence artifact exists with all four fields; file <= 500 lines.
- [x] [P3-T2] Add `public int CalendarViewFallbackDays { get; set; } = 14;` to `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` with XML docs citing master §9.2 (14-day forward window) and documenting that a non-positive value skips the fallback (opt-out).
  - Acceptance: property present with default 14 and XML docs; file <= 500 lines; `dotnet build OpenClaw.MailBridge.sln` succeeds.
- [x] [P3-T3] Update the existing worker test mocks to explicitly stub `GetCalendarViewAsync` returning an empty `IReadOnlyList<SchedulingEventDto>`: `ServiceReturningContext()` (and any per-test service mocks that reach the pipeline past hydration) in `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`, and the service mocks in `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs`. Do not rely on Moq loose-mock default values (spec Constraints & Risks requires explicit stubs).
  - Acceptance: every `Mock<ISchedulingService>` used by worker tests that can reach the fallback path has an explicit `GetCalendarViewAsync` setup; both files remain <= 500 lines. This task MUST complete before P3-T5's solution build/test run (build-order gate: the production change in P3-T4 alters which service methods existing tests exercise).
- [x] [P3-T4] Implement the fallback in `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` inside `ProcessMessageAsync`, immediately after the `GetEventForMessageAsync` call: when the direct lookup returns null and `options.CalendarViewFallbackDays > 0`, compute `now = timeProvider.GetUtcNow()`, fetch `schedulingService.GetCalendarViewAsync(now, now.AddDays(options.CalendarViewFallbackDays), cancellationToken)`, and apply `RelatedEventMatcher` to select the related event (the matched event, or null, flows into the existing `MeetingContextNormalizer.Normalize(MailboxUpn(), message, meetingEvent)` call — no new exception path). Log `LogDebug` when the fallback is attempted (message id, window bounds), `LogInformation` on match (message id, event id, score via the internal matcher overload from P1-T1), `LogDebug` on no-match. Skip the fetch entirely when `CalendarViewFallbackDays <= 0`. No wall-clock APIs; I/O through `ISchedulingService` only.
  - Acceptance: file <= 500 lines; `dotnet build OpenClaw.MailBridge.sln` succeeds with zero warnings; the fallback runs for any direct-lookup miss (not gated on `item_kind`).
- [x] [P3-T5] Add the remaining fallback tests to `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs` (these compile only after P3-T2): (d) `CalendarViewFallbackDays = 0` and `= -1` never call `GetCalendarViewAsync` (mock `Verify` never); (e) all window events scoring below 4 fall through to message-only triage; (f) a failure envelope mapped to an empty window (mock returning empty list, matching `HostAdapterSchedulingService` degradation) proceeds message-only without exception. Then run the full worker suite: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorker"`.
  - Acceptance: filtered run exits 0; the P3-T1 expect-fail tests now pass; file <= 500 lines.

### Phase 4 — Dedupe Regression (Ordinary-Mail Scenario)

- [x] [P4-T1] Extend `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` with an ordinary-mail scenario per AC-6: a plain-mail message (non-meeting `MeetingMessageType`) whose direct event lookup misses, with `SendEnabled = true`, processed across two scheduling cycles; assert the proposal reply is sent exactly once and the sent-action store is consulted/recorded with the unchanged #101 key shape `SentActionKey.Build(mailboxUpn, messageId, SentActionKey.ProposalReply)`.
  - Acceptance: new test passes via `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerDedupeTests"`; file <= 500 lines; no changes to `SentActionKey` or the production send path.

### Phase 5 — Final QA Loop and Coverage Comparison

> Loop rule: if any task P5-T1 through P5-T4 fails or changes files, fix the cause and restart from P5-T1. Tasks P5-T1 through P5-T6 are unconditional; `SKIPPED` is not a valid outcome for any of them. Artifacts below record the final clean pass.

- [x] [P5-T1] Run `csharpier format .` from the repo root and write `<FEATURE>/evidence/qa-gates/final-qa-csharpier-format.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (files reformatted count; 0 on the final pass).
  - Acceptance: artifact exists with all four fields; final pass reformats zero files.
- [x] [P5-T2] Run `csharpier check .` from the repo root and write `<FEATURE>/evidence/qa-gates/final-qa-csharpier-check.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0`.
- [x] [P5-T3] Run `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as lint/type gate) and write `<FEATURE>/evidence/qa-gates/final-qa-dotnet-build.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0`; zero warnings (warnings-as-errors).
- [x] [P5-T4] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (architecture-boundary tests, unit tests, property tests, and dedupe tests all run in this suite), copy raw TRX/Cobertura outputs under `artifacts/csharp/`, and write `<FEATURE>/evidence/qa-gates/final-qa-dotnet-test.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` including pass/fail counts and numeric post-change line-coverage and branch-coverage percentages.
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0`; numeric coverage values recorded (no placeholders).
- [x] [P5-T5] Create the coverage-comparison artifact `<FEATURE>/evidence/qa-gates/coverage-comparison.<ISO>.md` from the P0-T4 baseline and P5-T4 post-change Cobertura data, recording: baseline line/branch %, post-change line/branch %, per-file line/branch coverage for the changed production files (`src/OpenClaw.Core/Agent/RelatedEventMatcher.cs`, `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`, `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`), and explicit verdicts: line >= 85%, branch >= 75%, and no coverage regression on changed lines.
  - Acceptance: artifact exists with `Timestamp:` and all numeric values; every threshold verdict is PASS. If any required numeric value is unavailable or any threshold fails, the plan outcome is remediation-required, not PASS.
- [x] [P5-T6] Verify scope and size caps: run `git diff --name-only main...HEAD` (or the branch base) and confirm the changed set is confined to the files enumerated in Conventions plus the test files from Phases 1-4 and feature-folder evidence/docs; verify every touched `.cs` file is <= 500 lines; write `<FEATURE>/evidence/qa-gates/final-qa-scope-and-caps.<ISO>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing each touched file with its line count.
  - Acceptance: artifact exists with all four fields; no out-of-scope production file appears in the diff; no touched file exceeds 500 lines.

## Test Plan

- Unit (new): `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherTests.cs` — scoring rows (subject-only, attendee-only, combined, sub-threshold, exact-threshold), tie-breaks (Start, then Id), empty/degenerate inputs, normalization, null-arg contracts (AC-2).
- Property (new): `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherPropertyTests.cs` — CsCheck: null-or->=4, permutation invariance, case-insensitivity, set semantics (AC-3).
- Unit (new): `tests/OpenClaw.Core.Tests/Agent/Runtime/CacheSchedulingCandidateSourceTests.cs` — in-memory shared-cache SQLite `CoreCacheRepository`; mail + meeting inclusion, lookback, limit, ordering (AC-1).
- Unit (new): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs` — miss -> window fetch with `FakeTimeProvider` bounds, match hydrates context, no-match message-only, non-positive skip, empty-window degradation (AC-4, AC-5).
- Regression (extended): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` — ordinary-mail send dedupe across cycles, unchanged #101 key shape (AC-6).
- Toolchain/coverage (AC-7): baseline artifacts `<FEATURE>/evidence/baseline/baseline-*.<ISO>.md`; final-QA artifacts `<FEATURE>/evidence/qa-gates/final-qa-*.<ISO>.md`; comparison artifact `<FEATURE>/evidence/qa-gates/coverage-comparison.<ISO>.md`; expect-fail evidence `<FEATURE>/evidence/regression-testing/expect-fail-*.<ISO>.md`; raw intermediates in `artifacts/csharp/`.

## Open Questions / Notes

- Test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but `tests/OpenClaw.Core.Tests` is established on MSTest + Moq + FluentAssertions + CsCheck and the spec (Constraints & Risks) mandates that stack; this plan follows the repository's established convention.
- Match-score logging: the spec's normative public API returns only `SchedulingEventDto?`; the score named in the spec's logging note is obtained via an internal matcher overload (P1-T1) rather than widening the public surface.
- `CacheSchedulingCandidateSource` currently uses `DateTimeOffset.UtcNow` for the lookback anchor; this is pre-existing behavior outside the AC set and is intentionally not touched (diff-scope confinement). The new candidate-source tests must therefore seed rows relative to real now with generous margins rather than a fake clock.
- No dependency changes: CsCheck 4.7.0, Moq 4.20.72, MSTest 3.6.4, FluentAssertions 6.12.0, `Microsoft.Extensions.TimeProvider.Testing` are already referenced.
