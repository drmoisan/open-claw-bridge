# send-idempotency-dedupe (Issue #101)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/send-idempotency-dedupe/ (Issue #101)

- Issue: #101
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/101
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

With `HostAdapterSchedulingService.SendMailAsync` now wired (issue #99 / PR #100), a retried or repeated scheduling cycle can resend the same reply: no dedupe-key concept exists in `SchedulingWorker`'s pipeline, `MailRoutes`, or `SendMailRpcHandler`. The master specification requires idempotency keys for mail sends with an internal dedupe key shaped like `{tenantId}:{mailboxUpn}:{messageId}:{actionType}` (`docs/open-claw-approach.master.md` §6.3); the Local MVP equivalent is a persisted per-message/action guard consulted before send. This was recorded as the accepted interim risk in the #99 spec, to be closed by this feature. Identified as gap F6 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

- Add a persisted `sent_actions` unit to `CoreCacheRepository` (new table via the established guarded-migration pattern) keyed by a deterministic dedupe key: `{mailbox}:{messageId}:{actionType}` (local single-mailbox form of the master's key; tenant component deferred to Stage 1).
- `SchedulingWorker.ProposeAndActAsync` consults the store before invoking `ISchedulingService.SendMailAsync`; if the key exists, the send is skipped and logged as a dedupe hit. On successful send, the key is recorded in the same cycle.
- Dedupe state survives process restart (persisted in the Core SQLite cache, not in-memory).
- Recording uses the deterministic clock seam (`TimeProvider`/injected clock) for the timestamp column.

## Acceptance Criteria

- [x] A `sent_actions` table exists in the Core cache with an idempotent guarded migration (fresh-database and pre-existing-database upgrade paths both covered by tests; running the migration twice is safe).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md` (all `CoreCacheRepositorySentActionsTests` pass, including migration idempotency, pre-existing-database upgrade, and lazy schema-ensure)
- [x] A pure, deterministic dedupe-key builder produces `{mailbox}:{messageId}:{actionType}` and is unit-tested, including at least one CsCheck property test per T1 convention (determinism, component ordering, and distinctness for colon-free inputs).
  - Evidence: `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md` (`SentActionKeyTests` + `SentActionKeyPropertyTests` all passing; 100% file coverage per `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md`)
- [x] Inside the existing `SendEnabled`-gated block, `SchedulingWorker` consults the store before sending: on a hit it skips the send and logs a structured dedupe-hit message (message id and dedupe key as named template parameters), completing the message as a normal (not failed) outcome; on a miss it sends and then records the key with a timestamp from the injected `TimeProvider`.
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (fail-before counterpart: `evidence/regression-testing/dedupe-expect-fail.2026-07-02T12-10.md`)
- [x] A repeated worker cycle for the same candidate message does not invoke `ISchedulingService.SendMailAsync` a second time, including across a simulated restart (new worker and store instances over the same in-memory shared-cache SQLite database).
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (`RunCycle_TwoWorkerStorePairsOverOneDatabase_SendExactlyOnceInTotal`)
- [x] A failed send does NOT record the key (retry remains possible), and the failure stays isolated to that message per the existing `ProcessMessageSafelyAsync` behavior.
  - Evidence: `evidence/regression-testing/dedupe-pass-after.2026-07-02T12-11.md` (`RunCycle_SendFailure_DoesNotRecordAndProcessesNextCandidate`)
- [x] Full C# toolchain passes (format, lint, type-check, architecture, tests); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; MSTest + FluentAssertions + Moq; no temp files in tests; all files remain under the 500-line cap.
  - Evidence: `evidence/qa-gates/final-format.2026-07-02T12-12.md`, `evidence/qa-gates/final-build.2026-07-02T12-13.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T12-16.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T12-16.md`, `evidence/other/scope-and-size-verification.2026-07-02T12-18.md`

## Constraints & Risks

- Keep the store consult/record inside the existing `SendEnabled`-gated block so the kill switch remains the outer boundary.
- No temp files in tests (in-memory shared-cache SQLite pattern); MSTest + FluentAssertions + Moq; CsCheck available for property tests; 500-line cap (new table logic goes in a new `CoreCacheRepository.SentActions.cs` partial).
- Recording after send introduces a small at-least-once window (crash between send and record); document as accepted for Stage 0, consistent with the master's queue-retry semantics.

## Test Conditions to Consider

- [ ] Unit: dedupe-key builder determinism and component ordering.
- [ ] Unit: repository record/exists round-trip, migration idempotency.
- [ ] Unit: worker skip-on-hit, record-on-success, no-record-on-failure, kill-switch composition.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [ ] Create active feature folder from the template
