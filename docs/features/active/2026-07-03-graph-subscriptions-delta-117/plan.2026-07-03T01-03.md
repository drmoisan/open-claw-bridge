# graph-subscriptions-delta - Plan

- **Issue:** #117
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-03T01-03
- **Status:** Ready for preflight
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata)

## Required References

- Policy compliance order: `CLAUDE.md` auto-loaded rules, then `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`
- Requirements source: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/spec.md` (authoritative; 5 AC, design decisions D-1..D-8), `issue.md`, `user-story.md`
- Conventions to reuse (read before implementing, do not modify unless a task names the file):
  - `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs` (internal executor, D-8 reuse), `src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs` (options bind + `ValidateOnStart` + typed `HttpClient` pattern), `src/OpenClaw.Core/Program.cs` lines 50-65 (opt-in block precedent)
  - `src/OpenClaw.Core/CoreCacheRepository.SentActions.cs` (partial + lazy schema-ensure precedent), `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (`CreateTablesSql` fresh DDL)
  - `tests/OpenClaw.Core.Tests/CloudGraph/` (FakeHttpHandler via `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`, recorded payload constants in `GraphPayloadFixtures.cs`, CsCheck property tests, `FakeTimeProvider`), `tests/OpenClaw.Core.Tests/CloudGraph/GraphBackendSelectionTests.cs` + `tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs` (`UseSetting` composition-time configuration precedent)

**All work must comply with these policies; do not duplicate their content here.**

## Global Conventions for This Plan

- `<FEATURE>` = `docs/features/active/2026-07-03-graph-subscriptions-delta-117`. All evidence artifacts go under `<FEATURE>/evidence/<kind>/` (baseline, qa-gates, regression-testing). Raw command intermediates (TRX, Cobertura XML, build logs) go under `artifacts/csharp/`; evidence markdown files summarize them. Evidence paths outside `<FEATURE>/evidence/` are prohibited.
- `<ts>` = actual run timestamp in `yyyy-MM-ddTHH-mm` format, substituted at execution time.
- Every evidence artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- Test stack: MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`). No live Graph calls; all Graph responses are recorded payloads as in-code string constants. No temporary files (SQLite in-memory / shared-cache connection strings per existing repository tests). No `Task.Delay`/`Thread.Sleep`/wall-clock reads.
- The C# toolchain loop for this plan is, in order: (1) `csharpier format .` then `csharpier check .` (global tool 1.3.0; do NOT use `dotnet csharpier`), (2) `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors), (3) `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (includes NetArchTest architecture tests). If any step fails or changes files, restart the loop from step 1 until all steps pass in a single pass.
- Diff-scope confinement: production changes are limited to new files under `src/OpenClaw.Core/CloudSync/`, new partials `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` and `src/OpenClaw.Core/CoreCacheRepository.DeltaLinks.cs`, the `CreateTablesSql` addition in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, and the D-6 opt-in block in `src/OpenClaw.Core/Program.cs`. Test changes are limited to new files under `tests/OpenClaw.Core.Tests/CloudSync/` and new `tests/OpenClaw.Core.Tests/CoreCacheRepositorySubscriptionsTests.cs` / `CoreCacheRepositoryDeltaLinksTests.cs`. No file may exceed 500 lines.
- No new package dependencies. `System.Threading.Channels` is in-box; no Azure SDKs (explicit F16 deferral, spec Constraints).

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Compliance

- [x] [P0-T1] Read the repository policy files in the required order: `CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, then `<FEATURE>/spec.md`, `<FEATURE>/issue.md`, `<FEATURE>/user-story.md`
  - Acceptance: `<FEATURE>/evidence/baseline/phase0-instructions-read.md` exists containing `Timestamp:`, `Policy Order:`, and the explicit list of files read
- [x] [P0-T2] Capture the formatting baseline by running `csharpier check .` from the repository root
  - Acceptance: `<FEATURE>/evidence/baseline/csharpier-check.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail and any offending file count)
- [x] [P0-T3] Capture the build/analyzer baseline by running `dotnet build OpenClaw.MailBridge.sln`
  - Acceptance: `<FEATURE>/evidence/baseline/dotnet-build.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts)
- [x] [P0-T4] Capture the test-and-coverage baseline by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and extracting numeric line and branch coverage from the produced Cobertura report (raw report retained under `artifacts/csharp/`)
  - Acceptance: `<FEATURE>/evidence/baseline/dotnet-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing test pass/fail counts and numeric baseline line-coverage percent and branch-coverage percent (no placeholders)

### Phase 1 — CloudSync Options, ClientState Generator, and Queue Seam

- [x] [P1-T1] Create `src/OpenClaw.Core/CloudSync/CloudSyncOptions.cs` with properties and defaults per the spec config table: `Enabled` (false), `NotificationUrl` (null, required when enabled), `SubscriptionLifetimeMinutes` (10080), `RenewalLeadMinutes` (30), `ReconcileIntervalMinutes` (60), `QueueCapacity` (1000); section constant `OpenClaw:CloudSync`
  - Acceptance: file compiles; defaults match the spec table exactly
- [x] [P1-T2] Create `src/OpenClaw.Core/CloudSync/CloudSyncOptionsValidator.cs` (D-7 fail-closed rules, mirroring `GraphAdapterOptionsValidator` style): validation errors when `Enabled=true` and the Graph backend flag is false (graph-enabled state passed in by the caller); `NotificationUrl` must be an absolute `https` URI; `SubscriptionLifetimeMinutes` in `[1, 10080]` (10,080 cap per master §6.1); `RenewalLeadMinutes >= 1` and `< SubscriptionLifetimeMinutes`; `ReconcileIntervalMinutes >= 1`; `QueueCapacity >= 1`
  - Acceptance: static/pure validator returning an error list; no I/O; no clock use
- [x] [P1-T3] Create `src/OpenClaw.Core/CloudSync/IClientStateGenerator.cs` and `src/OpenClaw.Core/CloudSync/CryptoClientStateGenerator.cs` (D-5): production implementation over `System.Security.Cryptography.RandomNumberGenerator`, 32 random bytes encoded base64url, result length under Graph's 128-char limit
  - Acceptance: no `Random.Shared` or other banned APIs; interface is the only consumer-facing surface
- [x] [P1-T4] Create `src/OpenClaw.Core/CloudSync/NotificationWorkItem.cs` defining the queue item shapes: `NotificationWorkItem` record with `Mailbox`, `MessageId`, `ChangeType` (spec/master §8.2 shape) and a lifecycle work-item shape carrying `SubscriptionId` + lifecycle event (`reauthorizationRequired`/`removed`/`missed`) so one queue serves both change and lifecycle notifications
  - Acceptance: both shapes representable and pattern-matchable by a consumer; records are immutable
- [x] [P1-T5] Create `src/OpenClaw.Core/CloudSync/INotificationQueue.cs` (non-blocking try-enqueue + async dequeue/read surface) and `src/OpenClaw.Core/CloudSync/ChannelNotificationQueue.cs` (D-4): bounded `System.Threading.Channels` channel sized by `CloudSyncOptions.QueueCapacity` with `BoundedChannelFullMode.DropWrite` and a Warning log on every dropped write
  - Acceptance: enqueue never blocks; drop path logs Warning with enough detail to identify the dropped item kind
- [x] [P1-T6] Create `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncOptionsValidatorTests.cs`: defaults-with-valid-url pass; each D-7 rule fails individually (graph backend disabled, missing/relative/http `NotificationUrl`, lifetime 0 and 10081, lead 0 and lead == lifetime, interval 0, capacity 0); boundary passes (lifetime 10080, lead 1)
  - Acceptance: all listed cases present and green; Arrange-Act-Assert; MSTest + FluentAssertions
- [x] [P1-T7] Create `tests/OpenClaw.Core.Tests/CloudSync/CryptoClientStateGeneratorTests.cs`: generated value is non-empty, uses only base64url alphabet, length < 128, and two consecutive generations differ
  - Acceptance: all listed cases present and green
- [x] [P1-T8] Create `tests/OpenClaw.Core.Tests/CloudSync/ChannelNotificationQueueTests.cs`: FIFO enqueue/dequeue round-trip for both work-item shapes; at-capacity enqueue drops the write, returns without blocking, and logs Warning; dequeue completes when an item arrives (deterministic, no timers)
  - Acceptance: all listed cases present and green; no `Task.Delay`/real waits
- [x] [P1-T9] Run the C# toolchain loop (`csharpier format .`; `csharpier check .`; `dotnet build OpenClaw.MailBridge.sln`; `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`), restarting from formatting on any failure or file change, until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 2 — Persistence: graph_subscriptions and graph_delta_links

- [x] [P2-T1] Create `src/OpenClaw.Core/CloudSync/ISubscriptionStore.cs` defining a subscription record (subscription id, resource, mailbox, clientState, expiration `DateTimeOffset`, status `active`/`reauthorize_failed`) and store operations needed by the manager and webhook processor: get-by-id, get-by-mailbox/list-all, upsert, update-status, delete; all timestamps caller-supplied (clock-free contract)
  - Acceptance: interface-only file; no implementation; supports the AC-2 lookup and restart-survival scenarios
- [x] [P2-T2] Create `src/OpenClaw.Core/CloudSync/IDeltaLinkStore.cs` with get (returns null when absent) and set (mailbox, opaque delta link stored verbatim, caller-supplied `updatedAtUtc`)
  - Acceptance: interface-only file; delta link treated as an opaque string
- [x] [P2-T3] Create `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` partial implementing `ISubscriptionStore` on `CoreCacheRepository` following the `CoreCacheRepository.SentActions.cs` precedent: per-call connection, lazy once-per-instance `CREATE TABLE IF NOT EXISTS graph_subscriptions(...)` schema-ensure (columns exactly per spec Data & State DDL), timestamps rendered with the `"O"`/invariant convention, no clock dependency
  - Acceptance: file <= 500 lines; upsert keyed on `subscription_id`; SQL matches the spec DDL
- [x] [P2-T4] Create `src/OpenClaw.Core/CoreCacheRepository.DeltaLinks.cs` partial implementing `IDeltaLinkStore` with lazy schema-ensure for `CREATE TABLE IF NOT EXISTS graph_delta_links(...)` per the spec DDL (mailbox primary key, verbatim `delta_link`, `updated_at_utc`)
  - Acceptance: file <= 500 lines; set is an upsert (`ON CONFLICT(mailbox) DO UPDATE`); SQL matches the spec DDL
- [x] [P2-T5] Update `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` by appending the `graph_subscriptions` and `graph_delta_links` `CREATE TABLE IF NOT EXISTS` statements to `CreateTablesSql` (fresh-database DDL path), identical to the partials' DDL
  - Acceptance: only `CreateTablesSql` is modified in this file; DDL strings identical between fresh path and lazy-ensure path
- [x] [P2-T6] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositorySubscriptionsTests.cs` (following `CoreCacheRepositorySentActionsTests.cs` conventions): upsert/get round-trip preserving all fields including expiration precision; status update to `reauthorize_failed`; delete removes the row; schema-ensure idempotency (two repository instances/initializations against the same database, no error); restart survival (write via one repository instance, read via a fresh instance on the same connection string)
  - Acceptance: all listed cases present and green; in-memory SQLite; no temp files
- [x] [P2-T7] Create `tests/OpenClaw.Core.Tests/CoreCacheRepositoryDeltaLinksTests.cs`: set/get round-trip with the link stored verbatim (URL with query string and special characters); get for unknown mailbox returns null; second set overwrites (upsert); schema-ensure idempotency
  - Acceptance: all listed cases present and green
- [x] [P2-T8] Run the C# toolchain loop (same commands and restart rule as P1-T9) until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 3 — Webhook Endpoint (Handshake, ClientState Validation, Enqueue)

- [x] [P3-T1] Create `src/OpenClaw.Core/CloudSync/NotificationWireModels.cs`: change-notification and lifecycle-notification wire shapes (`value` array; `subscriptionId`, `clientState`, `changeType`, `resource`, `resourceData.id`, `lifecycleEvent`) deserialized with `GraphRequestExecutor.JsonOptions` (web defaults, case-insensitive)
  - Acceptance: models cover both notification kinds; unmodeled fields are ignored, not fatal
- [x] [P3-T2] Create `src/OpenClaw.Core/CloudSync/NotificationRequestProcessor.cs` (host-neutral logic, no ASP.NET types beyond primitives): (a) handshake — when a `validationToken` value is supplied, return 200 with content type `text/plain` and body = URL-decoded token passed through `WebUtility.HtmlEncode`; (b) notifications — parse the body, look up each item's subscription in `ISubscriptionStore`, compare `clientState` with `CryptographicOperations.FixedTimeEquals` over the stored value, drop missing-subscription/mismatch items with a Warning log naming the subscriptionId (D-1), enqueue exactly one `NotificationWorkItem {Mailbox, MessageId, ChangeType}` per valid change notification and one lifecycle work item per valid lifecycle notification onto `INotificationQueue`, then return 202; (c) malformed JSON returns 400 with the repo `{ code: "INVALID_REQUEST", message }` shape; (d) no Graph or database-write I/O on any path (store read only), no `HttpClient` dependency
  - Acceptance: file <= 500 lines; constant-time comparison used for every clientState check; 202 returned for batches containing only invalid items
- [x] [P3-T3] Create `src/OpenClaw.Core/CloudSync/GraphNotificationsEndpoint.cs` with a `MapGraphNotificationsEndpoint` extension mapping `POST /graph/notifications` to the processor (thin ASP.NET glue only: request query/body in, processor result out)
  - Acceptance: no validation, comparison, or enqueue logic in this file; endpoint delegates entirely to `NotificationRequestProcessor`
- [x] [P3-T4] Create `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorTests.cs`: handshake echoes URL-decoded token HTML-encoded (token containing `<script>` and `%20` sequences), status 200, content type `text/plain`; clientState matrix — unknown subscriptionId dropped + Warning, mismatched clientState dropped + Warning, valid item enqueued; response is 202 in all three cases; exactly one work item with the expected `{Mailbox, MessageId, ChangeType}` payload per valid notification
  - Acceptance: all listed cases present and green; recorded notification payloads as in-code constants
- [x] [P3-T5] Create `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorEdgeTests.cs`: malformed JSON body returns 400 with `INVALID_REQUEST` code; queue-at-capacity enqueue drops with Warning while still returning 202; lifecycle notifications (`reauthorizationRequired`, `removed`, `missed`) with valid clientState enqueue lifecycle work items; payload variant with missing `resourceData` is dropped with Warning (fail-visible), not thrown
  - Acceptance: all listed cases present and green
- [x] [P3-T6] Create `tests/OpenClaw.Core.Tests/CloudSync/ClientStatePropertyTests.cs` with CsCheck properties for the pure clientState-comparison helper (T1 property-test rule): comparison returns true iff the candidate byte sequence equals the stored value; arbitrary unequal strings (including equal-length) return false; null/empty candidates return false without throwing
  - Acceptance: at least one CsCheck property per listed behavior; deterministic seeds surfaced on failure per CsCheck defaults
- [x] [P3-T7] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphNotificationsEndpointTests.cs` exercising the mapped route over HTTP through a minimal in-test host (TestServer/minimal `WebApplication` mapping only `MapGraphNotificationsEndpoint` with substituted `ISubscriptionStore`/`INotificationQueue`): handshake round-trip returns 200 `text/plain` encoded token; valid notification POST returns 202 and the item appears on the queue; malformed body returns 400
  - Acceptance: all listed cases present and green; no `Program.cs` dependency in this phase
- [x] [P3-T8] Run the C# toolchain loop (same commands and restart rule as P1-T9) until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 4 — Subscription Manager, Renewal Worker, Lifecycle Routing

- [x] [P4-T1] Create `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs` (D-8: constructs its own internal `GraphRequestExecutor` exactly as `GraphHostAdapterClient` does — typed `HttpClient` with `BaseAddress` from `GraphAdapterOptions.BaseUrl`, `IAppTokenProvider`, `TimeProvider`, `ILogger`): `CreateAsync` issues `POST {BaseUrl}subscriptions` with JSON body `changeType: "created,updated"`, `resource: "users/{PrincipalMailboxUpn}/mailFolders('Inbox')/messages"`, `notificationUrl`/`lifecycleNotificationUrl` from `CloudSyncOptions.NotificationUrl`, freshly generated clientState from `IClientStateGenerator`, `expirationDateTime = now + SubscriptionLifetimeMinutes` (ISO-8601 UTC), persisting the resulting record via `ISubscriptionStore`; `RenewAsync` issues `PATCH {BaseUrl}subscriptions/{id}` with `expirationDateTime` only and updates the store; renewal-due exposed as a pure static function `now >= expiration - RenewalLeadMinutes`; Information logs for create/renew/recreate, no token/body logging
  - Acceptance: file <= 500 lines (split a `GraphSubscriptionManager.Lifecycle.cs` partial if needed); request shapes exactly as spec API surface
- [x] [P4-T2] Implement lifecycle routing on `GraphSubscriptionManager` (same file or `GraphSubscriptionManager.Lifecycle.cs` partial): `reauthorizationRequired` -> renew now, and on auth failure (`CONFIGURATION_ERROR`/`UNAUTHORIZED` envelope) mark the store record `reauthorize_failed` and log Warning; `removed` -> delete the local record and recreate the subscription; `missed` -> invoke an injected delta-reconcile trigger seam (delegate or narrow interface, satisfied by `GraphDeltaReconciler` in Phase 5) for the subscription's mailbox
  - Acceptance: routing table implemented for all three events; no other lifecycle value throws (unknown values log Warning and are ignored)
- [x] [P4-T3] Create `src/OpenClaw.Core/CloudSync/SubscriptionRenewalWorker.cs`: `BackgroundService` driven entirely by `TimeProvider`-based delays (no `Task.Delay` with wall clock); performs an immediate startup sweep (recreates expired subscriptions, creates the subscription when none exists, renews due ones), then re-checks on a `TimeProvider` schedule; renew-vs-recreate decided from the stored expiration and the pure renewal-due function
  - Acceptance: file <= 500 lines; zero direct clock reads; all waits cancellable
- [x] [P4-T4] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerTests.cs` pinning create/renew request shapes handler-level with a `FakeHttpHandler`-style recorded handler: create — URL `{BaseUrl}subscriptions`, method POST, JSON body fields (`changeType`, `resource` with principal UPN, `notificationUrl`, `lifecycleNotificationUrl`, `clientState` from a substituted deterministic generator, `expirationDateTime` = FakeTimeProvider-now + lifetime in ISO-8601 UTC); renew — URL `{BaseUrl}subscriptions/{id}`, method PATCH, body contains `expirationDateTime` only; successful create/renew persists/updates the store record
  - Acceptance: all listed cases present and green; recorded Graph subscription responses as in-code constants
- [x] [P4-T5] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerLifecycleTests.cs`: `reauthorizationRequired` triggers PATCH and, on a mocked 401-mapped envelope, marks the record `reauthorize_failed` with Warning; `removed` deletes then issues a fresh create POST; `missed` invokes the reconcile trigger seam with the subscription's mailbox and issues no Graph call from the manager; unknown lifecycle value logs Warning without throwing
  - Acceptance: all listed cases present and green
- [x] [P4-T6] Create `tests/OpenClaw.Core.Tests/CloudSync/SubscriptionRenewalWorkerTests.cs` with `FakeTimeProvider`: renewal-due boundary at `expiration - lead` exactly, one tick before (not due), one tick after (due); startup sweep with an already-expired stored subscription recreates it promptly; startup sweep with no stored subscription creates one; advancing fake time to the schedule tick triggers the periodic check
  - Acceptance: all listed cases present and green; time advanced only via `FakeTimeProvider`
- [x] [P4-T7] Create `tests/OpenClaw.Core.Tests/CloudSync/RenewalDuePropertyTests.cs` with CsCheck properties over the pure renewal-due function (T1 property-test rule): monotonicity (once due, remains due as now advances), equivalence with the arithmetic definition for arbitrary expiration/lead/now triples, and never-due when `now < expiration - lead`
  - Acceptance: at least one CsCheck property per listed behavior
- [x] [P4-T8] Run the C# toolchain loop (same commands and restart rule as P1-T9) until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 5 — Delta Reconciler and Reconciliation Worker

- [x] [P5-T1] Create `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs` (D-8 executor reuse as in P4-T1): `ReconcileAsync(mailbox)` starts from the stored `IDeltaLinkStore` link when present, else issues the initial request `GET {BaseUrl}users/{mailbox}/mailFolders/Inbox/messages/delta?$select={GraphHostAdapterClient.MessageSelect}`; follows `@odata.nextLink` URLs verbatim, bounded by `GraphAdapterOptions.MaxPages`; maps each returned message with `GraphMessageMapper.Map` and upserts each page through `CoreCacheRepository.UpsertMessagesAsync` with the D-3 synthesized `BridgeStatusDto` (`"ready"`, `"graph"`, `OutlookConnected: true`, `CacheStale: false`) justified by the immediately-preceding successful Graph call; skips `@removed` entries with a Debug log; persists the terminal `@odata.deltaLink` via `IDeltaLinkStore`; records the run outcome via `AddIngestRunAsync` with operation name `delta_reconcile`; exposes a full-re-sync entry point (ignore stored link) used by the `missed` lifecycle trigger; no fabricated healthy status on failure paths
  - Acceptance: file <= 500 lines; stored links and nextLinks used verbatim; ingest run recorded for success and failure outcomes
- [x] [P5-T2] Create `src/OpenClaw.Core/CloudSync/DeltaReconciliationWorker.cs`: `BackgroundService` running `ReconcileAsync` for the principal mailbox on a periodic `ReconcileIntervalMinutes` schedule driven by `TimeProvider` (webhooks are wake signals, delta is truth); failures logged Warning and the loop continues
  - Acceptance: zero direct clock reads; all waits cancellable; a failing reconcile does not stop the worker
- [x] [P5-T3] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerTests.cs` with recorded multi-page payload constants: three-page walk (two `@odata.nextLink` pages then `@odata.deltaLink`) issues requests in order with the initial URL shape pinned (path + `$select`), follows nextLinks verbatim, upserts every page's messages through the repository sink, and persists the terminal delta link; a subsequent reconcile resumes from the stored link verbatim
  - Acceptance: all listed cases present and green; request sequence asserted handler-level
- [x] [P5-T4] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerRecoveryTests.cs`: absent/empty stored link starts from the initial delta request (full re-sync); the `missed`-triggered entry point re-syncs even when a link is stored; `MaxPages` bound stops a runaway nextLink chain; `@removed` entries are skipped with Debug and not upserted; re-running the same pages is idempotent through the `ON CONFLICT(bridge_id)` upsert; upserted rows carry the D-3 synthesized ready/graph status; an `ingest_runs` row with operation `delta_reconcile` is written for success and for a failed walk
  - Acceptance: all listed cases present and green
- [x] [P5-T5] Create `tests/OpenClaw.Core.Tests/CloudSync/DeltaReconciliationWorkerTests.cs` with `FakeTimeProvider`: no reconcile before the first interval elapses; advancing fake time by the interval triggers exactly one reconcile; a reconcile failure logs Warning and the next tick still runs
  - Acceptance: all listed cases present and green
- [x] [P5-T6] Run the C# toolchain loop (same commands and restart rule as P1-T9) until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 6 — Dispatch Worker, DI Extension, Opt-in Composition, Architecture Rules

- [x] [P6-T1] Create `src/OpenClaw.Core/CloudSync/NotificationDispatchWorker.cs`: `BackgroundService` consuming `INotificationQueue`; for each message work item calls `IHostAdapterClient.GetMessageAsync(messageId)` and on success upserts via `CoreCacheRepository.UpsertMessagesAsync` with the D-3 synthesized `BridgeStatusDto`; a failed fetch logs Warning and drops the item (delta reconciliation is the recovery path); lifecycle work items are routed to `GraphSubscriptionManager`'s lifecycle handler
  - Acceptance: no fabricated healthy status on failed fetches; worker loop survives individual item failures
- [x] [P6-T2] Create `tests/OpenClaw.Core.Tests/CloudSync/NotificationDispatchWorkerTests.cs`: successful fetch upserts the fetched message with ready/graph status and the fetch envelope's request id; failed fetch (error envelope) logs Warning, performs no upsert, and processing continues with the next item; a lifecycle work item invokes the manager's lifecycle routing and no `GetMessageAsync` call
  - Acceptance: all listed cases present and green; `IHostAdapterClient` mocked with Moq
- [x] [P6-T3] Create `src/OpenClaw.Core/CloudSync/CloudSyncServiceCollectionExtensions.cs` with `AddCloudSync(IConfiguration)` mirroring `AddGraphHostAdapterClient`: bind `CloudSyncOptions` from `OpenClaw:CloudSync` with `.Validate(...)` using `CloudSyncOptionsValidator` (passing the `OpenClaw:GraphAdapter:Enabled` value for the D-7 cross-check) and `.ValidateOnStart()`; register typed `HttpClient`s for `GraphSubscriptionManager` and `GraphDeltaReconciler` with `BaseAddress` from `GraphAdapterOptions.BaseUrl`; register `INotificationQueue` -> `ChannelNotificationQueue`, `IClientStateGenerator` -> `CryptoClientStateGenerator`, `ISubscriptionStore`/`IDeltaLinkStore` -> the `CoreCacheRepository` singleton; add hosted services `SubscriptionRenewalWorker`, `DeltaReconciliationWorker`, `NotificationDispatchWorker`
  - Acceptance: one public entry point; no registration occurs outside this extension besides the Program.cs guard
- [x] [P6-T4] Create `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncServiceCollectionExtensionsTests.cs`: with valid CloudSync + Graph/CloudAuth configuration all CloudSync services resolve and the three hosted workers are registered; with `CloudSync:Enabled=true` and `GraphAdapter:Enabled` absent, options validation fails on start (fail-closed, D-7); with an `http` (non-https) `NotificationUrl`, options validation fails on start
  - Acceptance: all listed cases present and green
- [x] [P6-T5] [expect-fail] Create `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncSelectionTests.cs` in the `GraphBackendSelectionTests` style (`CoreTestWebApplicationFactory` + `UseSetting`, D-6): (a) flag absent — POST `/graph/notifications` returns 404 and no CloudSync hosted services are registered (composition root unchanged); (b) `OpenClaw:CloudSync:Enabled=true` plus valid Graph/CloudAuth/CloudSync settings — the route is mapped (handshake returns 200) and the three CloudSync workers are registered; then run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CloudSyncSelectionTests"` BEFORE modifying `Program.cs` and record the expected failure of the opt-in case
  - Acceptance: `<FEATURE>/evidence/regression-testing/cloudsync-selection-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), `Output Summary:` naming the failing opt-in test(s); the flag-absent case passes
- [x] [P6-T6] Update `src/OpenClaw.Core/Program.cs` with the two D-6 conditional pieces only: `if (builder.Configuration.GetValue<bool>("OpenClaw:CloudSync:Enabled")) { builder.Services.AddCloudSync(builder.Configuration); }` alongside the existing backend-selection block, and a matching `app.MapGraphNotificationsEndpoint()` guard after `app.UseRouting()`; no other Program.cs line changes
  - Acceptance: with the flag absent, registrations and routes are byte-identical to today's behavior; diff touches only the two guarded additions
- [x] [P6-T7] Rerun `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CloudSyncSelectionTests"` and record the passing result
  - Acceptance: `<FEATURE>/evidence/regression-testing/cloudsync-selection-pass-after.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (all selection tests green)
- [x] [P6-T8] Create `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncArchitectureBoundaryTests.cs` (NetArchTest, following `CloudGraphArchitectureBoundaryTests.cs`): (1) `OpenClaw.Core.CloudSync` types depend only on `OpenClaw.Core.CloudSync`, `OpenClaw.Core.CloudGraph`, `OpenClaw.Core.CloudAuth`, contracts namespaces (`OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`), the `OpenClaw.Core` repository/ingest surface they consume, and BCL/hosting abstractions; (2) no CloudSync dependency on COM interop namespaces; (3) no type outside `OpenClaw.Core.CloudSync` (composition root excepted) depends on CloudSync internals
  - Acceptance: all three rules implemented and green against the finished production surface
- [x] [P6-T9] Run the C# toolchain loop (same commands and restart rule as P1-T9) until one clean pass completes
  - Acceptance: all commands exit 0 in a single uninterrupted pass

### Phase 7 — Final QA Loop, Coverage Comparison, and Scope Verification

- [x] [P7-T1] Run `csharpier format .` then `csharpier check .` from the repository root as final-QA step 1; if formatting changed any file, note it and plan to restart the loop after P7-T3
  - Acceptance: `<FEATURE>/evidence/qa-gates/csharpier.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
- [x] [P7-T2] Run `dotnet build OpenClaw.MailBridge.sln` as final-QA step 2 (analyzers, nullable, warnings-as-errors)
  - Acceptance: `<FEATURE>/evidence/qa-gates/dotnet-build.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
- [x] [P7-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` as final-QA step 3 (includes NetArchTest architecture tests and the full MSTest suite); if P7-T1..P7-T3 produced any failure or file change, restart from P7-T1 until one clean pass completes, and keep only the clean-pass artifacts as the final gate evidence (raw Cobertura/TRX under `artifacts/csharp/`)
  - Acceptance: `<FEATURE>/evidence/qa-gates/dotnet-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing test counts plus numeric post-change line-coverage and branch-coverage percentages (no placeholders); artifact reflects a single clean pass of all three steps
- [x] [P7-T4] Produce the coverage comparison from the Phase 0 baseline Cobertura report and the final-QA Cobertura report: overall line coverage >= 85%, overall branch coverage >= 75%, no regression versus baseline, and per-file line/branch coverage for every new production file under `src/OpenClaw.Core/CloudSync/` and the two new `CoreCacheRepository` partials (changed-code coverage); interface-only files (`INotificationQueue.cs`, `IClientStateGenerator.cs`, `ISubscriptionStore.cs`, `IDeltaLinkStore.cs`) may be noted as interface-only per policy
  - Acceptance: `<FEATURE>/evidence/qa-gates/coverage-comparison.<ts>.md` exists with `Timestamp:`, baseline numeric values, post-change numeric values, per-new-file values, and an explicit PASS/FAIL verdict per threshold; a FAIL on any threshold makes the plan outcome remediation-required, not PASS
- [x] [P7-T5] Verify diff-scope confinement by running `git diff --name-only origin/main...HEAD` (or `git status --porcelain` plus branch diff) and checking every changed path against the allowed set: `src/OpenClaw.Core/CloudSync/**`, `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs`, `src/OpenClaw.Core/CoreCacheRepository.DeltaLinks.cs`, `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, `src/OpenClaw.Core/Program.cs`, `tests/OpenClaw.Core.Tests/CloudSync/**`, `tests/OpenClaw.Core.Tests/CoreCacheRepositorySubscriptionsTests.cs`, `tests/OpenClaw.Core.Tests/CoreCacheRepositoryDeltaLinksTests.cs`, `<FEATURE>/**`, `artifacts/csharp/**`
  - Acceptance: `<FEATURE>/evidence/qa-gates/diff-scope.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing every changed path and an explicit in-scope/out-of-scope verdict per path; any out-of-scope path is a blocking finding
- [x] [P7-T6] Verify the 500-line cap for every new or modified production and test file from P7-T5's changed-path list (Markdown evidence files exempt per policy)
  - Acceptance: `<FEATURE>/evidence/qa-gates/file-size-cap.<ts>.md` exists with `Timestamp:`, the line count per checked file, and a PASS/FAIL verdict; any file over 500 lines is a blocking finding

## Test Plan

- **Unit (MSTest + FluentAssertions + Moq):** options validator matrix (P1-T6), clientState generator (P1-T7), bounded queue drop semantics (P1-T8), store round-trips and schema-ensure idempotency (P2-T6/T7), handshake/clientState/enqueue/malformed-body/queue-full/lifecycle matrix (P3-T4/T5), create/renew request-shape pinning and lifecycle routing with mocked `HttpMessageHandler` (P4-T4/T5), renewal-due boundary math and startup sweep with `FakeTimeProvider` (P4-T6), delta walk/recovery/idempotency/ingest-run matrix (P5-T3/T4), periodic worker scheduling (P5-T5), dispatch success/failure/lifecycle routing (P6-T2), DI extension fail-closed validation (P6-T4).
- **Property-based (CsCheck, T1 rule):** clientState comparison (P3-T6), renewal-due pure function (P4-T7).
- **Composition/integration:** minimal-host endpoint HTTP round-trip (P3-T7); `CoreTestWebApplicationFactory` + `UseSetting` selection tests with `[expect-fail]` fail-before/pass-after evidence (P6-T5/T7).
- **Architecture:** NetArchTest namespace-scoped CloudSync boundary rules (P6-T8), executed inside every `dotnet test` run.
- **Coverage evidence:** baseline `<FEATURE>/evidence/baseline/dotnet-test-coverage.<ts>.md` (P0-T4); post-change `<FEATURE>/evidence/qa-gates/dotnet-test-coverage.<ts>.md` (P7-T3); comparison `<FEATURE>/evidence/qa-gates/coverage-comparison.<ts>.md` (P7-T4). Raw Cobertura/TRX intermediates under `artifacts/csharp/`.

## Open Questions / Notes

- **F16 deferrals (recorded per spec):** Azure Service Bus/Storage Queue implementations stay behind `INotificationQueue`; no Azure SDK dependency is added. Live end-to-end webhook delivery (tenant + public https `NotificationUrl`) is verified post-deployment under F16 plus the existing HI-1 tenant handoff. No new `human_interaction` requirement: every deliverable in this plan is locally verifiable.
- **Non-goals (per user-story):** calendar/event subscriptions, cache deletion propagation for `@removed`, rich notifications (resource data), and replacing the local polling workers are out of scope.
- The lifecycle work-item shape on the shared queue (P1-T4) is an implementation detail; the binding constraints are: one queue, both notification kinds representable, dispatch worker routes lifecycle items to the manager (P6-T1).
- `GraphSubscriptionManager` may split into a `GraphSubscriptionManager.Lifecycle.cs` partial to respect the 500-line cap (P4-T1/T2); no other structural deviation from the spec's file list is authorized.
