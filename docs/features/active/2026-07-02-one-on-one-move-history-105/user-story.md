# `one-on-one-move-history` — User Story

- Issue: #105
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02T13-48

## Story Statement

- As a mailbox owner whose calendar is managed by the OpenClaw scheduling agent, I want the agent to remember how often each recurring 1:1 has been moved, so that my 1:1s are never displaced more than twice in any rolling six occurrences and never two weeks in a row, and my direct reports can rely on that time actually happening.
- As the developer of Stage 2 organizer reschedule (F18), I want a persisted move-history store and a pure 1:1 move guard delivered as a tested, DI-registered contract now, so that when calendar writes are enabled the §10.3 rule is enforced from the first write without redesign.

## Problem / Why

The master specification's recurring-meeting move policy (`docs/open-claw-approach.master.md` §10.3) states a 1:1 "may be moved at most twice per rolling six occurrences and never two weeks in a row." The pure `MovePolicy.CanMove` (`src/OpenClaw.Core/Agent/MovePolicy.cs`) explicitly defers this rule to the orchestration layer because it needs per-series move history that the normalized meeting context does not carry — and no move-history persistence exists anywhere in the repository. Without it, the deterministic scheduler cannot enforce the rolling-occurrence and consecutive-week constraints when evaluating whether a 1:1 can be displaced. Identified as gap F8 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

From the mailbox owner's perspective the risk is concrete: a 1:1 is exactly the meeting the pure policy treats as most movable (it is small and non-VIP), so every higher-priority conflict would bump it. Recurring 1:1s exist to give a direct report guaranteed, predictable time; a 1:1 that slides every time something "more important" arrives stops serving that purpose. The twice-per-six-occurrences and never-consecutive-weeks limits are the master's codification of "movable, but not shuffled repeatedly" — and they are unenforceable without memory of past moves.

## Personas & Scenarios

- Persona: **Dana, mailbox owner (people manager)**
  - Who: an executive whose inbox and calendar the OpenClaw agent triages and (in Stage 2) reschedules; owns several weekly 1:1s with direct reports.
  - Cares about: her direct reports trusting that their 1:1 happens; high-priority requests still finding slots; not having to police the agent's decisions.
  - Constraints: does not review each move; the agent must enforce the policy deterministically without her intervention.
  - Goals and frustrations: wants urgent P0 requests accommodated, but is frustrated when the same recurring 1:1 keeps absorbing the disruption week after week.
  - Context and motivations: agreed to agent-managed scheduling on the condition that the master §10.3 protections apply exactly as written.

- Persona: **The Stage 2 executor (F18, organizer reschedule)**
  - Who: the developer/agent implementing `PATCH /events/{id}` behind `ENABLE_ORGANIZER_RESCHEDULE`.
  - Cares about: a stable, already-tested contract for the 1:1 history rule so the write path composes it rather than inventing it.
  - Constraints: must record a move when (and only when) a move action is actually taken, and must consult the guard before displacing a 1:1.
  - Goals: resolve `ISeriesMoveHistory` from DI exactly as `SchedulingWorker` resolves `ISentActionStore`, call `OneOnOneMoveGuard`, and change nothing in this feature's code.

- Scenario: **A P0 request tries to claim Dana's Tuesday 1:1 slot (third strike blocked)**
  - Who is acting: the scheduling agent, on behalf of a P0 (VIP) requester who needs a Tuesday-morning slot.
  - Trigger: the only candidate slot is occupied by Dana's weekly 1:1 with her direct report. The series was already moved twice within its last six occurrences (recorded in `series_moves`).
  - Steps: the agent classifies the meeting (`ONE_ON_ONE`), resolves the series key (`SeriesMasterId`), loads the recorded move history, computes the six-occurrence window and previous-week answers, and evaluates `OneOnOneMoveGuard.CanMove`.
  - Obstacle/decision: moves-in-window is 2, so the guard denies the move even for a P0 request.
  - Expected outcome: the 1:1 stays put; the agent proposes a different slot to the requester. Dana's direct report never learns their meeting was at risk.

- Scenario: **The same 1:1, four weeks later (recovered allowance)**
  - Who is acting: the scheduling agent, for a P1 conflict.
  - Trigger: the same series; the two recorded moves have aged out of the rolling six-occurrence window, and no move occurred in the previous 7 days.
  - Steps: same evaluation path; moves-in-window is now 0 and moved-previous-week is false.
  - Outcome: the guard allows the move, the occurrence is rescheduled, and the agent records one new `series_moves` row (series key, occurrence start, moved-at time) so future decisions see it.

- Scenario: **Back-to-back weeks blocked**
  - Trigger: the series was moved last week (recorded anchor exactly 7 days before the candidate occurrence's anchor); only one move is in the six-occurrence window.
  - Decision: moves-in-window (1) passes the `< 2` test, but moved-previous-week is true.
  - Outcome: the guard denies the move — "never two weeks in a row" holds even when the move count would allow it.

## Acceptance Criteria

- [x] `series_moves` table exists with an idempotent migration (fresh-database DDL and pre-existing-database upgrade paths both tested); `ISeriesMoveHistory` record/query round-trips survive restart; duplicate records are idempotent (`ON CONFLICT ... DO NOTHING`); the repository partial is clock-free (caller-supplied timestamps only).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (12 `CoreCacheRepositorySeriesMovesTests` cases pass); `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (clock-free scan).
- [x] Pure `OneOnOneMoveGuard` implements master §10.3 for `ONE_ON_ONE` exactly — allow only when moves-in-last-six-occurrences `< 2` AND NOT moved-previous-week — with the week-anchor and rolling-window semantics documented in `spec.md` (D1/D2), covered by a unit truth table (0/1/2 moves in window, previous-week move present/absent, boundary at exactly six occurrences, 7-day boundary at exactly −7 days and −8 days) and CsCheck property tests including monotonicity (adding a recorded move never turns a blocked decision into an allowed one).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md` (27 guard unit cases + 4 CsCheck properties, all passing).
- [x] Non-`ONE_ON_ONE` classifications are unaffected: the guard delegates to the existing `MovePolicy.CanMove` unchanged, proven by regression/equivalence tests (guard result equals `MovePolicy.CanMove` for all non-1:1 generated contexts).
  - Evidence: `evidence/regression-testing/movepolicy-regression.2026-07-02T14-17.md` (17 existing tests pass unmodified; diff-scope confirmation).
- [x] `ISeriesMoveHistory` lives in `Agent/Contracts` and is DI-registered in `Program.cs` mirroring the `ISentActionStore` pattern; no Runtime behavior changes (no production call site exists yet — the guard + store are the seam consumed by Stage 2 organizer reschedule, F18), and this in-scope-by-design status is documented in `spec.md` (D5).
  - Evidence: `evidence/other/scope-and-size-verification.2026-07-02T14-25.md` (diff scope); `evidence/qa-gates/final-build.2026-07-02T14-20.md`; spec.md D5.
- [x] Full C# toolchain passes (CSharpier → analyzers → nullable → architecture → tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; all new files <= 500 lines; no temp files in tests.
  - Evidence: `evidence/qa-gates/final-format.2026-07-02T14-20.md`, `evidence/qa-gates/final-build.2026-07-02T14-20.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T14-20.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T14-20.md`, `evidence/other/scope-and-size-verification.2026-07-02T14-25.md`.

## Non-Goals

- No calendar writes: no organizer reschedule, no `PATCH /events/{id}`, no `ENABLE_ORGANIZER_RESCHEDULE` flag, no attendee propose-new-time. Those are Stage 2 (F18 and later).
- No production call site for the guard: no changes to `SchedulingWorker`, `SchedulingWorker.Pipeline`, or any Runtime decision path beyond the single DI registration.
- No changes to `MovePolicy.CanMove`, `RecurringMeetingClassifier`, or `NormalizedMeetingContext` — existing pure-policy behavior is preserved verbatim.
- No HostAdapter, MailBridge, or wire-contract changes; no new HTTP endpoints, CLI flags, config keys, logging, or telemetry.
- No occurrence-calendar modeling inside the store: the store records moves only; the six-occurrence window is computed by the pure guard from caller-supplied occurrence dates (rationale in `spec.md` D2).
