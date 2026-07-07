# Research: Send-on-Behalf Principal Allowlist (F15, issue #119)

Date: 2026-07-06
Feature: F15 `send-on-behalf-allowlist` (epic `docs/features/epics/openclaw-vision/epic-plan.md`, band C3, floor signal `auth_or_token_handling`)
Upstream dependency: F13 Graph-backed adapter (issue #115, merged as PR #116), whose design decision D7 explicitly defers "Full send-on-behalf semantics including the recipient allowlist" to F15 (`docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md` line 91).

All findings below were verified by reading the named files in this worktree on 2026-07-06.

---

## 1. Current State Analysis

### 1.1 The exact current send path (verified)

`src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`:

- `SendMailAsync(SendMailRequest, string? requestId, CancellationToken)` builds `POST users/{a}/sendMail` (where `{a}` = `options.AssistantMailboxUpn`, URL-escaped via the `Assistant` property in `GraphHostAdapterClient.cs` line 101) and hands a request factory to `executor.ExecuteAsync<object?>` (the shared D5/D6 pipeline in `GraphRequestExecutor.cs`).
- `ComposeSendMailBody` serializes the wire request (camelCase, `GraphRequestExecutor.JsonOptions`) and injects `message.from.emailAddress.address = options.PrincipalMailboxUpn` **only when** `PrincipalMailboxUpn != AssistantMailboxUpn` compared with `StringComparison.OrdinalIgnoreCase` (lines 55-66). The wire contract (`SendMailRequest`, `MailContracts.cs`) carries no `from` field of its own, so the `from` identity is exclusively configuration-supplied — arbitrary caller-supplied `from` mailboxes are already impossible, which satisfies the second developer safeguard of master Step 8 but not the first ("Hard-code an allowlist of mailbox identities the app may send from", `docs/open-claw-approach.master.md` line 1559).
- There is **no authorization check of any kind** today: any configured `PrincipalMailboxUpn` that differs from the assistant is silently represented. This is the gap F15 closes.

Order of operations inside `GraphRequestExecutor.ExecuteAsync` (relevant to gate placement): token acquisition happens **before** the request factory is invoked (lines 67-93). A gate placed inside `SendMailAsync` before `executor.ExecuteAsync` therefore prevents both the HTTP call and the token acquisition — the deny path performs zero I/O.

### 1.2 Configuration and validation (verified)

- `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs` — plain options bag bound from section `OpenClaw:GraphAdapter` (env pattern `OpenClaw__GraphAdapter__<Key>`). Existing identity keys: `PrincipalMailboxUpn`, `AssistantMailboxUpn` (both required non-whitespace when `Enabled`).
- `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs` — pure, full-violation-list validator; messages "name the offending configuration keys and never echo configured values"; all rules apply only when `Enabled == true`.
- `src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs` — opt-in DI registration with `.Validate(...).ValidateOnStart()` fail-closed startup validation.

### 1.3 Error vocabulary (verified)

- `src/OpenClaw.HostAdapter.Contracts/ApiError.cs`: `ApiError(string Code, string Message, string? BridgeErrorCode = null, bool Retryable = false)`; `ApiEnvelope<T>(bool Ok, T? Data, ApiMeta Meta, ApiError? Error)`; `ApiMeta(RequestId, AdapterVersion, Bridge)`.
- Graph adapter codes emitted today (`GraphRequestExecutor.cs`): `CONFIGURATION_ERROR` (token acquisition failure), `TRANSPORT_FAILURE`, `INVALID_REQUEST` (400), `UNAUTHORIZED` (401/403), `NOT_FOUND` (404), `THROTTLED` (429 exhausted), `INTERNAL_ERROR`. `BridgeErrorCode` carries the Graph `error.code` passthrough on HTTP failures and is `null` on locally synthesized failures.
- The only runtime consumer of the send envelope, `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` lines 124-146, throws `InvalidOperationException($"Outbound sendMail failed: {code}: {message}")` on any non-Ok envelope. It interpolates the code into a message; nothing switches on a closed code enum. Both reuse of `UNAUTHORIZED` and an additive code are therefore mechanically safe.

### 1.4 Master vision requirements (verified)

- §5.2 line 371: "Maintain an explicit allowlist of principal mailboxes that the assistant mailbox is allowed to represent."
- Step 8 line 1558-1561 developer safeguards: hard-coded allowlist of `from` identities; no caller-supplied `from`; log every outbound send with correlation IDs (correlation logging landed in F9).
- §5.3 last paragraph (line 407): "Before broad rollout, validate the exact rendered result in Outlook and OWA for your tenant" — tenant validation, not code.
- §5.2 / Step 5 (lines 1378-1400): the tenant-side `Set-Mailbox -GrantSendOnBehalfTo` grant plus "Document exactly which principal mailboxes the assistant mailbox may represent."

### 1.5 Precedents (verified)

- **F11 (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/`)**: tenant configuration shipped as scripts + a human runbook (`runbooks/exchange-rbac-setup.runbook.md`), with the tenant-execution requirement recorded as `human_interaction` requirement HI-1, `response: "exception"`, `runbook_path` pointing into the feature folder (quoted in `evidence/other/human-interaction-record.2026-07-02T18-47.md`). F11's runbook already covers the `GrantSendOnBehalfTo` grant itself (HI-1 text names "Send-on-behalf grants").
- **F12** established `IAppTokenProvider`/CloudAuth app-only auth, already consumed by the executor; F15 needs no auth-model changes.
- **F13 architecture tests** (`tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphArchitectureBoundaryTests.cs`) enforce three namespace-prefix rules over `OpenClaw.Core.CloudGraph`; any new type in that namespace is automatically in scope.
- **Test stack** (verified in `GraphHostAdapterClientSendMailTests.cs`): MSTest (`[TestClass]`/`[TestMethod]`/`[DataTestMethod]`) + FluentAssertions + Moq (`MockBehavior.Strict` token provider) + `FakeTimeProvider` + the repo `FakeHttpHandler`; CsCheck for property tests (e.g. `GraphEventMapperPropertyTests.cs`). The F13 spec (line 233) records that this stack — not the xUnit/NSubstitute wording in `.claude/rules/csharp.md` — is the repository's actual convention.

---

## 2. Recommended Design

### 2.1 Summary

Add a deterministic, fail-closed, per-send authorization gate for principal representation, composed of four additive pieces, all in `OpenClaw.Core.CloudGraph`:

1. **Options key (additive)** — `GraphAdapterOptions.AllowedPrincipalMailboxUpns`, a string collection bound from `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` (env pattern `OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, `__1`, ...). Default: empty collection. An empty allowlist means **no on-behalf send is permitted** (fail-closed default); self-send (`{p} == {a}`) remains unaffected because no representation occurs.
2. **Pure authorizer (new file)** — `SendOnBehalfAuthorizer`, an `internal static` class (matching the minimal-surface preference; `GraphHostAdapterClient` itself is internal and tests reach it via the existing `InternalsVisibleTo`) with one pure function:

   ```csharp
   internal static SendAuthorizationDecision Authorize(
       string principalMailboxUpn,
       string assistantMailboxUpn,
       IEnumerable<string> allowedPrincipalMailboxUpns)
   ```

   returning a three-value decision enum: `AllowedSelf` (principal equals assistant, `OrdinalIgnoreCase` after `Trim()`), `AllowedOnBehalf` (principal differs and is a member of the allowlist, `OrdinalIgnoreCase` after trimming both sides), `DeniedNotAllowlisted` (principal differs and is not a member — including the empty-allowlist case). No I/O, no clock, no logging — the same purity contract `GraphAdapterOptionsValidator` documents.
3. **Gate insertion** — `GraphHostAdapterClient.SendMailAsync` calls the authorizer first. On `DeniedNotAllowlisted` it returns a synchronous failure envelope (`Task.FromResult`) **before** `executor.ExecuteAsync`, so no token acquisition and no HTTP request occur. The existing `ResolveRequestId` helper (`GraphHostAdapterClient.cs` line 122) supplies the `ApiMeta` request id exactly as the executor would have. The decision also replaces the inline `principalIsAssistant` comparison in `ComposeSendMailBody`: `from` is injected iff the decision is `AllowedOnBehalf`, so the representation predicate and the authorization predicate can never diverge (single source of truth).
4. **Validator addition (shape only)** — when `Enabled`, every `AllowedPrincipalMailboxUpns` entry must be non-whitespace (message style: "AllowedPrincipalMailboxUpns entries must be non-whitespace."). An empty list is valid configuration (it expresses deny-all-on-behalf). Membership of `PrincipalMailboxUpn` in the allowlist is deliberately **not** a startup validation rule — see rejected alternatives.

### 2.2 Error surface decision

Reuse the existing `UNAUTHORIZED` code with a distinguishing `BridgeErrorCode` discriminator:

```csharp
new ApiError(
    "UNAUTHORIZED",
    "The configured principal mailbox is not on the send-on-behalf allowlist "
        + "(OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns); the send was rejected "
        + "before any request was made.",
    "SendOnBehalfDenied",
    Retryable: false
)
```

Rationale:

- The denial is semantically an authorization failure, the same category the D5 matrix assigns to Graph 401/403. Reuse keeps the cross-adapter vocabulary closed (the `BridgeErrorCodes` constants in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` already include `UNAUTHORIZED`).
- The `BridgeErrorCode` field is the established discriminator channel: on Graph HTTP failures it carries the Graph `error.code` (for example Exchange's `ErrorSendAsDenied` on a tenant-side rejection); the stable literal `SendOnBehalfDenied` lets telemetry and tests distinguish the local policy denial from a remote 403 without growing the top-level code set.
- `Retryable: false` is correct: retrying without a configuration change cannot succeed.
- Message style follows the validator convention: it names the offending configuration key and never echoes configured values (no UPN in the message). The deny should also log one `LogWarning` with the request id only (the executor's precedent: no tokens, no bodies, no UPNs in log templates).

### 2.3 Behavior semantics (decision table)

Comparisons: `StringComparison.OrdinalIgnoreCase` on `Trim()`ed values, matching the existing D7 comparison; no culture-aware comparison.

| # | Condition (adapter `Enabled`) | Decision | Effect |
|---|---|---|---|
| 1 | `{p} == {a}` (case-insensitive) | `AllowedSelf` | Send proceeds; no `from` injected (unchanged D7 behavior); allowlist irrelevant |
| 2 | `{p} != {a}`, allowlist contains `{p}` (case-insensitive, trimmed) | `AllowedOnBehalf` | Send proceeds; `from = {p}` injected (unchanged D7 body) |
| 3 | `{p} != {a}`, allowlist empty | `DeniedNotAllowlisted` | Fail-closed deny envelope; no token call, no HTTP |
| 4 | `{p} != {a}`, allowlist non-empty, `{p}` absent | `DeniedNotAllowlisted` | Same deny envelope |
| 5 | Allowlist entry differing from `{p}` only by case or surrounding whitespace | membership | Treated as a match (deterministic normalization) |
| 6 | Whitespace-only allowlist entry | n/a | Rejected at startup by the validator (shape rule); never reaches the authorizer |
| 7 | Duplicate allowlist entries | membership | Harmless; set semantics |

Deny envelope shape: `ApiEnvelope<object?>(false, null, new ApiMeta(resolvedRequestId, "cloudgraph", null), error)` — identical `ApiMeta` synthesis to every other adapter failure.

Read routes, `GetStatusAsync`, and the local (non-Graph) adapter are untouched. The local adapter path (`HostAdapterHttpClient` → HostAdapter → MailBridge COM send) performs no `from` injection at all, so no parity change is required; the spec should record this as an intentional, Graph-backend-only failure mode (the contract-parity obligation is surface parity, and `SendMailAsync`'s signature and envelope type are unchanged).

### 2.4 Rejected alternatives (brief)

- **Startup membership validation only** (validator requires `{p}` ∈ allowlist when `{p} != {a}`): fails the whole adapter at boot — including read routes — for a send-policy misconfiguration, and produces no per-send deny envelope, which is the acceptance behavior F15 names ("rejected rather than silently sent"). Also makes the runtime gate an untestable dead branch.
- **New top-level code `SEND_ON_BEHALF_DENIED`**: additive and mechanically safe (no consumer enumerates codes), but grows the cross-adapter vocabulary for no functional gain over `UNAUTHORIZED` + `BridgeErrorCode` discriminator.
- **Gate inside `GraphRequestExecutor`**: wrong layer; the executor is transport-generic and knows nothing about send semantics.
- **Caller-supplied `from` validated against the allowlist**: the wire contract intentionally carries no `from` (D7), and master Step 8 line 1560 prohibits caller-supplied `from` mailboxes; the gate validates configuration, not request input.
- **Semicolon-delimited single-string config key**: the .NET configuration binder natively binds indexed children to collections; a delimited scalar would add bespoke parsing with no benefit.

---

## 3. Requirements Mapping — file targets

| File | Change |
|---|---|
| `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs` | Additive `AllowedPrincipalMailboxUpns` collection property (get-only initialized list works with the binder and analyzer stack), XML doc with env-binding pattern and fail-closed-empty semantics |
| `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs` (new) | Pure decision function + `SendAuthorizationDecision` enum |
| `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs` | Non-whitespace-entries rule (Enabled-only, full-list style) |
| `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs` | Gate before `executor.ExecuteAsync`; deny envelope + warning log; `ComposeSendMailBody` consumes the decision instead of re-deriving `principalIsAssistant` |
| `quality-tiers.yml` | No change (OpenClaw.Core already T1) |
| `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/` (new) | Tenant validation runbook (section 6) |

No public contract file changes: `IHostAdapterClient`, `ApiError`, `ApiEnvelope`, and the wire DTOs are untouched. The change is additive at the options surface and behavioral only on the Graph send path.

---

## 4. Tier Classification and Gate Obligations

`OpenClaw.Core` is classified **T1** in `quality-tiers.yml` (line 13), and this feature is squarely inside the T1 examples list of `.claude/rules/quality-tiers.md` ("auth/token handling"). The epic manifest independently assigns F15 band C3 with floor signal `auth_or_token_handling`. Obligations that bind this feature:

- Line coverage >= 85%, branch coverage >= 75% (uniform), no regression on changed lines.
- Property tests: >= 1 per pure function (CsCheck) — `SendOnBehalfAuthorizer.Authorize` and the extended `GraphAdapterOptionsValidator.Validate` both qualify.
- Mutation score >= 75% (Stryker.NET, pre-merge/nightly pipeline, not the per-commit loop).
- Zero untyped escape hatches; architecture violations 0; analyzer/format/nullable gates per the seven-stage toolchain loop.

---

## 5. Test Strategy and Matrix

Stack (repository-actual, per `tests/OpenClaw.Core.Tests/CloudGraph/`): MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` + `FakeHttpHandler`. No live Graph calls; no temporary files. New test files mirror production structure under `tests/OpenClaw.Core.Tests/CloudGraph/`.

### 5.1 `SendOnBehalfAuthorizerTests.cs` (unit, pure)

| Test | Scenario |
|---|---|
| Self-send equal UPNs → `AllowedSelf` | rows 1 |
| Self-send differing only by case → `AllowedSelf` | OrdinalIgnoreCase parity with D7 |
| Member principal → `AllowedOnBehalf` | row 2 |
| Case-differing member → `AllowedOnBehalf` | row 5 |
| Whitespace-padded member/principal → `AllowedOnBehalf` | row 5 |
| Empty allowlist, `{p} != {a}` → `DeniedNotAllowlisted` | row 3 |
| Non-member principal → `DeniedNotAllowlisted` | row 4 |
| Duplicates in allowlist → unchanged decision | row 7 |

### 5.2 `SendOnBehalfAuthorizerPropertyTests.cs` (CsCheck, T1 obligation)

- **Case-invariance**: random casing of principal and allowlist entries never changes the decision.
- **Deny-completeness**: for generated allowlists that exclude the principal (and principal != assistant), the decision is always `DeniedNotAllowlisted`.
- **Membership soundness**: inserting the principal (any casing/padding) into any generated allowlist yields `AllowedOnBehalf`.
- **Self-send dominance**: when principal equals assistant (any casing), the decision is `AllowedSelf` for every generated allowlist, including lists containing or excluding the principal.

### 5.3 `GraphHostAdapterClientSendMailTests.cs` (extend; authorization contract at the HTTP seam)

| Test | Assertion |
|---|---|
| Allowlisted principal, `{p} != {a}` | POST reaches `users/{a}/sendMail`; body carries `from = {p}`; 202 → `ok: true, data: null` (existing D7 assertions preserved with the allowlist configured) |
| Non-allowlisted principal | `Ok == false`; `Error.Code == "UNAUTHORIZED"`; `Error.BridgeErrorCode == "SendOnBehalfDenied"`; `Error.Retryable == false`; `Meta.RequestId`/`AdapterVersion` correct; **`FakeHttpHandler` invoked zero times and the `MockBehavior.Strict` `IAppTokenProvider` mock has no setups and is never called** (proves rejection before any token or HTTP I/O) |
| Empty allowlist, `{p} != {a}` | Same deny contract (fail-closed default) |
| Self-send with empty allowlist | Succeeds; no `from` in body (existing `SendMail_PrincipalEqualsAssistant_OmitsFrom` continues to pass unmodified — regression guard) |
| Case-differing allowlist entry | Send proceeds (normalization holds at the composed layer) |
| Deny message content | Names the configuration key; does not contain the principal UPN |

Existing D5 error-mapping and throttling tests must pass unchanged with an allowlisted configuration, proving the gate composes transparently with the retry pipeline.

### 5.4 `GraphAdapterOptionsValidatorTests.cs` (extend)

- Enabled + whitespace-only entry → violation naming `AllowedPrincipalMailboxUpns`.
- Enabled + empty list → valid.
- Disabled + malformed entries → valid (Enabled-only rule parity).
- Property test (CsCheck): lists of non-whitespace entries never produce the new violation; any list containing a whitespace-only entry always does.

### 5.5 `GraphServiceCollectionExtensionsTests.cs` (extend)

- Indexed configuration keys (`OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:0`, `:1`) bind to the collection (in-memory `ConfigurationBuilder`, consistent with existing binding tests).
- Startup validation failure surfaces when an entry is whitespace-only (`ValidateOnStart` path).

### 5.6 Architecture and parity

- `CloudGraphArchitectureBoundaryTests` automatically covers the new type via namespace prefix (no MailBridge-non-Contracts dependency, no COM interop, Agent partition still independent of CloudGraph). No new rules needed; run to confirm.
- `CloudGraphContractParityTests` must remain green — the `IHostAdapterClient` surface is unchanged.

---

## 6. Automation Feasibility

### Fully automatable in this environment (code side)

Everything in sections 2-5 is host-neutral C# gated by mocked seams and executes deterministically in CI with no credentials:

- The pure authorizer, options binding, validator rules, and the per-send gate — unit, property, and handler-level contract tests against `FakeHttpHandler` and a strict-mocked `IAppTokenProvider`.
- The decisive authorization contract — "allowed principal sends; disallowed principal is rejected before any HTTP call or token acquisition" — is provable entirely with mocks, because the deny path by design performs zero I/O.
- Architecture-boundary tests, coverage, mutation testing, and the full seven-stage toolchain loop.

### Not automatable in this environment (tenant side)

No Azure/Exchange credentials exist in this environment or CI (epic manifest Non-goals, `epic-plan.md` lines 93-96). The following are tenant configuration and tenant-rendered behavior, not code, and cannot be executed or verified here:

1. **The Exchange Send-on-behalf grant itself** — `Set-Mailbox -Identity <principal> -GrantSendOnBehalfTo @{Add=<assistant>}` plus the `Add-MailboxPermission` FullAccess grant (master §5.2 / Step 5). The code-side allowlist authorizes the app to *attempt* representation; Exchange enforces the grant at submit time. Both layers must agree. The grant procedure is already covered by F11's runbook (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, Send-on-behalf step) and was recorded as F11 requirement HI-1 (`response: "exception"`).
2. **End-to-end acceptance of an on-behalf submit by Graph** — a live `POST /users/{a}/sendMail` with `from = {p}` returning 202 (vs. `ErrorSendAsDenied`) requires a real tenant, app registration, and the RBAC scope from F11/F12.
3. **The rendered "Assistant on behalf of Executive" appearance** in Outlook and OWA — master §5.3 line 407 explicitly requires per-tenant validation before broad rollout; the F13 spec (line 237) records this as validated by F15/F17.
4. **Confirming Send As is absent** for the principal/assistant pair (master §5.2: if Send As is also present, Exchange can prefer the less transparent outcome) — a tenant-state inspection.

### Recommended split (per the F11 HI-1 precedent)

- **(a) Ship as code + mocked contract tests**: the entire allowlist gate (section 5 matrix). This is the deliverable of F15's implementation phases.
- **(b) Ship as a human runbook + `human_interaction` exception**: a new runbook at `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md` covering items 1-4 above — cross-referencing (not duplicating) the F11 RBAC runbook for the grant procedure, and adding the F15-specific validation steps: configure `AllowedPrincipalMailboxUpns` to mirror the tenant's `GrantSendOnBehalfTo` documentation, perform one allowed and one deliberately non-allowlisted send, verify the deny envelope (`UNAUTHORIZED` / `SendOnBehalfDenied`) for the latter and the rendered on-behalf appearance for the former. Record it in the child orchestrator checkpoint as a `human_interaction` requirement with `response: "exception"` and that `runbook_path`, exactly following the HI-1 record shape quoted in F11's `evidence/other/human-interaction-record.2026-07-02T18-47.md` and the invariants in `.claude/rules/orchestrator-state.md` (an `exception` requires a non-empty `runbook_path`).

### The precise code/tenant boundary

The code-side allowlist is an application-level policy gate evaluated deterministically before any network activity; the tenant-side `GrantSendOnBehalfTo` is Exchange's enforcement at submission time. They are independent controls that must both pass: the code gate is fully mockable and is F15's automatable scope; the tenant grant and rendered appearance are configuration state that only a human with tenant credentials can apply and observe.

---

## 7. Testing-Policy Notes for the Planner

- Follow the repository-actual MSTest stack, not the csharp.md xUnit wording (known rule-vs-repo mismatch, recorded in the F13 spec line 233).
- Test files go under `tests/OpenClaw.Core.Tests/CloudGraph/` mirroring production structure; no colocation.
- No `Task.Delay`/wall-clock in tests; the existing throttling test's `FakeTimeProvider.Advance` loop is the established pattern if any retry-composition test is added.
- The deny path has no time dependency, so most new tests need no clock at all.

## 8. References

- `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs` (D7 send path; gate insertion point)
- `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs`, `GraphAdapterOptionsValidator.cs`, `GraphServiceCollectionExtensions.cs` (options/validation/DI precedent)
- `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs` (error vocabulary, token-before-request ordering)
- `src/OpenClaw.HostAdapter.Contracts/ApiError.cs`, `ApiEnvelope.cs`, `ApiMeta.cs` (envelope contract)
- `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (runtime consumer of the send envelope)
- `docs/open-claw-approach.master.md` §4.4, §5.1-5.3, Step 5, Step 8 (allowlist mandate, fail-closed doctrine, tenant validation)
- `docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md` (D5/D7, F15 deferral, test-stack note)
- `docs/features/active/2026-07-02-exchange-rbac-scripts-111/` (tenant-runbook + HI-1 exception precedent)
- `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs`, `CloudGraphArchitectureBoundaryTests.cs` (test patterns, boundary rules)
- `quality-tiers.yml`, `.claude/rules/quality-tiers.md` (T1 classification and gates)
- `docs/features/epics/openclaw-vision/epic-plan.md` (F15 entry, C3 band, automation-feasibility non-goal)
