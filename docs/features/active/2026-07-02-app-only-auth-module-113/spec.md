# app-only-auth-module — Spec

- **Issue:** #113
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Ready
- **Version:** 1.0

## Overview

Product Increment 1 mandates app-only OAuth 2.0 client-credentials auth with application permissions and `.default` scope, certificate-first, fail-closed on missing/expired credentials (`docs/open-claw-approach.master.md` §4.1/§4.4, §13 Steps 1-2). Zero auth code exists: no `Microsoft.Identity.Client`, `Azure.Identity`, or token-acquisition seam anywhere in `src/` (verified in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, Epic C item 12 / gap F12). The Graph-backed `IHostAdapterClient` (F13) cannot be built without a token provider.

This feature delivers a self-contained token-provider module: a host-neutral contract (`IAppTokenProvider`), an `Azure.Identity`-backed implementation (`ClientCredentialsTokenProvider`), validated options (`CloudAuthOptions`), an opt-in DI registration extension, and a fully mocked unit-test suite. No consumer is wired; the running application's behavior is unchanged.

## Design Decisions (recorded)

### D1 — Namespace inside `OpenClaw.Core`, not a new project

**Decision:** the module lives in namespace `OpenClaw.Core.CloudAuth` at `src/OpenClaw.Core/CloudAuth/`, with boundary enforcement by a new namespace-scoped test suite. No new project is created.

**Evidence:**

- `.claude/rules/architecture-boundaries.md` contains no rule requiring auth code to be a separate project. Its .NET layer assertions name future `TaskMaster.*` projects; its enforceable No-COM assertions (no VSTO/interop/COM-visible surface, mailbox access only via Office.js or Graph) are all satisfiable inside `OpenClaw.Core` — a Graph token provider is the sanctioned access route (rule 7).
- The repository precedent is namespace-partition enforcement, not project isolation: the deterministic agent surface (issue #74) is a namespace partition inside `OpenClaw.Core` enforced by `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs` (NetArchTest `ResideInNamespaceStartingWith` plus a reflection dependency walk).
- The existing boundary tests are unaffected by the package addition. Both tests in `AgentArchitectureBoundaryTests.cs` scope their assertions to types in `OpenClaw.Core.Agent` (excluding/including `Runtime` as appropriate) and ban only `OpenClaw.MailBridge`, `Microsoft.Office.Interop.Outlook`, `System.Runtime.InteropServices`, and the `OpenClaw.HostAdapter` host implementation. `Azure.*` is in no banned list, and adding a `PackageReference` to `OpenClaw.Core.csproj` does not create a type-level dependency in the `Agent` partition.
- What the package addition does require is a new guard so the dependency cannot spread: see D2.

### D2 — New boundary suite: Agent stays Azure-free; the CloudAuth contract stays Azure-free

New test file `tests/OpenClaw.Core.Tests/CloudAuth/CloudAuthArchitectureBoundaryTests.cs`:

1. **Agent isolation (NetArchTest):** types residing in `OpenClaw.Core.Agent` (including `Runtime`) must not depend on `Azure` or `Microsoft.Identity` (prefix match covers `Azure.Identity`, `Azure.Core`, and MSAL). Only `OpenClaw.Core.CloudAuth` may reference Azure types.
2. **Contract purity (reflection walk, same helper pattern as the Agent suite):** the public surface of `IAppTokenProvider`, `AppAccessToken`, `CloudAuthOptions`, `TokenAcquisitionException`, `ClientCredentialsTokenProvider`, and `CloudAuthServiceCollectionExtensions` exposes no type from an `Azure.*` or `Microsoft.Identity*` namespace. (The internal test constructor in D4 is exempt: the walk covers public members only.)

### D3 — Contract shape

```csharp
public interface IAppTokenProvider
{
    ValueTask<AppAccessToken> GetTokenAsync(CancellationToken cancellationToken);
}

public sealed record AppAccessToken(string Token, DateTimeOffset ExpiresOn)
{
    public override string ToString() => $"AppAccessToken(ExpiresOn: {ExpiresOn:O})";
}
```

- `ValueTask` because the cache-hit path completes synchronously (the dominant case).
- `AppAccessToken` overrides the record-generated `ToString()` so the token value cannot leak through logging or interpolation of the record itself (fail-closed against accidental secret disclosure; record `ToString()` would otherwise print `Token`).
- No `Azure.*` or MSAL type crosses the contract; F13 consumers depend only on this interface.

### D4 — `TokenCredential` seam: internal constructor, not a public factory delegate

**Decision:** `ClientCredentialsTokenProvider` has two constructors:

- **Public:** `ClientCredentialsTokenProvider(CloudAuthOptions options, TimeProvider timeProvider, ILogger<ClientCredentialsTokenProvider> logger)` — validates options (fail-closed at construction) and builds the credential via the internal `CredentialFactory`.
- **Internal (tests):** `internal ClientCredentialsTokenProvider(TokenCredential credential, CloudAuthOptions options, TimeProvider timeProvider, ILogger<ClientCredentialsTokenProvider> logger)` — accepts the abstract `Azure.Core.TokenCredential`, which Moq mocks directly (`GetTokenAsync` is overridable). `OpenClaw.Core.csproj` already declares `InternalsVisibleTo("OpenClaw.Core.Tests")`, so no configuration change is needed.

**Rejected alternative:** a public `Func<CloudAuthOptions, TokenCredential>` factory parameter. It would place an Azure type on the public surface, violating D2's contract-purity rule, and offers no testability the internal constructor does not already provide.

`CredentialFactory` (internal static, one file) is the only code that instantiates `ClientCertificateCredential` / `ClientSecretCredential`; it is exercised through the public constructor path in tests that assert construction behavior only (no token call).

### D5 — Certificate-first selection: exactly-one credential source, reject-ambiguous

`CloudAuthOptions` (plain sealed class with `get; set;` properties and primitive defaults, matching the `AgentPolicyOptions` convention):

| Property | Type | Default | Validation |
|---|---|---|---|
| `TenantId` | `string` | `""` | required, non-whitespace |
| `ClientId` | `string` | `""` | required, non-whitespace |
| `CertificatePath` | `string` | `""` | part of exactly-one rule; when set, non-whitespace |
| `ClientSecret` | `string` | `""` | part of exactly-one rule; when set, non-whitespace |
| `Scope` | `string` | `https://graph.microsoft.com/.default` | absolute URI form ending with `/.default` |
| `AuthorityHost` | `string` | `https://login.microsoftonline.com/` | absolute `https` URI |
| `RefreshSkewMinutes` | `int` | `5` | `0 <= value <= 60` |

Rules, all enforced by the pure static `CloudAuthOptionsValidator.Validate(CloudAuthOptions)` (returns the full list of violation messages, names the offending keys, never echoes values):

- **Exactly one** of `CertificatePath`, `ClientSecret` must be configured. Zero configured → error. Both configured → error (**reject-ambiguous**, not silent precedence: silent certificate-wins would mask a misconfiguration where an operator believes the secret is in use; the fail-closed mandate of master §4.4 favors refusing to start).
- **Certificate-first** is realized as the documented preferred source (configuration guidance and F13 wiring will use `CertificatePath`); `ClientSecret` is the documented fallback per master §13 Step 2 ("Prefer certificate auth to client secrets").
- **Certificate thumbprint is deferred** (recorded scope decision): master §13 Step 1 lists "certificate thumbprint / certificate path / key identifier or client secret" as alternatives; `OpenClaw.Core` deploys as a Linux container where the natural certificate mechanism is a mounted file, and thumbprint support would add `X509Store` lookup I/O for no current consumer. If a Windows-hosted consumer needs it later, it is an additive option plus one factory branch.
- Scope validation ends-with-`/.default` guards against accidentally configuring a delegated-style scope, which the client-credentials flow cannot serve (master §4.4); the override exists only for national-cloud Graph endpoints.

### D6 — Caching: single-flight refresh with expiry skew on `TimeProvider`

- The provider holds one cached `AppAccessToken` and a `SemaphoreSlim(1, 1)`.
- Freshness is a pure internal static predicate (`TokenFreshness.IsFresh(AppAccessToken? token, DateTimeOffset nowUtc, TimeSpan skew)`): fresh iff `token is not null && nowUtc < token.ExpiresOn - skew`. Pure so it gets a CsCheck property test (T1 obligation) and is trivially mutation-testable.
- `GetTokenAsync` fast path: if fresh per `timeProvider.GetUtcNow()`, return the cached token synchronously. Slow path: `await semaphore.WaitAsync(cancellationToken)`, double-check freshness, then call `credential.GetTokenAsync(new TokenRequestContext([options.Scope]), cancellationToken)`; on success store and return the mapped `AppAccessToken`.
- **Single-flight:** N concurrent callers with a stale cache produce exactly one credential call; the rest observe the refreshed cache at the double-check.
- **Fail-closed:** on acquisition failure the cached value is not returned even if a stale one exists; the failure propagates (D7). `OperationCanceledException` propagates unwrapped.
- The provider-level cache implements master §4.4's "cache tokens in-process" with a deterministic, testable skew. `Azure.Identity` credentials carry an internal MSAL cache, but that is an implementation detail this module does not rely on for its contract.
- All time reads go through the injected `TimeProvider` (repo convention: `SlotProposer`, `SchedulingWorker`; `Program.cs` already registers `TimeProvider.System`). No `DateTimeOffset.UtcNow` in production or test code; tests use `FakeTimeProvider`.

### D7 — Error contract: `TokenAcquisitionException`, no secrets anywhere

- Acquisition failures (`AuthenticationFailedException`, `CredentialUnavailableException`, or any non-cancellation failure from the credential) are caught at the single credential call site and rethrown as `OpenClaw.Core.CloudAuth.TokenAcquisitionException` with: tenant id, client id, scope, and the inner exception preserved. Rationale: consumers would otherwise need `Azure.Identity` types to catch failures, leaking the dependency D2 bans.
- Exception messages and log entries never contain the token, the client secret, the certificate path contents, or any credential material. Refresh success logs at `Debug` with the new `ExpiresOn` only; failures log at `Error` with the same non-secret context.
- Construction with invalid options throws (`ArgumentException`-family carrying all validator messages) — fail-closed at construction per the issue mandate.

### D8 — Opt-in DI registration; running application unchanged

- `CloudAuthServiceCollectionExtensions.AddCloudAuth(this IServiceCollection services, IConfiguration configuration)` binds `CloudAuthOptions` from section `OpenClaw:CloudAuth` (env form `OpenClaw__CloudAuth__TenantId`, etc. — the established binding mechanism per the issue #109 decision; no `ENABLE_*`/alias env layer is invented), applies `.Validate(...)` using `CloudAuthOptionsValidator` and `.ValidateOnStart()`, and registers `IAppTokenProvider` as a singleton `ClientCredentialsTokenProvider`. This introduces `ValidateOnStart` to the repo; it is justified here (and not retrofitted onto existing options) because fail-closed startup is this feature's explicit mandate.
- **`Program.cs` is not modified.** Nothing calls `AddCloudAuth`; the local Docker deployment is byte-for-byte unaffected. No feature flag is needed — absence of registration is the gate. F13 will call `AddCloudAuth` when the Graph-backed adapter lands, at which point the new env vars must also be added to the docker-compose `environment:` forward list (compose forwards an explicit list).

### D9 — Live verification is runbook-covered; no new human_interaction requirement

Live token exchange against a real Entra app registration is tenant-dependent and classified "Automatable-with-credentials → exception with a runbook" in the gap analysis automation-fit table. The prerequisites (tenant id, client id, certificate/secret details, in-/out-of-scope test mailboxes) are exactly the F11 engineering handoff package: `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, Step 7. This spec records the post-provisioning verification note (see Data & State); **this feature raises no new `human_interaction` requirement** — module authoring and its entire test suite run fully locally against mocked credentials.

## Behavior

Production files, all under `src/OpenClaw.Core/CloudAuth/`, all <= 500 lines:

| # | File | Purpose |
|---|---|---|
| 1 | `IAppTokenProvider.cs` | Contract (D3) |
| 2 | `AppAccessToken.cs` | Token record with redacting `ToString()` (D3) |
| 3 | `CloudAuthOptions.cs` | Options POCO (D5) |
| 4 | `CloudAuthOptionsValidator.cs` | Pure static validation, full violation list (D5) |
| 5 | `TokenFreshness.cs` | Pure static freshness predicate (D6) |
| 6 | `TokenAcquisitionException.cs` | Failure contract (D7) |
| 7 | `ClientCredentialsTokenProvider.cs` | Caching single-flight provider (D4/D6/D7) |
| 8 | `CredentialFactory.cs` | Internal; the only `Azure.Identity` instantiation site (D4/D5) |
| 9 | `CloudAuthServiceCollectionExtensions.cs` | Opt-in `AddCloudAuth` (D8) |

Test files, mirroring under `tests/OpenClaw.Core.Tests/CloudAuth/`:

| # | Test file | Focus |
|---|---|---|
| 1 | `AppAccessTokenTests.cs` | Redacting `ToString()`, value semantics |
| 2 | `CloudAuthOptionsValidatorTests.cs` | Full validation matrix + CsCheck properties |
| 3 | `TokenFreshnessTests.cs` | Boundary cases + CsCheck property (monotonicity, skew boundary) |
| 4 | `ClientCredentialsTokenProviderTests.cs` | Mocked `TokenCredential`: success, cache-hit, refresh, single-flight, cancellation, failure wrapping, construction fail-closed |
| 5 | `CloudAuthServiceCollectionExtensionsTests.cs` | Binding from in-memory configuration, `ValidateOnStart` failure on bad config, singleton resolution |
| 6 | `CloudAuthArchitectureBoundaryTests.cs` | D2 assertions |

Behavioral contract summary:

- `GetTokenAsync` returns the cached token while `now < ExpiresOn - RefreshSkewMinutes`; otherwise acquires exactly once per staleness window regardless of concurrency, using scope `Options.Scope` (default Graph `.default`).
- Construction validates options and builds the certificate credential when `CertificatePath` is set, the secret credential when `ClientSecret` is set, and throws when neither or both are set.
- Cancellation is honored at both the semaphore wait and the credential call; `OperationCanceledException` is never wrapped.
- Any other acquisition failure surfaces as `TokenAcquisitionException` (inner preserved); the stale cache is not served.

## Inputs / Outputs

- **Inputs:** configuration section `OpenClaw:CloudAuth` (env form `OpenClaw__CloudAuth__*`) with the keys and defaults in D5. Secret material (certificate file, client secret value) is provisioned by the operator via mounted files/environment from a secret manager per master §4.4/§13 Step 2; this module never reads a secret store directly and never persists credentials.
- **Outputs:** `AppAccessToken` instances to in-process consumers only. Logs: `Debug` on refresh (expiry timestamp only), `Error` on acquisition failure (tenant/client/scope context, no secrets). No files, no telemetry, no network listeners.
- **Config keys and defaults:** table in D5. Nothing is required at startup today because nothing registers the module (D8).
- **Versioning / backward compatibility:** all-new public surface; no existing public API changes. `OpenClaw.Core.csproj` gains one `PackageReference`.

## API / CLI Surface

No CLI. Public API:

```csharp
namespace OpenClaw.Core.CloudAuth;

public interface IAppTokenProvider
{
    ValueTask<AppAccessToken> GetTokenAsync(CancellationToken cancellationToken);
}

public sealed record AppAccessToken(string Token, DateTimeOffset ExpiresOn); // ToString() redacts Token

public sealed class CloudAuthOptions { /* D5 table */ }

public sealed class TokenAcquisitionException : Exception { /* TenantId, ClientId, Scope context */ }

public sealed class ClientCredentialsTokenProvider : IAppTokenProvider
{
    public ClientCredentialsTokenProvider(
        CloudAuthOptions options,
        TimeProvider timeProvider,
        ILogger<ClientCredentialsTokenProvider> logger);
    // internal ctor(TokenCredential, ...) for tests via existing InternalsVisibleTo
}

public static class CloudAuthServiceCollectionExtensions
{
    public static IServiceCollection AddCloudAuth(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

Example configuration (certificate-first; placeholder values):

```jsonc
{
  "OpenClaw": {
    "CloudAuth": {
      "TenantId": "00000000-0000-0000-0000-000000000001",
      "ClientId": "00000000-0000-0000-0000-000000000002",
      "CertificatePath": "/run/secrets/openclaw-graph-client.pem"
      // Scope, AuthorityHost, RefreshSkewMinutes: defaults per spec D5
    }
  }
}
```

Validation rules: see D5 (exactly-one credential source, reject-ambiguous; `/.default` scope suffix; https authority; skew 0-60).

## Data & State

- **State:** one in-memory cached `AppAccessToken` per provider instance (registered as a singleton). No persistence, no migration, no backfill. Restart discards the cache; the first call reacquires.
- **Invariants:** a returned token always satisfies `now < ExpiresOn - skew` at return time per the injected `TimeProvider`; a failed refresh never yields a previously cached token; the token string never appears in logs, exception messages, or `ToString()` output.
- **Live verification note (post-provisioning, tenant-dependent):** after an administrator completes the F11 runbook and delivers the Step 7 handoff package, an engineer verifies a real token exchange (acquire token, read an in-scope mailbox, fail against an out-of-scope mailbox — master §13 Step 2/Step 3). That verification is out of scope for this feature's automated suite and CI, consistent with the gap analysis "exception with a runbook" classification. No new `human_interaction` requirement is raised (D9).

## Constraints & Risks

- **New dependency — `Azure.Identity`** (pinned 1.21.0, the latest stable at implementation time), added only to `src/OpenClaw.Core/OpenClaw.Core.csproj`. Justification per `.claude/rules/general-code-change.md` dependency policy: mandated by master §4.4 (confidential client with certificate auth, MSAL acquire-silently pattern — `Azure.Identity` is Microsoft's supported wrapper over MSAL confidential-client for exactly this flow) and gap analysis Epic C item 12; first-party Microsoft package, actively maintained, brings `Azure.Core` and `Microsoft.Identity.Client` transitively. The test project needs no new package: `Azure.Core` flows transitively via the project reference, and Moq/CsCheck/`FakeTimeProvider`/NetArchTest are already referenced.
- **Dependency spread risk:** mitigated by D2's boundary suite (Agent namespace Azure-free; contract surface Azure-free).
- **Test framework note:** `.claude/rules/csharp.md` prescribes xUnit + NSubstitute, but the actual test tree (`tests/OpenClaw.Core.Tests/`) uses MSTest + FluentAssertions + Moq; new tests follow the repository reality, consistent with prior features.
- **Secret handling:** no secrets in code, config committed to the repo, tests, or logs. Tests use obviously fake constants (for example `"fake-token-value"`) and never real credential material. `ClientSecret` support exists only as the documented fallback; the runbook-driven guidance is certificate-first.
- **Determinism:** all expiry math via `TimeProvider`; tests via `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, already referenced). No `Task.Delay`/`Thread.Sleep`/wall-clock reads; the single-flight concurrency test coordinates with `TaskCompletionSource` gating inside the mocked credential, not timing.
- **T1 obligations** (`quality-tiers.yml` maps `OpenClaw.Core` to T1): zero `any`/`dynamic` escape hatches, >= 1 CsCheck property per pure function (`TokenFreshness.IsFresh`, `CloudAuthOptionsValidator.Validate`), mutation score >= 75% in the pre-merge/nightly pipeline, line >= 85% / branch >= 75%.
- **500-line cap:** the file split in Behavior keeps every file well under the cap.

## Implementation Strategy

- **Scope of change:** 9 new production files under `src/OpenClaw.Core/CloudAuth/`, 6 new test files under `tests/OpenClaw.Core.Tests/CloudAuth/`, one `PackageReference` line in `src/OpenClaw.Core/OpenClaw.Core.csproj`. No existing source file is modified (`Program.cs` untouched; existing boundary tests untouched).
- **New classes/functions:** per the Behavior tables; the only Azure-facing code paths are `CredentialFactory` and the credential call inside `ClientCredentialsTokenProvider`.
- **Dependency changes:** `Azure.Identity` only (rationale in Constraints & Risks).
- **Logging:** `ILogger<ClientCredentialsTokenProvider>` — `Debug` refresh events (expiry only), `Error` acquisition failures (non-secret context). No new logging infrastructure.
- **Rollout:** no flag; the module is inert until F13 calls `AddCloudAuth` (D8). Fallback path is trivial: nothing consumes the module, so reverting is deleting the folder and the package reference.
- **Evidence:** toolchain/QA/coverage evidence for this feature is written under `docs/features/active/2026-07-02-app-only-auth-module-113/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`.

## Acceptance Criteria

- [x] The `OpenClaw.Core.CloudAuth` namespace at `src/OpenClaw.Core/CloudAuth/` provides `IAppTokenProvider` (`ValueTask<AppAccessToken> GetTokenAsync(CancellationToken)`), `AppAccessToken` (token string + `ExpiresOn`, with a redacting `ToString()` override), `CloudAuthOptions` bound from configuration section `OpenClaw:CloudAuth`, and `ClientCredentialsTokenProvider`, requesting a scope that defaults to `https://graph.microsoft.com/.default` and is validated to end with `/.default`; no `Azure.*` or `Microsoft.Identity*` type appears on the public surface, enforced by a new architecture-boundary test suite at `tests/OpenClaw.Core.Tests/CloudAuth/CloudAuthArchitectureBoundaryTests.cs` that also asserts the `OpenClaw.Core.Agent` namespace remains free of Azure/MSAL dependencies; every new file is <= 500 lines. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Fail-closed configuration: validation rejects a missing/blank tenant id or client id, rejects zero or more than one configured credential source among `CertificatePath` and `ClientSecret` (reject-ambiguous; certificate is the documented preferred source), and rejects an out-of-range refresh skew — enforced both at DI startup (`Validate` + `ValidateOnStart` inside `AddCloudAuth`) and at direct provider construction; acquisition failures propagate as `TokenAcquisitionException` carrying tenant/client/scope context with the inner exception preserved; no stale token is ever served after a failed refresh; no secret, certificate, or token value appears in exception messages or logs. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Deterministic caching: the token is cached in-process and reused until `ExpiresOn` minus `RefreshSkewMinutes` (default 5) as measured by an injected `TimeProvider`; refresh is single-flight under concurrency (`SemaphoreSlim(1,1)` with a double-check, so N concurrent callers produce exactly one credential call); `FakeTimeProvider` tests prove cache-hit, skew-boundary, and expiry-refresh behavior without wall-clock reads. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`)
- [x] Unit tests (MSTest + FluentAssertions + Moq mocking the abstract `Azure.Core.TokenCredential` via the internal test constructor; CsCheck property tests for the pure freshness predicate and the options-validation matrix; no temp files; no live Entra calls) cover success, cache-hit, expiry-refresh, single-flight concurrency, cancellation propagation, failure propagation, and the full credential-source selection matrix; live token exchange is documented as a tenant-dependent post-provisioning step covered by the F11 handoff package (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, Step 7) with no new `human_interaction` requirement. (Evidence: `evidence/regression-testing/cloudauth-suite.2026-07-02T19-10.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)
- [x] Full C# toolchain passes (format -> lint -> type-check -> architecture -> test); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered; the only new package dependency is `Azure.Identity` in `src/OpenClaw.Core/OpenClaw.Core.csproj`, recorded in the spec per the dependency policy; `Program.cs` and the local Docker deployment are unchanged (registration exists only as the opt-in `AddCloudAuth` extension, which nothing calls yet). (Evidence: `evidence/qa-gates/final-qa-format.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-build.2026-07-02T19-12.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T19-13.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T19-13.md`, `evidence/other/dependency-build-gate.2026-07-02T18-55.md`, `evidence/other/scope-and-inertness-check.2026-07-02T19-11.md`)

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments (module is inert in the running Docker deployment)
- [ ] Tests updated/added (fully mocked unit tests; no integration tests — live token exchange is runbook-covered per D9)
- [ ] Edge cases and error handling covered by tests (validation matrix, skew boundary, cancellation, failure propagation, single-flight)
- [ ] Docs updated (this spec's live-verification note; feature-folder links current)
- [ ] Logging added per Implementation Strategy (no telemetry applicable)
- [ ] Toolchain pass completed (format -> lint -> type-check -> architecture -> test, restart on any failure or auto-fix)

## Seeded Test Conditions (from potential)

- [ ] Unit: token cached until expiry-skew; refresh after; concurrent callers single-flight (decided in D6: implemented via `SemaphoreSlim(1,1)` with double-check; test proves exactly one credential invocation for N concurrent stale-cache callers).
- [ ] Unit: certificate vs secret configuration selection matrix; invalid combinations rejected (decided in D5: exactly-one rule, reject-ambiguous for both-set, reject for neither-set; matrix covers cert-only valid, secret-only valid, both invalid, neither invalid, blank tenant/client invalid, malformed scope/authority invalid, out-of-range skew invalid).
- [ ] Unit: skew boundary — at exactly `ExpiresOn - skew` the token is stale (refresh occurs); one tick earlier it is fresh (CsCheck property plus example-based boundary test with `FakeTimeProvider`).
- [ ] Unit: cancellation — a canceled token before the semaphore wait and during the credential call surfaces `OperationCanceledException` unwrapped; the cache remains unchanged.
- [ ] Unit: failure propagation — credential failure surfaces `TokenAcquisitionException` with tenant/client/scope context and inner exception; message contains no secret/token; a prior cached-but-stale token is not returned.
- [ ] Unit: `AppAccessToken.ToString()` output contains the expiry and does not contain the token value.
- [ ] Architecture: `OpenClaw.Core.Agent` (all of it) has no `Azure`/`Microsoft.Identity` dependency; CloudAuth public surface exposes no Azure types.
