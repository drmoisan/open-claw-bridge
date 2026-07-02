# Feature Audit: send-idempotency-dedupe (#101)

- **Audit Date:** 2026-07-02
- **Branch:** `feature/send-idempotency-dedupe-101` @ `a3521833996bbf66e6d0e0ddedaebfaf8dcc85ec`
- **Work Mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`)
- **AC Sources:** `docs/features/active/2026-07-02-send-idempotency-dedupe-101/spec.md` (`## Acceptance Criteria`) and `docs/features/active/2026-07-02-send-idempotency-dedupe-101/user-story.md` (`## Acceptance Criteria`); mirrored in `issue.md`. The six criteria are textually identical across all three files (spec.md carries slightly more detailed per-item evidence annotations).

## Scope and Baseline

- Resolved base branch: `main` (resolved to `origin/main`; the local `main` ref is stale per caller inputs).
- Merge-base SHA: `d90681c766d8a9b9cff93fd59bc1989c80632d1f`; head SHA: `a3521833996bbf66e6d0e0ddedaebfaf8dcc85ec`; range: `d90681c..a352183`.
- Evidence sources: `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` (fresh — the summary's Base/Head section head SHA matches the current branch head), the authoritative `git diff` file list (27 files: 12 `.cs`, 15 `.md`; +1494/-3), executor evidence under `evidence/{baseline,qa-gates,regression-testing,other}/`, and the reviewer's independent toolchain re-run and cobertura re-parse (`evidence/qa-gates/coverage-review.2026-07-02T12-27.md`).
- The audit scope is the full feature-vs-base branch diff. No caller narrowing was attempted or accepted (see the policy audit's Rejected Scope Narrowing section).

## Acceptance Criteria Inventory

| # | Criterion (abbreviated) | Source(s) | Format |
|---|---|---|---|
| AC-1 | `sent_actions` table in the Core cache with an idempotent guarded migration; fresh-database and pre-existing-database upgrade paths covered by tests; running the migration twice is safe | spec.md, user-story.md (issue.md mirror) | `- [x]` checkbox (already checked by executor) |
| AC-2 | Pure, deterministic dedupe-key builder producing `{mailbox}:{messageId}:{actionType}`, unit-tested including at least one CsCheck property test per T1 convention (determinism, component ordering, colon-free distinctness) | spec.md, user-story.md | `- [x]` checkbox |
| AC-3 | Inside the `SendEnabled`-gated block the worker consults the store before sending: hit -> skip + structured dedupe-hit log (message id and dedupe key as named template parameters), normal (not failed) outcome; miss -> send then record with a timestamp from the injected `TimeProvider` | spec.md, user-story.md | `- [x]` checkbox |
| AC-4 | Repeated worker cycle for the same candidate does not invoke `ISchedulingService.SendMailAsync` a second time, including across a simulated restart (new worker and store instances over one shared in-memory SQLite database) | spec.md, user-story.md | `- [x]` checkbox |
| AC-5 | A failed send does NOT record the key (retry remains possible); failure stays isolated to that message per the existing `ProcessMessageSafelyAsync` behavior | spec.md, user-story.md | `- [x]` checkbox |
| AC-6 | Full C# toolchain passes (format, lint, type-check, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; MSTest + FluentAssertions + Moq; no temp files in tests; all files under the 500-line cap | spec.md, user-story.md | `- [x]` checkbox |

## Acceptance Criteria Evaluation

| # | Verdict | Evidence |
|---|---|---|
| AC-1 | **PASS** | Diff: `sent_actions` DDL appended to `CreateTablesSql` in `CoreCacheRepository.Schema.cs` as `CREATE TABLE IF NOT EXISTS` (additive; same statement serves fresh and upgrade paths idempotently), plus the lazy once-per-instance ensure guard in `CoreCacheRepository.SentActions.cs`. Tests (all passing in the reviewer run): `InitializeAsync_twice_should_not_throw_and_sent_actions_should_exist` (migration twice is safe, table exists), `InitializeAsync_should_add_sent_actions_to_pre_existing_database` (upgrade path — database seeded with a pre-#101 shape, `sent_actions` absent before / present after), `Store_methods_should_work_on_fresh_database_without_InitializeAsync` (lazy ensure). |
| AC-2 | **PASS** | `SentActionKey.Build` is a pure static function returning `$"{mailbox}:{messageId}:{actionType}"` with fail-fast per-component `ArgumentException` guards; the no-escaping/colon-free-distinctness limitation is documented in the XML remarks as the spec requires. Unit tests: 11 results (format, `ProposalReply` constant, 9 guard rows asserting parameter names). Property tests: 3 CsCheck properties — determinism, fixed component ordering under split, distinctness for colon-free triples — 1000 iterations each, exceeding the "at least one" T1 obligation. Reviewer cobertura: `SentActionKey.cs` 100% line / 100% branch. |
| AC-3 | **PASS** | Implementation verified in diff: consult/skip/record sits entirely inside the pre-existing `SendEnabled` else-branch (`SchedulingWorker.Pipeline.cs` lines 129-157); hit path logs `LogInformation("Send for message {MessageId} already recorded under dedupe key {DedupeKey}; skipping.", ...)` with the required named template parameters and returns normally; miss path awaits `SendMailAsync` then `RecordAsync(dedupeKey, timeProvider.GetUtcNow(), ...)`. Tests: `RunCycle_StoreHit_SkipsSendAndCompletesWithoutThrowing` (skip + `NotThrowAsync` — normal, not failed, outcome), `RunCycle_StoreMiss_SendsThenRecordsKeyWithInjectedClockTimestamp` (records `Msg1Key` with the exact `FakeTimeProvider` value; call order `["send", "record"]` proven via callbacks). Fail-before EXIT 1 / pass-after EXIT 0 evidence: `evidence/regression-testing/dedupe-expect-fail.2026-07-02T12-10.md`, `dedupe-pass-after.2026-07-02T12-11.md`. Note: the log's message template and parameters are verified by code inspection rather than a capturing-logger assertion — recorded as a Minor, non-blocking finding in the code review; the criterion's behavioral requirements (skip, normal outcome, record-with-injected-timestamp) are each directly test-verified. |
| AC-4 | **PASS** | `RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal`: two worker/store pairs (fresh `CoreCacheRepository` instances, i.e., fresh lazy-ensure state) over one GUID-named shared in-memory SQLite database run two successive cycles on the same candidate; `SendMailAsync` verified `Times.Once` in total. This covers both the repeated-cycle case and the simulated-restart case in one test, exactly as the criterion specifies. Passing in the reviewer run. |
| AC-5 | **PASS** | `RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate`: every send throws; the cycle does not throw (isolation via unchanged `ProcessMessageSafelyAsync` — `SchedulingWorker.cs` shows only the constructor-parameter addition in the diff); `RecordAsync` verified `Times.Never`; the second candidate is still hydrated. Correct-by-construction ordering: `RecordAsync` is only reachable after `SendMailAsync` completes (diff lines 152-157). Pre-existing `SchedulingWorkerTests` (13 tests) unregressed — the only change is the helper factory's default not-recorded store mock. |
| AC-6 | **PASS** | Reviewer re-ran the full toolchain at branch head: `csharpier check .` EXIT 0 (212 files); `dotnet build` 0 warnings / 0 errors (analyzers + nullable as errors); NetArchTest 2/2; full solution tests 701 passed / 0 failed / 5 pre-existing env-gated skips; fresh coverage pooled 90.63% line / 80.25% branch (thresholds 85%/75%), T1 `OpenClaw.Core` package 98.66%/92.07%; new files 100%/100%; no changed-line regression vs independently re-parsed baseline (90.56%/80.05%; Pipeline.cs's 50% file branch rate is identical at baseline and attributable to two pre-existing untouched ternaries). MSTest + FluentAssertions + Moq throughout; no temp files (in-memory shared-cache SQLite only, reviewer-inspected); all 12 changed/new code files under 500 lines (max: Program.cs 329). The new dedupe logic's async body is uninstrumented under the pre-existing runsettings attribute exclusion and is behaviorally covered with fail-before/pass-after evidence (policy audit Section 8). |

## Summary

All six acceptance criteria evaluate to **PASS** against the full feature-vs-base diff, with each verdict backed by direct diff inspection, the reviewer's independent toolchain re-run and per-file cobertura re-measurement, and executor regression evidence. The policy audit (`policy-audit.2026-07-02T12-27.md`) is FULLY COMPLIANT with no Blocking findings; the code review (`code-review.2026-07-02T12-27.md`) records one Minor finding (dedupe-hit log content not test-asserted; follow-up recommended) and three Info observations, none requiring action on this branch. Remediation is not required. Recommendation: **Go for PR** targeting `main` (merge-commit policy). This feature closes the accepted duplicate-send interim risk carried from issue #99; the documented at-least-once window (crash between send and record) is an explicit, accepted Stage 0 trade-off.

## Acceptance Criteria Check-off

All six criteria were already checked off (`- [x]`) by the executor in all three source files (`spec.md`, `user-story.md`, and the `issue.md` mirror) at the time of this review. The reviewer independently verified each criterion as PASS above; per the acceptance-criteria-tracking protocol, no check-off edits were needed and none were made. No criteria were left unchecked; no phantom criteria were added.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-02-send-idempotency-dedupe-101/spec.md`, `docs/features/active/2026-07-02-send-idempotency-dedupe-101/user-story.md` (mirror: `issue.md`)
- Total AC items: 6
- Checked off (delivered): 6
- Remaining (unchecked): 0
- Items remaining: none
