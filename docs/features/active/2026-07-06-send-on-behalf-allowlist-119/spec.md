# send-on-behalf-allowlist — Spec

- **Issue:** #119
- **Parent (optional):** epic `docs/features/epics/openclaw-vision/epic-plan.md` (F15)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-06
- **Status:** Ready
- **Version:** 1.0

## Overview

The Graph-backed adapter (F13, issue #115, merged as PR #116) submits outbound mail through the assistant mailbox and injects `message.from.emailAddress.address = {p}` whenever the configured principal differs from the assistant (`docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md`, design decision D7). D7 explicitly defers "full send-on-behalf semantics including the recipient allowlist" to this feature. Today there is no authorization check of any kind on that path: any configured `PrincipalMailboxUpn` that differs from `AssistantMailboxUpn` is silently represented (verified in `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`, `ComposeSendMailBody`).

The master vision requires closing this gap: "Maintain an explicit allowlist of principal mailboxes that the assistant mailbox is allowed to represent" (`docs/open-claw-approach.master.md` §5.2), reinforced by the Step 8 developer safeguard "Hard-code an allowlist of mailbox identities the app may send from". The second Step 8 safeguard — no caller-supplied `from` — is already satisfied because the wire contract carries no `from` field; the identity is exclusively configuration-supplied.

This feature adds a deterministic, fail-closed, per-send authorization gate for principal representation, composed of four additive pieces in `OpenClaw.Core.CloudGraph`: a new options key, a pure authorizer, a gate at the top of `SendMailAsync`, and a shape-only validator rule. No public contract changes; `IHostAdapterClient`, `ApiError`, `ApiEnvelope`, and the wire DTOs are untouched.

## Behavior

### Decision semantics

All UPN comparisons use `StringComparison.OrdinalIgnoreCase` on `Trim()`ed values, matching the existing D7 comparison. `{p}` = `GraphAdapterOptions.PrincipalMailboxUpn`, `{a}` = `GraphAdapterOptions.AssistantMailboxUpn`. All rows assume the adapter is `Enabled`.

| # | Condition | Decision | Effect |
|---|---|---|---|
| 1 | `{p} == {a}` (case-insensitive) | `AllowedSelf` | Send proceeds; no `from` injected (unchanged D7 behavior); allowlist irrelevant |
| 2 | `{p} != {a}`, allowlist contains `{p}` (case-insensitive, trimmed) | `AllowedOnBehalf` | Send proceeds; `from = {p}` injected (unchanged D7 body shape) |
| 3 | `{p} != {a}`, allowlist empty or absent | `DeniedNotAllowlisted` | Fail-closed deny envelope; no token acquisition, no HTTP |
| 4 | `{p} != {a}`, allowlist non-empty, `{p}` absent | `DeniedNotAllowlisted` | Same deny envelope |
| 5 | Allowlist entry differs from `{p}` only by case or surrounding whitespace | membership | Treated as a match (deterministic normalization) |
| 6 | Whitespace-only allowlist entry | n/a | Rejected at startup by the options validator (shape rule); never reaches the authorizer |
| 7 | Duplicate allowlist entries | membership | Harmless; set semantics |

### Gate placement and zero-I/O deny

`GraphRequestExecutor.ExecuteAsync` acquires a token from `IAppTokenProvider` **before** invoking the request factory (verified in `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs`). The authorization gate therefore runs at the top of `SendMailAsync`, before `executor.ExecuteAsync` is called. On `DeniedNotAllowlisted` the method returns a synchronous failure envelope (`Task.FromResult`) — no token acquisition and no HTTP request occur. The existing `ResolveRequestId` helper supplies the `ApiMeta` request id exactly as the executor would have.

### Single decision source

The authorization decision replaces the inline `principalIsAssistant` comparison in `ComposeSendMailBody`: `from` is injected if and only if the decision is `AllowedOnBehalf`. The from-injection predicate and the authorization predicate share one source (`SendOnBehalfAuthorizer.Authorize`) and cannot diverge.

### Error surface

A denied on-behalf send returns:

```json
{
  "ok": false,
  "data": null,
  "meta": { "requestId": "<resolved>", "adapterVersion": "cloudgraph", "bridge": null },
  "error": {
    "code": "UNAUTHORIZED",
    "message": "The configured principal mailbox is not on the send-on-behalf allowlist (OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns); the send was rejected before any request was made.",
    "bridgeErrorCode": "SendOnBehalfDenied",
    "retryable": false
  }
}
```

- `ApiError.Code` reuses `UNAUTHORIZED` — the denial is semantically the same category the D5 matrix assigns to Graph 401/403, and the code vocabulary stays closed.
- `BridgeErrorCode = "SendOnBehalfDenied"` is the stable discriminator distinguishing the local policy denial from a remote 403 (where `BridgeErrorCode` carries the Graph `error.code`, for example `ErrorSendAsDenied`).
- `Retryable = false`: retrying without a configuration change cannot succeed.
- The message names the offending configuration key and never echoes UPNs (existing validator message convention).
- The deny path emits one `LogWarning` carrying the request id only — no UPNs, tokens, or bodies in log templates (executor precedent).

### Untouched surfaces

Read routes, `GetStatusAsync`, and the local (non-Graph) adapter are unchanged. The local adapter path performs no `from` injection at all, so no parity change is required; this is an intentional, Graph-backend-only failure mode. `SendMailAsync`'s signature and envelope type are unchanged, so contract parity holds.

## Design Decisions

- **D1 — Additive options key.** `GraphAdapterOptions.AllowedPrincipalMailboxUpns`, a string collection bound from `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` (indexed env form `OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, `__1`, ...). Default: empty collection. An empty or absent allowlist denies all on-behalf sends (fail-closed); self-send is unaffected because no representation occurs. A semicolon-delimited scalar key was rejected: the .NET configuration binder natively binds indexed children to collections, and a delimited scalar would add bespoke parsing for no benefit.
- **D2 — Pure authorizer.** New file `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`: an `internal static` class (tests reach it via the existing `InternalsVisibleTo`) with one pure function `Authorize(string principalMailboxUpn, string assistantMailboxUpn, IEnumerable<string> allowedPrincipalMailboxUpns)` returning the enum `SendAuthorizationDecision { AllowedSelf, AllowedOnBehalf, DeniedNotAllowlisted }`. No I/O, no clock, no logging — the same purity contract `GraphAdapterOptionsValidator` documents.
- **D3 — Runtime gate, not startup membership validation.** A validator rule requiring `{p}` ∈ allowlist when `{p} != {a}` was rejected: it would fail the whole adapter at boot — including read routes — for a send-policy misconfiguration, produce no per-send deny envelope, and turn the runtime gate into an untestable dead branch. The validator addition is shape-only.
- **D4 — Error code reuse.** `UNAUTHORIZED` + `BridgeErrorCode = "SendOnBehalfDenied"` rather than a new top-level code (`SEND_ON_BEHALF_DENIED` would be additive and mechanically safe, but grows the cross-adapter vocabulary for no functional gain). The only runtime consumer of the send envelope, `HostAdapterSchedulingService`, interpolates `Code`/`Message` into an exception string and switches on no closed enum (verified), so reuse is safe.
- **D5 — Gate layer.** The gate lives in `SendMailAsync`, not `GraphRequestExecutor`. The executor is transport-generic and knows nothing about send semantics.
- **D6 — Configuration-validated, not request-validated.** The gate validates configuration state. The wire contract intentionally carries no `from` field (F13 D7), and master Step 8 prohibits caller-supplied `from` mailboxes; there is no request input to validate.
- **D7 — Tenant-side controls are out of code scope.** The code-side allowlist authorizes the application to *attempt* representation; Exchange independently enforces the `GrantSendOnBehalfTo` grant at submit time. Both layers must agree. Tenant configuration and rendered appearance are covered by a human runbook (see Constraints & Risks), following the F11 HI-1 precedent.

## Inputs / Outputs

- **Inputs:** configuration only; no CLI flags or files.
- **Outputs:** unchanged HTTP behavior for allowed sends; a locally synthesized deny envelope plus one `warning` log line for denied sends.
- **New config key** (section `OpenClaw:GraphAdapter`):

| Key | Default | Meaning |
|---|---|---|
| `AllowedPrincipalMailboxUpns` | *(empty collection)* | Principal UPNs the assistant mailbox may represent on send. Empty/absent = deny all on-behalf sends (fail-closed). Entries must be non-whitespace when `Enabled`. Membership comparison is trimmed, `OrdinalIgnoreCase`. Env form: `OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, `__1`, ... |

- **Validator addition** (`GraphAdapterOptionsValidator`, applies only when `Enabled`, full-violation-list style): every `AllowedPrincipalMailboxUpns` entry must be non-whitespace. An empty list is valid configuration (it expresses deny-all-on-behalf). Messages name the key and never echo configured values.
- **Versioning / backward compatibility:** additive only. Existing deployments with no `AllowedPrincipalMailboxUpns` key and `{p} == {a}` (self-send) behave identically. Existing deployments with `{p} != {a}` change behavior from silent representation to denial until the allowlist is configured — this is the intended fail-closed hardening and must be called out in the PR description.

## API / CLI Surface

No new HTTP routes, CLI commands, or public contract types. The deny envelope (Behavior section) is the only new observable wire-level shape, synthesized locally with the same `ApiMeta` composition (`requestId`, `"cloudgraph"`, `null`) as every other adapter failure.

Contracts and validation rules:

- Allowed on-behalf send: request shape identical to F13 D7 (`POST users/{a}/sendMail`, `from = {p}` in the body); Graph 202 -> `ok: true, data: null`.
- Self-send: request shape identical to F13 D7 (no `from` in the body), regardless of allowlist contents.
- Denied send: the envelope above; zero outbound HTTP; zero token acquisition.

## Data & State

No storage, persistence, or migration. The authorizer is a pure static function: UPN strings and an allowlist in, a three-value decision out; no mutation, no I/O, no clock.

Invariants:

- Decision totality: every input maps to exactly one of `AllowedSelf`, `AllowedOnBehalf`, `DeniedNotAllowlisted`.
- Normalization determinism: trimming and `OrdinalIgnoreCase` comparison make casing and surrounding whitespace irrelevant to the decision.
- Self-send dominance: `{p} == {a}` yields `AllowedSelf` for every allowlist, including lists containing or excluding `{p}`.
- Fail-closed default: the empty allowlist yields `DeniedNotAllowlisted` for every `{p} != {a}`.
- Single source: `ComposeSendMailBody` consumes the decision; it does not re-derive the principal/assistant comparison.

## Constraints & Risks

- **Tier T1 obligations bind this feature.** `OpenClaw.Core` is T1 in `quality-tiers.yml`, and the feature is inside the T1 "auth/token handling" example set (`.claude/rules/quality-tiers.md`). Line coverage >= 85%, branch coverage >= 75%, no regression on changed lines; >= 1 CsCheck property test per pure function; mutation score >= 75% (Stryker.NET, pre-merge/nightly pipeline, not the per-commit loop); zero untyped escape hatches; zero architecture violations.
- **Test stack is repository-actual:** MSTest + FluentAssertions + Moq (`MockBehavior.Strict` token provider) + CsCheck + `FakeTimeProvider` + the repo `FakeHttpHandler` — not the xUnit/NSubstitute wording in `.claude/rules/csharp.md` (known rule-vs-repo mismatch, recorded in the F13 spec). No live Graph calls in any test; no temporary files; test files under `tests/OpenClaw.Core.Tests/CloudGraph/` mirroring production structure.
- **Tenant configuration is not code and is not automatable here.** No Azure/Exchange credentials exist in this environment or CI (epic non-goal). The following are out of scope for automated verification and are covered by a human runbook at `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md`, recorded in the orchestrator checkpoint as a `human_interaction` requirement with `response: "exception"` and that `runbook_path` (F11 HI-1 precedent; `.claude/rules/orchestrator-state.md` invariants):
  1. The Exchange `Set-Mailbox -GrantSendOnBehalfTo` grant itself (procedure already in the F11 runbook `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`; cross-reference, do not duplicate).
  2. End-to-end acceptance of a live on-behalf submit by Graph (202 vs. `ErrorSendAsDenied`).
  3. The rendered "Assistant on behalf of Executive" appearance in Outlook and OWA (master §5.3 requires per-tenant validation before broad rollout; also validated later by F17).
  4. Confirming Send As is absent for the principal/assistant pair (tenant-state inspection).
- **Two independent controls must agree.** Operators must keep `AllowedPrincipalMailboxUpns` mirrored with the tenant's documented `GrantSendOnBehalfTo` grants; the runbook includes this reconciliation step. Divergence produces either a local deny (allowlist missing an entry the tenant grants) or a remote `ErrorSendAsDenied` (allowlist contains an entry the tenant does not grant) — both fail closed, neither sends silently.
- **Behavior change for misconfigured deployments** (see Versioning above) is deliberate and is the feature's purpose.
- **500-line cap:** all touched files remain under 500 lines; `SendOnBehalfAuthorizer.cs` is small by design.

## Implementation Strategy

- **Scope:** additive changes in `OpenClaw.Core.CloudGraph` plus tests and one runbook. No changes to `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.*`, `OpenClaw.Core.Agent.*` production code, `HostAdapterHttpClient`, or DI registration shape.
- **New file:** `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs` — `SendAuthorizationDecision` enum + pure `Authorize` function (D2).
- **Changed files:**
  - `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs` — additive `AllowedPrincipalMailboxUpns` collection property (get-only initialized list works with the binder and analyzer stack); XML doc records the env-binding pattern and fail-closed-empty semantics.
  - `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs` — non-whitespace-entries rule (Enabled-only, full-violation-list style).
  - `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs` — gate before `executor.ExecuteAsync`; deny envelope + warning log; `ComposeSendMailBody` consumes the decision instead of re-deriving `principalIsAssistant`.
- **New runbook:** `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md` (Constraints & Risks items 1–4).
- **New/extended tests** (under `tests/OpenClaw.Core.Tests/CloudGraph/`): `SendOnBehalfAuthorizerTests.cs` (unit), `SendOnBehalfAuthorizerPropertyTests.cs` (CsCheck), extensions to `GraphHostAdapterClientSendMailTests.cs`, `GraphAdapterOptionsValidatorTests.cs`, and `GraphServiceCollectionExtensionsTests.cs`.
- **Dependency changes:** none.
- **Logging:** one `LogWarning` on deny, request id only; no UPNs, tokens, or bodies.
- **Rollout:** inert until `OpenClaw:GraphAdapter:Enabled` is `true` (the Graph backend remains dark by default per F13 D8). Fallback is configuration-only: populate the allowlist or revert to self-send. `quality-tiers.yml` is unchanged (`OpenClaw.Core` is already classified).

## Acceptance Criteria

- [x] `GraphAdapterOptions.AllowedPrincipalMailboxUpns` exists as an additive string-collection property defaulting to an empty collection; indexed configuration keys (`OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:0`, `:1`) bind to it via an in-memory `ConfigurationBuilder` test in `GraphServiceCollectionExtensionsTests.cs`, and a whitespace-only entry fails the `ValidateOnStart` startup-validation path.
- [x] `SendOnBehalfAuthorizer.Authorize(principal, assistant, allowlist)` is a pure static function returning `AllowedSelf` | `AllowedOnBehalf` | `DeniedNotAllowlisted` with trimmed, `OrdinalIgnoreCase` comparison; unit tests in `SendOnBehalfAuthorizerTests.cs` cover all seven decision-table rows (self-send equal and case-differing, allowlisted member, case/whitespace-differing member, empty allowlist, non-member, duplicates).
- [x] CsCheck property tests in `SendOnBehalfAuthorizerPropertyTests.cs` establish: case-invariance (random casing of principal and entries never changes the decision), deny-completeness (allowlists excluding the principal with `{p} != {a}` always yield `DeniedNotAllowlisted`), membership soundness (inserting the principal in any casing/padding yields `AllowedOnBehalf`), and self-send dominance (`{p} == {a}` yields `AllowedSelf` for every generated allowlist).
- [x] A disallowed principal (`{p} != {a}`, not allowlisted — including the empty-allowlist case) yields a failure envelope with `Error.Code == "UNAUTHORIZED"`, `Error.BridgeErrorCode == "SendOnBehalfDenied"`, `Error.Retryable == false`, correct `Meta.RequestId`/`AdapterVersion`, **and** the mocked `HttpMessageHandler` is invoked zero times with the `MockBehavior.Strict` `IAppTokenProvider` mock never called — proving the deny occurs before token acquisition and any HTTP I/O (contract test in `GraphHostAdapterClientSendMailTests.cs`).
- [x] The deny `ApiError.Message` names `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` and contains no principal or assistant UPN; the deny path logs one warning with the request id only (asserted in `GraphHostAdapterClientSendMailTests.cs`).
- [x] An allowlisted principal with `{p} != {a}` sends successfully: POST reaches `users/{a}/sendMail` with `from = {p}` in the body and 202 maps to `ok: true, data: null`; a case-differing allowlist entry also permits the send; and the existing self-send test (`SendMail_PrincipalEqualsAssistant_OmitsFrom`) passes unmodified with an empty allowlist, demonstrating the from-injection predicate and the authorization decision share one source.
- [x] `GraphAdapterOptionsValidator` rejects whitespace-only `AllowedPrincipalMailboxUpns` entries when `Enabled` (message names the key, echoes no values), accepts an empty list, and ignores malformed entries when disabled; covered by unit tests plus a CsCheck property test in `GraphAdapterOptionsValidatorTests.cs`.
- [x] Existing `CloudGraphArchitectureBoundaryTests` and `CloudGraphContractParityTests` pass unchanged (the new type falls under the namespace-prefix rules automatically; `IHostAdapterClient` surface is untouched), and the existing D5 error-mapping and throttling tests pass with an allowlisted configuration.
- [x] The full seven-stage toolchain passes in a single pass (CSharpier, analyzers, nullable, architecture tests, MSTest unit + CsCheck property tests, contract checks, integration tests); line coverage >= 85% and branch coverage >= 75% with changed lines covered; Stryker.NET mutation score >= 75% on the changed T1 surface in the pre-merge/nightly pipeline; every touched file <= 500 lines; no live Graph calls and no temporary files in any test.
- [x] The tenant-validation runbook exists at `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md`, cross-references the F11 RBAC runbook for the grant procedure, covers allowlist/tenant-grant reconciliation, one allowed and one deliberately non-allowlisted live send, deny-envelope verification, and rendered-appearance checks; the orchestrator checkpoint records it as a `human_interaction` requirement with `response: "exception"` and that `runbook_path`.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Authorizer decision-table rows 1–7 as unit tests; four CsCheck properties (case-invariance, deny-completeness, membership soundness, self-send dominance).
- [ ] Handler-seam contract: denied principal -> deny envelope with zero handler invocations and zero token-provider calls; allowed principal -> D7 body with `from`; self-send -> no `from`, allowlist irrelevant.
- [ ] Validator: whitespace-only entry violation (Enabled), empty-list validity, disabled-mode pass-through; CsCheck property over entry lists.
- [ ] Binding: indexed env-form keys bind to the collection; `ValidateOnStart` failure on malformed entry.
- [ ] Regression: existing D5 error-mapping, throttling, and self-send tests unchanged and green with an allowlisted configuration.
