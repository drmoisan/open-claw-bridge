# graph-subscriptions-delta (Issue #117)

- Date captured: 2026-07-03
- Author: drmoisan
- Status: Promoted -> docs/features/active/graph-subscriptions-delta/ (Issue #117)

- Issue: #117
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/117
- Last Updated: 2026-07-03
- Work Mode: full-feature

## Problem / Why

Product Increment 1's eventing model is Graph change-notification subscriptions waking a thin webhook that enqueues work, with `messages/delta` as the authoritative reconciliation layer (`docs/open-claw-approach.master.md` §6.1-6.2, §8.1-8.2, §13 Step 4). None of it exists: no subscription management, no webhook handshake handling, no delta-link persistence, no lifecycle-notification handling (`reauthorizationRequired`/`removed`/`missed`). The Local MVP's polling workers (`MessagePollingWorker`/`CalendarPollingWorker` + `poll_cursors`) are the local analog, not a Graph-native implementation. Identified as gap F14 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

## Proposed Behavior

Host-neutral core logic (new `OpenClaw.Core.CloudSync` namespace) + thin ASP.NET endpoints in the existing Core web host, so everything is unit-testable now and deployable to Azure later (F16 provides IaC; the same handlers run in Functions/Container Apps or the Core host):

- **Webhook endpoint** (`POST /graph/notifications` on the Core host, opt-in mapping): validation-handshake responder (echo `validationToken` as `text/plain` 200 within the 10-second contract; HTML-escape the token per master §6.1), notification receiver that validates `clientState`, enqueues work items, and returns 202 immediately. Queue seam `INotificationQueue` with an in-process channel implementation (Azure Service Bus/Storage Queue implementations are Stage 1 deployment concerns behind the same seam; deferred to F16-adjacent work — record explicitly).
- **Subscription manager** (`GraphSubscriptionManager` + `ISubscriptionStore` persisted in the Core cache): create/renew subscriptions via Graph REST (`POST/PATCH /subscriptions`) using the F13 `GraphRequestExecutor` conventions; track id, resource, expiration, clientState; renewal ahead of the 10,080-minute (~7-day) maximum lifetime (1,440 minutes for rich notifications) per master §6.1; lifecycle-notification handling that marks subscriptions for reauthorization/recreation and triggers reconciliation on `missed`.
- **Delta reconciliation** (`GraphDeltaReconciler` + delta-link persistence per mailbox): `GET /users/{id}/mailFolders/Inbox/messages/delta` walk persisting `@odata.deltaLink`, feeding changed message ids into the same processing path the poller uses today; scheduled periodic reconciliation per master §6.2 (webhooks are wake signals, delta is truth).
- All new tables (subscriptions, delta_links) via the established guarded-migration pattern; clock-free repository; deterministic tests with mocked Graph handler + FakeTimeProvider; no live Graph calls.

## Acceptance Criteria

- [x] Webhook: `POST /graph/notifications` handshake echoes the decoded `validationToken` HTML-encoded as `text/plain` HTTP 200; notification batches with a missing or mismatched `clientState` are dropped with HTTP 202 and a Warning log (202-and-drop decision per spec); each valid notification enqueues exactly one `{mailbox, messageId, changeType}` work item onto `INotificationQueue`; the endpoint handler performs no Graph I/O.
- [x] Subscription manager: `POST /subscriptions` create and `PATCH /subscriptions/{id}` renew request shapes verified handler-level (mocked `HttpMessageHandler`); renewal-due computation is deterministic via `TimeProvider` with a configurable lead (default 30 minutes) against the 10,080-minute maximum lifetime; lifecycle notifications route `reauthorizationRequired` -> renew, `removed` -> recreate, `missed` -> delta reconcile; subscription state (id, resource, clientState, expiration) persists in `graph_subscriptions` and survives restart (store round-trip + idempotent schema-ensure verified).
- [x] Delta reconciler: walks multi-page `@odata.nextLink` responses of `GET /users/{mailbox}/mailFolders/Inbox/messages/delta` to the terminal `@odata.deltaLink`, persists the delta link per mailbox in `graph_delta_links`, and upserts changed messages into the Core cache through the same `CoreCacheRepository.UpsertMessagesAsync` sink the poller uses; an absent/empty stored link or a `missed` lifecycle notification triggers a full re-sync from an initial delta request; request shapes verified handler-level.
- [x] Host-neutral and opt-in: all new logic lives in `OpenClaw.Core.CloudSync`; the endpoint and CloudSync workers are registered only when `OpenClaw:CloudSync:Enabled=true`; with the flag absent the composition root's routes and service registrations are unchanged (backend-selection-style factory test); a namespace-scoped NetArchTest rule verifies CloudSync depends only on CloudGraph/CloudAuth seams and Contracts.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture tests, MSTest suite); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered; tests use mocked Graph handlers and `FakeTimeProvider` only — no live Graph calls, no temp files, no file exceeds 500 lines.

## Constraints & Risks

- Live subscription creation requires a tenant + public webhook URL: out of scope; covered by runbook/deployment work (F16) — no live calls in tests; record explicitly that end-to-end webhook delivery is verified post-deployment (existing HI-1 handoff covers tenant provisioning; F16 adds deployment).
- Azure queue implementations deferred behind `INotificationQueue` (in-process channel now) — record as an explicit F16 note; do NOT add Azure SDK dependencies in this feature.
- MSTest + FluentAssertions + Moq + CsCheck; FakeTimeProvider; no temp files; 500-line cap; namespace-not-project convention.

## Test Conditions to Consider

- [ ] Handshake echo, escaping, content type; clientState matrix; enqueue payload shape.
- [ ] Renewal-due boundary math; lifecycle routing table; subscription store round-trip/migration idempotency.
- [ ] Delta walk (multi-page nextLink -> deltaLink), link persistence, re-sync trigger matrix.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
