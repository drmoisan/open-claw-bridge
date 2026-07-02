# one-on-one-move-history (Issue #105)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/one-on-one-move-history/ (Issue #105)

- Issue: #105
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/105
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

The master specification's recurring-meeting move policy (`docs/open-claw-approach.master.md` §10.3) states a 1:1 "may be moved at most twice per rolling six occurrences and never two weeks in a row." The pure `MovePolicy.CanMove` (`src/OpenClaw.Core/Agent/MovePolicy.cs`) explicitly defers this rule to the orchestration layer because it needs per-series move history that the normalized meeting context does not carry — and no move-history persistence exists anywhere in the repository. Without it, the deterministic scheduler cannot enforce the rolling-occurrence and consecutive-week constraints when evaluating whether a 1:1 can be displaced. Identified as gap F8 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

- Add a persisted `series_moves` unit to `CoreCacheRepository` (new partial, `CREATE TABLE IF NOT EXISTS` migration) recording one row per accepted move of a recurring series: series key (`SeriesMasterId` falling back to event id), moved-at timestamp (caller-supplied, clock-free repository), and the occurrence week anchor.
- Add an `ISeriesMoveHistory` contract in `Agent/Contracts` with query operations shaped for the rule: moves within the last N occurrences window and whether the previous week contained a move.
- Add a pure `OneOnOneMoveGuard` (or extend the Runtime seam) that composes `RecurringMeetingClassifier` + history answers into the §10.3 decision: for a `ONE_ON_ONE` classification, allow a move only when `movesInLastSixOccurrences < 2` AND NOT `movedPreviousWeek`; other classifications defer to the existing `MovePolicy.CanMove` unchanged.
- Wire the guard into the Runtime seam at the point where move/bump decisions are evaluated; record a move into the history when (and only when) a move action is actually taken. NOTE: Stage 0 has no calendar-write path yet, so recording is exercised through the seam and tests; the guard becomes load-bearing when Stage 2 enables organizer reschedule (F18) — design the contract now so F18 consumes it unchanged.

## Acceptance Criteria

- [x] `series_moves` table exists with an idempotent migration (fresh-database DDL and pre-existing-database upgrade paths both tested); `ISeriesMoveHistory` record/query round-trips survive restart; duplicate records are idempotent (`ON CONFLICT ... DO NOTHING`); the repository partial is clock-free (caller-supplied timestamps only).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (12 `CoreCacheRepositorySeriesMovesTests` cases pass); `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (clock-free scan).
- [x] Pure `OneOnOneMoveGuard` implements master §10.3 for `ONE_ON_ONE` exactly — allow only when moves-in-last-six-occurrences `< 2` AND NOT moved-previous-week — with the week-anchor and rolling-window semantics documented in `spec.md`, covered by a unit truth table (0/1/2 moves in window, previous-week move present/absent, boundary at exactly six occurrences, 7-day boundary at exactly −7 days and −8 days) and CsCheck property tests including monotonicity (adding a recorded move never turns a blocked decision into an allowed one).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (27 guard unit cases + 4 CsCheck properties, all passing).
- [x] Non-`ONE_ON_ONE` classifications are unaffected: the guard delegates to the existing `MovePolicy.CanMove` unchanged, proven by regression/equivalence tests (guard result equals `MovePolicy.CanMove` for all non-1:1 generated contexts).
  - Evidence: `evidence/regression-testing/movepolicy-regression.2026-07-02T14-17.md` (17 existing tests pass unmodified; diff-scope confirmation).
- [x] `ISeriesMoveHistory` lives in `Agent/Contracts` and is DI-registered in `Program.cs` mirroring the `ISentActionStore` pattern; no Runtime behavior changes (no production call site exists yet — the guard + store are the seam consumed by Stage 2 organizer reschedule, F18), and this in-scope-by-design status is documented in `spec.md`.
  - Evidence: `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (diff scope); `evidence/qa-gates/final-build.2026-07-02T14-20.md`; spec.md D5.
- [x] Full C# toolchain passes (CSharpier → analyzers → nullable → architecture → tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all new files <= 500 lines; no temp files in tests.
  - Evidence: `evidence/qa-gates/final-format.2026-07-02T14-20.md`, `evidence/qa-gates/final-build.2026-07-02T14-20.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T14-20.md`, `evidence/other/scope-and-size-verification.2026-07-02T14-25.md`.

## Constraints & Risks

- No consumer takes actions that move meetings yet (calendar writes are Stage 2); the guard + store must be fully tested at the seam without production writes. Keep scope tight: no HostAdapter/MailBridge/wire changes.
- MSTest + FluentAssertions + Moq + CsCheck; no temp files; 500-line cap; pure logic in Agent, persistence in a new CoreCacheRepository partial.

## Test Conditions to Consider

- [ ] Unit: guard truth table (0/1/2 moves in window, previous-week move present/absent, boundary at exactly six occurrences).
- [ ] Unit: repository round-trip, migration idempotency, restart persistence.
- [ ] Property: rolling-window and consecutive-week invariants.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
