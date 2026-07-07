# graph-subscriptions-delta — Spec

- **Issue:** #117
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-03
- **Status:** Ready
- **Version:** 1.0

## Overview

Product Increment 1's eventing model is Graph change-notification subscriptions waking a thin webhook that enqueues work, with `messages/delta` as the authoritative reconciliation layer (`docs/open-claw-approach.master.md` §6.1-6.2, §8.1-8.2, §13 Step 4). None of it exists today: no subscription management, no webhook handshake handling, no delta-link persistence, no lifecycle-notification handling (`reauthorizationRequired`/`removed`/`missed`). The Local MVP's polling workers (`MessagePollingWorker`/`CalendarPollingWorker` + `poll_cursors`) are the local analog, not a Graph-native implementation. Identified as gap F14 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Epic C item 14; the gap analysis marks the webhook/enqueue/delta logic as "fully automatable in-repo" against a mocked Graph client and a fake queue).

This feature delivers the host-neutral core of that eventing model in a new `OpenClaw.Core.CloudSync` namespace, wired into the existing Core web host behind an opt-in configuration flag, reusing the F13 CloudGraph/CloudAuth seams (`GraphRequestExecutor`, `GraphAdapterOptions`, `IAppTokenProvider`, `TimeProvider`). Everything is unit-testable now; Azure hosting and queue backends arrive with F16 behind the same seams.

## Behavior

### Webhook endpoint (`POST /graph/notifications`)

Mapped on the Core host only when `OpenClaw:CloudSync:Enabled=true` (see Opt-in mechanism below). Behavior per master §6.1 and §8.2:

1. **Validation handshake.** When the request carries a `validationToken` query parameter, respond HTTP 200, content type `text/plain`, body = the URL-decoded token, HTML-encoded (`WebUtility.HtmlEncode`) because the token is opaque untrusted input (master §6.1: "Escape HTML/JS to reduce XSS-style risks"). The handler is synchronous apart from response writing, so the 10-second contract is met by construction; no Graph or database I/O occurs on this path.
2. **Change notifications.** Otherwise parse the JSON body (`{ "value": [ ... ] }`). For each notification:
   - Look up the stored subscription by `subscriptionId` and compare `clientState` using `CryptographicOperations.FixedTimeEquals`. A missing subscription record or mismatched `clientState` drops the notification: the response is still HTTP 202 and the drop is logged at Warning (Decision D-1 below).
   - A valid notification enqueues exactly one `NotificationWorkItem { Mailbox, MessageId, ChangeType }` onto `INotificationQueue` (work-item shape per master §8.2: `{userId, messageId, changeType}`).
   - Respond HTTP 202 immediately after enqueueing; the handler performs no Graph I/O (master §6.1: validate quickly, enqueue, return immediately; all Graph reads happen in workers).
3. **Lifecycle notifications.** Items carrying a `lifecycleEvent` field (`reauthorizationRequired`, `removed`, `missed`) are validated the same way (subscription lookup + constant-time `clientState` compare) and enqueued as lifecycle work items routed to the subscription manager / reconciler (routing table under Subscription manager). One endpoint serves both `notificationUrl` and `lifecycleNotificationUrl`.

### Subscription manager (`GraphSubscriptionManager` + `ISubscriptionStore`)

- **Create:** `POST /subscriptions` with `changeType: "created,updated"`, `resource: "users/{PrincipalMailboxUpn}/mailFolders('Inbox')/messages"`, `notificationUrl`/`lifecycleNotificationUrl` from `CloudSyncOptions.NotificationUrl`, a freshly generated random `clientState`, and `expirationDateTime = now + SubscriptionLifetimeMinutes` (default and cap 10,080 minutes, the master §6.1 maximum for Outlook message subscriptions without resource data; rich notifications' 1,440-minute lifetime is out of scope because no resource data is requested). Requires `Application Mail.Read` (master §13 Step 4). One subscription per principal Inbox — the design reuses subscriptions and stays far below the 1,000-active-subscriptions-per-mailbox cap (master §6.1).
- **Renew:** `PATCH /subscriptions/{id}` with a new `expirationDateTime`. `SubscriptionRenewalWorker` (a `BackgroundService` driven by `TimeProvider`-based delays, matching the repo clock-seam rules) computes renewal-due as `now >= expiration - RenewalLeadMinutes` (default 30) and renews or recreates as needed; on startup it performs an immediate sweep so an expired-while-down subscription is recreated promptly.
- **Lifecycle routing:**
  - `reauthorizationRequired` -> renew (PATCH) the subscription now; on auth failure surface the mapped `CONFIGURATION_ERROR`/`UNAUTHORIZED` envelope and mark the subscription `reauthorize_failed`.
  - `removed` -> delete the local record and recreate the subscription.
  - `missed` -> trigger a delta reconciliation for the subscription's mailbox (delta is the recovery mechanism; master §6.1/§6.2).
- **Persistence:** `ISubscriptionStore` implemented by a `CoreCacheRepository.Subscriptions` partial (table `graph_subscriptions`); state survives restart. Track subscription id, resource, expiration timestamp, and clientState in durable storage per master §6.1 and §13 Step 4.

### Delta reconciliation (`GraphDeltaReconciler` + `IDeltaLinkStore`)

- Walks `GET /users/{mailbox}/mailFolders/Inbox/messages/delta?$select={MessageSelect}` following `@odata.nextLink` pages (bounded by `GraphAdapterOptions.MaxPages`, the D3 runaway bound) until the terminal `@odata.deltaLink`, which is persisted per mailbox in `graph_delta_links`. Subsequent rounds resume from the stored delta link. An absent/empty stored link, or a `missed` lifecycle event, starts a full re-sync from the initial delta request (master §6.2 recovery semantics).
- Each returned message is mapped with the existing `GraphMessageMapper.Map` and upserted through `CoreCacheRepository.UpsertMessagesAsync` — the identical sink `MessagePollingWorker` uses — so the existing `SchedulingWorker`/`CacheSchedulingCandidateSource` pipeline picks changes up with no pipeline changes (seam decision D-2 below). `@removed` entries are logged at Debug and skipped; cache deletion propagation is a recorded non-goal (the Core cache is advisory and delta re-adds live items).
- `DeltaReconciliationWorker` runs the reconciler on a periodic `TimeProvider` schedule (`ReconcileIntervalMinutes`, default 60) per master §6.2: webhooks are wake signals, delta is truth.

### Notification dispatch (`NotificationDispatchWorker`)

Consumes `INotificationQueue`; for each message work item it calls `IHostAdapterClient.GetMessageAsync(messageId)` and upserts the result via `CoreCacheRepository.UpsertMessagesAsync`. Because Graph-adapter envelopes carry `Meta.Bridge == null` (the executor synthesizes `ApiMeta(requestId, "cloudgraph", null)`), the worker synthesizes the D2-shaped status `BridgeStatusDto("ready", "graph", OutlookConnected: true, CacheStale: false, ...)` after a successful Graph fetch — a successful call is itself the liveness evidence, mirroring `GraphHostAdapterClient.GetStatusAsync`. A failed fetch logs a Warning and drops the item; the periodic delta reconciliation is the recovery path (Decision D-3).

## Design Decisions

- **D-1 — Bad `clientState` returns 202-and-drop, not 401.** The endpoint accepts the batch (HTTP 202), drops invalid notifications, and logs at Warning with the subscriptionId. Rationale: a 401 turns the endpoint into a validation oracle for `clientState` guessing and provokes Graph redelivery retries for traffic we will never accept; master §8.2 shows the webhook acknowledging with 202 and doing all real work in the worker. Comparison is constant-time (`CryptographicOperations.FixedTimeEquals`) over the stored per-subscription value.
- **D-2 — Processing seam: cache upsert is the sink; the queue carries only webhook wake signals.** Evidence: `CacheSchedulingCandidateSource` reads candidate ids from the cache the pollers populate, and `CoreCacheRepository.UpsertMessagesAsync(messages, bridgeStatus, requestId, observedAtUtc)` is the existing write surface. Webhook notifications carry only ids, so they flow through `INotificationQueue` to `NotificationDispatchWorker`, which fetches and upserts. Delta pages already contain the full `$select` payload, so the reconciler upserts directly (via `GraphMessageMapper`) rather than re-enqueueing ids — no second Graph round-trip per message. No new ingest pipeline is created; `SchedulingWorker` is untouched.
- **D-3 — `BridgeStatusDto` synthesis for CloudSync upserts.** `UpsertMessagesAsync` requires a `BridgeStatusDto`, but Graph-adapter envelope metadata has `Bridge == null`. CloudSync writers synthesize the same status shape `GetStatusAsync` produces on probe success ("ready"/"graph"/connected/not-stale), justified by the successful Graph call that immediately precedes each upsert. No fabricated healthy status is written on a failed call.
- **D-4 — `INotificationQueue` with in-process `System.Threading.Channels`.** `ChannelNotificationQueue` wraps a bounded channel (`QueueCapacity`, default 1000) with `BoundedChannelFullMode.DropWrite` plus a Warning log: the webhook must never block, and dropped wake signals are recovered by delta reconciliation (master §6.2). Azure Service Bus/Storage Queue implementations are F16 deployment concerns behind this same interface; no Azure SDK dependency is added in this feature.
- **D-5 — `clientState` generation seam.** A narrow `IClientStateGenerator` interface with a production implementation over `RandomNumberGenerator` (32 bytes, base64url, under Graph's 128-char limit) satisfies the banned-API rules (no `Random.Shared`) while keeping tests deterministic via a substituted generator. The value is generated per subscription, stored in `graph_subscriptions`, and compared constant-time (D-1).
- **D-6 — Endpoint and worker opt-in mirrors the F13 selection block.** `Program.cs` gains one conditional block: `if (builder.Configuration.GetValue<bool>("OpenClaw:CloudSync:Enabled")) { builder.Services.AddCloudSync(builder.Configuration); }` plus a matching `app.MapGraphNotificationsEndpoint()` guard after `UseRouting()`. With the flag absent (local default OFF) the composition root registers and maps exactly what it does today, verified by a `CoreTestWebApplicationFactory` test in the style of `GraphBackendSelectionTests` (using `UseSetting`, which is required for configuration read at composition time under minimal hosting).
- **D-7 — CloudSync requires the Graph backend.** `CloudSyncOptionsValidator` fails closed at startup (`ValidateOnStart`, mirroring `GraphAdapterOptionsValidator` wiring) when `CloudSync:Enabled=true` and `GraphAdapter:Enabled` is false, because dispatch fetches by Graph message id through `IHostAdapterClient` and delta/subscription calls need `GraphAdapterOptions` (BaseUrl, principal UPN, paging/retry bounds) and CloudAuth. It also requires `NotificationUrl` to be an absolute `https` URI.
- **D-8 — `GraphRequestExecutor` reuse, not a sibling.** `GraphSubscriptionManager` and `GraphDeltaReconciler` construct their own `GraphRequestExecutor` instances exactly as `GraphHostAdapterClient` does (same assembly; the type is `internal`), with an injected typed `HttpClient` whose `BaseAddress` comes from `GraphAdapterOptions.BaseUrl`, plus `IAppTokenProvider`, `TimeProvider`, and `ILogger`. This inherits bearer/`client-request-id` headers, 429/502/503/504 retry with `Retry-After` precedence, the D5 error matrix, and envelope synthesis with zero new pipeline code.

## Inputs / Outputs

- **Inputs:** Graph change/lifecycle notification POSTs; Graph REST responses for `/subscriptions` and `messages/delta` (mocked in tests); configuration below.
- **Outputs:** rows in `graph_subscriptions`, `graph_delta_links`, `messages` (via the existing upsert), and `ingest_runs` (delta reconciliation outcomes recorded with the poller's `AddIngestRunAsync` convention, operation name `delta_reconcile`); structured logs (Warning for drops/failures, Information for subscription lifecycle actions). Tokens and notification bodies are never logged (executor convention).
- **Config keys and defaults** (section `OpenClaw:CloudSync`, env prefix `OpenClaw__CloudSync__`):

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `false` | Opt-in gate for endpoint + workers (D-6). |
| `NotificationUrl` | — (required when enabled) | Public absolute `https` URL Graph posts to; used as `notificationUrl` and `lifecycleNotificationUrl`. |
| `SubscriptionLifetimeMinutes` | `10080` | Requested lifetime; validator caps at 10,080 (master §6.1). |
| `RenewalLeadMinutes` | `30` | Renew when `now >= expiration - lead`. Must be `>= 1` and `< SubscriptionLifetimeMinutes`. |
| `ReconcileIntervalMinutes` | `60` | Periodic delta reconciliation cadence. |
| `QueueCapacity` | `1000` | Bounded channel capacity (D-4). |

- **Backward compatibility:** flag-absent behavior is byte-identical registration and routing; all schema changes are additive `CREATE TABLE IF NOT EXISTS`.

## API / CLI Surface

- `POST /graph/notifications?validationToken={token}` -> `200 text/plain`, body = HTML-encoded decoded token.
- `POST /graph/notifications` with body `{"value":[{"subscriptionId":"...","clientState":"...","changeType":"created","resource":"...","resourceData":{"id":"..."}}]}` -> `202` always (valid items enqueued; invalid items dropped + Warning). Malformed JSON -> `400` with the repo's `{ code: "INVALID_REQUEST", message }` shape (matches existing endpoint error convention in `Program.cs`).
- Outbound Graph requests (handler-level verified):
  - `POST {BaseUrl}subscriptions` — JSON body with `changeType`, `resource`, `notificationUrl`, `lifecycleNotificationUrl`, `clientState`, `expirationDateTime` (ISO-8601 UTC).
  - `PATCH {BaseUrl}subscriptions/{id}` — JSON body with `expirationDateTime` only.
  - `GET {BaseUrl}users/{p}/mailFolders/Inbox/messages/delta?$select={MessageSelect}` and subsequent stored `nextLink`/`deltaLink` URLs verbatim.
- No CLI surface.

## Data & State

New tables via the established guarded-migration pattern (fresh-DDL in `CreateTablesSql` plus lazy once-per-instance schema-ensure in the new partials, following the `sent_actions` precedent; idempotent `CREATE TABLE IF NOT EXISTS`):

```sql
CREATE TABLE IF NOT EXISTS graph_subscriptions(
    subscription_id TEXT PRIMARY KEY,
    resource TEXT NOT NULL,
    mailbox TEXT NOT NULL,
    client_state TEXT NOT NULL,
    expiration_utc TEXT NOT NULL,
    status TEXT NOT NULL,           -- active | reauthorize_failed
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS graph_delta_links(
    mailbox TEXT PRIMARY KEY,
    delta_link TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);
```

- Repository partials are clock-free: all timestamps are caller-supplied `DateTimeOffset` values rendered with the existing `"O"`/invariant convention (SentActions precedent).
- Invariants: one `graph_subscriptions` row per (mailbox, resource) in practice (single Inbox subscription); `delta_link` is opaque and stored verbatim; delta upserts are idempotent through the existing `ON CONFLICT(bridge_id) DO UPDATE` message upsert.
- Migration/backfill: none required; both tables start empty and self-create.

## Constraints & Risks

- Live subscription creation requires a tenant + public webhook URL: out of scope; covered by runbook/deployment work (F16) — no live calls in tests. End-to-end webhook delivery is verified post-deployment (existing HI-1 handoff covers tenant provisioning; F16 adds deployment). **No new `human_interaction` requirement is added by this feature: every deliverable here is locally verifiable code** (handler-level request shapes, endpoint behavior through the test factory, deterministic time math, store round-trips).
- Azure queue implementations deferred behind `INotificationQueue` (in-process channel now) — explicit F16 note; do NOT add Azure SDK dependencies in this feature.
- MSTest + FluentAssertions + Moq + CsCheck (the repository's actual test stack); `FakeTimeProvider`; no temp files (`CoreTestWebApplicationFactory` uses in-memory SQLite); 500-line cap per file; namespace-not-project convention (new `OpenClaw.Core.CloudSync` namespace inside the existing project, guarded by a namespace-scoped NetArchTest rule, per the #74/#113 precedent).
- Risk: dropped wake signals under queue pressure (D-4) delay processing until the next reconcile tick; mitigated by the delta-as-truth design and a Warning log for observability.
- Risk: the single shared endpoint handles both change and lifecycle notifications; a Graph payload variant not modeled in `NotificationWireModels` would be dropped with a Warning rather than crash (fail-visible, not fail-silent).

## Implementation Strategy

- **Implementation scope** (all new files under `src/OpenClaw.Core/CloudSync/` unless noted):
  - `CloudSyncOptions.cs`, `CloudSyncOptionsValidator.cs` (fail-closed startup validation, D-7).
  - `NotificationWireModels.cs` (change + lifecycle notification wire shapes; web-default JSON options shared with `GraphRequestExecutor.JsonOptions`).
  - `NotificationWorkItem.cs` (record: `Mailbox`, `MessageId`, `ChangeType`), `INotificationQueue.cs`, `ChannelNotificationQueue.cs` (D-4).
  - `NotificationRequestProcessor.cs` (host-neutral handshake/validation/enqueue logic) + `GraphNotificationsEndpoint.cs` (`MapGraphNotificationsEndpoint` extension; thin ASP.NET glue only).
  - `IClientStateGenerator.cs`, `CryptoClientStateGenerator.cs` (D-5).
  - `ISubscriptionStore.cs`, `IDeltaLinkStore.cs`; `CoreCacheRepository.Subscriptions.cs` and `CoreCacheRepository.DeltaLinks.cs` partials in `src/OpenClaw.Core/` (schema above; `CoreCacheRepository.Schema.cs` gains the fresh-DDL entries).
  - `GraphSubscriptionManager.cs` (create/renew/lifecycle routing; own `GraphRequestExecutor`, D-8), `SubscriptionRenewalWorker.cs`.
  - `GraphDeltaReconciler.cs` (delta walk + upsert + link persistence), `DeltaReconciliationWorker.cs`, `NotificationDispatchWorker.cs` (D-2/D-3).
  - `CloudSyncServiceCollectionExtensions.cs` (`AddCloudSync(IConfiguration)` mirroring `AddGraphHostAdapterClient`: options bind + `ValidateOnStart`, typed `HttpClient` registrations, hosted workers, queue/store/generator singletons).
  - `src/OpenClaw.Core/Program.cs`: the two D-6 conditional lines (services + endpoint map) — the only touched existing file besides the schema partial.
  - Architecture tests: namespace-scoped rule — `OpenClaw.Core.CloudSync` may depend only on `OpenClaw.Core.CloudGraph`, `OpenClaw.Core.CloudAuth`, contracts, and BCL/hosting abstractions; nothing outside CloudSync may depend on CloudSync internals except the composition root.
- **Dependency changes:** none. `System.Threading.Channels` is in-box; no Azure SDKs (explicit deferral).
- **Logging/telemetry:** Warning for clientState drops, queue-full drops, failed dispatch fetches, renewal failures; Information for subscription create/renew/recreate and reconcile completion; `ingest_runs` rows for reconcile outcomes. No token or body logging.
- **Rollout:** local default OFF (`CloudSync:Enabled` absent). Enabling requires the Graph backend (D-7) and a reachable `NotificationUrl`, which arrives with F16 deployment; until then the feature is exercised entirely by tests.

## Acceptance Criteria

- [x] Webhook: `POST /graph/notifications` handshake echoes the decoded `validationToken` HTML-encoded as `text/plain` HTTP 200; notification batches with a missing or mismatched `clientState` are dropped with HTTP 202 and a Warning log (202-and-drop decision per spec); each valid notification enqueues exactly one `{mailbox, messageId, changeType}` work item onto `INotificationQueue`; the endpoint handler performs no Graph I/O.
- [x] Subscription manager: `POST /subscriptions` create and `PATCH /subscriptions/{id}` renew request shapes verified handler-level (mocked `HttpMessageHandler`); renewal-due computation is deterministic via `TimeProvider` with a configurable lead (default 30 minutes) against the 10,080-minute maximum lifetime; lifecycle notifications route `reauthorizationRequired` -> renew, `removed` -> recreate, `missed` -> delta reconcile; subscription state (id, resource, clientState, expiration) persists in `graph_subscriptions` and survives restart (store round-trip + idempotent schema-ensure verified).
- [x] Delta reconciler: walks multi-page `@odata.nextLink` responses of `GET /users/{mailbox}/mailFolders/Inbox/messages/delta` to the terminal `@odata.deltaLink`, persists the delta link per mailbox in `graph_delta_links`, and upserts changed messages into the Core cache through the same `CoreCacheRepository.UpsertMessagesAsync` sink the poller uses; an absent/empty stored link or a `missed` lifecycle notification triggers a full re-sync from an initial delta request; request shapes verified handler-level.
- [x] Host-neutral and opt-in: all new logic lives in `OpenClaw.Core.CloudSync`; the endpoint and CloudSync workers are registered only when `OpenClaw:CloudSync:Enabled=true`; with the flag absent the composition root's routes and service registrations are unchanged (backend-selection-style factory test); a namespace-scoped NetArchTest rule verifies CloudSync depends only on CloudGraph/CloudAuth seams and Contracts.
- [x] Full C# toolchain passes (CSharpier, analyzers, build, architecture tests, MSTest suite); line coverage >= 85% and branch coverage >= 75% hold with changed lines covered; tests use mocked Graph handlers and `FakeTimeProvider` only — no live Graph calls, no temp files, no file exceeds 500 lines.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Handshake echo, escaping, content type; clientState matrix (missing subscription, mismatched value, valid, constant-time compare seam); enqueue payload shape; queue-full drop behavior; malformed-body 400.
- [ ] Renewal-due boundary math (`FakeTimeProvider` at `expiration - lead ± 1 tick`); lifecycle routing table (`reauthorizationRequired`/`removed`/`missed`); subscription store round-trip; schema-ensure idempotency (double initialize); create/renew request-shape pinning (URL, method, JSON body, headers).
- [ ] Delta walk: multi-page `nextLink` -> `deltaLink`, `MaxPages` bound, link persistence and resume-from-link, empty-link full re-sync trigger, `missed`-triggered re-sync, `@removed` skip, upsert idempotency; property-based (CsCheck) coverage for pure renewal-due and clientState-comparison logic per the T1/T2 property-test rule.
- [ ] Opt-in composition: flag absent -> no `/graph/notifications` route, no CloudSync hosted services (factory test); flag true with valid Graph/CloudAuth settings -> route mapped and workers registered (`UseSetting` precedent); `CloudSync:Enabled` without `GraphAdapter:Enabled` -> fail-closed startup validation error.
