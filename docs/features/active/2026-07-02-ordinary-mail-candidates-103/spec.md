# ordinary-mail-candidates — Spec

- **Issue:** #103
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Draft
- **Version:** 0.2

## Overview

The master specification defines two input classes (`docs/open-claw-approach.master.md` §2.2): formal meeting traffic and **ordinary scheduling mail** ("can we move this?", "what works next week?"). Today only the first class is handled: `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` (`src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`) selects only `item_kind = 'meeting'` messages, and `HostAdapterSchedulingService.GetEventForMessageAsync` does a direct event-by-id lookup returning null on a miss — there is no `calendarView`-window fallback with the subject/attendee-overlap matching heuristic the master specifies (§9.2 `chooseMostLikelyRelatedEvent`, §9.3 step 3). Ordinary scheduling threads are never triaged. Identified as gap F7 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

This feature (a) widens the candidate source to ordinary mail, (b) ports the master's §9.2 `chooseMostLikelyRelatedEvent` as a pure matcher in `OpenClaw.Core.Agent`, and (c) adds a calendar-window fallback in the `SchedulingWorker` pipeline when the direct event lookup misses.

## Behavior

1. **Candidate widening.** `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` calls `CoreCacheRepository.ListMessagesAsync("all", sinceUtc, limit)` instead of `"meeting"`. The repository predicate for `"all"` matches every cached message row regardless of `item_kind` (`src/OpenClaw.Core/CoreCacheRepository.Messages.cs` lines 31–33); the scanner writes only `'meeting'` and `'mail'` for messages (`src/OpenClaw.MailBridge/OutlookScanner.cs` line 391), so the widened set is exactly meeting messages plus ordinary mail. Lookback window (`Polling.MessageLookbackHours`), limit (`Defaults.Limit`), and recency ordering are unchanged.
2. **Pure matcher.** A new pure static class `RelatedEventMatcher` in `OpenClaw.Core.Agent` ports §9.2 `chooseMostLikelyRelatedEvent` (see Design Decisions for the exact algorithm and tie-break rule).
3. **Calendar-window fallback.** In `SchedulingWorker.ProcessMessageAsync` (`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`), when `GetEventForMessageAsync` returns null, the worker fetches `schedulingService.GetCalendarViewAsync(now, now + CalendarViewFallbackDays, ct)` with `now = timeProvider.GetUtcNow()` and applies `RelatedEventMatcher.ChooseMostLikelyRelatedEvent(message, windowEvents)`. The fallback runs for any direct-lookup miss — matching the master's `if (!event)` guard — not only for `item_kind = 'mail'` messages; meeting-message event linkage is itself deferred (#71–#76), so formal traffic benefits from the same fallback.
4. **Downstream unchanged.** A matched event flows into `MeetingContextNormalizer.Normalize(mailboxUpn, message, matchedEvent)` — the identical call and shape used for a directly-linked event. No match yields `Normalize(mailboxUpn, message, null)`, the message-only context the pipeline already produces today; `TriageEngine.Triage` already handles that shape (empty subject+body → `IGNORE`; sensitivity defaults `"normal"`; event-derived flags default false — verified in `MeetingContextNormalizer.cs` lines 44–74 and `TriageEngine.cs`).
5. **Failure degradation.** `HostAdapterSchedulingService.GetCalendarViewAsync` already maps a failure envelope to an empty list (`HostAdapterSchedulingService.cs` lines 54–63), so an unavailable window read degrades to no-match/message-only triage. Client exceptions propagate and are isolated per message by the existing `ProcessMessageSafelyAsync` guard (`SchedulingWorker.cs` lines 80–103). No new exception paths are introduced.

## Inputs / Outputs

- Inputs: cached message rows (Core SQLite cache, `messages` table) and calendar events served through the Graph-shaped `calendarView` surface (`GET /users/{id}/calendarView`), which the Local MVP backs with `list_calendar_window` over the MailBridge cache.
- Outputs: unchanged pipeline outputs — triage/priority log lines and (when `SendEnabled`) proposal replies. New structured log lines for the fallback (attempted, matched event id, or no match).
- Config keys and defaults:
  - `OpenClaw:AgentPolicy:CalendarViewFallbackDays` (int, **default 14**, mirroring the 14-day forward window in master §9.2). A non-positive value skips the fallback entirely (documented opt-out; consistent with the options bag carrying no validators today).
- Versioning / backward compatibility: no wire, schema, or public-contract changes. `ISchedulingService` is unchanged — `GetCalendarViewAsync` already exists on the interface (`src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` lines 26–35, documented as "the calendar-view fallback").

## API / CLI Surface

No external API or CLI changes. Internal surface additions:

```csharp
namespace OpenClaw.Core.Agent;

/// <summary>Pure port of master §9.2 chooseMostLikelyRelatedEvent.</summary>
public static class RelatedEventMatcher
{
    public static SchedulingEventDto? ChooseMostLikelyRelatedEvent(
        SchedulingMessageDto message,
        IReadOnlyList<SchedulingEventDto> events
    );
}
```

- `AgentPolicyOptions` gains `public int CalendarViewFallbackDays { get; set; } = 14;`.
- Contracts and validation: `ChooseMostLikelyRelatedEvent` throws `ArgumentNullException` for null arguments (matching `MeetingContextNormalizer.Normalize` and `TriageEngine.Triage` conventions); it never throws for empty/degenerate content (null subjects, empty attendee lists, null ids) and returns null when no event scores >= 4.

## Data & State

- No schema changes. The `messages` table already carries `item_kind` with values `'meeting'`/`'mail'`, and `ListMessagesAsync` already supports the `"all"` kind (used by `/messages` route validation in `Program.cs` lines 258–259).
- Data transformations: window events arrive as `SchedulingEventDto` via the existing `SchedulingDtoMapper.MapEvent` path (attendee JSON parsed, emails preserved); the matcher normalizes emails with the existing public helpers `MeetingContextNormalizer.NormalizeEmail`/`EmailOf` (trim + lowercase) so message-side and event-side comparisons use identical normalization.
- Invariants: the matcher is pure and order-independent (deterministic tie-break); the pipeline remains single I/O seam (`ISchedulingService`) only.
- No migration or backfill.

## Design Decisions

### D1 — Matcher algorithm (exact §9.2 port) and tie-breaking

- Message subject tokens: lowercase, split on runs of non-alphanumeric characters (`[^a-z0-9]+`), keep tokens of length >= 4, distinct (set semantics, as in the master's `Set`).
- Message participants: normalized emails of `From`, `Sender`, all `ToRecipients`, all `CcRecipients`; empties dropped; distinct.
- Event side: subject tokenized identically; attendee emails are the union of `RequiredAttendees`, `OptionalAttendees`, `ResourceAttendees` (the master scores `event.attendees`, which carries all three types; the organizer is intentionally **not** included, matching the reference).
- Score: **+2** per message subject token present in the event token set; **+3** per participant email present in the event attendee set. Accept the best-scoring event only when the score is **>= 4** (so two subject tokens, or one token plus one participant, suffice; a single participant match does not).
- Tie-break (deviation from the reference, recorded deliberately): the master's loop keeps the first event with a strictly greater score, making the result depend on input order. `CacheRepository.ListCalendarWindowAsync` ordering is an implementation detail of the bridge, so first-wins would couple the decision to storage ordering. Instead, among events sharing the maximum qualifying score, select the one with the earliest `Start` (null `Start` sorts last), then the smallest ordinal `Id` (null treated as empty string). This is strictly stronger determinism with identical accept/reject behavior.

### D2 — Fallback event source: `ISchedulingService.GetCalendarViewAsync` (client path), not a direct Core-cache read

Evidence-based choice of the live client path:

1. The seam method already exists and was built for this purpose: `ISchedulingService.GetCalendarViewAsync` is documented "the calendar-view fallback" and implemented over `IHostAdapterClient.ListCalendarWindowAsync` (`HostAdapterSchedulingService.cs` lines 48–63; `IHostAdapterClient.cs` lines 71–87, route `GET /users/{id}/calendarView`).
2. The pipeline's documented invariant is I/O through `ISchedulingService` only (`SchedulingWorker.Pipeline.cs` line 15). Reading `CoreCacheRepository` events directly from the pipeline would add a second data path and break the seam swap that carries the agent unchanged to Graph in Product Increment 1.
3. Offline-friendliness is already satisfied: per the master's Local MVP mapping table, `calendarView` is served by `list_calendar_window` as "a bounded window over the cached events" — a local SQLite read in `OpenClaw.MailBridge/CacheRepository.cs`. The Core-cache alternative would not remove any network dependency that matters.
4. No new availability dependency: `GetSchedulingMessageAsync` in the same per-message path already requires the adapter; if the adapter is down, hydration fails before the fallback is reached, and the per-message isolation in `ProcessMessageSafelyAsync` contains it.
5. Less code: a Core-cache read would require a new `MailBridge.Contracts.Models.EventDto` → `SchedulingEventDto` mapping path and a pipeline dependency on the internal `CoreCacheRepository`; the client path reuses `SchedulingDtoMapper.MapEvent` as-is.

### D3 — Fallback placement: `SchedulingWorker.ProcessMessageAsync`, not inside `HostAdapterSchedulingService.GetEventForMessageAsync`

The matcher needs the hydrated message, a clock, and the window option. The worker already holds all three (`message`, injected `TimeProvider`, `AgentPolicyOptions`); the service holds none and would need re-hydration plus two new constructor dependencies. Placing the orchestration in the pipeline keeps `HostAdapterSchedulingService` a thin adapter and keeps the scoring pure in `OpenClaw.Core.Agent`.

### D4 — Candidate widening predicate and reprocessing

- New predicate: every cached message within `Polling.MessageLookbackHours`, up to `Defaults.Limit`, ordered by recency — implemented by switching the kind literal from `"meeting"` to `"all"`.
- **Verified: no candidate cursor exists.** The only cursors in the codebase are polling-ingest bookmarks (for example `calendar_window_last_run_utc` in `CalendarPollingWorker.cs`). `CacheSchedulingCandidateSource` re-lists the window every cycle today, so meeting messages are already reprocessed each minute by design; the pipeline is deterministic-compute-plus-logs, and side effects are bounded by the #101 sent-action store (`SentActionKey.Build(mailboxUpn, messageId, ProposalReply)` consulted before send, recorded after — `SchedulingWorker.Pipeline.cs` lines 129–157) and the `SendEnabled`/`CalendarWriteEnabled` kill switches. Widening changes processed **volume**, not reprocessing semantics; no new cursor or option flag is required.
- Volume bound: `Defaults.Limit` (default 100) messages per cycle. Risk: ordinary mail typically outnumbers meeting messages and both classes now compete for the same recency-ordered limit; accepted for the Local MVP and noted under Constraints & Risks.

### D5 — Per-message window fetch (no memoization)

The fallback issues one `GetCalendarViewAsync` call per unlinked message per cycle. Locally this is a named-pipe read over the MailBridge SQLite cache (cheap); per the repository's simplicity-first design rule, per-cycle memoization of the window is deferred until measurement shows it is needed.

### D6 — `Prefer: outlook.body-content-type="text"` (master §9.3 step 3)

Not applicable to the Local MVP wire: `SchedulingDtoMapper.MapEvent` maps `BodyContent`/`BodyContentType` to null and only `BodyPreview` (already plain text) is available, so deterministic code never sees HTML event bodies here. The `Prefer` header is a requirement on the future Graph-backed client in Product Increment 1 and is recorded as a non-goal for this feature.

## Constraints & Risks

- Widening the candidate set increases processed volume; the dedupe store (#101) and `SendEnabled` gate bound the blast radius. Verified: candidate cursoring does **not** exist — re-listing per cycle is the existing behavior for meeting traffic and is unchanged in kind (see D4).
- With a shared `Defaults.Limit`, high mail volume can crowd meeting messages out of a cycle's candidate list (recency-ordered). Accepted for the Local MVP; a per-kind quota is a possible follow-up if observed.
- Matching is heuristic by design; the master mandates the deterministic decision come from code, which this preserves (fixed weights/threshold, order-independent tie-break).
- Existing worker tests stub `GetEventForMessageAsync` as null; after this change those paths invoke `GetCalendarViewAsync`, so the mocks must be extended (Moq loose mocks return empty task results for enumerable types, but the stubs will be made explicit rather than relying on default-value providers).
- MSTest + FluentAssertions + Moq + CsCheck (all already referenced by `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj`); no temp files (in-memory shared-cache SQLite for repository tests, per `CoreCacheRepositoryGraphFieldsTests` convention); 500-line cap; pure logic in `OpenClaw.Core.Agent`, I/O in the Runtime seam only.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs` — **new** pure static class (D1). Reuses `MeetingContextNormalizer.NormalizeEmail`/`EmailOf`; tokenizer implemented with a `GeneratedRegex` following the `MeetingContextNormalizer.Helpers.cs` pattern.
  - `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` — add `CalendarViewFallbackDays` (default 14) with XML docs citing master §9.2.
  - `src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs` — kind literal `"meeting"` → `"all"`; update the XML summary (it currently says "meeting-message identifiers").
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` — fallback block after the `GetEventForMessageAsync` call (skip when `CalendarViewFallbackDays <= 0`); file is 195 lines today, remains well under 500.
- New tests:
  - `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherTests.cs` — scoring rows: subject-only, attendee-only, combined, sub-threshold (score 3), exact-threshold (score 4), tie-break (Start then Id), empty events, null/short subjects, case/normalization.
  - `tests/OpenClaw.Core.Tests/Agent/RelatedEventMatcherPropertyTests.cs` — CsCheck properties per AC-3 (mirrors `MeetingContextNormalizerPropertyTests` conventions).
  - `tests/OpenClaw.Core.Tests/Agent/Runtime/CacheSchedulingCandidateSourceTests.cs` — **new file** (none exists today): seed an in-memory shared-cache `CoreCacheRepository` with `meeting` and `mail` rows; assert both ids returned, lookback and limit respected.
  - Update `SchedulingWorkerTests` (fallback: miss → window fetch → match hydrates context; miss → no match → message-only; `CalendarViewFallbackDays <= 0` skips the fetch; window bounds asserted against `FakeTimeProvider` now + 14 days) and `SchedulingWorkerDedupeTests` (ordinary-mail scenario).
- Dependency changes: none (CsCheck 4.7.0, Moq 4.20.72, MSTest 3.6.4, FluentAssertions 6.12.0, `Microsoft.Extensions.TimeProvider.Testing` already referenced).
- Logging: `LogDebug` when the fallback is attempted (message id, window bounds); `LogInformation` on match (message id, event id, score) and `LogDebug` on no-match — same `ILogger<SchedulingWorker>` patterns as the existing pipeline stages.
- Rollout: no new kill switch; the fallback is compute-plus-logs like the rest of the pipeline, and all side effects remain behind `SendEnabled`/`CalendarWriteEnabled`. `CalendarViewFallbackDays <= 0` is the documented opt-out.

## Acceptance Criteria

- [x] `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` returns ordinary (`item_kind = 'mail'`) messages alongside meeting messages (repository kind `"all"`), preserving the existing lookback window, limit, and ordering; formal meeting-traffic handling is unchanged; covered by a new unit test against an in-memory SQLite `CoreCacheRepository`.
- [x] A new pure static `RelatedEventMatcher` in `OpenClaw.Core.Agent` ports §9.2 `chooseMostLikelyRelatedEvent` exactly: distinct lowercase subject tokens (split on non-alphanumeric runs, length >= 4) score +2 per token also present in the event subject; distinct normalized participant emails (from, sender, to, cc) score +3 per email present in the event attendee set (required + optional + resource); the best event is returned only when its score >= 4; ties resolve deterministically by earliest `Start` (null last) then ordinal `Id`; unit tests cover subject-only, attendee-only, combined, sub-threshold, empty-input, and tie-break rows.
- [x] CsCheck property/invariant tests cover the matcher: the result is null or scores >= 4; the selected event is invariant under permutation of the input event list; scoring is case-insensitive; duplicate tokens/emails do not double-count (set semantics).
- [x] When `GetEventForMessageAsync` returns null, `SchedulingWorker.ProcessMessageAsync` fetches `ISchedulingService.GetCalendarViewAsync(now, now + CalendarViewFallbackDays)` and applies the matcher; `CalendarViewFallbackDays` is a new `AgentPolicyOptions` value defaulting to 14; `now` comes from the injected `TimeProvider` (no wall-clock APIs); a non-positive configured value skips the fallback; verified with mocked `ISchedulingService` and `FakeTimeProvider`.
- [x] No-match (empty window, all scores < 4, or a failure envelope mapped to an empty window) falls through to message-only triage (`MeetingContextNormalizer.Normalize(mailboxUpn, message, null)`) without a new exception path; a matched event hydrates the context through the same `Normalize` call formal traffic uses.
- [x] Widened candidates are re-listed every cycle (no candidate cursor exists); outbound proposal sends remain deduplicated across cycles by the #101 sent-action store with the unchanged key shape — verified by extending the existing dedupe tests with an ordinary-mail scenario.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all touched files remain <= 500 lines.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit: matcher scoring rows (subject-only, attendee-only, combined, sub-threshold, tie-break).
- [ ] Unit: candidate source includes ordinary mail; excludes nothing new — re-listing per cycle is the existing (cursor-less) behavior, bounded by lookback and limit.
- [ ] Unit: runtime fallback path (miss -> window fetch -> match / no-match), mocked client.
