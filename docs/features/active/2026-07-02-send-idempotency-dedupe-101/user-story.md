# `send-idempotency-dedupe` — User Story

- Issue: #101
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02T11-40

## Story Statement

- As the mailbox owner whose inbox the scheduling assistant acts on, I want the assistant to send each scheduling reply exactly one time per message and action, so that my correspondents never receive duplicate proposal emails when the worker's polling loop revisits the same message.
- As the mailbox owner operating the assistant locally, I want the sent-reply record to survive process restarts and transient send failures, so that a restart never causes a re-send of an already-delivered reply, and a genuinely failed send remains eligible for retry on the next cycle.

## Problem / Why

With `HostAdapterSchedulingService.SendMailAsync` now wired (issue #99 / PR #100), a retried or repeated scheduling cycle can resend the same reply: no dedupe-key concept exists in `SchedulingWorker`'s pipeline, `MailRoutes`, or `SendMailRpcHandler`. The master specification requires idempotency keys for mail sends with an internal dedupe key shaped like `{tenantId}:{mailboxUpn}:{messageId}:{actionType}` (`docs/open-claw-approach.master.md` §6.3); the Local MVP equivalent is a persisted per-message/action guard consulted before send. This was recorded as the accepted interim risk in the #99 spec, to be closed by this feature. Identified as gap F6 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Personas & Scenarios

- Persona: **The mailbox owner (Dan)** — a single-mailbox user running the Local MVP assistant against his own inbox with `SendEnabled` turned on.
  - Who the user is: the owner of the mailbox the assistant reads from and replies on behalf of; also the operator who starts, stops, and restarts the Core service on his machine.
  - What they care about: the assistant's outbound mail must be trustworthy. A duplicate "Proposed times: ..." reply to a colleague is embarrassing and erodes confidence in letting the assistant send at all.
  - Their constraints: the worker polls on a cycle (`SchedulingWorker` re-runs every minute) and the same candidate message legitimately reappears in consecutive cycles; the process restarts routinely (deploys, reboots), so any in-memory "already sent" marker is insufficient.
  - Their goals and frustrations: goal — leave `SendEnabled` on with confidence; frustration — today the only safe posture after any restart or repeated cycle is to assume a double-send is possible, because nothing records what was already sent.
  - Their context and motivations: this is the accepted interim risk from issue #99; closing it is the precondition for treating the read-and-reply loop as safe for daily use.

- Scenario: **Repeated cycle over the same candidate message.**
  - Who is acting: the `SchedulingWorker` background service, on behalf of the mailbox owner.
  - What triggered the action: a colleague's meeting-request message is selected as a scheduling candidate; the worker's cycle runs, triages it to a decision requiring action, proposes slots, and — `SendEnabled` being true — sends the proposal reply. One minute later the next cycle runs and selects the same message again (the candidate source has no send-awareness).
  - What steps do they take: on the first cycle the worker finds no `sent_actions` record for `{mailbox}:{messageId}:proposal-reply`, sends the reply, and records the key with a clock-seam timestamp. On the second cycle the worker finds the key already recorded, logs a structured dedupe-hit message, and skips the send.
  - What obstacles or decisions occur: the dedupe hit is a normal, expected outcome — it is logged at Information level and the message counts as processed, not failed; the worker moves on to the next candidate.
  - What outcome do they expect: the colleague receives exactly one proposal email, regardless of how many cycles revisit the message.

- Scenario: **Restart between cycles.**
  - Who is acting: the mailbox owner restarting the Core service; then the `SchedulingWorker` in the new process.
  - What triggered the action: a routine restart (deploy, reboot) after a cycle that already sent a proposal reply for message `msg-1`.
  - What steps do they take: the owner stops and restarts the service. The new process's first scheduling cycle selects `msg-1` again, consults the persisted `sent_actions` table in the Core SQLite cache, finds the key recorded, and skips the send.
  - What obstacles or decisions occur: none for the owner — persistence in the database (not process memory) makes the restart invisible to dedupe behavior.
  - What outcome do they expect: no second email after the restart.

- Scenario: **Transient send failure, then retry.**
  - Who is acting: the `SchedulingWorker`, with the HostAdapter temporarily unreachable.
  - What triggered the action: the worker attempts the proposal-reply send and `SendMailAsync` throws.
  - What steps do they take: because recording happens only after a successful send, no key is written. The existing failure isolation logs the error and continues with the next candidate. On a later cycle, the send is attempted again and, on success, the key is recorded.
  - What obstacles or decisions occur: a crash in the narrow window between a successful send and the record step could allow one re-send after restart — the accepted Stage 0 at-least-once trade-off, matching the master's queue-retry semantics.
  - What outcome do they expect: a failed send is retried until it succeeds; a successful send is never repeated (outside the documented at-least-once window).

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

## Non-Goals

- No `{tenantId}` component in the dedupe key (multi-tenant/Graph is Stage 1; the local key is `{mailbox}:{messageId}:{actionType}`).
- No changes to `OpenClaw.HostAdapter`, `OpenClaw.MailBridge`, or any wire contract — `SendMailRpcHandler` and `MailRoutes` remain dedupe-unaware; dedupe is enforced at the `SchedulingWorker` call site only.
- No calendar-write idempotency (no calendar-write path exists yet; master §6.3's `transactionId` guidance applies when Stage 2 adds one).
- No exactly-once guarantee: the crash window between a successful send and the record step is accepted for Stage 0 (at-least-once, per the master's queue-retry semantics).
- No audit-log table, retention/cleanup policy, or admin/query surface for `sent_actions` beyond the store interface the worker consumes.
- No change to kill-switch semantics: `SendEnabled=false` still suppresses all sends and, with this feature, also leaves the dedupe store untouched.
