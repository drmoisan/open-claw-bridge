# Feature Audit: graph-subscriptions-delta (#117)

- **Audit Date:** 2026-07-03
- **Auditor:** feature-review agent
- **Work Mode:** `full-feature` (persisted marker in `issue.md`); AC sources: `spec.md` and `user-story.md` (mirrored in `issue.md`)

## Scope and Baseline

- **Feature branch:** `feature/graph-subscriptions-delta-117` @ `f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3` (single commit)
- **Resolved base branch:** `main` (origin/main @ `402703c6c18d0131128696147443ef9f41110f3c`; the local `main` ref is stale per caller inputs — reviewer confirmed `git merge-base origin/main HEAD` resolves the same SHA)
- **Merge base:** `402703c6c18d0131128696147443ef9f41110f3c`
- **Diff range:** `402703c..f6aea4d` — 63 files, +6211/-1 (20 new production `.cs`, 2 modified production `.cs`, 21 new test `.cs`, 24 Markdown)
- **Evidence sources:** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` (fresh: head SHA matches), the authoritative git diff, executor evidence under `evidence/{baseline,qa-gates,regression-testing}/`, and the reviewer's independent toolchain re-run at branch head (fresh cobertura under `evidence/qa-gates/coverage-review/`).
- **Known artifact quirk (recorded):** the PR-context summary's file categorization reports "Core logic changes: 0 files" for this C# branch (ninth recurrence); scope was taken from the git diff, not the summary categorization.

## Acceptance Criteria Inventory

Both AC sources carry the identical five criteria, in checkbox format, all currently checked `[x]` by the executor:

| # | Criterion (abbreviated) | spec.md | user-story.md |
|---|---|---|---|
| AC-1 | Webhook: handshake echoes decoded token HTML-encoded as text/plain 200; missing/mismatched clientState dropped with 202 + Warning; each valid notification enqueues exactly one `{mailbox, messageId, changeType}` item; handler performs no Graph I/O | `[x]` (line 141) | `[x]` (line 56) |
| AC-2 | Subscription manager: POST create / PATCH renew shapes verified handler-level; deterministic renewal-due via TimeProvider (default lead 30 min, 10,080-min max); lifecycle routing (reauthorizationRequired -> renew, removed -> recreate, missed -> delta reconcile); state persists in `graph_subscriptions` and survives restart | `[x]` (line 142) | `[x]` (line 57) |
| AC-3 | Delta reconciler: multi-page nextLink walk to terminal deltaLink; link persisted per mailbox in `graph_delta_links`; upserts via the poller's `UpsertMessagesAsync` sink; absent/empty link or `missed` triggers full re-sync; request shapes verified handler-level | `[x]` (line 143) | `[x]` (line 58) |
| AC-4 | Host-neutral and opt-in: all new logic in `OpenClaw.Core.CloudSync`; endpoint/workers registered only when `OpenClaw:CloudSync:Enabled=true`; flag-absent composition unchanged (factory test); namespace-scoped NetArchTest rule | `[x]` (line 144) | `[x]` (line 59) |
| AC-5 | Full C# toolchain passes; line >= 85% and branch >= 75% hold with changed lines covered; mocked handlers and FakeTimeProvider only; no live Graph calls, no temp files, no file over 500 lines | `[x]` (line 145) | `[x]` (line 60) |

## Acceptance Criteria Evaluation

### AC-1 — Webhook endpoint: **PASS**

- Handshake: `NotificationRequestProcessor.HandleHandshake` returns 200/`text/plain` with `WebUtility.HtmlEncode(WebUtility.UrlDecode(token))`; pinned by `Handshake_echoes_the_url_decoded_token_html_encoded_as_text_plain_200` (unit) and `Handshake_round_trip_returns_200_text_plain_with_the_encoded_token` (factory round-trip through the real route).
- 202-and-drop: unknown subscription, mismatched clientState (constant-time `CryptographicOperations.FixedTimeEquals`), and missing `resourceData.id` each drop with a Warning and the batch still returns 202 (`NotificationRequestProcessorTests`, `NotificationRequestProcessorEdgeTests`); malformed JSON returns 400 in the repo `{code, message}` shape (unit + factory).
- Exactly-one enqueue: `Valid_notification_enqueues_exactly_one_work_item_and_returns_202` asserts the single `{Mailbox, MessageId, ChangeType}` item; lifecycle DataRows assert lifecycle work items for all three events.
- No Graph I/O: the processor has no `HttpClient`/executor dependency at all (constructor takes store, queue, logger only) — verified by code read; the endpoint file is glue-only.

### AC-2 — Subscription manager: **PASS**

- Request shapes: `CreateAsync_pins_the_exact_post_request_shape` (POST `subscriptions`, changeType `created,updated`, resource, both notification URLs, generated clientState, ISO-8601 expiration) and `RenewAsync_pins_the_exact_patch_request_shape_with_expiration_only` (PATCH `subscriptions/{id}`, body with `expirationDateTime` only), both over mocked `HttpMessageHandler`.
- Deterministic renewal-due: pure `IsRenewalDue(now, expiration, lead)` with the boundary asserted exactly at `expiration - lead` (`SubscriptionRenewalWorkerTests`) plus 3 CsCheck properties (`RenewalDuePropertyTests`); default lead 30 and lifetime cap 10,080 enforced by `CloudSyncOptionsValidator` with boundary tests at 10080/10081 and lead==lifetime.
- Lifecycle routing: dedicated tests for all three routes plus the unknown-event Warning guard (`GraphSubscriptionManagerLifecycleTests`), including `reauthorize_failed` marking on a 401 renewal.
- Persistence/restart: `graph_subscriptions` round-trip to tick precision, status update, delete idempotency, double schema-ensure, double `InitializeAsync`, and restart survival via a fresh repository instance on the same connection string (`CoreCacheRepositorySubscriptionsTests`, 8 tests).

### AC-3 — Delta reconciler: **PASS**

- Multi-page walk: `Three_page_walk_pins_the_initial_url_follows_next_links_and_persists_the_delta_link` pins the initial URL (including the `$select` reuse of `MessageSelect`), verbatim nextLink follows, and terminal deltaLink persistence; `Subsequent_reconcile_resumes_from_the_stored_link_verbatim` pins resume semantics; the `MaxPages` bound is tested with a failed `delta_reconcile` ingest run.
- Sink identity: upserts flow through `CoreCacheRepository.UpsertMessagesAsync` with the D-3 ready/graph status — asserted by row inspection in `Rerunning_the_same_pages_is_idempotent_and_rows_carry_ready_graph_status`; `@removed` entries skipped and not upserted.
- Re-sync triggers: empty stored link starts from the initial request; the `missed` entry point (`TriggerResyncAsync`) re-syncs even when a link is stored (`GraphDeltaReconcilerRecoveryTests`).

### AC-4 — Host-neutral and opt-in: **PASS**

- Namespace: all 18 new logic files live in `OpenClaw.Core.CloudSync`; the two store partials implement the CloudSync seams on `CoreCacheRepository` as the spec directs.
- Opt-in: `CloudSyncSelectionTests` verify flag-absent (no `/graph/notifications` route, no CloudSync hosted workers) and opt-in (route mapped, three workers registered) through `CoreTestWebApplicationFactory` with `UseSetting`; executor red/green evidence `cloudsync-selection-expect-fail.2026-07-03T02-12.md` (EXIT 1, before the Program.cs conditionals) -> `cloudsync-selection-pass-after.2026-07-03T02-13.md` (EXIT 0).
- Boundary: `CloudSyncArchitectureBoundaryTests` (namespace-scoped, in the 707/707 pass) pin the CloudGraph/CloudAuth/Contracts allowlist, the Agent-partition independence, the COM-interop ban, and the nothing-outside-CloudSync direction with documented carve-outs.

### AC-5 — Toolchain and coverage: **PARTIAL**

- Toolchain: PASS — reviewer re-ran csharpier check (EXIT 0, 322 files), `dotnet build` (0 warnings/0 errors with analyzers+nullable as errors), and the full test suite (1154 passed, 0 failed, 5 pre-existing env-gated skips; architecture suites included) in a single clean pass at branch head.
- Pooled/package coverage: PASS — pooled 92.83% line / 83.25% branch (baseline 92.34/83.16, both improved); Core package 94.51/85.29; every instrumented new file at 100.00% line; no regression anywhere (MailBridge/HostAdapter unchanged from baseline).
- Test hygiene: PASS — mocked Graph handlers and `FakeTimeProvider` only, no live calls, no temp files (in-memory shared-cache SQLite), all 43 touched `.cs` files under 500 lines (max 358).
- **Gap:** the uniform branch gate applied per new file fails on two files — `GraphSubscriptionManager.cs` 2/4 = 50.00% and `CoreCacheRepository.Subscriptions.cs` 1/2 = 50.00% instrumented branch (< 75%). The untaken arms are untested fail-fast/fallback guards (`ParseSubscription` null-body and missing-`id` throws; `ReadSubscription` MinValue fallback) — measured-and-uncovered scenarios, not instrumentation exclusions. Policy audit finding B-117-01 (Blocking); remediation triggered. Until those arms are covered, the criterion's "branch coverage >= 75% hold with changed lines covered" clause is not fully demonstrated at the per-file level, so the criterion is graded PARTIAL rather than PASS.

## Summary

| AC | Verdict |
|----|---------|
| AC-1 Webhook | PASS |
| AC-2 Subscription manager | PASS |
| AC-3 Delta reconciler | PASS |
| AC-4 Host-neutral / opt-in | PASS |
| AC-5 Toolchain + coverage | PARTIAL |

Four of five acceptance criteria pass with direct, independently re-verified evidence. AC-5 is PARTIAL solely on the per-new-file branch gate (policy audit B-117-01); the remediation scope is three to four directed tests (optionally one fail-fast refactor), enumerated in `remediation-inputs.2026-07-03T02-34.md`. Companion artifacts: `policy-audit.2026-07-03T02-34.md` (one Blocking finding; toolchain and pooled gates pass), `code-review.2026-07-03T02-34.md` (2 Blocking rows mirroring B-117-01, 3 Minor, 2 Info). Recommendation: **No-Go for PR until the remediation cycle closes**; the fix is small and mechanical, and no production behavior change is required unless the fail-fast option is chosen.

## Acceptance Criteria Check-off

- Source files: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/spec.md` and `docs/features/active/2026-07-03-graph-subscriptions-delta-117/user-story.md` (work mode `full-feature`; `issue.md` mirrors the same list).
- AC-1 through AC-4 evaluated **PASS**: already checked `[x]` by the executor in both source files — check-off state confirmed correct; no reviewer edit required.
- AC-5 evaluated **PARTIAL**: the executor has already checked it `[x]` in both source files. Per the acceptance-criteria-tracking protocol the reviewer does not add check-offs for non-PASS items and does not rewrite executor check-off state; the discrepancy is recorded here and in the remediation inputs instead. The re-audit at remediation-cycle exit should confirm AC-5 once B-117-01 closes, at which point the existing `[x]` becomes accurate.
- No new AC items were added and no criterion text was modified.

### Acceptance Criteria Status
- Source: `spec.md` and `user-story.md` (mirrored in `issue.md`)
- Total AC items: 5
- Checked off (delivered): 5 (executor check-offs; 4 confirmed PASS by this audit, 1 contested as PARTIAL — AC-5, pending B-117-01 remediation)
- Remaining (unchecked): 0
- Items remaining: none unchecked; AC-5 requires re-confirmation at remediation exit
