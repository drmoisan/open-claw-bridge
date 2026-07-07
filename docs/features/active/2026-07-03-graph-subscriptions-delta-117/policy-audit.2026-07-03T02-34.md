# Policy Compliance Audit: graph-subscriptions-delta (#117)

**Audit Date:** 2026-07-03
**Code Under Test:** C# only. 20 NEW production `.cs` files — 18 under `src/OpenClaw.Core/CloudSync/` (GraphSubscriptionManager 326 lines, GraphDeltaReconciler 260, NotificationRequestProcessor 166, NotificationDispatchWorker 114, SubscriptionRenewalWorker 99, CloudSyncOptionsValidator 84, ISubscriptionStore 83, CloudSyncServiceCollectionExtensions 81, CloudSyncOptions 56, DeltaReconciliationWorker 56, GraphNotificationsEndpoint 53, ChannelNotificationQueue 48, NotificationWorkItem 43, NotificationWireModels 33, IDeltaLinkStore 27, INotificationQueue 25, CryptoClientStateGenerator 23, IClientStateGenerator 15) plus the 2 repository store partials `CoreCacheRepository.Subscriptions.cs` (178) and `CoreCacheRepository.DeltaLinks.cs` (90) — and 2 MODIFIED production `.cs` files (`Program.cs`, the two D-6 opt-in conditional blocks, 358 lines post-change; `CoreCacheRepository.Schema.cs`, fresh-DDL concatenation of the two new table constants, 275 lines post-change). 21 NEW test `.cs` files (19 under `tests/OpenClaw.Core.Tests/CloudSync/` plus `CoreCacheRepositorySubscriptionsTests.cs` and `CoreCacheRepositoryDeltaLinksTests.cs` at the test-project root), adding 91 runtime cases (Core.Tests 616 -> 707) including 7 CsCheck properties. Plus 20 feature scoping/evidence Markdown files and 4 agent-memory Markdown files. No Python, PowerShell, Bash, or TypeScript files changed in the branch diff.

**Scope:** Full feature branch `feature/graph-subscriptions-delta-117` @ `f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3` versus resolved base `main` @ merge-base `402703c6c18d0131128696147443ef9f41110f3c` (origin/main; the local `main` ref is stale per the caller inputs — reviewer-confirmed `git merge-base origin/main HEAD` resolves the same SHA and the PR-context artifacts record the same range). Scope is feature-vs-base over the complete branch diff: 63 files, +6211/-1 (43 `.cs`, 24 `.md` counting the four agent-memory files). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md` (mirrored in `issue.md`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 20 production `.cs` (all new) + 2 production `.cs` modified + 21 test `.cs` (all new) | 1159 (solution) / 707 (Core.Tests) | 1154 pass, 0 fail, 5 env-gated skips | 92.34% line, 83.16% branch (pooled solution) | 92.83% line, 83.25% branch (pooled solution, reviewer re-run and re-parsed) | Every instrumented new file at 100.00% line; pooled gates hold with margin; HOWEVER the instrumented branch subsets of two new files are below the 75% per-file gate: GraphSubscriptionManager.cs 2/4 = 50.00% and CoreCacheRepository.Subscriptions.cs 1/2 = 50.00% (Blocking finding B-117-01, Section 8); GraphDeltaReconciler.cs sits at exactly 12/16 = 75.00% (zero margin); async bodies, auto-property and record-only files are uninstrumented under the pre-existing runsettings CompilerGenerated exclusion and were verified behaviorally (Section 8); modified Program.cs 100.00% line and 100.00% branch, modified Schema partial 100.00%/100.00% |

**Note:** Python, PowerShell, Bash, and TypeScript rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit **FAIL** on the per-new-file branch gate (two files at 50.00% instrumented branch, details in Sections 5 and 8); the pooled, package, and per-file line dimensions all pass.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md` (line 92.34% / branch 83.16% pooled; Core package 93.73%/85.25%)
- C# post-change coverage artifact: `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/dotnet-test-coverage.2026-07-03T02-16.md` and `evidence/qa-gates/coverage-comparison.2026-07-03T02-16.md`
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review/{2047da4b...,812dc593...,f7f63777...}/coverage.cobertura.xml`; independently parsed pooled 92.83% line (5622/6056) / 83.25% branch (1312/1576) — identical to the executor's committed figures, with per-file line AND branch re-measured for all 22 changed production files
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura at branch head. The pooled gates are met (92.83% line >= 85%, 83.25% branch >= 75%, both improved over baseline; every instrumented new file at 100.00% line). The per-new-file branch gate is NOT met for two files (Section 8, finding B-117-01); the audit is therefore marked with one Blocking FAIL and remediation is required.

---

## Executive Summary

This feature branch closes issue #117 (gap F14): the host-neutral core of the Graph eventing model — change-notification subscriptions waking a thin webhook that only validates and enqueues, with `messages/delta` as the authoritative reconciliation layer. The delivery is a self-contained `OpenClaw.Core.CloudSync` namespace: (a) `CloudSyncOptions` bound from `OpenClaw:CloudSync` with the six spec keys and defaults, plus the pure full-list `CloudSyncOptionsValidator` (D-7 fail-closed rules active only when `Enabled`, including the Graph-backend cross-check and the absolute-https `NotificationUrl` rule); (b) the webhook — host-neutral `NotificationRequestProcessor` (handshake echo of the URL-decoded token HTML-encoded as `text/plain` 200; constant-time `clientState` comparison via `CryptographicOperations.FixedTimeEquals` with D-1 202-and-drop semantics and Warning logs; exactly-one-work-item enqueue; malformed-JSON 400 in the repo error shape) behind the thin `GraphNotificationsEndpoint` ASP.NET glue; (c) the D-4 bounded in-process `ChannelNotificationQueue` (DropWrite + Warning, capacity-configurable) behind the `INotificationQueue` seam with the Azure implementations explicitly deferred to F16 and zero new dependencies; (d) `GraphSubscriptionManager` — create/renew request shapes per the spec API surface, the pure `IsRenewalDue` function, and the lifecycle routing table (`reauthorizationRequired` -> renew with `reauthorize_failed` marking on auth-mapped failure, `removed` -> delete + recreate, `missed` -> delta re-sync) — reusing `GraphRequestExecutor` per D-8 with zero new pipeline code; (e) `GraphDeltaReconciler` walking `@odata.nextLink` pages bounded by `MaxPages` to the terminal `@odata.deltaLink`, persisting the link per mailbox, skipping `@removed` at Debug, upserting through the identical `CoreCacheRepository.UpsertMessagesAsync` sink the poller uses (D-2) with the D-3 synthesized ready/graph status, and recording `delta_reconcile` ingest runs; (f) the three `TimeProvider`-driven workers (renewal sweep with startup recovery, periodic reconciliation, queue dispatch); (g) clock-free `graph_subscriptions`/`graph_delta_links` store partials with lazy schema-ensure plus fresh-DDL reuse in the Schema partial; and (h) the D-6 opt-in composition (`AddCloudSync` + endpoint map, both behind `OpenClaw:CloudSync:Enabled`) with the flag-absent path verified unchanged by fail-before/pass-after selection tests.

The mandatory toolchain was independently re-run by the reviewer against the branch head `f6aea4d` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 322 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** the new `CloudSyncArchitectureBoundaryTests` (namespace-scoped allowlist including the Agent-partition independence direction and the COM-interop ban) plus the pre-existing `CloudGraphArchitectureBoundaryTests` and `AgentArchitectureBoundaryTests` run inside `OpenClaw.Core.Tests` — included in the 707/707 pass.
- **Tests + coverage:** full solution `dotnet test` with XPlat Code Coverage — 1154 passed, 0 failed, 5 environment-gated skips (same skips as baseline); pooled coverage 92.83% line / 83.25% branch, above the uniform gates; T1 property-test obligation satisfied with seven genuine CsCheck properties (four on the constant-time `ClientStateMatches` comparison, three on the pure `IsRenewalDue` renewal arithmetic).
- **Regression evidence:** all 616 baseline Core.Tests cases pass unmodified inside the reviewer's full run; zero existing test files were modified (the test diff is the twenty-one new files); MailBridge and HostAdapter coverage figures are unchanged from baseline; executor fail-before/pass-after evidence for the opt-in composition (`cloudsync-selection-expect-fail.2026-07-03T02-12.md` EXIT 1 -> `cloudsync-selection-pass-after.2026-07-03T02-13.md` EXIT 0).

**One Blocking finding (B-117-01, Section 8):** the instrumented branch subsets of two new production files measure below the uniform 75% per-file branch gate — `GraphSubscriptionManager.cs` at 2/4 = 50.00% (the two untaken arms are the untested `ParseSubscription` fail-fast guards: null-deserialized body and missing `id`) and `CoreCacheRepository.Subscriptions.cs` at 1/2 = 50.00% (the untaken arm is the silent `?? DateTimeOffset.MinValue` fallback in `ReadSubscription`). Unlike the async-instrumentation exclusions accepted on the #99–#115 reviews (excluded lines with behavioral verification), these are measured-but-uncovered branch arms with no test anywhere in the suite — genuinely untested error-handling scenarios, the same category the #18 review graded Blocking at 71.43%. Remediation is small and concrete (three to four directed tests); see `remediation-inputs.2026-07-03T02-34.md`. All other findings are Minor or informational. The feature is recommended **No-Go for PR until the remediation cycle closes**.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/csharp.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/orchestrator-state.md` (not triggered — no checkpoint changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is twenty new production files, two modified production files, twenty-one test files, four agent-memory records, and documentation/evidence Markdown. The executor's raw cobertura intermediates under `artifacts/csharp/baseline-117/` and `artifacts/csharp/final-117/` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`402703c`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only (16 files). That categorization is inaccurate; the authoritative `git diff 402703c..f6aea4d` contains 20 new production C# files, 2 modified production C# files, and 21 new test C# files (63 files total, +6211/-1). This is the ninth C#-branch review (#99, #101, #103, #105, #107, #109, #113, #115, #117) where the summary miscategorizes a C# branch as docs-only. The audit used the authoritative git diff file list, not the summary categorization. Related parsing noise: the summary's author-asserted autoclose list contains `#113`, `#74`, and the non-issue tokens `#AC-2`, `#HI-1`, and `#ISO-8601` lifted from AC labels and spec prose (#113 and #74 are cited as design-precedent context in spec.md and are already closed; they are not closed by this change); only #117 is the closing issue. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 402703c..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/<kind>/` locations (baseline, qa-gates, regression-testing).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/coverage-review` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, and #99–#115 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Every test constructs its own options, mocked `HttpMessageHandler`, `FakeTimeProvider`, Moq token provider, uniquely-named in-memory shared-cache SQLite connection string, and (where needed) its own `ServiceCollection`/`WebApplicationFactory`; no shared state, no static mutation. 707/707 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | One handshake/validation/enqueue aspect per method; one lifecycle route per method; one validator rule per method/DataRow; one store operation per method; one delta-walk recovery trigger per method; one worker schedule behavior per method. |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 707 tests in ~2 s (reviewer run); all new tests are in-memory computation, mocked handlers with raw-string payloads, in-memory SQLite, and in-memory configuration — no I/O beyond in-memory SQLite, no live Graph calls. |
| **Determinism** | PASS | All time via `FakeTimeProvider` (worker delays advanced explicitly; renewal-due boundaries evaluated tick-exactly); CsCheck uses the suite's seeded `Gen`/`Sample` convention with failing-seed printing; fixed `DateTimeOffset` constants in store tests; reviewer scans: zero matches for `Thread.Sleep`, `DateTime.UtcNow`, `DateTime.Now`, `Random.Shared`, or raw `Task.Delay` in the new test tree. |
| **Readability & Maintainability** | PASS | Descriptive scenario names (`Handshake_echoes_the_url_decoded_token_html_encoded_as_text_plain_200`, `Missed_triggers_the_reconcile_seam_and_issues_no_graph_call`, `Startup_sweep_recreates_an_already_expired_subscription`), FluentAssertions because-messages, Arrange/Act/Assert structure, XML docs on test classes citing the AC and design decisions covered. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 92.34% line (5234/5668), 83.16% branch (1269/1526). Source: `evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md`. |
| **No Coverage Regression** | PASS | Post-change pooled: 92.83% line (5622/6056), 83.25% branch (1312/1576) — +0.49pp / +0.09pp, reviewer re-run and re-parsed, identical to the executor's committed figures. MailBridge and HostAdapter figures unchanged from baseline; the modified `Program.cs` and Schema partial are at 100.00% line and branch. |
| **New Code Coverage** | FAIL | Every instrumented new file reports 100.00% line coverage, and the pooled/package gates hold. However the per-new-file BRANCH gate (>= 75%) fails for two files: `GraphSubscriptionManager.cs` 2/4 = 50.00% and `CoreCacheRepository.Subscriptions.cs` 1/2 = 50.00%, reviewer-parsed from fresh cobertura (Section 5 table). The untaken arms are untested defensive guards with no test anywhere in the suite (Section 8, B-117-01). `GraphDeltaReconciler.cs` sits at exactly 75.00% (12/16) — at the gate with zero margin (Minor CR-117-02). |
| **Comprehensive Coverage** | PASS | Handshake echo/escaping/content type; clientState matrix (unknown subscription, mismatch, valid, missing resourceData); malformed-body 400; queue-full drop; lifecycle enqueue DataRows; create/renew request-shape pinning; renewal-due boundary math; lifecycle routing table incl. unknown event; store round-trips, restart survival, double schema-ensure; delta walk (multi-page, resume-verbatim, empty-link re-sync, missed re-sync, MaxPages bound, `@removed` skip, idempotent rerun, failed-run ingest row); worker scheduling and failure-continuation; DI opt-in/fail-closed; composition both ways. |
| **Positive Flows** | PASS | Valid notification enqueue; successful create/renew with store persistence; full delta walk with link persistence and ready/graph rows; dispatch fetch-and-upsert; opt-in composition resolves route and three workers. |
| **Negative Flows** | PASS | Every D-1 drop path with Warning + 202; validator rejection matrix incl. Graph-backend-absent and http URL; renewal auth failure marks `reauthorize_failed`; failed fetch drops item; failed walk records failed ingest run; DI fail-closed `OptionsValidationException`. |
| **Edge Cases** | PARTIAL | Renewal-due boundary at `expiration - lead` exactly; lifetime 10080/lead 1 bounds; queue at exact capacity; empty delta page; expiration round-trip precision to ticks. Gaps: the `ParseSubscription` fail-fast arms (null body, missing `id`), the `ReadSubscription` MinValue fallback arm, and three defensive `ParseDeltaPage` arms are untested (B-117-01 and CR-117-02). |
| **Error Handling** | PARTIAL | Envelope failure propagation, drop-and-warn paths, and fail-closed startup validation are tested; the documented fail-fast guards in `ParseSubscription` (missing `id` -> `GraphMappingException`, null body -> `JsonException`) have no tests (B-117-01). |
| **Concurrency** | PASS | `ChannelNotificationQueueTests` cover FIFO round-trip of both item shapes, at-capacity DropWrite without blocking (Warning asserted), and pending-dequeue completion; the schema-ensure guards are documented safe under concurrent duplicate DDL (idempotent CREATE IF NOT EXISTS) and tested across two repository instances. |
| **State Transitions** | PASS | Subscription lifecycle transitions (active -> reauthorize_failed; removed -> deleted -> recreated; expired -> recreated) and the delta-link absent -> stored -> resumed progression driven tick-exactly via `FakeTimeProvider`. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 92.34% line, 83.16% branch (pooled solution) -> Post-change: 92.83% line, 83.25% branch. Change: +0.49% line, +0.09% branch. New/changed-code coverage: every instrumented new production file at 100.00% line (CloudSyncOptionsValidator 46/46 line and 22/22 branch; ChannelNotificationQueue 20/20; CloudSyncServiceCollectionExtensions 40/40; CryptoClientStateGenerator 5/5; DeltaReconciliationWorker 7/7; GraphDeltaReconciler 88/88 and 12/16 branch at exactly 75.00%; GraphNotificationsEndpoint 35/35; GraphSubscriptionManager 55/55 and 2/4 branch = 50.00% BELOW the per-file gate; ISubscriptionStore 8/8; NotificationDispatchWorker 17/17; NotificationRequestProcessor 31/31 and 2/2; NotificationWireModels 11/11; SubscriptionRenewalWorker 8/8; CoreCacheRepository.Subscriptions 9/9 and 1/2 branch = 50.00% BELOW the per-file gate; modified Schema partial 37/37 and 6/6; modified Program.cs 254/254 and 6/6 with both opt-in arms tested) — interface-only, record-only, and auto-property files are uninstrumented per the coverage policy clarification, and the async method bodies are uninstrumented under the pre-existing runsettings CompilerGenerated exclusion with behavioral verification recorded in Section 8. Disposition: FAIL — the per-new-file branch gate is not met for GraphSubscriptionManager.cs (50.00%) and CoreCacheRepository.Subscriptions.cs (50.00%); pooled gates, per-file line, and no-regression all pass; remediation finding B-117-01. Evidence: `evidence/baseline/dotnet-test-coverage.2026-07-03T01-26.md`, `evidence/qa-gates/dotnet-test-coverage.2026-07-03T02-16.md`, `evidence/qa-gates/coverage-comparison.2026-07-03T02-16.md`, reviewer re-run cobertura under `evidence/qa-gates/coverage-review/`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions because-clauses throughout ("@removed entries are skipped, not upserted", "the default path must map exactly what it maps today"); CsCheck prints the failing seed. |
| **Arrange-Act-Assert Pattern** | PASS | All directed tests follow Arrange/Act/Assert; property tests use the suite's generator + `Sample` structure. |
| **Document Intent** | PASS | XML docs on test classes state the AC coverage (AC-1..AC-5), design decisions (D-1..D-8), and determinism approach; each method has a scenario-describing name. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No live Graph calls anywhere: all HTTP flows through mocked `HttpMessageHandler` doubles returning Graph-shaped raw-string JSON; persistence uses in-memory shared-cache SQLite (`Mode=Memory;Cache=Shared` with per-test GUID names); composition tests use `WebApplicationFactory` with `UseSetting` (in-process). |
| **Use Mocks/Stubs** | PASS | Handler doubles, Moq `IAppTokenProvider`/`IHostAdapterClient`, recording logger doubles, deterministic `IClientStateGenerator` substitutes, `FakeTimeProvider` (8 of the test files). |
| **Environment Stability** | PASS | No temporary files (reviewer grep for GetTempPath/GetTempFileName/TempFile over the new test files: zero matches); no environment variables read; no mutable global state. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. One Blocking item outstanding (B-117-01) routed to remediation. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #117, `spec.md` v1.0 (eight recorded design decisions D-1..D-8 with the config-key table, lifecycle routing table, DDL, and API surface), user-story scenarios, and the master-spec §6.1-6.2/§8.1-8.2/§13 references define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-03T01-03.md` present. |
| **Document the plan** | PASS | `plan.2026-07-03T01-03.md` with per-phase evidence under `evidence/**`; completed tasks recorded in the PR-context summary. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | Raw REST + System.Text.Json; one webhook processor consumed by thin endpoint glue; pure static helpers for renewal math and clientState comparison; in-box `System.Threading.Channels`; no new frameworks or dependencies. |
| **Reusability** | PASS | Auth/retry/error-mapping/envelope synthesis reused from `GraphRequestExecutor` (D-8, zero new pipeline code); the message upsert sink and ingest-run convention reused from the poller (D-2); the fresh-DDL constants shared between the Schema partial and the lazy-ensure guards ("identical by construction"). |
| **Extensibility** | PASS | `INotificationQueue`, `ISubscriptionStore`, `IDeltaLinkStore`, `IClientStateGenerator`, and `IDeltaReconcileTrigger` seams keep Azure queue backends and test substitution behind narrow interfaces; options bind backward-compatibly with defaults; all schema changes additive. |
| **Separation of concerns** | PASS | Host-neutral webhook logic (`NotificationRequestProcessor`, no HttpClient/ASP.NET dependency) is separated from ASP.NET glue (`GraphNotificationsEndpoint`); pure validation (`CloudSyncOptionsValidator`) from DI wiring; clock-free persistence partials from `TimeProvider`-driven callers; workers from the domain services they drive. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | All new logic under `src/OpenClaw.Core/CloudSync/` in namespace `OpenClaw.Core.CloudSync` (namespace-not-project convention per the #74/#113 precedent recorded in the spec); store partials on `CoreCacheRepository` implement the CloudSync seams; tests mirror under `tests/OpenClaw.Core.Tests/CloudSync/` and the test-project root for the repository partials. |
| **Under 500 lines** | PASS | Reviewer-verified via `wc -l`: production max 326 (`GraphSubscriptionManager.cs`), modified `Program.cs` 358, test max 311 (`GraphDeltaReconcilerRecoveryTests.cs`). Executor evidence: `evidence/qa-gates/file-size-cap.2026-07-03T02-16.md`. |
| **Public vs internal** | PASS | The processor, endpoint, queue, manager, reconciler, workers, stores, and wire models are all `internal`; the public surface is exactly `CloudSyncOptions`, `CloudSyncOptionsValidator`, and `AddCloudSync`. |
| **No circular dependencies** | PASS | No project-reference or package changes; the namespace-scoped NetArchTest suite passes inside the 707/707 Core.Tests run, including the nothing-outside-CloudSync-depends-on-CloudSync direction (composition-root and store-partial carve-outs documented in the test). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `NotificationRequestProcessor`, `IsRenewalDue`, `ComputeCheckInterval`, `TriggerResyncAsync`, `EnsureGraphSubscriptionsSchemaAsync` — PascalCase, self-describing; `Async` suffix on async methods; repository field conventions followed. |
| **Docs/docstrings** | PASS | XML docs on every type and member, recording the D-1 202-and-drop rationale, the D-3 status-synthesis justification, the D-4 drop-write rationale, the clock-free store contract, and per-key env-binding forms on the options bag. |
| **Comment why, not what** | PASS | Inline comments explain the D-3 liveness-evidence justification at the upsert call sites and the schema-reuse-by-construction decision — not line-by-line narration. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 322 files, EXIT 0. Executor: `evidence/qa-gates/csharpier.2026-07-03T02-16.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors; wire-model fields honestly nullable with fail-fast enforcement at the parse boundary. |
| **4. Architecture** | PASS | New `CloudSyncArchitectureBoundaryTests` plus existing boundary suites pass within the full Core.Tests run. |
| **5. Testing** | PASS | Reviewer: full solution test run — 1154 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the seven T1 CsCheck property tests. |
| **6. Contract/schema checks** | PASS | No existing wire contract or HTTP surface changed; `IHostAdapterClient` and all existing DTOs untouched. The new webhook endpoint is additive and opt-in (flag-absent path verified byte-identical by the selection test with fail-before/pass-after evidence). Schema changes are additive `CREATE TABLE IF NOT EXISTS` only, with double-initialize idempotency tests. |
| **7. Integration tests** | N/A | Live webhook delivery and subscription creation require a tenant + public URL and are explicitly out of scope (spec Constraints & Risks; F16 deployment + HI-1 tenant handoff). The selection tests exercise the full composition root in-process via `WebApplicationFactory`, the deepest integration available without a tenant. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass final QA set at 2026-07-03T02-16. |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in the reviewer cobertura under `evidence/qa-gates/coverage-review/`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (the eighteen CloudSync files, two store partials, Schema fresh-DDL entries, and the two Program.cs conditional blocks). |
| **Design choices explained** | PASS | Eight recorded design decisions (D-1 202-and-drop with the validation-oracle rationale; D-2 cache-upsert sink; D-3 status synthesis; D-4 bounded DropWrite channel; D-5 clientState generation seam; D-6 opt-in mirroring the F13 selection block; D-7 fail-closed Graph-backend requirement; D-8 executor reuse). |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md`, `user-story.md`, and the `issue.md` mirror (AC-5's check-off is contested by finding B-117-01 — see the feature audit); the F16 queue deferral and post-deployment verification are recorded explicitly in both spec and issue. |
| **Provide next steps** | PASS | Spec records rollout as dark-by-default; enabling requires the Graph backend (D-7) and a reachable `NotificationUrl` arriving with F16; Azure queue implementations named as F16-adjacent work behind the existing seam. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, and TypeScript sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, #19, #18, and #99–#115 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; notification wire models carry honest `?` annotations with validation at the processor boundary; no null-forgiving operators outside the post-`Ok`-check envelope accesses that mirror the established CloudGraph pattern. |
| **Null-safety** | PASS | `ArgumentNullException.ThrowIfNull` guards every constructor and public entry point; missing required Graph fields fail fast with `GraphMappingException`/`JsonException` at the parse boundary (though those two arms are untested — B-117-01). |
| **Async / resource safety** | PASS | `HttpRequestMessage` construction per attempt via the executor's factory seam; SQLite connections via `await using`; all worker delays use the `Task.Delay(delay, timeProvider, ct)` TimeProvider overload — the deterministic injected-clock form, not the banned wall-clock form (banned-API posture verified by build + reviewer grep). |
| **Exceptions fail-fast** | PASS | Options validated fail-closed at startup (`ValidateOnStart`); workers catch broadly only at the defined loop boundary with Warning + continue (documented recovery-by-reconciliation rationale); unknown lifecycle events log Warning rather than throwing (fail-visible per spec risk note). See Minor CR-117-03 for a narrow cancellation-filter gap. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces in all 43 new/modified files; PascalCase publics; `Async` suffix; repository field convention followed. |
| **No new suppressions and no banned APIs** | PASS | Reviewer grep of the CloudSync production and test folders plus both store partials for pragma, SuppressMessage, nullable directives, dynamic, wall-clock APIs, and sleeps returned zero matches. All time flows through the injected `TimeProvider`; randomness flows through `RandomNumberGenerator` behind the D-5 seam. |
| **Dependency policy** | PASS | Zero new dependencies. No csproj changes anywhere in the diff; `System.Threading.Channels` is in-box; no Azure SDKs (explicit spec deferral). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq + CsCheck. The new tests follow the established repo convention, consistent with the prior validated #70, #80, #19, #18, and #99–#115 audits. Pre-existing repo-wide divergence, not a finding against this branch (spec.md Constraints & Risks records the MSTest convention explicitly).

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq + CsCheck)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataTestMethod]`+`[DataRow]`; FluentAssertions matchers; Moq for `IAppTokenProvider`/`IHostAdapterClient`; handler doubles; CsCheck `Gen`/`Sample` per suite convention. |
| **Test file location** | PASS | Nineteen test files under `tests/OpenClaw.Core.Tests/CloudSync/` mirroring `src/OpenClaw.Core/CloudSync/`; the two repository-partial test files at the test-project root mirroring the partials' location in `src/OpenClaw.Core/`. No colocation in the production tree. |
| **Coverage expectation** | FAIL | Pooled 92.83% line / 83.25% branch and every instrumented new file at 100.00% line; however two new files measure 50.00% instrumented branch, below the uniform 75% per-file gate (B-117-01, Section 8); one further file sits at exactly 75.00% with zero margin (CR-117-02). |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); CsCheck 4.7.0 is referenced by the test project. Seven genuine CsCheck properties cover the branch's pure functions: `ClientStateMatches` (equality-iff, identical-always-match, equal-length-unequal-never-match, null/empty-never-throw — 4 properties) and `IsRenewalDue` (arithmetic-definition equivalence, monotonicity once due, never-due-before-window — 3 properties). The remaining trivial pure functions (`ComputeCheckInterval`, the validator) have exhaustive directed/DataRow partition tests, consistent with the #105 grading precedent. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80 and #99–#115 T1 audits). |
| **Determinism (no sleeps, no wall clock)** | PASS | `FakeTimeProvider` in all eight time-dependent suites drives sweeps, ticks, and renewal boundaries tick-exactly; seeded CsCheck with failing-seed printing; zero wall-clock or sleep APIs in test code (reviewer grep: zero matches). |
| **No temporary files** | PASS | In-memory shared-cache SQLite for all store tests (documented in the test XML docs); fixtures are raw strings; zero filesystem access in any new test. |
| **No live network** | PASS | Every HTTP client is built over a mocked handler; the composition tests run in-process via `WebApplicationFactory`. |
| **Focused / isolated** | PASS | Fresh doubles/clock/options/connection string per test; no shared fixtures beyond the static `CloudSyncTestDoubles` helpers (stateless factories). |

---

## 5. Test Coverage Detail

Reviewer-parsed per-file coverage at branch head (fresh cobertura, line AND branch, duplicate class entries deduplicated per file+line; the executor's committed per-file raw counts are exactly 2x these values because the cobertura reports contain duplicate class entries per partial/nested type — the percentages agree on every file):

| File | Status | Line | Branch | Notes |
|------|--------|------|--------|-------|
| CloudSync/ChannelNotificationQueue.cs | NEW | 20/20 = 100.00% | no branches | Ctor + sync TryEnqueue/DequeueAsync fully instrumented. |
| CloudSync/CloudSyncOptions.cs | NEW | not instrumented | not instrumented | Auto-property options bag; compiler-generated accessors excluded by the pre-existing runsettings filter (Section 8). Behaviorally covered by DI binding/defaults tests. |
| CloudSync/CloudSyncOptionsValidator.cs | NEW | 46/46 = 100.00% | 22/22 = 100.00% | Every D-7 rule and both edges of every bound tested. |
| CloudSync/CloudSyncServiceCollectionExtensions.cs | NEW | 40/40 = 100.00% | no branches | Opt-in DI wiring; valid/fail-closed/null-guard paths tested. |
| CloudSync/CryptoClientStateGenerator.cs | NEW | 5/5 = 100.00% | no branches | Base64url alphabet, length bound, and uniqueness tested. |
| CloudSync/DeltaReconciliationWorker.cs | NEW | 7/7 = 100.00% | no branches | Async `ExecuteAsync` body uninstrumented (Section 8); 3 FakeTimeProvider scheduling tests. |
| CloudSync/GraphDeltaReconciler.cs | NEW | 88/88 = 100.00% | 12/16 = 75.00% | AT the branch gate exactly, zero margin. Async `RunAsync` body (lines 94-187) uninstrumented (Section 8). Partial arms are defensive guards in the sync `ParseDeltaPage` (lines 229, 235, 243 — CR-117-02). |
| CloudSync/GraphNotificationsEndpoint.cs | NEW | 35/35 = 100.00% | no branches | Endpoint lambda instrumented; handshake/notify/malformed round-trips tested through the factory. |
| CloudSync/GraphSubscriptionManager.cs | NEW | 55/55 = 100.00% | 2/4 = 50.00% | **BELOW the 75% per-file branch gate — Blocking B-117-01.** Async `CreateAsync`/`RenewAsync`/`HandleLifecycleAsync` bodies (lines 104-306) uninstrumented and behaviorally verified (Section 8); the measured-but-untaken arms are the two `ParseSubscription` fail-fast guards (lines 315, 320) with no test in the suite. |
| CloudSync/IClientStateGenerator.cs | NEW | interface-only | n/a | Omission permitted per the coverage policy clarification. |
| CloudSync/IDeltaLinkStore.cs | NEW | interface-only | n/a | Same. |
| CloudSync/INotificationQueue.cs | NEW | interface-only | n/a | Same. |
| CloudSync/ISubscriptionStore.cs | NEW | 8/8 = 100.00% | no branches | Status constants + record; interface members uninstrumented per policy. |
| CloudSync/NotificationDispatchWorker.cs | NEW | 17/17 = 100.00% | no branches | Async loop/dispatch bodies uninstrumented (Section 8); success/failure/lifecycle routing tested. |
| CloudSync/NotificationRequestProcessor.cs | NEW | 31/31 = 100.00% | 2/2 = 100.00% | Sync `HandleHandshake`/`ClientStateMatches`/`InvalidRequest` instrumented at 100%; async batch/item processing uninstrumented (Section 8) and covered by the 9-scenario processor matrix. |
| CloudSync/NotificationWireModels.cs | NEW | 11/11 = 100.00% | no branches | Record definitions. |
| CloudSync/NotificationWorkItem.cs | NEW | not instrumented | n/a | Records + const-only class; compiler-generated equality members excluded (same disposition as #109/#113 record/auto-property files). |
| CloudSync/SubscriptionRenewalWorker.cs | NEW | 8/8 = 100.00% | no branches | `ComputeCheckInterval` + ctor instrumented; async sweep bodies uninstrumented (Section 8) and covered by 5 FakeTimeProvider tests. |
| CoreCacheRepository.DeltaLinks.cs | NEW | not instrumented | not instrumented | Both store methods async; entire partial excluded under the pre-existing filter (extreme form precedent #105/#107). Behaviorally covered by 4 dedicated round-trip/idempotency tests. |
| CoreCacheRepository.Schema.cs | MODIFIED | 37/37 = 100.00% | 6/6 = 100.00% | Fresh-DDL constant concatenation; double-initialize test proves both tables exist on a fresh database. |
| CoreCacheRepository.Subscriptions.cs | NEW | 9/9 = 100.00% | 1/2 = 50.00% | **BELOW the 75% per-file branch gate — Blocking B-117-01.** Async store bodies uninstrumented (Section 8) and covered by 8 dedicated tests; the measured-but-untaken arm is the silent `?? DateTimeOffset.MinValue` fallback in `ReadSubscription` (line 152) with no test. |
| Program.cs | MODIFIED | 254/254 = 100.00% | 6/6 = 100.00% | Both D-6 opt-in arms tested (`CloudSyncSelectionTests`); fail-before/pass-after evidence EXIT 1 -> EXIT 0. |

Test suites delivered (21 files, 91 new runtime cases): processor matrix (7 + 3 lifecycle DataRows + edge cases), endpoint round-trips (3), queue semantics (3), options validator matrix (bound edges), DI extension (5), composition selection (2, with red/green evidence), clientState generator (4), subscription manager request-shape/persistence (4), lifecycle routing (4), renewal worker sweeps/boundary/schedule (5), delta reconciler walk (2) + recovery matrix (6), reconciliation worker schedule (3), dispatch worker routing (3), subscription store (8), delta-link store (4), architecture boundary suite, and 7 CsCheck properties across 2 files.

**Coverage:** pooled 92.83% line / 83.25% branch; every instrumented new file at 100.00% line. **Gap:** the per-file branch gate fails on two new files (B-117-01, Blocking, remediation triggered); one file at the gate with zero margin plus named defensive arms (CR-117-02, Minor).

**Regression:** zero existing test files modified (reviewer-verified from the branch diff — the only test changes are the twenty-one new files); all 616 baseline Core.Tests cases pass inside the reviewer's 707/707 run; MailBridge and HostAdapter coverage unchanged from baseline.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 1159 (1154 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 707 passed / 707 (baseline 616; +91 = the new CloudSync cases) | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~2 s | PASS |
| Pooled Code Coverage | 92.83% line, 83.25% branch | PASS |
| Core package (T1) | 94.51% line, 85.29% branch (both improved over baseline) | PASS |
| Per-new-file line coverage | 100.00% on every instrumented new file | PASS |
| Per-new-file branch coverage | GraphSubscriptionManager.cs 50.00%; CoreCacheRepository.Subscriptions.cs 50.00% (gate 75%) | FAIL |
| Net new tests vs baseline | +91 runtime cases (incl. 7 CsCheck properties) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 322 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest, namespace-scoped) | Included in `dotnet test` Core.Tests run | 707/707 pass (CloudSync, CloudGraph, and Agent boundary suites included) | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review"` | 1154 passed, 0 failed, 5 skipped | PASS |
| Per-file coverage re-measure | Reviewer cobertura parse (dedupe per file+line, max-pooled across the 3 reports) | Two new files below the 75% per-file branch gate | FAIL |
| File-size cap | `wc -l` over all new/modified `.cs` files | Production max 326; Program.cs 358; test max 311 — all under 500 | PASS |
| Evidence-location scan | `git diff --name-only 402703c..HEAD \| grep -E '^artifacts/'` | No matches | PASS |
| Banned-API / suppression scan | grep over the CloudSync folders and both store partials for pragma / SuppressMessage / nullable directives / dynamic / wall-clock APIs / sleeps | Zero matches | PASS |
| Test-hygiene scan | grep over the new test files for sleeps, wall clock, temp files, `Random.Shared` | Zero matches | PASS |
| Workflow-path scan (`modified-workflow-needs-green-run`) | `git diff --name-only 402703c..HEAD -- .github/workflows scripts/benchmarks .github/actions` | No matches — rule not triggered | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `f6aea4d` on 2026-07-03. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**One Blocking finding; one Minor coverage margin finding; informational observations.**

- **B-117-01 (Blocking) — per-new-file branch gate failure on two files.** Reviewer-parsed from fresh cobertura at branch head:
  - `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs`: instrumented branch 2/4 = **50.00%** (< 75%). The two untaken arms are both in the sync `ParseSubscription` helper: (line 315) the `?? throw new JsonException(...)` arm for a subscription body that deserializes to JSON `null`, and (line 320) the `string.IsNullOrWhiteSpace(wire.Id)` true-arm throwing `GraphMappingException` for a subscription response missing `id`. The file's XML doc and spec describe this fail-fast behavior explicitly, and no test in the suite exercises either arm (reviewer grep for `GraphMappingException` and missing-id fixtures across the CloudSync test tree: zero matches).
  - `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs`: instrumented branch 1/2 = **50.00%** (< 75%). The untaken arm is (line 152) the `ReadDateTimeOffset(reader, "expiration_utc") ?? DateTimeOffset.MinValue` fallback in `ReadSubscription`, which silently converts an unparseable stored expiration into `DateTimeOffset.MinValue` (see also CR-117-04 in the code review: a fail-fast alternative is preferable, but the minimal remediation is a directed test).
  - Distinction from the accepted #99–#115 dispositions: those covered lines/arms that were *excluded from instrumentation* (async bodies, auto-properties) and behaviorally verified. These arms are *measured and uncovered*, with no behavioral test anywhere — genuinely untested error-handling scenarios (general-unit-test Scenario Completeness requires error-handling coverage). Precedent: the #18 review graded a new file at 71.43% branch as a Blocking FAIL. The executor's `coverage-comparison.2026-07-03T02-16.md` reported both subsets (4/8 and 2/4, its raw counts doubled by duplicate class entries) but graded them PASS on the grounds that the surrounding files have 100% instrumented line coverage and the package gate holds; that grading is not supported by the uniform per-file gate text. Remediation is small: three to four directed tests (see `remediation-inputs.2026-07-03T02-34.md`).
- **CR-117-02 (Minor, cross-reference) — `GraphDeltaReconciler.cs` at exactly 75.00% branch (12/16), zero margin.** The four partial arms are defensive guards in the sync `ParseDeltaPage`: (line 229) the false arm of the `value`-present-and-array check (no test feeds a page without a `value` array), (line 235) the `@removed`-entry-without-`id` fallback to `"(unknown)"` (the only `@removed` fixture carries an id), and (line 243) the `?? throw` arm for a delta entry deserializing to JSON `null`. Concrete one-case test recommendations in `code-review.2026-07-03T02-34.md`. Exact-gate files carry no headroom for future edits; recommended to harden during the same remediation cycle.
- **Async-body instrumentation exclusion (Informational).** The pre-existing `mailbridge.runsettings` coverlet setting excluding `CompilerGeneratedAttribute` members means the async bodies of `GraphSubscriptionManager` (`CreateAsync`/`RenewAsync`/`HandleLifecycleAsync`, lines 104-306), `GraphDeltaReconciler.RunAsync` (lines 94-187), `NotificationRequestProcessor.ProcessNotificationsAsync`/`ProcessItemAsync`, all three worker `ExecuteAsync`/sweep bodies, and the entirety of the async store partials (`CoreCacheRepository.DeltaLinks.cs` has zero instrumented lines; `CoreCacheRepository.Subscriptions.cs` retains only its sync helpers) contribute zero instrumented lines. **The executor's `coverage-comparison.2026-07-03T02-16.md` states this exclusion explicitly** (an improvement over the #113/#115 non-disclosure pattern). Per the disposition accepted on the #99–#115 reviews, the reviewer verified the excluded bodies behaviorally: the manager's create/renew/lifecycle routing has 8 dedicated tests (request-shape pinning, persistence, all three lifecycle routes plus the unknown-event guard); the reconciler walk has 8 (multi-page, resume-verbatim, empty-link re-sync, missed re-sync, MaxPages bound, `@removed` skip, idempotent rerun, failed-run ingest row); the processor has 9 (handshake, drop matrix, enqueue shapes, malformed 400, queue-full, lifecycle DataRows, missing resourceData); the workers have 11 FakeTimeProvider scheduling/failure-continuation tests; the stores have 12 round-trip/idempotency/restart tests. The runsettings file is unchanged on this branch; the setting is an attribute-level filter, not a production-path exclude entry of the kind the coverage-exclusion policy prohibits. The recommended runsettings follow-up remains open (also recorded on #99–#115).
- **Auto-property/record instrumentation exclusion (Informational).** `CloudSyncOptions.cs` (auto-properties) and `NotificationWorkItem.cs` (records + const-only class) are absent from cobertura under the same pre-existing filter; behavioral coverage is direct (binding/defaults tests read every property; the queue and processor tests construct and route both record shapes). Same accepted disposition as the #109/#113/#115 audits.
- **Duplicate-class-entry doubling in executor evidence (Informational).** The executor's per-file table reports raw counts exactly 2x the deduplicated values (e.g., reconciler 176/176 vs 88/88; manager 110/110 vs 55/55) because the cobertura reports emit duplicate class entries per partial/nested type. Percentages agree on every file; no verdict is affected. Recorded so future audits compare like-for-like (the #19 dedupe precedent).

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, #19, #18, and #99–#115 audits. The format check ran to EXIT 0 over all 322 files.
- **MCP template/validator tools:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing C# artifact set (issue #115 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No existing test file was modified, deleted, or weakened (reviewer-verified: the branch diff contains only the twenty-one new test files under `tests/`). The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `402703c`)

Branch `feature/graph-subscriptions-delta-117`, head `f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3` (single commit). Range: `402703c6c18d0131128696147443ef9f41110f3c..f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3` (63 files, +6211/-1).

### Files Modified (categories)

1. **`src/OpenClaw.Core/CloudSync/`** (NEW, 18 files, 1589 lines total) — the CloudSync eventing module: options bag + pure full-list validator (D-7), notification wire models and work-item records, the D-4 bounded channel queue behind `INotificationQueue`, the D-5 clientState seam, the host-neutral webhook processor + thin endpoint glue (D-1), the subscription manager with lifecycle routing (D-8 executor reuse), the delta reconciler (D-2/D-3), the three `TimeProvider`-driven workers, and the opt-in DI extension (D-6).
2. **`src/OpenClaw.Core/CoreCacheRepository.{Subscriptions,DeltaLinks}.cs`** (NEW, 2 partials) — clock-free `graph_subscriptions`/`graph_delta_links` stores with lazy schema-ensure; **`CoreCacheRepository.Schema.cs`** (MODIFIED) — fresh-DDL reuse of the same constants; **`src/OpenClaw.Core/Program.cs`** (MODIFIED, +14) — the two D-6 opt-in conditional blocks.
3. **`tests/OpenClaw.Core.Tests/`** (NEW, 21 files, 3611 lines total) — 91 runtime cases: processor/endpoint/queue/validator/DI/selection/generator/manager/lifecycle/renewal/reconciler/recovery/worker/dispatch/store suites, the namespace-scoped architecture boundary suite, 7 CsCheck properties, and the shared test doubles.
4. **`docs/features/active/2026-07-03-graph-subscriptions-delta-117/`** (NEW, 20 files) — issue/spec/user-story/plan and canonical evidence (baseline, qa-gates, regression-testing); **`.claude/agent-memory/`** — 4 memory records (atomic-executor, prd-feature; metadata, not code).

---

## 10. Compliance Verdict

### Overall Status: MOSTLY COMPLIANT — ONE BLOCKING FINDING; REMEDIATION REQUIRED

The C# change passes formatting, linting, nullable type-checking, the namespace-scoped architecture-boundary suite, the full unit-test suite, the pooled and package coverage gates, the file-size cap, and every hygiene scan, all independently re-run by the reviewer at branch head. The T1 property-test obligation is satisfied directly (seven genuine CsCheck properties on the two pure functions the spec names). The module introduces zero new dependencies, is inert by default (both opt-in arms tested with red/green evidence), no existing test was modified, and there are no evidence-location, suppression, or banned-API violations. The `modified-workflow-needs-green-run` rule does not fire (verified: no workflow, benchmark, or action paths in the diff).

The single gate failure is B-117-01: two new production files measure 50.00% instrumented branch coverage, below the uniform 75% per-file gate, with the untaken arms being untested fail-fast/fallback guards. Remediation is triggered with a concrete, minimal fix list (`remediation-inputs.2026-07-03T02-34.md`).

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked FAIL-with-remediation because a required per-file gate is failing, not because any artifact or metric is absent.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (seams, executor/sink reuse, pure helpers; zero new dependencies)
- Module & File Structure: PASS (all files under 500 lines, production max 326)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-closed startup validation; drop-and-warn boundaries with documented recovery; fail-fast parse guards — untested, see B-117-01)
- Dependency Policy: PASS (zero new dependencies)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: FAIL (pooled 92.83%/83.25% pass; per-new-file branch gate fails on two files — B-117-01)
- Test Structure: PASS
- External Dependencies: PASS (mocked handlers, in-memory SQLite, no temp files, no live Graph calls)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq + CsCheck repo convention; tests/ mirror)
- Determinism: PASS (FakeTimeProvider tick-exact schedules and boundaries; seeded CsCheck)
- T1 obligations: PASS on property density (seven genuine properties; mutation gate is pipeline-stage, not per-commit); FAIL on the per-file branch coverage expectation (B-117-01)

---

### Metrics Summary

- 1154/1154 runnable solution tests passing (5 pre-existing environment-gated skips)
- 92.83% pooled line coverage, 83.25% pooled branch coverage (gates: 85%/75%), both improved over baseline
- Core package (T1): 94.51% line, 85.29% branch, both improved over baseline
- Every instrumented new file: 100.00% line coverage
- Per-file branch gate: FAIL on 2 of 22 changed production files (both at 50.00% instrumented, gate 75%)
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 43 touched `.cs` files under the 500-line cap (production max 326)

---

### Recommendation

**No-Go for PR until remediation closes.** All toolchain stages and pooled gates pass against branch head `f6aea4d`, but the per-new-file branch gate fails on `GraphSubscriptionManager.cs` (50.00%) and `CoreCacheRepository.Subscriptions.cs` (50.00%) — finding B-117-01. The remediation scope is three to four directed tests exercising the named untaken arms (plus the optional CR-117-02 hardening of the exact-gate reconciler file); no production behavior change is required unless the CR-117-04 fail-fast alternative is chosen for the MinValue fallback. Remediation inputs: `remediation-inputs.2026-07-03T02-34.md`.

---

## Appendix A: Test Inventory

C# test changes in this feature (19 files in `tests/OpenClaw.Core.Tests/CloudSync/`, 2 at the test-project root):

1. `CloudSyncOptionsValidatorTests.cs` (NEW, 194 lines) — defaults-with-valid-url pass; each D-7 rule fails individually (Graph backend disabled; missing/relative/http NotificationUrl; lifetime 0 and 10081; lead 0 and lead == lifetime; interval 0; capacity 0); boundary passes (lifetime 10080, lead 1); disabled-block always valid; null fail-fast.
2. `CryptoClientStateGeneratorTests.cs` (NEW, 80 lines) — non-empty; base64url alphabet only; under the Graph 128-character limit; two calls differ.
3. `ChannelNotificationQueueTests.cs` (NEW, 114 lines) — FIFO round-trip of both work-item shapes; at-capacity DropWrite without blocking with Warning asserted via a recording logger; pending dequeue completes on arrival.
4. `ClientStatePropertyTests.cs` (NEW, 87 lines) — 4 CsCheck properties on `ClientStateMatches`: equality-iff, identical-always-match, equal-length-unequal-never-match, null/empty-never-throw.
5. `NotificationRequestProcessorTests.cs` (NEW, 185 lines) — handshake echo (URL-decoded, HTML-encoded, text/plain 200); unknown-subscription drop; mismatched-clientState drop (Warning + 202 both); valid notification enqueues exactly one `{mailbox, messageId, changeType}` item.
6. `NotificationRequestProcessorEdgeTests.cs` (NEW, 189 lines) — malformed-JSON 400 in the repo error shape; queue-full still 202 with Warning; lifecycle DataRows (`reauthorizationRequired`/`removed`/`missed`) enqueue lifecycle items; missing resourceData dropped with Warning, not thrown.
7. `GraphNotificationsEndpointTests.cs` (NEW, 133 lines) — factory round-trips: handshake 200 text/plain with encoded token; valid POST 202 with item on the queue; malformed body 400.
8. `GraphSubscriptionManagerTests.cs` (NEW, 247 lines) — exact `POST subscriptions` shape (changeType, resource, both notification URLs, generated clientState, ISO-8601 expiration); created record persisted; exact `PATCH subscriptions/{id}` shape with expirationDateTime only; renewed record's expiration/status updated.
9. `GraphSubscriptionManagerLifecycleTests.cs` (NEW, 202 lines) — `reauthorizationRequired` renews and marks `reauthorize_failed` on 401; `removed` deletes and recreates; `missed` triggers the reconcile seam with no Graph call; unknown event logs Warning without throwing.
10. `SubscriptionRenewalWorkerTests.cs` (NEW, 236 lines) — renewal-due boundary exact at `expiration - lead`; sweep renews due and leaves undue; startup sweep recreates expired; startup sweep creates when none stored; fake-time tick triggers the periodic check.
11. `RenewalDuePropertyTests.cs` (NEW, 87 lines) — 3 CsCheck properties on `IsRenewalDue`: arithmetic-definition equivalence for arbitrary triples; monotonic once due; never due before the window.
12. `GraphDeltaReconcilerTests.cs` (NEW, 181 lines) — three-page walk pins the initial URL, follows nextLinks, persists the deltaLink; subsequent reconcile resumes from the stored link verbatim.
13. `GraphDeltaReconcilerRecoveryTests.cs` (NEW, 311 lines) — empty stored link starts from the initial request; `missed` entry point re-syncs despite a stored link; MaxPages bound stops a runaway chain (failed ingest run); `@removed` skipped at Debug and not upserted; rerun idempotent with ready/graph rows; failed walk records a failed `delta_reconcile` ingest run.
14. `DeltaReconciliationWorkerTests.cs` (NEW, 197 lines) — no run before the first interval; advancing by the interval triggers exactly one reconcile; a failing reconcile logs Warning and the next tick still runs.
15. `NotificationDispatchWorkerTests.cs` (NEW, 228 lines) — successful fetch upserts with ready/graph status and the envelope request id; failed fetch logs Warning, drops, loop continues; lifecycle item routes to the manager with no message fetch.
16. `CloudSyncServiceCollectionExtensionsTests.cs` (NEW, 161 lines) — valid configuration resolves all CloudSync services; three hosted workers registered; Graph-backend-absent fails closed; http NotificationUrl fails closed; null guards.
17. `CloudSyncSelectionTests.cs` (NEW, 117 lines) — flag absent: no `/graph/notifications` route and no CloudSync workers; opt-in: route mapped and three workers registered. Fail-before/pass-after evidence: `evidence/regression-testing/cloudsync-selection-{expect-fail,pass-after}.*` (EXIT 1 -> EXIT 0).
18. `CloudSyncArchitectureBoundaryTests.cs` (NEW, 206 lines) — namespace-scoped rules: CloudSync depends only on the allowed OpenClaw surfaces (CloudGraph/CloudAuth/Contracts/repository-ingest, never the Agent partition); COM-interop ban; nothing outside CloudSync depends on CloudSync internals except the composition root and the store partials.
19. `CloudSyncTestDoubles.cs` (NEW, 165 lines) — stateless factories for handler doubles, recording loggers, deterministic clientState generators, and in-memory stores.
20. `CoreCacheRepositorySubscriptionsTests.cs` (NEW, 197 lines, test-project root) — upsert/get round-trip with tick-precision expiration; unknown-id null; second upsert replaces; status update to `reauthorize_failed`; delete idempotent; schema-ensure idempotent across two instances; double InitializeAsync; restart survival via a fresh repository on the same connection string.
21. `CoreCacheRepositoryDeltaLinksTests.cs` (NEW, 97 lines, test-project root) — set/get round-trips the link verbatim; unknown mailbox null; second set overwrites; schema-ensure idempotent across two instances.

Reviewer run: `OpenClaw.Core.Tests` 707 passed, 0 failed; solution total 1154 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review"

# Opt-in composition regression subset (executor, AC-4 evidence)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CloudSyncSelectionTests"

# File-size cap
wc -l src/OpenClaw.Core/CloudSync/*.cs src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs src/OpenClaw.Core/CoreCacheRepository.DeltaLinks.cs src/OpenClaw.Core/CoreCacheRepository.Schema.cs src/OpenClaw.Core/Program.cs tests/OpenClaw.Core.Tests/CloudSync/*.cs

# Evidence-location scan
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD | grep -E '^artifacts/'

# Workflow-path scan (modified-workflow-needs-green-run trigger)
git diff --name-only 402703c6c18d0131128696147443ef9f41110f3c..HEAD -- .github/workflows scripts/benchmarks .github/actions

# Banned-API / suppression / hygiene scans of the new folders
grep -rnE '#pragma|SuppressMessage|#nullable|\bdynamic\b' src/OpenClaw.Core/CloudSync tests/OpenClaw.Core.Tests/CloudSync
grep -rnE 'Thread\.Sleep|DateTime\.UtcNow|DateTime\.Now|Random\.Shared' tests/OpenClaw.Core.Tests/CloudSync
grep -rnE 'GetTempPath|GetTempFileName|TempFile' tests/OpenClaw.Core.Tests/CloudSync
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-03
**Policy Version:** Current (as of audit date)
