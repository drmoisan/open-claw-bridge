# Negative-Scope Startup Validation — Human-Exception Runbook

- **Feature:** negative-scope-smoke-test (Issue #120, Epic openclaw-vision F17)
- **Runbook path:** `docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md`
- **Authored:** 2026-07-06
- **Source checklist:** `docs/open-claw-approach.master.md` section 13 Steps 2-3; feature spec `docs/features/active/2026-07-06-negative-scope-smoke-test-120/spec.md` (D5, D6, D7)
- **Precedent:** F11 HI-1 runbook `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`

## Cue

Act on this runbook when the orchestrator records live-tenant verification of the Exchange Online Application RBAC startup boundary as a permitted human exception: `artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]` entry (HI-1) with `response: "exception"` and `runbook_path` pointing to this file.

F17 ships the application-runtime scope-boundary check (`ScopeBoundaryStartupValidator`, a one-shot `IHostedService` that probes one in-scope and one out-of-scope mailbox at startup and asserts the authorization split) with mocked-Graph contract and property tests in the per-commit suite. The decision logic is exercised in CI against a fake HTTP handler. The live-tenant confirmation cannot run in CI: no Azure/Exchange credentials exist in this environment or in CI, no CI job may attempt a live Graph call, and Exchange Application RBAC permission changes are subject to a propagation cache of 30 minutes to 2 hours. A human Exchange/Graph administrator therefore performs the live confirmation described below. The trigger event is the readiness check for enabling the `OpenClaw:ScopeValidation` section against a real tenant before F17 is relied upon in a deployed environment.

## Prerequisites

All of the following must be true before starting.

1. **F11 RBAC provisioning complete (prerequisite; not performed here).** The app-only service principal RBAC scope must already be provisioned and its boundary confirmed by the F11 administrator procedure. Complete `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` first, including its Step 6 boundary check (`Test-OpenClawScopeBoundary` / `Test-ServicePrincipalAuthorization`) returning `Succeeded = $true`. At minimum the service principal must hold `Application Mail.Read` scoped to the in-scope mailbox set, because F17's probe issues a messages read.
2. **Roles.** The executing administrator holds the roles required to review the RBAC configuration if a correction is needed: **Organization Management** in Exchange Online and **Exchange Administrator** in Microsoft Entra ID. These are the same roles used by the F11 runbook.
3. **Two known test mailboxes (master section 13 Step 1).** One mailbox that is inside the RBAC scope (`IN_SCOPE_TEST_MAILBOX`) and one mailbox that is outside the scope (`OUT_OF_SCOPE_TEST_MAILBOX`). The out-of-scope mailbox must exist in the tenant: a non-existent mailbox returns Graph `404 / ErrorItemNotFound`, which the evaluator classifies as a wrong-error failure, not a denial.
4. **App-only credential configured, not embedded (master section 13 Step 2).** The application's `OpenClaw:CloudAuth` settings supply the tenant ID, client (application) ID, and an app-only credential for the client-credentials token flow. Treat the app identity as a production credential: keep it in Azure Key Vault or equivalent, never bake secrets into code or container images, and prefer a certificate credential over a client secret.
5. **Graph adapter enabled.** `OpenClaw:GraphAdapter:Enabled` is `true` and `OpenClaw:GraphAdapter` is configured with the Graph base URL and the principal mailbox settings the F13 adapter already uses. F17 rejects composition at startup if scope validation is enabled while the Graph adapter is disabled (spec D6).
6. **Host and access.** A build of the `OpenClaw.Core` host that includes F17, the ability to start it in an environment that can reach the tenant, and access to its structured log output (console or the configured log sink).

## Step-by-step Instructions

**All steps below are configuration and host-start steps. No third-party UI navigation is strictly required for the validation run itself.** App registration and RBAC scope provisioning — the only tasks that involve the Microsoft Entra admin center or Exchange PowerShell — are referenced prerequisites handled by the F11 runbook (Prerequisites items 1-2), not steps in this runbook.

Replace every placeholder (mailbox UPNs, tenant and client IDs, credential references) with real tenant values.

### Step 1 — Configure the app-only authentication and Graph settings

Confirm `OpenClaw:CloudAuth` and `OpenClaw:GraphAdapter` are populated so the host can acquire an app-only Microsoft Graph token via the OAuth 2.0 client-credentials grant and issue scoped reads. Supply the tenant ID, client (application) ID, and the certificate reference (preferred) or client secret through the configured secret source (Key Vault or environment), not through checked-in files. Do not place the credential value in `appsettings.json` or a container image.

### Step 2 — Configure the `OpenClaw:ScopeValidation` section

Set the new section keys (spec D6). Environment-variable form:

```text
OpenClaw__GraphAdapter__Enabled=true
OpenClaw__ScopeValidation__Enabled=true
OpenClaw__ScopeValidation__InScopeTestMailboxUpn=in-scope-user@contoso.com
OpenClaw__ScopeValidation__OutOfScopeTestMailboxUpn=out-of-scope-user@contoso.com
```

Rules enforced at startup (`ValidateOnStart`):

- `Enabled` defaults to `false`; the validator registers nothing unless it is set to `true`.
- When enabled, both UPNs are required, non-whitespace, and must differ (compared case-insensitively). A missing or equal pair fails startup with an `OptionsValidationException`.
- Enabling scope validation while `OpenClaw:GraphAdapter:Enabled` is `false` throws `InvalidOperationException` at composition time. The probe is Graph-only.

### Step 3 — Start the host and run the positive (pass) validation

Start the `OpenClaw.Core` host. The one-shot `ScopeBoundaryStartupValidator` runs during `StartAsync`, before the server accepts requests. It performs two probe reads in deterministic order — the in-scope mailbox first, then the out-of-scope mailbox — each a harmless `GET users/{mailbox}/messages?$top=1&$select=id`. Both probes always execute so the log carries both sides.

Observe the structured startup log entry:

- **Pass:** a single `Information`-level entry with `Succeeded = true`; the host continues starting.
- **Fail:** a single `Critical`-level entry carrying `FailureReason` and both outcome summaries (`Ok` / `ErrorCode` / `BridgeErrorCode` only — tokens and response bodies are never logged), followed by an `InvalidOperationException` that aborts host startup.

If the in-scope probe is denied shortly after F11 provisioning, this is most likely the RBAC propagation cache (30 minutes to 2 hours), not a real misconfiguration. Wait and restart the host to re-run the validation. See Step 5.

### Step 4 — Rehearse the negative (denial) path

Confirm the boundary genuinely holds by observing a real out-of-scope denial rather than assuming it. With a correctly provisioned scope, the configured out-of-scope mailbox must be denied by Exchange Application RBAC with Graph `error.code == "ErrorAccessDenied"` (HTTP 403), which F17 maps to the envelope shape `UNAUTHORIZED` + `ErrorAccessDenied`. This is the only failure shape the evaluator accepts as a denial (spec D3).

To rehearse the denial deliberately, point `OutOfScopeTestMailboxUpn` at a mailbox you know is outside the scope and confirm the validator reports `OutOfScopeDenied = true` on the pass run (Step 3). If instead the out-of-scope read succeeds, the RBAC scope does not hold (a scope leak); the log reports `out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold`, and the correction is on the Exchange RBAC side — return to the F11 runbook.

### Step 5 — Handle transient and propagation-related failures

The validator is fail-closed: any ambiguous or transient failure produces a failed validation and aborts startup rather than a false pass. If the log reports a transient class (`THROTTLED`, `TRANSPORT_FAILURE`) or an in-scope denial immediately after provisioning:

1. Wait for RBAC propagation. Application RBAC permission changes are cached for 30 minutes to 2 hours depending on recent app usage. The F11 `Test-ServicePrincipalAuthorization` path bypasses this cache and is the fastest independent confirmation that the assignment exists; the running application does not bypass it.
2. Restart the host to re-run the startup validation.
3. If the in-scope side keeps failing after the propagation window, re-verify the F11 role assignment and the in-scope mailbox's direct group membership, correct on the Exchange side, then restart.

### Step 6 — Confirm and record the observed denial code

Read the observed Graph error code for the out-of-scope probe from the structured log (`BridgeErrorCode`). The expected value is `ErrorAccessDenied`. If a live tenant surfaces a different `error.code` for the RBAC denial, validation fails closed with the observed code quoted in `FailureReason`. Record the observed code and report it: the code fix is a single named constant (`ScopeBoundaryEvaluator.ExpectedDenialGraphCode`) plus its pinned tests (spec D3, Constraints & Risks). Do not weaken the classifier in the field; report the observed code for a code-side correction.

## Verification

Confirm success by the observable log outputs below.

1. **Configuration accepted (Steps 1-2):** the host does not throw at composition time (Graph adapter enabled) and does not throw an `OptionsValidationException` at startup (both UPNs present, non-whitespace, distinct).
2. **Boundary holds (Step 3):** exactly one `Information`-level startup entry with `Succeeded = true`, `InScopeAllowed = true`, and `OutOfScopeDenied = true`; the in-scope outcome shows `Ok = true`; the out-of-scope outcome shows `Ok = false` with `ErrorCode = UNAUTHORIZED` and `BridgeErrorCode = ErrorAccessDenied`; the host completes startup. This is the correct pass.
3. **On failure (Step 3):** one `Critical`-level entry with `Succeeded = false` and a `FailureReason` naming the failing side(s), and the host startup aborts. Diagnose by `FailureReason`:
   - `out-of-scope mailbox read unexpectedly succeeded ...` — scope leak; correct on the Exchange RBAC side (F11 runbook).
   - `out-of-scope mailbox read failed but not with the expected authorization denial (expected UNAUTHORIZED/ErrorAccessDenied; observed ...)` — the out-of-scope mailbox may not exist (`NOT_FOUND` / `ErrorItemNotFound`); use an existing out-of-scope mailbox, or record the observed code per Step 6 if it is a denial under a different code.
   - `in-scope mailbox read failed: ...` — likely RBAC propagation (wait and restart per Step 5) or a missing in-scope role assignment (re-verify via F11).
4. **Transient re-run resolves (Step 5):** after waiting through the propagation window and restarting, the run reaches the `Succeeded = true` state in item 2.

Verification is complete when a host start produces the `Succeeded = true` `Information` entry (item 2) and the observed out-of-scope `BridgeErrorCode` is recorded (Step 6).

## Source and Citation

The steps in this runbook are configuration and host-start steps; no third-party UI navigation is required for the validation run itself, so the MCP-first / web-second UI sourcing order applies only to the referenced prerequisite (app-registration and RBAC provisioning), which is handled by the F11 runbook. Per the current repository limitation, no callable MCP documentation tool is wired as a dependency in this repository; `WebFetch` (web-second) is therefore the sole available sourcing mechanism, and it was used to confirm each source below. Each source was captured on 2026-07-06; the `updated_at` column records the documentation page's own last-updated date as read on that capture date.

The internal source for step content is `docs/open-claw-approach.master.md` section 13 Steps 2-3 and the feature spec (D3, D5, D6, D7), captured 2026-07-06.

| Step | Documented topic | Source URL | updated_at |
|---|---|---|---|
| Prerequisites 1-2, Step 4-5 | Exchange Online Application RBAC (management scope; `Test-ServicePrincipalAuthorization`; propagation cache 30 minutes to 2 hours) | https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac | 2026-03-16 |
| Prerequisites 4, Step 1 | App-only (client-credentials) access token acquisition for Microsoft Graph | https://learn.microsoft.com/en-us/graph/auth-v2-service | 2025-08-29 |
| Prerequisites 4, Step 1 | Certificate credentials for application authentication (preferred over client secrets) | https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials | 2026-06-15 |
| Step 3, Step 5 | Background tasks with hosted services in ASP.NET Core (`IHostedService` / `AddHostedService`; `StartAsync` runs at host startup) | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services | 2026-05-05 |
| Step 4, Step 6, Verification | Microsoft Graph error responses (HTTP 403 Forbidden; JSON `error.code`) | https://learn.microsoft.com/en-us/graph/errors | 2025-08-06 |
