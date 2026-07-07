# `graph-subscriptions-delta` — User Story

- Issue: #117
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-03

## Story Statement

- As the operator of the OpenClaw assistant, I want the Core host to receive Graph change notifications through a thin webhook that only validates and enqueues, so that inbound coordination mail wakes the processing pipeline within seconds instead of waiting for the next polling interval.
- As the operator, I want `messages/delta` reconciliation with a persisted delta link per mailbox, so that missed, duplicated, or delayed notifications never cause permanently missed mail — the delta walk is the source of truth and the webhook is only a wake signal.
- As the operator, I want subscription lifetimes tracked durably and renewed ahead of expiration, with lifecycle notifications (`reauthorizationRequired`/`removed`/`missed`) handled automatically, so that the eventing path does not silently go dark when a subscription expires or is dropped.
- As the operator preparing the Stage 1 Azure deployment (F16), I want all of this logic host-neutral behind seams (`INotificationQueue`, `ISubscriptionStore`, `IDeltaLinkStore`), so that the same handlers run unchanged in the Core host today and in Functions/Container Apps later, and so nothing changes for the Local MVP while the flag is off.

## Problem / Why

Product Increment 1's eventing model is Graph change-notification subscriptions waking a thin webhook that enqueues work, with `messages/delta` as the authoritative reconciliation layer (`docs/open-claw-approach.master.md` §6.1-6.2, §8.1-8.2, §13 Step 4). None of it exists: no subscription management, no webhook handshake handling, no delta-link persistence, no lifecycle-notification handling (`reauthorizationRequired`/`removed`/`missed`). The Local MVP's polling workers (`MessagePollingWorker`/`CalendarPollingWorker` + `poll_cursors`) are the local analog, not a Graph-native implementation. Identified as gap F14 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`.

For operations this means the assistant's reaction latency is bounded by the polling interval, every poll pays a full list-query cost whether or not anything changed, and there is no recovery story for the cloud eventing path because the path does not exist yet.

## Personas & Scenarios

- Persona: **Operations owner (drmoisan)** — runs the OpenClaw Core host for the principal mailbox.
  - Cares about: reaction latency on inbound coordination mail; not losing messages when infrastructure hiccups; a local setup that keeps working untouched until he deliberately opts into the cloud path.
  - Constraints: no tenant-facing webhook URL exists until F16 deploys; everything delivered now must be verifiable locally with deterministic tests.
  - Goals and frustrations: wants event-driven wake-up with a self-healing reconciliation layer; frustrated by eventing designs that trust webhooks alone and go quietly stale when a notification is dropped.
  - Context: the Local MVP polls every 60 seconds; Stage 1 moves ingestion to Graph-native eventing per the master approach.

- Scenario: **Event-driven wake-up (happy path).**
  - Who: the operations owner, after enabling `OpenClaw:CloudSync:Enabled=true` on a deployed host with the Graph backend active.
  - Trigger: a counterparty sends a scheduling email to the principal Inbox.
  - Steps: Graph posts a change notification to `POST /graph/notifications`; the endpoint verifies the stored `clientState` (constant-time), enqueues `{mailbox, messageId, changeType}`, and returns 202 immediately; `NotificationDispatchWorker` dequeues, fetches the message through the Graph adapter, and upserts it into the Core cache; the existing scheduling pipeline picks it up as a candidate.
  - Obstacles/decisions: a notification with a wrong or missing `clientState` is dropped (still 202) and logged at Warning — the operator sees probe attempts in logs without the endpoint confirming anything to the sender.
  - Expected outcome: the message is in the cache and eligible for triage seconds after arrival, not at the next polling tick.

- Scenario: **Missed notifications recovered by delta (source of truth).**
  - Who: the same operator during a transient outage (host restart, queue overflow, or a Graph `missed` lifecycle notification).
  - Trigger: wake signals were dropped while the host was down; Graph sends a `missed` lifecycle notification, or the periodic reconcile timer fires.
  - Steps: `GraphDeltaReconciler` resumes from the stored `@odata.deltaLink` for the mailbox (or starts a full re-sync when the link is absent or `missed` was received), walks all `nextLink` pages, upserts every changed message into the same cache sink the poller uses, and persists the new delta link.
  - Expected outcome: the cache converges to the mailbox's true state; nothing is permanently missed regardless of webhook delivery gaps.

- Scenario: **Subscription lifecycle without operator intervention.**
  - Who: the operator, days later, doing nothing.
  - Trigger: the subscription approaches its 10,080-minute maximum lifetime, or Graph sends `reauthorizationRequired`/`removed`.
  - Steps: `SubscriptionRenewalWorker` computes renewal-due deterministically from the stored expiration and the configured lead (default 30 minutes) and PATCHes a new expiration; `removed` triggers recreation; state persists in `graph_subscriptions` and survives restarts, with a startup sweep recreating anything that expired while the host was down.
  - Expected outcome: the eventing path stays alive indefinitely; the operator only sees Information-level log entries recording renewals.

- Scenario: **Local MVP untouched (flag off).**
  - Who: the operator running the current local setup.
  - Trigger: upgrading to a build containing this feature without setting any new configuration.
  - Steps: none — `CloudSync:Enabled` defaults to false; no `/graph/notifications` route is mapped, no CloudSync workers start, polling behaves exactly as before.
  - Expected outcome: zero behavioral change, verified by a composition-root test.

## Acceptance Criteria

- [x] Webhook: `POST /graph/notifications` handshake echoes the decoded `validationToken` HTML-encoded as `text/plain` HTTP 200; notification batches with a missing or mismatched `clientState` are dropped with HTTP 202 and a Warning log (202-and-drop decision per spec); each valid notification enqueues exactly one `{mailbox, messageId, changeType}` work item onto `INotificationQueue`; the endpoint handler performs no Graph I/O.
- [x] Subscription manager: `POST /subscriptions` create and `PATCH /subscriptions/{id}` renew request shapes verified handler-level (mocked `HttpMessageHandler`); renewal-due computation is deterministic via `TimeProvider` with a configurable lead (default 30 minutes) against the 10,080-minute maximum lifetime; lifecycle notifications route `reauthorizationRequired` -> renew, `removed` -> recreate, `missed` -> delta reconcile; subscription state (id, resource, clientState, expiration) persists in `graph_subscriptions` and survives restart (store round-trip + idempotent schema-ensure verified).
- [x] Delta reconciler: walks multi-page `@odata.nextLink` responses of `GET /users/{mailbox}/mailFolders/Inbox/messages/delta` to the terminal `@odata.deltaLink`, persists the delta link per mailbox in `graph_delta_links`, and upserts changed messages into the Core cache through the same `CoreCacheRepository.UpsertMessagesAsync` sink the poller uses; an absent/empty stored link or a `missed` lifecycle notification triggers a full re-sync from an initial delta request; request shapes verified handler-level.
- [x] Host-neutral and opt-in: all new logic lives in `OpenClaw.Core.CloudSync`; the endpoint and CloudSync workers are registered only when `OpenClaw:CloudSync:Enabled=true`; with the flag absent the composition root's routes and service registrations are unchanged (backend-selection-style factory test); a namespace-scoped NetArchTest rule verifies CloudSync depends only on CloudGraph/CloudAuth seams and Contracts.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture tests, MSTest suite); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered; tests use mocked Graph handlers and `FakeTimeProvider` only — no live Graph calls, no temp files, no file exceeds 500 lines.

## Non-Goals

- **Azure queue implementations** (Service Bus / Storage Queue): deferred behind `INotificationQueue` to F16-adjacent work; no Azure SDK dependency in this feature.
- **Live end-to-end webhook delivery:** requires a tenant and a public HTTPS `NotificationUrl`; verified post-deployment under F16 (deployment) plus the existing HI-1 tenant handoff. This feature adds no new `human_interaction` requirement because every deliverable is locally verifiable code.
- **Calendar/event subscriptions and event delta:** scope is the principal Inbox messages resource only; the calendar path remains on the existing poller.
- **Cache deletion propagation:** `@removed` delta entries are logged and skipped; the Core cache is advisory and live items are re-added by delta.
- **Rich notifications (resource data):** not requested, so the 1,440-minute rich-notification lifetime and its encryption requirements are out of scope.
- **Replacing the local polling workers:** they remain the Local MVP ingestion path; CloudSync is additive and opt-in.
