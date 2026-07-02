# ordinary-mail-candidates (Issue #103)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/ordinary-mail-candidates/ (Issue #103)

- Issue: #103
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/103
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

The master specification defines two input classes (`docs/open-claw-approach.master.md` §2.2): formal meeting traffic and **ordinary scheduling mail** ("can we move this?", "what works next week?"). Today only the first class is handled: `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` (`src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`) selects only `item_kind = 'meeting'` messages, and `HostAdapterSchedulingService.GetEventForMessageAsync` does a direct event-by-id lookup returning null on a miss — there is no `calendarView`-window fallback with the subject/attendee-overlap matching heuristic the master specifies (§9.2 `chooseMostLikelyRelatedEvent`, §9.3 step 3). Ordinary scheduling threads are never triaged. Identified as gap F7 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

- Extend the candidate source so ordinary (non-meeting) messages become scheduling candidates alongside formal meeting traffic.
- Add a pure, deterministic event-matching helper in `OpenClaw.Core.Agent` porting the master's §9.2 `chooseMostLikelyRelatedEvent`: tokenize the message subject (length >= 4 tokens), collect message participants, score candidate events by subject-token overlap (+2 per token) and attendee-email overlap (+3 per person), and return the best match only when the score is >= 4; ties resolve deterministically (stable ordering).
- When the direct event-by-id lookup misses, the runtime queries a bounded `calendarView` window (default 14 days forward, matching §9.2) through the existing Graph-shaped surface and applies the matcher to select the most likely related event; no match means the triage proceeds with a message-only context (which the existing `TriageEngine` already supports — e.g. `IGNORE`/`HUMAN_APPROVAL` outcomes).
- All scoring/selection logic is pure and unit-testable; only the window fetch touches the client seam.

## Acceptance Criteria

- [x] `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` returns ordinary (`item_kind = 'mail'`) messages alongside meeting messages (repository kind `"all"`), preserving the existing lookback window, limit, and ordering; formal meeting-traffic handling is unchanged; covered by a new unit test against an in-memory SQLite `CoreCacheRepository`.
- [x] A new pure static `RelatedEventMatcher` in `OpenClaw.Core.Agent` ports §9.2 `chooseMostLikelyRelatedEvent` exactly: distinct lowercase subject tokens (split on non-alphanumeric runs, length >= 4) score +2 per token also present in the event subject; distinct normalized participant emails (from, sender, to, cc) score +3 per email present in the event attendee set (required + optional + resource); the best event is returned only when its score >= 4; ties resolve deterministically by earliest `Start` (null last) then ordinal `Id`; unit tests cover subject-only, attendee-only, combined, sub-threshold, empty-input, and tie-break rows.
- [x] CsCheck property/invariant tests cover the matcher: the result is null or scores >= 4; the selected event is invariant under permutation of the input event list; scoring is case-insensitive; duplicate tokens/emails do not double-count (set semantics).
- [x] When `GetEventForMessageAsync` returns null, `SchedulingWorker.ProcessMessageAsync` fetches `ISchedulingService.GetCalendarViewAsync(now, now + CalendarViewFallbackDays)` and applies the matcher; `CalendarViewFallbackDays` is a new `AgentPolicyOptions` value defaulting to 14; `now` comes from the injected `TimeProvider` (no wall-clock APIs); a non-positive configured value skips the fallback; verified with mocked `ISchedulingService` and `FakeTimeProvider`.
- [x] No-match (empty window, all scores < 4, or a failure envelope mapped to an empty window) falls through to message-only triage (`MeetingContextNormalizer.Normalize(mailboxUpn, message, null)`) without a new exception path; a matched event hydrates the context through the same `Normalize` call formal traffic uses.
- [x] Widened candidates are re-listed every cycle (no candidate cursor exists); outbound proposal sends remain deduplicated across cycles by the #101 sent-action store with the unchanged key shape — verified by extending the existing dedupe tests with an ordinary-mail scenario.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all touched files remain <= 500 lines.

## Constraints & Risks

- Widening the candidate set increases processed volume; the dedupe store (#101) and `SendEnabled` gate bound the blast radius. Verified during spec authoring: no candidate cursor exists — the source re-lists the lookback window every cycle for meeting traffic today, so widening changes volume, not reprocessing semantics (see spec.md, Design Decisions).
- Matching is heuristic by design; the master mandates the deterministic decision come from code, which this preserves (fixed weights/threshold).
- MSTest + FluentAssertions + Moq + CsCheck; no temp files; 500-line cap; pure logic in `OpenClaw.Core.Agent`, I/O in Runtime seam only.

## Test Conditions to Consider

- [ ] Unit: matcher scoring rows (subject-only, attendee-only, combined, sub-threshold, tie-break).
- [ ] Unit: candidate source includes ordinary mail; excludes already-processed per existing cursor semantics.
- [ ] Unit: runtime fallback path (miss -> window fetch -> match / no-match), mocked client.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
