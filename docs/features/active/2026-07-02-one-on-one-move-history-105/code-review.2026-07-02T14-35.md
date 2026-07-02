# Code Review: one-on-one-move-history (#105)

**Review Date:** 2026-07-02
**Branch:** `feature/one-on-one-move-history-105` @ `be385342eb4f5b0ccc999e15730f7450d59a19b9`
**Base:** `main` @ merge-base `ee387dc8c8498148a0220bd8107b22161c5d444a` (origin/main)
**Scope:** Full branch diff — 5 production `.cs`, 3 test `.cs`, feature docs/evidence Markdown (22 files, +1834/-1)

## Executive Summary

The implementation is small, disciplined, and mirrors the established issue-#101 `ISentActionStore` pattern precisely. The `ISeriesMoveHistory` contract is a narrow two-method surface shaped exactly for the Section 10.3 predicates. The repository partial reuses the repo's per-call connection, lazy schema-ensure, parameterized-SQL, `ON CONFLICT DO NOTHING`, and round-trip "O" UTC-string conventions, and is clock-free by design (caller-supplied timestamps — reviewer-verified zero clock reads). The pure `OneOnOneMoveGuard` keeps all window arithmetic in one readable LINQ pipeline implementing spec D1/D2 exactly, delegates every non-1:1 kind to `MovePolicy.CanMove` verbatim (leaving the existing policy untouched), and fails fast on missing series identity. Tests are strong: 43 new cases including a complete six-cell truth table, all four previous-week boundary offsets, the exactly-six window boundary, exhaustive `ResolveSeriesKey` partition coverage, a real in-memory SQLite repository suite with upgrade and restart scenarios, and four genuine CsCheck properties (monotonicity, delegation equivalence, window invariance, an independent interval oracle). No Blocking or Major findings; three Info observations below, none requiring remediation.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Info | src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs | whole file | Every member is async, so under the pre-existing `mailbridge.runsettings` `ExcludeByAttribute=CompilerGeneratedAttribute` filter the file contributes zero instrumented coverage lines; per-line cobertura cannot attest it. | Keep the existing behavioral-verification disposition (12 dedicated tests covering both public methods and every branch). Repo follow-up (already recommended on the #99 and #103 reviews): evaluate removing `CompilerGeneratedAttribute` from `ExcludeByAttribute` so async bodies contribute per-line data. | Instrumentation-scope masking is a known accepted pattern in this repo; the runsettings file is byte-identical to base on this branch, and the pre-existing `SentActions` partial is measured identically, so this is not a finding against the branch. | Reviewer cobertura parse: `CoreCacheRepository.SeriesMoves.cs` has no line entries in any of the three fresh reports. `evidence/qa-gates/coverage-review.2026-07-02T14-35.md`. |
| Info | src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs | `ResolveSeriesKey` (lines 139-157) | The only new pure function without a CsCheck property test; it is covered by five directed tests enumerating its complete input partition (master present; null/empty master with event id; both null; both empty). | Optional hardening: add one small property (for any non-empty `SeriesMasterId` string, the resolved key equals it verbatim; for any context with empty master and non-empty `EventId`, the key equals the event id) for uniformity with the rest of the guard surface. | The T1 density gate's intent is met for the algorithmic pure functions (`ComputeAnswers`, `CanMove` — four genuine properties); a three-branch total selector with exhaustive partition enumeration gains little from random sampling, but a property would sample unusual strings (whitespace, unicode) for free. Not a gate failure. | `OneOnOneMoveGuardTests.cs` lines 191-243 (five directed tests); `OneOnOneMoveGuardPropertyTests.cs` (no ResolveSeriesKey property). |
| Info | src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs | `GetMovedOccurrenceStartsAsync` (line 61) | Validation is asymmetric: `RecordMoveAsync` throws `ArgumentException` for a blank `seriesKey`, but the query silently returns an empty list for a blank key. | No change required; if F18 wants symmetric fail-fast, add the same guard to the query in that feature. | The asymmetry exactly matches the established `SentActions` precedent (`IsRecordedAsync` also queries without key validation; only the write path validates), and the spec's validation rules require the throw on `RecordMoveAsync` only. A blank-key query is harmless (no rows can exist under a blank key because the write path rejects them). | `CoreCacheRepository.SentActions.cs` lines 23, 69-77 (write-side-only validation precedent); spec.md "API / CLI Surface" validation rules. |

## Implementation Audit

### C# implementation audit

#### What changed well

- **Contract shape (AC-1/AC-4).** `ISeriesMoveHistory` exposes exactly the two operations the Section 10.3 predicates need — one idempotent write, one distinct most-recent-first read — pushing all window arithmetic into the pure guard. XML docs state the caller-supplied-timestamp contract, the idempotency guarantee, and the blank-key exception explicitly.
- **Repository partial fidelity.** `CoreCacheRepository.SeriesMoves.cs` mirrors `SentActions.cs` structurally: same `Open()` per-call connection pattern, same lazy once-per-instance schema-ensure with a documented benign DDL race (`CREATE TABLE IF NOT EXISTS` is concurrency-safe; the flag only avoids repeated round-trips), same parameterized SQL, same `"O"`-format invariant-culture UTC storage with `DateTimeStyles.RoundtripKind` parsing. Lexicographic `ORDER BY occurrence_start_utc DESC` equals chronological ordering because of the fixed-width "O" format — the invariant the spec documents and the ordering test pins.
- **Schema migration (AC-1).** Both paths covered: the DDL appended to `CreateTablesSql` (fresh database via `InitializeAsync`) and the identical DDL in the lazy ensure guard (databases that never re-ran initialization). Idempotent by construction (`IF NOT EXISTS`, no ALTER, no backfill); both paths tested, including a seeded pre-#105-schema upgrade.
- **Pure guard (AC-2).** `ComputeAnswers` implements D1/D2 in one pipeline: UTC-date anchoring, the six-greatest-distinct-anchors window drawn from occurrences plus the candidate with future anchors excluded, count-per-move-row vs membership-per-anchor semantics, and the half-open `[candidateAnchor - 7d, candidateAnchor)` previous-week interval. The XML docs document the D2 conservative-degradation contract (an incomplete occurrence list shrinks the window and blocks more, never less) — the property suite's monotonicity test pins the fail-safe direction.
- **Delegation (AC-3).** `CanMove` normalizes the owner email with the same `MeetingContextNormalizer.NormalizeEmail` call `MovePolicy` uses, classifies once, applies the Section 10.3 rule only to `ONE_ON_ONE`, and passes every other kind to `MovePolicy.CanMove` with identical arguments. `MovePolicy.cs`, `RecurringMeetingClassifier.cs`, and `NormalizedMeetingContext.cs` are absent from the diff — existing behavior is preserved by construction, and the delegation-equivalence property proves it over generated inputs.
- **DI registration (AC-4).** One line in `Program.cs` resolving the existing `CoreCacheRepository` singleton, exactly adjacent to and shaped like the `ISentActionStore` registration, so the same repository instance backs both stores. No Runtime worker changes (reviewer-verified: no `SchedulingWorker*` files in the diff).

#### Type safety and API notes

- Null contracts are explicit and complete: `ArgumentNullException.ThrowIfNull` on all five reference arguments of `CanMove`, both lists of `ComputeAnswers`, and the meeting of `ResolveSeriesKey` — all eight guards pinned by tests.
- `SeriesMoveHistoryAnswers` is a small sealed positional record with documented parameters — the right shape for a value carrying two predicate answers.
- Public surface additions are exactly the spec-sanctioned set; the repository partial stays `internal sealed partial`.

#### Error handling and logging

- Fail-fast throughout: blank series key rejected before any connection is opened; double-null identity in `ResolveSeriesKey` throws `ArgumentException` with a clear message rather than producing an unkeyed history row (the spec's stated rationale).
- No catch blocks added; no logging added — matching the `SentActions` partial precedent and the spec's explicit "no logging/telemetry additions" statement.

## Test Quality Audit

### Reviewed test and QA artifacts

- `tests/OpenClaw.Core.Tests/CoreCacheRepositorySeriesMovesTests.cs` (NEW) — 12 cases on real in-memory shared-cache SQLite (uniquely named per test); anchor connections used correctly to keep in-memory databases alive across repository instances for the upgrade and restart scenarios; direct SQL verification for the duplicate row count and the byte-exact stored "O" string.
- `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardTests.cs` (NEW) — 27 cases; the six-cell truth table as DataRows with display names; all four previous-week boundary offsets (-7/-8/-1/0); the exactly-six window boundary with the seventh-most-recent move excluded; same-anchor double-move count semantics; future-anchor exclusion; complete `ResolveSeriesKey` partition; all eight null guards with `WithParameterName` where applicable.
- `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardPropertyTests.cs` (NEW) — 4 CsCheck properties at 1000 iterations following the suite's seeded `Gen`/`Sample` convention (failing seed printed). The monotonicity property is the AC-mandated invariant; the delegation-equivalence property filters to the non-1:1 partition and compares guard vs `MovePolicy.CanMove` for identical inputs across generated attendee sets, requesters, priorities, and histories; the window-invariance property uses a weekly grid with in-window (weeks 0-5) vs strictly-older (weeks 6-20) move generators; the previous-week property checks against an independently coded half-open-interval oracle.
- Executor evidence set under `evidence/` (baseline, qa-gates, regression-testing, other) — timestamps, commands, and exit codes present in every artifact; coverage counts independently reproduced by the reviewer exactly (4353/4788 line, 998/1236 branch).

### Quality assessment

- **Scenario completeness:** positive, negative, boundary, normalization, ordering, isolation, migration (both paths), persistence-across-restart, and all documented D1/D2 boundary semantics are covered; the truth table covers the full 3x2 decision space.
- **Determinism:** all timestamps are fixed literals; the store is clock-free so no `FakeTimeProvider` is needed (spec-documented); CsCheck is seeded with printed failing seeds; no sleeps, timers, or wall-clock reads in any test.
- **No weakening:** no existing test file is touched by the diff; the 17 pre-existing MovePolicy/classifier tests pass unmodified (regression evidence EXIT 0, reconfirmed in the reviewer's full run).
- **Observation:** the repository suite verifies stored state through direct anchor-connection SQL rather than only through the API — the right technique for proving the `ON CONFLICT` no-op and the "O"-format storage contract without weakening the black-box round-trip tests.

## Security / Correctness Checks

- No secrets, credentials, or `.env` content in the diff.
- SQL injection safe: both statements use `$`-prefixed named parameters; the series key is never concatenated into SQL.
- No new external input surface: series keys originate from Graph event identity fields via `ResolveSeriesKey`; timestamps are caller-supplied `DateTimeOffset` values normalized to UTC on write.
- Correctness of the Section 10.3 port: `< 2` on moves-in-window with the candidate's prospective move not yet recorded matches the master pseudocode (`moves_in_last_six_occurrences(meeting) < 2`); the deliberately conservative 7-day half-open previous-week reading is documented in spec D1 with boundary tests at both edges.
- Side-effect safety: the feature adds no production call site; nothing writes to `series_moves` at runtime until F18 lands, and rollback is a no-op (orphan empty table).

## Research Log

- Verified the guard's window arithmetic against spec D1/D2 line by line: anchor = `UtcDateTime.Date`; window = six greatest distinct anchors <= candidate from occurrences ∪ {candidate}; count per move row; previous-week = half-open 7-day interval. All match.
- Verified the delegation call passes the original (non-normalized) `ownerEmail` to `MovePolicy.CanMove` — correct, since `MovePolicy.CanMove` performs its own normalization; normalizing twice would be harmless but the chosen form keeps arguments identical, which the equivalence property then proves.
- Verified `CoreCacheRepository.SentActions.cs` for pattern fidelity (per-call `Open()`, lazy ensure flag, write-side-only key validation) — the new partial matches on every axis.
- Verified the `series_moves` DDL is byte-identical between `CreateTablesSql` and `CreateSeriesMovesTableSql` (single-statement style, composite primary key).
- Confirmed no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` paths in the diff (the `modified-workflow-needs-green-run` rule does not fire).

## Verdict

**Approve — no blockers.** Three Info observations (async instrumentation scope — pre-existing repo-wide measurement configuration; optional `ResolveSeriesKey` property test; write-side-only key validation matching the established precedent), none of which gate the merge. Code quality, test quality, determinism, and seam discipline all meet repository policy.
