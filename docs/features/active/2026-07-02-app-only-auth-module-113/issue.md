# app-only-auth-module (Issue #113)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-07-02-app-only-auth-module-113/ (Issue #113)

- Issue: #113
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/113
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

Product Increment 1 mandates app-only OAuth 2.0 client-credentials auth with application permissions and `.default` scope, certificate-first, fail-closed on missing/expired credentials (`docs/open-claw-approach.master.md` §4, §13 Step 2). Zero auth code exists: no `Microsoft.Identity.Client`, `Azure.Identity`, or token-acquisition seam anywhere in `src/` (verified in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, gap F12). The Graph-backed `IHostAdapterClient` (F13) cannot be built without a token provider.

## Proposed Behavior

- New `OpenClaw.CloudAuth` namespace inside `OpenClaw.Core` (per the architecture rule that new Core-adjacent logic folds into a namespace, not a new project) OR a small new class library if architecture tests require isolation — decide in spec with evidence from `.claude/rules/architecture-boundaries.md` and existing NetArchTest rules.
- `IAppTokenProvider` contract: `GetTokenAsync(CancellationToken)` returning an access token + expiry; consumers never see MSAL types.
- `ClientCredentialsTokenProvider` implementation using `Azure.Identity` `ClientCertificateCredential` (preferred) / `ClientSecretCredential` (documented fallback), requesting `https://graph.microsoft.com/.default`, with in-process caching honoring `ExpiresOn` minus a refresh skew, and fail-closed behavior: missing/malformed configuration throws at construction (options validation), acquisition failure propagates with context.
- Options type `CloudAuthOptions` (tenant id, client id, certificate thumbprint/path OR secret reference, authority host) with data-annotation/explicit validation at startup; no secrets in code or logs.
- Unit tests against a mocked `TokenCredential` seam (no live Entra calls); live token-exchange verification is tenant-dependent and covered by a runbook note referencing the F11 runbook's handoff package (no new human_interaction requirement beyond documenting that live verification happens post-provisioning).

## Acceptance Criteria

- [x] The `OpenClaw.Core.CloudAuth` namespace at `src/OpenClaw.Core/CloudAuth/` provides `IAppTokenProvider` (`ValueTask<AppAccessToken> GetTokenAsync(CancellationToken)`), `AppAccessToken` (token string + `ExpiresOn`, with a redacting `ToString()` override), `CloudAuthOptions` bound from configuration section `OpenClaw:CloudAuth`, and `ClientCredentialsTokenProvider`, requesting a scope that defaults to `https://graph.microsoft.com/.default` and is validated to end with `/.default`; no `Azure.*` or `Microsoft.Identity*` type appears on the public surface, enforced by a new architecture-boundary test suite at `tests/OpenClaw.Core.Tests/CloudAuth/CloudAuthArchitectureBoundaryTests.cs` that also asserts the `OpenClaw.Core.Agent` namespace remains free of Azure/MSAL dependencies; every new file is <= 500 lines. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Fail-closed configuration: validation rejects a missing/blank tenant id or client id, rejects zero or more than one configured credential source among `CertificatePath` and `ClientSecret` (reject-ambiguous; certificate is the documented preferred source), and rejects an out-of-range refresh skew — enforced both at DI startup (`Validate` + `ValidateOnStart` inside `AddCloudAuth`) and at direct provider construction; acquisition failures propagate as `TokenAcquisitionException` carrying tenant/client/scope context with the inner exception preserved; no stale token is ever served after a failed refresh; no secret, certificate, or token value appears in exception messages or logs. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Deterministic caching: the token is cached in-process and reused until `ExpiresOn` minus `RefreshSkewMinutes` (default 5) as measured by an injected `TimeProvider`; refresh is single-flight under concurrency (`SemaphoreSlim(1,1)` with a double-check, so N concurrent callers produce exactly one credential call); `FakeTimeProvider` tests prove cache-hit, skew-boundary, and expiry-refresh behavior without wall-clock reads. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Unit tests (MSTest + FluentAssertions + Moq mocking the abstract `Azure.Core.TokenCredential` via the internal test constructor; CsCheck property tests for the pure freshness predicate and the options-validation matrix; no temp files; no live Entra calls) cover success, cache-hit, expiry-refresh, single-flight concurrency, cancellation propagation, failure propagation, and the full credential-source selection matrix; live token exchange is documented as a tenant-dependent post-provisioning step covered by the F11 handoff package (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, Step 7) with no new `human_interaction` requirement. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Full C# toolchain passes (format -> lint -> type-check -> architecture -> test); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; the only new package dependency is `Azure.Identity` in `src/OpenClaw.Core/OpenClaw.Core.csproj`, recorded in the spec per the dependency policy; `Program.cs` and the local Docker deployment are unchanged (registration exists only as the opt-in `AddCloudAuth` extension, which nothing calls yet). (Evidence: `evidence/qa-gates/final-qa-format.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-build.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T19-13.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T19-13.md`, `evidence/other/dependency-build-gate.2026-07-02T18-55.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)

## Constraints & Risks

- New NuGet dependency (Azure.Identity) — justified by master §4.4 (MSAL/confidential client); record in spec per dependency policy.
- Do not wire any consumer yet (F13 consumes it); DI registration optional/gated so the local Docker deployment is unaffected.
- MSTest + FluentAssertions + Moq + CsCheck; no temp files; 500-line cap; TimeProvider for expiry math.

## Test Conditions to Consider

- [ ] Unit: token cached until expiry-skew; refresh after; concurrent callers single-flight (if implemented — decide in spec).
- [ ] Unit: certificate vs secret configuration selection matrix; invalid combinations rejected.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [ ] Create active feature folder from the template
