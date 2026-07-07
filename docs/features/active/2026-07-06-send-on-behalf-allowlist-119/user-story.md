# `send-on-behalf-allowlist` — User Story

- Issue: #119
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-06

## Story Statement

- As the executive principal whose mailbox the assistant represents, I want the assistant mailbox to be able to send on behalf of only an explicitly configured list of principal mailboxes, so that a misconfiguration or compromised configuration cannot cause mail to be sent in my name — or anyone else's — without a deliberate allowlist entry.
- As the operator who deploys and configures the bridge, I want an on-behalf send with a non-allowlisted principal to be rejected with a clear, non-retryable error that names the configuration key, so that I can diagnose and fix the allowlist without reading code and without any mail having left the system.
- As a security reviewer, I want the deny decision to be made deterministically before any token acquisition or network call, and to be provable by tests, so that I can verify the fail-closed property from the test suite rather than from a live tenant.

## Problem / Why

The Graph-backed adapter (F13, issue #115) already injects `from = principal` on outbound sends whenever the configured principal mailbox differs from the assistant mailbox, but it performs no authorization check: any principal UPN placed in configuration is silently represented. The master vision (`docs/open-claw-approach.master.md` §5.2 and Step 8) requires an explicit allowlist of principal mailboxes the assistant may represent, and F13 design decision D7 explicitly deferred that allowlist to this feature. Until it lands, the code-side half of the two-layer send-on-behalf control (application allowlist + tenant `GrantSendOnBehalfTo` grant) is missing.

## Personas & Scenarios

- **Persona: Executive principal (mailbox owner).**
  - Who: the person whose identity appears in the `from` line of assistant-sent mail.
  - Cares about: no mail ever leaving in their name without explicit, auditable authorization on both the application and tenant layers.
  - Constraints: does not read configuration or code; relies on the operator and the tenant admin.
  - Goal: representation is possible only when both the allowlist and the Exchange grant name their mailbox.

- **Persona: Operator (deploys and configures the bridge).**
  - Who: the person who sets `OpenClaw:GraphAdapter:*` configuration and runs the tenant runbooks.
  - Cares about: unambiguous failure modes, error messages that name the key to fix, and configuration that binds through standard indexed environment variables.
  - Constraints: no direct access to code internals; works from envelopes, logs, and runbooks.
  - Goal: keep `AllowedPrincipalMailboxUpns` reconciled with the tenant's documented `GrantSendOnBehalfTo` grants.

- **Persona: Security reviewer.**
  - Who: reviews the send path for fail-closed behavior before rollout.
  - Cares about: deny-before-I/O, no UPNs leaking into error messages or logs, and property-level test evidence for the pure decision function.
  - Goal: sign off on the allowlist gate from repository evidence alone.

- **Scenario 1: Allowlisted on-behalf send.** The scheduling runtime submits an outbound message. Configuration names `principal@contoso.com` as the principal, `assistant@contoso.com` as the assistant, and the allowlist contains `principal@contoso.com`. The gate returns `AllowedOnBehalf`; the request posts to `users/{assistant}/sendMail` with `from = principal@contoso.com`; Graph returns 202; the caller receives `ok: true`. Behavior is byte-identical to F13 D7, now with authorization proven rather than assumed.

- **Scenario 2: Non-allowlisted principal (fail-closed deny).** The operator changes `PrincipalMailboxUpn` to a new executive but forgets to extend the allowlist. The next send is denied before any token acquisition or HTTP call: the caller receives a failure envelope with code `UNAUTHORIZED`, bridge error code `SendOnBehalfDenied`, `retryable: false`, and a message naming `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` without echoing any UPN. One warning log line carries the request id. The operator adds the allowlist entry (and confirms the tenant grant per the runbook); the next send proceeds.

- **Scenario 3: Fresh deployment with no allowlist configured.** A deployment enables the Graph adapter with distinct principal and assistant mailboxes and no `AllowedPrincipalMailboxUpns` key. Every on-behalf send is denied (empty allowlist = deny all on-behalf, by design). Self-sends (`principal == assistant`) are unaffected because no representation occurs.

- **Scenario 4: Startup misconfiguration.** The operator sets an allowlist entry to an empty string via an indexed environment variable. Startup validation (`ValidateOnStart`) fails fast with a violation naming `AllowedPrincipalMailboxUpns`; the malformed entry never reaches the runtime gate.

- **Scenario 5: Tenant validation (human runbook).** Before rollout, a human with tenant credentials follows `runbooks/send-on-behalf-validation.runbook.md`: confirms the Exchange `GrantSendOnBehalfTo` grant (via the F11 runbook), reconciles the allowlist with the documented grants, performs one allowed and one deliberately non-allowlisted send, verifies the deny envelope for the latter and the rendered "Assistant on behalf of Executive" appearance for the former in Outlook and OWA. This step is tenant configuration, not code, and is recorded as a `human_interaction` exception.

## Acceptance Criteria

- [x] Given a Graph-enabled configuration whose principal differs from the assistant and whose allowlist contains the principal (exact, case-differing, or whitespace-padded entry), when `SendMailAsync` runs, then the message posts to `users/{assistant}/sendMail` with `from = principal` and a 202 maps to a success envelope — verified by handler-seam tests in `GraphHostAdapterClientSendMailTests.cs`.
- [x] Given a principal that differs from the assistant and is not on the allowlist (including the empty/absent-allowlist default), when `SendMailAsync` runs, then the caller receives `ok: false` with `Error.Code == "UNAUTHORIZED"`, `Error.BridgeErrorCode == "SendOnBehalfDenied"`, `Error.Retryable == false`, and zero I/O occurred: the mocked `HttpMessageHandler` is invoked zero times and the strict `IAppTokenProvider` mock is never called — verified by the decisive contract test in `GraphHostAdapterClientSendMailTests.cs`.
- [x] Given any allowlist contents, when the principal equals the assistant (case-insensitive), then the send proceeds with no `from` injected — the existing self-send test passes unmodified, and CsCheck self-send-dominance and case-invariance properties in `SendOnBehalfAuthorizerPropertyTests.cs` hold for generated inputs.
- [x] The deny error message names the configuration key `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` and contains no mailbox UPN; the deny warning log carries the request id only — verified by message-content assertions in `GraphHostAdapterClientSendMailTests.cs`.
- [x] The operator can configure the allowlist through standard indexed keys (`OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, ...); binding is verified in `GraphServiceCollectionExtensionsTests.cs`, and a whitespace-only entry fails startup validation with a violation naming the key — verified in `GraphAdapterOptionsValidatorTests.cs` and the `ValidateOnStart` test.
- [x] The authorization decision and the `from`-injection predicate share a single source (`SendOnBehalfAuthorizer.Authorize`), covered by unit tests for all decision-table rows and by the four CsCheck properties (case-invariance, deny-completeness, membership soundness, self-send dominance) satisfying the T1 property-test obligation.
- [x] Tenant-side validation (Exchange grant, live on-behalf acceptance, rendered on-behalf appearance, Send As absence check) is documented in `runbooks/send-on-behalf-validation.runbook.md` and recorded as a `human_interaction` requirement with `response: "exception"` — not claimed as automated verification.

## Non-Goals

- Modifying tenant state: the Exchange `GrantSendOnBehalfTo` grant and mailbox permissions remain tenant configuration, applied by a human via the F11/F15 runbooks; no code in this feature calls Exchange management endpoints.
- Live-tenant or end-to-end Graph verification in tests or CI (no credentials exist in this environment; deferred to the runbook and later F17).
- Validating caller-supplied `from` addresses: the wire contract carries no `from` field by design (F13 D7, master Step 8); the gate validates configuration only.
- Changing the local (non-Graph) adapter, read routes, `GetStatusAsync`, `IHostAdapterClient`, or any public contract type.
- A new top-level `ApiError` code: the denial reuses `UNAUTHORIZED` with the `SendOnBehalfDenied` bridge-error-code discriminator.
- Startup-time allowlist-membership validation: denial is a per-send runtime decision so read routes stay available under a send-policy misconfiguration.
