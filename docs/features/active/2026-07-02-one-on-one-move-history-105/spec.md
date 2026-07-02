# one-on-one-move-history — Spec

- **Issue:** #105
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T13-48
- **Status:** Draft
- **Version:** 0.2

## Overview

The master specification's recurring-meeting move policy (`docs/open-claw-approach.master.md` §10.3) states a 1:1 "may be moved at most twice per rolling six occurrences and never two weeks in a row" (`can_move` pseudocode: `moves_in_last_six_occurrences(meeting) < 2 and not moved_previous_week(meeting)`). The pure `MovePolicy.CanMove` (`src/OpenClaw.Core/Agent/MovePolicy.cs`) explicitly defers this rule to the orchestration layer: its `<remarks>` state the stateful rule "depends on per-series move history that is not part of the pure normalized context," so "a `RecurringMeetingKind.ONE_ON_ONE` is treated as movable here and the history guard is applied by the caller." No move-history persistence exists anywhere in the repository, and — verified by repository-wide search — `MovePolicy.CanMove` currently has **no production call site** (only its own definition and `MovePolicyTests`). Without the history store and guard, the deterministic scheduler cannot enforce the rolling-occurrence and consecutive-week constraints when Stage 2 begins evaluating whether a 1:1 can be displaced. Identified as gap F8 (item 8) in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Behavior

Three additions, mirroring the issue-#101 `ISentActionStore` pattern:

1. **`ISeriesMoveHistory` contract** — new file `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` (namespace `OpenClaw.Core.Agent`, matching `ISentActionStore`):
   - `Task RecordMoveAsync(string seriesKey, DateTimeOffset occurrenceStartUtc, DateTimeOffset movedAtUtc, CancellationToken ct)` — records one accepted move of one occurrence of a series. Idempotent: re-recording an identical (`seriesKey`, `occurrenceStartUtc`) pair succeeds and leaves a single row. Timestamps are caller-supplied; the store has no clock dependency.
   - `Task<IReadOnlyList<DateTimeOffset>> GetMovedOccurrenceStartsAsync(string seriesKey, CancellationToken ct)` — returns the distinct recorded occurrence-start timestamps for the series, most recent first. One query serves both §10.3 predicates; all window arithmetic is done by the pure guard.

2. **`CoreCacheRepository.SeriesMoves` partial** — new file `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` implementing `ISeriesMoveHistory` on `CoreCacheRepository`, mirroring `CoreCacheRepository.SentActions.cs` exactly: per-call connection via `Open()`, lazy once-per-instance schema-ensure guard (`CREATE TABLE IF NOT EXISTS` before the first store operation), `ON CONFLICT ... DO NOTHING` insert, UTC round-trip (`"O"`) timestamp format, no clock reads. The same DDL is appended to `CreateTablesSql` in `CoreCacheRepository.Schema.cs` so `InitializeAsync` covers the fresh-database path.

3. **Pure `OneOnOneMoveGuard`** — new file `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs`, a static class with no I/O:
   - `SeriesMoveHistoryAnswers ComputeAnswers(IReadOnlyList<DateTimeOffset> movedOccurrenceStartsUtc, IReadOnlyList<DateTimeOffset> occurrenceStartsUtc, DateTimeOffset candidateOccurrenceStartUtc)` — pure window arithmetic producing the two §10.3 predicate answers (semantics in Design Decisions below). `movedOccurrenceStartsUtc` is the store query result; `occurrenceStartsUtc` is the series' known occurrence starts supplied by the caller (Stage 2 derives them from the events cache, which persists series occurrences keyed by `series_master_id`).
   - `bool CanMove(NormalizedMeetingContext meeting, string ownerEmail, string requesterEmail, OwnerPriority requestPriority, OwnerSchedulingPolicy policy, SeriesMoveHistoryAnswers history)` — classifies via `RecurringMeetingClassifier.Classify`; for `ONE_ON_ONE` returns `history.MovesInLastSixOccurrences < 2 && !history.MovedPreviousWeek`; for every other kind delegates to the existing `MovePolicy.CanMove` **unchanged** (same arguments, no behavioral difference).
   - `SeriesMoveHistoryAnswers` — a small readonly record (`int MovesInLastSixOccurrences`, `bool MovedPreviousWeek`) in the Agent namespace.
   - `string ResolveSeriesKey(NormalizedMeetingContext meeting)` — series-key helper (see Design Decisions).

**DI registration:** `Program.cs` adds `builder.Services.AddSingleton<ISeriesMoveHistory>(sp => sp.GetRequiredService<CoreCacheRepository>());` adjacent to the existing `ISentActionStore` registration (line 65). No Runtime worker change: nothing invokes `MovePolicy.CanMove` today, so there is no existing move/bump decision point to wire into. The guard and store are delivered as a fully tested, DI-registered seam consumed by Stage 2 organizer reschedule (F18).

## Inputs / Outputs

- Inputs: none new at runtime. Store inputs are caller-supplied (`seriesKey`, `occurrenceStartUtc`, `movedAtUtc`); guard inputs are the normalized meeting context, policy, and history answers. No CLI flags, env vars, or config keys are added (F18's `ENABLE_ORGANIZER_RESCHEDULE` flag is out of scope).
- Outputs: rows in the new `series_moves` SQLite table; guard boolean decisions. No new logs, telemetry, endpoints, or artifacts.
- Config keys and defaults: none added.
- Versioning / backward compatibility: additive only. Existing databases gain the `series_moves` table idempotently (fresh DDL path via `InitializeAsync`; lazy schema-ensure covers databases that have not re-run initialization). No existing table, contract, or public API changes.

## API / CLI Surface

No CLI or HTTP surface. New public in-process contracts:

- `ISeriesMoveHistory.RecordMoveAsync(seriesKey, occurrenceStartUtc, movedAtUtc, ct)` / `ISeriesMoveHistory.GetMovedOccurrenceStartsAsync(seriesKey, ct)` (shapes above).
- `OneOnOneMoveGuard.ComputeAnswers(...)`, `OneOnOneMoveGuard.CanMove(...)`, `OneOnOneMoveGuard.ResolveSeriesKey(...)`, `SeriesMoveHistoryAnswers`.

Validation rules (fail fast, matching `SentActionKey`/`ParseSentActionKey` precedent):
- `RecordMoveAsync` throws `ArgumentException` for a null/empty/whitespace-only `seriesKey`.
- `ResolveSeriesKey` returns `SeriesMasterId` when non-empty, else `EventId` when non-empty, else throws `ArgumentException` (a move cannot be recorded against a series with no identity).
- `CanMove`/`ComputeAnswers` throw `ArgumentNullException` for null reference arguments, matching `MovePolicy.CanMove`.

Example (unit-level):

```csharp
var answers = OneOnOneMoveGuard.ComputeAnswers(
    movedOccurrenceStartsUtc: await history.GetMovedOccurrenceStartsAsync(key, ct),
    occurrenceStartsUtc: lastSixOccurrenceStarts, // caller-known series occurrences
    candidateOccurrenceStartUtc: candidateStart);
var allowed = OneOnOneMoveGuard.CanMove(meeting, owner, requester, priority, policy, answers);
```

## Data & State

New SQLite table, appended to `CreateTablesSql` in `CoreCacheRepository.Schema.cs` and duplicated as the lazy-ensure DDL constant in the new partial (same single-statement style as `sent_actions`):

```sql
CREATE TABLE IF NOT EXISTS series_moves(
    series_key TEXT NOT NULL,
    occurrence_start_utc TEXT NOT NULL,
    moved_at_utc TEXT NOT NULL,
    PRIMARY KEY(series_key, occurrence_start_utc)
);
```

- One row per accepted move of one occurrence. `occurrence_start_utc` is the occurrence's pre-move scheduled start (UTC, `"O"` format); `moved_at_utc` is the caller-supplied time the move action was taken (UTC, `"O"` format).
- The composite primary key makes retries idempotent (`ON CONFLICT(series_key, occurrence_start_utc) DO NOTHING`) while still counting a genuine re-move of the same occurrence (which has a different pre-move start after the first move) as a distinct move — correct per §10.3, since re-moving an occurrence is a second move of the series.
- Invariants: both timestamp columns are round-trip (`"O"`) UTC strings, so lexicographic `ORDER BY occurrence_start_utc DESC` equals chronological ordering; queries are keyed by exact `series_key` equality (no cross-series leakage).
- Migration: `CREATE TABLE IF NOT EXISTS` only — idempotent on fresh and existing databases; no ALTER, no backfill (no historical move data exists to backfill).

## Design Decisions

### D1 — Week semantics: UTC day anchor with a half-open 7-day window (not ISO weeks)

- **Anchor:** `Anchor(t) = t.UtcDateTime.Date` — the UTC calendar date of the occurrence start being moved.
- **`MovedPreviousWeek`:** true iff any recorded move's anchor `a` satisfies `candidateAnchor − 7 days ≤ a < candidateAnchor` (half-open: an anchor exactly 7 days before the candidate **is** "previous week"; an anchor equal to the candidate's own anchor is **not**).
- **Rationale:** ISO-8601 week numbers introduce year-boundary edge cases (week 52/53 wrap) and behave asymmetrically around the week's start day — a Sunday occurrence and the following Monday occurrence land in "consecutive ISO weeks" despite being one day apart, while two occurrences six days apart inside one ISO week would not be "two weeks in a row" at all. The 7-day half-open window is purely arithmetic, timezone-free (UTC dates), identical for series anchored on any weekday, and trivially property-testable. For the dominant weekly-1:1 case the two definitions agree (the previous occurrence's anchor sits exactly at −7 days). A recorded move anywhere in the 7 calendar days strictly before the candidate blocks — a deliberately conservative reading of "never two weeks in a row" that fails safe (the 1:1 stays put).
- **Boundary tests required:** anchors at exactly −7 days (blocked), −8 days (allowed), −1 day (blocked), same anchor (not previous-week).

### D2 — "Rolling six occurrences": series occurrences supplied by the caller, not derived from move records alone

- **Window:** the six greatest distinct anchors in `{Anchor(o) : o ∈ occurrenceStartsUtc} ∪ {candidateAnchor}` that are `≤ candidateAnchor` (all of them when fewer than six exist — correct for a young series). `MovesInLastSixOccurrences` = the count of recorded move rows whose anchor is a member of that window set. The candidate's own prospective move is not yet recorded, so `< 2` means the candidate would be at most the second move in the window, matching master `moves_in_last_six_occurrences(meeting) < 2`.
- **Why the caller supplies occurrence dates:** deriving the occurrence window from move records alone deadlocks. If a series' only known occurrence dates are its moved ones, a series with two recorded moves ever has a window containing both moves forever — it can never be moved again, and because moves are blocked, no new anchors ever accumulate to age the old ones out. The store therefore records the occurrence start of each move (per the promoted issue), but the six-occurrence window is computed by the pure guard from occurrence dates the caller already knows: the Core events cache persists recurring-series occurrences with `series_master_id` and `start_utc` (`CoreCacheRepository.Events.cs`), so the F18 consumer can supply the recent occurrence starts with a single cache query. The guard does not care about the source; tests supply lists directly.
- **Boundary tests required:** exactly six occurrences; seventh-most-recent occurrence's move falling outside the window; fewer than six known occurrences; two moves on the same UTC day (distinct starts, same anchor — the count is per move row, window membership per anchor).

### D3 — Series key: `SeriesMasterId` with fallback to event id

`ResolveSeriesKey` returns `NormalizedMeetingContext.SeriesMasterId` when non-empty, else `NormalizedMeetingContext.EventId` when non-empty, else throws `ArgumentException`. Rationale: `SeriesMasterId` is the stable identity shared by all occurrences of a recurring series (and is what `MeetingContextNormalizer` uses to set `IsRecurring`); the event id fallback covers contexts where the normalizer had an event but no series master. Failing fast on a double-null identity prevents unkeyed history rows. Keys are opaque strings; no escaping is needed because the key is a single column, not a joined composite like `SentActionKey`.

### D4 — Guard shape: pure static class in Agent; delegation leaves `MovePolicy` untouched

`OneOnOneMoveGuard` is a pure static class (no I/O, no clock, no store reference), keeping all logic unit-testable without mocks and preserving the Agent-layer purity that `MovePolicy` and `RecurringMeetingClassifier` establish. `MovePolicy.CanMove` itself is not modified — its documented contract (1:1 treated as movable, history guard applied by the caller) stays intact, and all non-`ONE_ON_ONE` kinds flow through it verbatim, so existing behavior is provably unchanged. The store is the only I/O component and lives behind `ISeriesMoveHistory` in the persistence layer, mirroring the `ISentActionStore` separation.

### D5 — No production consumer yet: in-scope-by-design seam for F18

Nothing in `src/` invokes `MovePolicy.CanMove` today; there is no move/bump decision point in `SchedulingWorker`/`SchedulingWorker.Pipeline` to wire the guard into. This feature therefore delivers the guard, contract, store, and DI registration as a complete, fully unit-tested seam with **no production call site**. This is deliberate, not dead code: gap-analysis item 18 (organizer reschedule behind `ENABLE_ORGANIZER_RESCHEDULE`, Stage 2) is the consumer, and defining the contract now means F18 consumes it unchanged. Architecture note: the DI registration mirrors `ISentActionStore` so the F18 executor resolves `ISeriesMoveHistory` exactly as `SchedulingWorker` resolves `ISentActionStore`; recording a move must happen when (and only when) a move action is actually taken, which is an F18 obligation.

## Constraints & Risks

- No consumer takes actions that move meetings yet (calendar writes are Stage 2); the guard + store must be fully tested at the seam without production writes. Keep scope tight: no HostAdapter/MailBridge/wire changes, no Runtime worker changes beyond none, no `ENABLE_ORGANIZER_RESCHEDULE` flag work.
- MSTest + FluentAssertions + Moq + CsCheck (repo test stack; note the tests use in-memory shared-cache SQLite, so Moq is likely unneeded here); no temp files (in-memory `Mode=Memory;Cache=Shared` SQLite per `CoreCacheRepositorySentActionsTests`); 500-line cap per file; pure logic in Agent, persistence in a new `CoreCacheRepository` partial; repository stays clock-free (caller-supplied timestamps; no `TimeProvider` needed since no component reads a clock).
- Risk: the six-occurrence window depends on caller-supplied occurrence dates (D2). If the F18 caller supplies an incomplete occurrence list, the guard is conservative (blocks more). Mitigated by documenting the input contract on `ComputeAnswers` and testing the fewer-than-six path.
- Risk: `CoreCacheRepository` accumulates interface implementations. Accepted — it follows the established partial-per-store pattern and each partial stays well under the 500-line cap.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - New: `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs`, `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` (including `SeriesMoveHistoryAnswers`), `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs`.
  - Modified: `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (append `series_moves` DDL to `CreateTablesSql`), `src/OpenClaw.Core/Program.cs` (one DI line).
  - New tests: `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardTests.cs`, `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardPropertyTests.cs`, `tests/OpenClaw.Core.Tests/CoreCacheRepositorySeriesMovesTests.cs`.
- New classes/functions/commands: listed under Behavior; no existing public API changes.
- Dependency changes: none. CsCheck, FluentAssertions, MSTest, and Microsoft.Data.Sqlite are already in use.
- Logging/telemetry additions: none. The store follows the `SentActions` partial, which adds no logging; the guard is pure.
- Rollout plan: additive schema + unused-by-production seam, so no flag or staged rollout is needed. The table is created idempotently on first `InitializeAsync` or first store call; rollback is a no-op (an orphan empty table is harmless).

### Test plan (maps to Acceptance Criteria)

- `CoreCacheRepositorySeriesMovesTests` (mirrors `CoreCacheRepositorySentActionsTests`): record/query round-trip; duplicate `(seriesKey, occurrenceStartUtc)` idempotency (single row); non-UTC offset normalized to `"O"`-format UTC; descending order; series isolation (key A rows invisible to key B); `InitializeAsync` twice without error; pre-existing-database upgrade (seeded pre-#105 shape gains `series_moves`); lazy schema-ensure without `InitializeAsync`; `ArgumentException` on blank `seriesKey`. All on in-memory shared-cache SQLite.
- `OneOnOneMoveGuardTests`: truth table over (moves-in-window ∈ {0, 1, 2}) × (previous-week move present/absent) for `ONE_ON_ONE`; window boundary at exactly six occurrences (older move excluded); fewer-than-six occurrences; previous-week boundaries (−7 d blocked, −8 d allowed, −1 d blocked, same-anchor not previous-week); same-day distinct-start double move; `ResolveSeriesKey` fallback chain and double-null throw; null-argument guards.
- `OneOnOneMoveGuardPropertyTests` (CsCheck, seeded, failing seed printed): monotonicity — adding a recorded move never converts a blocked decision to allowed; delegation equivalence — for generated non-`ONE_ON_ONE` contexts, guard result equals `MovePolicy.CanMove` for identical inputs regardless of history; window invariance — moves with anchors older than the six-occurrence window never affect the count; previous-week predicate depends only on `[−7 d, 0)` anchor membership.
- Existing `MovePolicyTests`, `RecurringMeetingClassifierTests`, and `RecurringMeetingClassifierPropertyTests` must pass unmodified (regression evidence for AC-3).
- Coverage evidence: baseline and post-change coverage artifacts under `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/coverage/`; QA gate outputs under `docs/features/active/2026-07-02-one-on-one-move-history-105/evidence/qa-gates/`.

## Acceptance Criteria

- [x] `series_moves` table exists with an idempotent migration (fresh-database DDL and pre-existing-database upgrade paths both tested); `ISeriesMoveHistory` record/query round-trips survive restart; duplicate records are idempotent (`ON CONFLICT ... DO NOTHING`); the repository partial is clock-free (caller-supplied timestamps only).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (12 `CoreCacheRepositorySeriesMovesTests` cases pass, incl. migration idempotency, upgrade, restart persistence, duplicate idempotency); `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (clock-free scan, section d).
- [x] Pure `OneOnOneMoveGuard` implements master §10.3 for `ONE_ON_ONE` exactly — allow only when moves-in-last-six-occurrences `< 2` AND NOT moved-previous-week — with the week-anchor and rolling-window semantics documented in this spec (D1/D2), covered by a unit truth table (0/1/2 moves in window, previous-week move present/absent, boundary at exactly six occurrences, 7-day boundary at exactly −7 days and −8 days) and CsCheck property tests including monotonicity (adding a recorded move never turns a blocked decision into an allowed one).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (27 `OneOnOneMoveGuardTests` cases incl. six-cell truth table and −7/−8-day boundaries; 4 `OneOnOneMoveGuardPropertyTests` incl. monotonicity — all pass).
- [x] Non-`ONE_ON_ONE` classifications are unaffected: the guard delegates to the existing `MovePolicy.CanMove` unchanged, proven by regression/equivalence tests (guard result equals `MovePolicy.CanMove` for all non-1:1 generated contexts).
  - Evidence: `evidence/regression-testing/movepolicy-regression.2026-07-02T14-17.md` (17 existing tests pass unmodified; diff-scope confirmation) plus the delegation-equivalence property in `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md`.
- [x] `ISeriesMoveHistory` lives in `Agent/Contracts` and is DI-registered in `Program.cs` mirroring the `ISentActionStore` pattern; no Runtime behavior changes (no production call site exists yet — the guard + store are the seam consumed by Stage 2 organizer reschedule, F18), and this in-scope-by-design status is documented in this spec (D5).
  - Evidence: `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (production diff limited to the five planned files; no Runtime-worker changes); `evidence/qa-gates/final-build.2026-07-02T14-20.md` (registration builds with 0 warnings/errors); D5 documented above.
- [x] Full C# toolchain passes (CSharpier → analyzers → nullable → architecture → tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all new files <= 500 lines; no temp files in tests.
  - Evidence: `evidence/qa-gates/final-format.2026-07-02T14-20.md`, `evidence/qa-gates/final-build.2026-07-02T14-20.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (incl. architecture-boundary suite), `evidence/qa-gates/coverage-comparison.2026-07-02T14-20.md` (line 90.92% >= 85%, branch 80.74% >= 75%, no changed-line regression), `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (500-line cap, no temp files).

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit: guard truth table (0/1/2 moves in window, previous-week move present/absent, boundary at exactly six occurrences).
- [ ] Unit: repository round-trip, migration idempotency, restart persistence.
- [ ] Property: rolling-window and consecutive-week invariants.
