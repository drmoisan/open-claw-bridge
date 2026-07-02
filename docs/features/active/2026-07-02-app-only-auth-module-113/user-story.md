# `app-only-auth-module` — User Story

- Issue: #113
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-02

## Story Statement

- As a **platform engineer building the Graph-backed host adapter (F13)**, I want a host-neutral token seam (`IAppTokenProvider`) with deterministic in-process caching and a mockable acquisition boundary, so that I can implement and unit-test Graph calls without live Entra credentials and without `Azure.Identity`/MSAL types spreading beyond the auth module.
- As an **operator deploying the assistant**, I want cloud-auth configuration that fails at startup when settings are missing or ambiguous and never emits secrets to logs, so that misconfiguration is caught before any mailbox traffic is attempted — while today's local Docker deployment keeps running unchanged because nothing wires the module yet.

## Problem / Why

Product Increment 1 mandates app-only OAuth 2.0 client-credentials auth with application permissions and `.default` scope, certificate-first, fail-closed on missing/expired credentials (`docs/open-claw-approach.master.md` §4, §13 Step 2). Zero auth code exists: no `Microsoft.Identity.Client`, `Azure.Identity`, or token-acquisition seam anywhere in `src/` (verified in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, gap F12). The Graph-backed `IHostAdapterClient` (F13) cannot be built without a token provider.

Without a dedicated seam, the concrete failure modes are: Azure/MSAL types leaking into domain and adapter code (making every consumer untestable without cloud credentials), token expiry handled ad hoc per call site, secrets surfacing in logs or exception messages, and misconfiguration discovered only at first Graph call instead of at startup.

## Personas & Scenarios

- **Persona: Evan, platform engineer**
  - Implements master §13 (app-only auth, ingestion, Graph adapter) once the admin handoff arrives; next task is the Graph-backed `IHostAdapterClient` (F13).
  - Cares about: unit-testable seams (repo standard: MSTest + FluentAssertions + Moq, `FakeTimeProvider`, no live network in tests), architecture boundaries that keep vendor SDK types contained, and coverage/mutation gates on `OpenClaw.Core` (tier T1).
  - Constraints: must not begin coding against a real mailbox until the F11 handoff package delivers positive and negative test mailboxes (master §13 Step 1); CI can never call a live tenant.
  - Frustrations: vendor credential SDKs that force every downstream test to know about token plumbing; nondeterministic expiry tests built on wall-clock sleeps.
- **Persona: Dana's counterpart on operations (deployment operator)**
  - Runs the local Docker deployment today; will enable cloud auth after the F11 runbook handoff.
  - Cares about: startup-time failure with actionable messages when configuration is wrong, certificate-first credential handling with the secret path as a documented fallback, and zero secret material in code, images, or logs (master §13 Step 2).
  - Constraint: docker-compose forwards an explicit environment list, so enabling the module later is a deliberate configuration change, never an accident.

- **Scenario: Building the Graph adapter against the seam (happy path)**
  - Evan starts F13. He depends only on `IAppTokenProvider` and `AppAccessToken` — no Azure namespace appears in adapter code, and the new boundary test suite would fail his PR if it did.
  - In unit tests he stubs `IAppTokenProvider` directly; for provider-level behavior he relies on this feature's suite, which mocks the abstract `TokenCredential` through the internal test constructor.
  - When F13 wiring lands, he calls `AddCloudAuth(configuration)`; `ValidateOnStart` proves the configuration before any worker begins polling.
- **Scenario: Misconfigured credentials (fail-closed path)**
  - The operator sets both `OpenClaw__CloudAuth__CertificatePath` and `OpenClaw__CloudAuth__ClientSecret` while rotating credentials.
  - The service refuses to start: options validation reports the ambiguous credential-source configuration by key name (reject-ambiguous, no silent certificate-wins), with no secret values echoed. The operator removes the stale secret entry and the service starts.
- **Scenario: Token expiry under concurrent load (deterministic refresh)**
  - Several polling workers request tokens simultaneously just after the cached token crosses `ExpiresOn - RefreshSkewMinutes`.
  - Exactly one credential call refreshes the token (single-flight); the other callers receive the refreshed value. The equivalent unit test drives this with `FakeTimeProvider` and a gated mock credential — no sleeps, no wall clock.
- **Scenario: First live token exchange (post-provisioning, runbook-covered)**
  - After the Exchange administrator completes the F11 runbook (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`) and delivers the Step 7 handoff package (tenant id, client id, certificate details, test mailboxes), Evan configures a real environment and verifies: token acquired, in-scope mailbox readable, out-of-scope mailbox denied (master §13 Steps 2-3).
  - This verification is tenant-dependent and stays out of CI and out of this feature's automated suite; it raises no new `human_interaction` requirement because authoring and testing the module are fully local.

## Acceptance Criteria

- [x] The `OpenClaw.Core.CloudAuth` namespace at `src/OpenClaw.Core/CloudAuth/` provides `IAppTokenProvider` (`ValueTask<AppAccessToken> GetTokenAsync(CancellationToken)`), `AppAccessToken` (token string + `ExpiresOn`, with a redacting `ToString()` override), `CloudAuthOptions` bound from configuration section `OpenClaw:CloudAuth`, and `ClientCredentialsTokenProvider`, requesting a scope that defaults to `https://graph.microsoft.com/.default` and is validated to end with `/.default`; no `Azure.*` or `Microsoft.Identity*` type appears on the public surface, enforced by a new architecture-boundary test suite at `tests/OpenClaw.Core.Tests/CloudAuth/CloudAuthArchitectureBoundaryTests.cs` that also asserts the `OpenClaw.Core.Agent` namespace remains free of Azure/MSAL dependencies; every new file is <= 500 lines. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Fail-closed configuration: validation rejects a missing/blank tenant id or client id, rejects zero or more than one configured credential source among `CertificatePath` and `ClientSecret` (reject-ambiguous; certificate is the documented preferred source), and rejects an out-of-range refresh skew — enforced both at DI startup (`Validate` + `ValidateOnStart` inside `AddCloudAuth`) and at direct provider construction; acquisition failures propagate as `TokenAcquisitionException` carrying tenant/client/scope context with the inner exception preserved; no stale token is ever served after a failed refresh; no secret, certificate, or token value appears in exception messages or logs. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Deterministic caching: the token is cached in-process and reused until `ExpiresOn` minus `RefreshSkewMinutes` (default 5) as measured by an injected `TimeProvider`; refresh is single-flight under concurrency (`SemaphoreSlim(1,1)` with a double-check, so N concurrent callers produce exactly one credential call); `FakeTimeProvider` tests prove cache-hit, skew-boundary, and expiry-refresh behavior without wall-clock reads. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Unit tests (MSTest + FluentAssertions + Moq mocking the abstract `Azure.Core.TokenCredential` via the internal test constructor; CsCheck property tests for the pure freshness predicate and the options-validation matrix; no temp files; no live Entra calls) cover success, cache-hit, expiry-refresh, single-flight concurrency, cancellation propagation, failure propagation, and the full credential-source selection matrix; live token exchange is documented as a tenant-dependent post-provisioning step covered by the F11 handoff package (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, Step 7) with no new `human_interaction` requirement. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Full C# toolchain passes (format -> lint -> type-check -> architecture -> test); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; the only new package dependency is `Azure.Identity` in `src/OpenClaw.Core/OpenClaw.Core.csproj`, recorded in the spec per the dependency policy; `Program.cs` and the local Docker deployment are unchanged (registration exists only as the opt-in `AddCloudAuth` extension, which nothing calls yet). (Evidence: `evidence/qa-gates/final-qa-format.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-build.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T19-13.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T19-13.md`, `evidence/other/dependency-build-gate.2026-07-02T18-55.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)

## Non-Goals

- No consumer wiring: `Program.cs` is not modified, no worker or adapter consumes `IAppTokenProvider` in this feature (F13 does), and the local Docker deployment's behavior is unchanged.
- No Graph client or Graph calls of any kind — this feature ends at the token seam.
- No live-tenant test, CI job, or startup smoke test against real Entra; live token-exchange verification is post-provisioning and runbook-covered (F11 handoff package), with no new `human_interaction` requirement.
- No certificate-thumbprint (`X509Store`) credential source in this feature: the container deployment mounts certificate files; thumbprint support is a documented additive follow-up if a Windows-hosted consumer needs it.
- No direct Key Vault (or other secret-manager) integration: secret material reaches the process as mounted files/environment values provisioned by the operator per master §4.4; retrieval tooling is out of scope.
- No delegated auth flow of any kind (master §4.1: client credentials only).
- No `ENABLE_*` or alias environment-variable layer: configuration binds exclusively through `OpenClaw:CloudAuth` / `OpenClaw__CloudAuth__*`, consistent with the issue #109 decision.
- No changes to existing architecture-boundary tests; the new CloudAuth suite is additive.
