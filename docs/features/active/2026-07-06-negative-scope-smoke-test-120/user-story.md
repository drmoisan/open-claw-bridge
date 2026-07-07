# `negative-scope-smoke-test` — User Story

- Issue: #120
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-06

## Story Statement

- As a **platform operator deploying OpenClaw against a live Exchange Online tenant**, I want the application to prove at startup that its RBAC scope boundary holds — an in-scope mailbox read succeeds and the same read against an out-of-scope mailbox is denied — so that the service never runs business logic on an authorization model that is broader (or more broken) than intended.
- As a **security reviewer / Exchange administrator**, I want the boundary verdict logged as a single structured record and any failure to hard-abort startup with a precise reason, so that a scope leak, a misapplied role assignment, or an authentication fault is visible immediately and cannot be mistaken for a healthy deployment.
- As a **maintainer of the OpenClaw codebase**, I want the boundary decision logic to be a pure, fully mocked-testable function that reuses the existing F13 Graph pipeline without changing the `IHostAdapterClient` contract, so that the check is deterministic in CI (which has no tenant credentials) and does not destabilize the Stage-0 local backend.

## Problem / Why

Master `docs/open-claw-approach.master.md` §13 Steps 2-3 requires that, before any business logic runs, the application proves its Exchange Online Application RBAC scope boundary: a harmless read against an in-scope mailbox must succeed and the same read against an out-of-scope mailbox must be denied, and the result must be logged as part of startup validation. Gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` records this as F17: "No test exercises an in-scope-succeeds / out-of-scope-fails assertion" on the application-runtime side.

F11 (#111) delivered the administrator-side PowerShell boundary probe (`Test-OpenClawScopeBoundary` using `Test-ServicePrincipalAuthorization`). F13 (#115) delivered the Graph-backed mailbox adapter the running application uses. F17 delivers the application-runtime counterpart: an opt-in startup validation that exercises the Graph read path against a configured in-scope and out-of-scope mailbox and asserts the expected authorization split, with mocked-Graph contract tests in the per-commit suite and a live-tenant human runbook recorded as a `human_interaction` exception (no Azure/Exchange credentials exist in this environment or CI). Full design is recorded in `spec.md` (D1-D8) from the research artifact `research/2026-07-06T23-00-negative-scope-smoke-test-research.md`.

## Personas & Scenarios

- **Persona: Platform operator (deploys and runs the OpenClaw host)**
  - Who: the engineer responsible for configuring `OpenClaw:GraphAdapter`, `OpenClaw:CloudAuth`, and the deployment environment for a Stage-1 (cloud Graph) rollout.
  - Cares about: knowing before traffic flows that the service can read only the mailboxes it was scoped to; a deterministic startup signal (start vs. abort) rather than a buried warning.
  - Constraints: has no direct Exchange admin rights; depends on the F11 handoff package (one in-scope and one existing out-of-scope test mailbox); cannot run tenant checks from CI.
  - Goals and frustrations: wants misconfiguration to fail loudly at startup, not silently degrade; is frustrated by validation modes that log a warning and continue.
  - Context: enables the check by setting `OpenClaw__ScopeValidation__Enabled=true` plus the two test-mailbox UPNs; the default configuration (flag off) is unchanged.

- **Persona: Security reviewer / Exchange administrator**
  - Who: the administrator who provisioned the F11 RBAC scope (service principal, management scope, role assignments) and must confirm the boundary holds from the application's side, not just the admin cmdlet's side.
  - Cares about: a fail-closed check — an authentication failure, throttle, or missing mailbox must never be counted as a "denial"; the structured log naming exactly which side failed and how.
  - Constraints: RBAC changes propagate on a 30-minute-to-2-hour cache; the live check can only be run by a human with tenant credentials, following the runbook.
  - Goals: evidence that the out-of-scope read was denied specifically with the RBAC denial shape (`UNAUTHORIZED`/`ErrorAccessDenied`), plus a rehearsed negative case proving the check aborts startup when the boundary is broken.

- **Persona: Codebase maintainer**
  - Who: the engineer maintaining `OpenClaw.Core` (T1) and the F13 adapter.
  - Cares about: pure decision logic with property tests and a high mutation score; no breaking change to the T2 `IHostAdapterClient` contract; no live-network dependency in the per-commit suite.
  - Constraints: T1 gates (line >= 85%, branch >= 75%, >= 1 property test per pure function, mutation >= 75%, zero `dynamic`), 500-line file cap, deterministic tests only (`TimeProvider`, no wall clock, no temp files).

- **Scenario 1: Successful startup validation (boundary holds)**
  - The operator enables `OpenClaw:ScopeValidation` with the two test-mailbox UPNs from the F11 handoff package and starts the host with the Graph adapter enabled.
  - Before the server accepts requests, the one-shot startup validator probes the in-scope mailbox (harmless `GET .../messages?$top=1&$select=id`) and then the out-of-scope mailbox.
  - The in-scope read returns 200 (even with an empty mailbox); the out-of-scope read returns the RBAC denial (`UNAUTHORIZED`/`ErrorAccessDenied`).
  - One `LogInformation` entry records the full structured result with `Succeeded=true`; the host continues startup normally.

- **Scenario 2: Scope leak detected (out-of-scope read succeeds)**
  - Same setup, but a role assignment was created without its management scope, so the out-of-scope mailbox is readable.
  - The validator evaluates (allowed, allowed): `Succeeded=false`, `FailureReason` states the out-of-scope read unexpectedly succeeded and the RBAC scope does not hold.
  - One `LogCritical` entry records the result; the startup validator throws and host startup aborts. Nothing runs against the mis-scoped tenant until the administrator fixes the assignment and restarts.

- **Scenario 3: Wrong-reason failure is not mistaken for a denial**
  - The out-of-scope probe fails with a 401 (expired credential) or a 404 (mailbox deleted) instead of the RBAC denial.
  - The fail-closed classifier refuses to count it as a denial; `FailureReason` quotes the observed `{ErrorCode}/{BridgeErrorCode}` classification against the expected `UNAUTHORIZED/ErrorAccessDenied`.
  - Startup aborts with the precise reason, directing the operator at the actual fault (credentials, mailbox existence) rather than reporting a false boundary pass.

- **Scenario 4: Live-tenant verification by a human (runbook)**
  - Because no tenant credentials exist in CI, a human follows `runbooks/negative-scope-startup-validation.runbook.md`: confirm F11 prerequisites, configure the keys, start the host, verify the `Succeeded=true` log entry, run the negative rehearsal (point the in-scope key at an out-of-scope mailbox and confirm the startup abort), and record the tenant-observed denial code.
  - If an in-scope read is denied shortly after provisioning, the runbook's propagation-cache guidance (30 minutes-2 hours) instructs a re-run before escalating.

## Acceptance Criteria

- [ ] A pure, host-neutral `ScopeBoundaryEvaluator` exists in `src/OpenClaw.Core/ScopeValidation/` with no I/O, clock, logging, or `CloudGraph` dependency, and `Evaluate(...)` returns a `ScopeBoundaryValidationResult` record with fields `InScopeMailbox`, `OutOfScopeMailbox`, `InScopeAllowed`, `OutOfScopeDenied`, `Succeeded`, `FailureReason`, `InScopeOutcome`, `OutOfScopeOutcome`, where `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason` is null iff `Succeeded` (F11 D5 analog).
- [ ] The out-of-scope denial classifier (`IsAuthorizationDenial`) accepts exactly the shape `!Ok && Error.Code == "UNAUTHORIZED" && Error.BridgeErrorCode == "ErrorAccessDenied"` (Ordinal comparisons), and unit tests pin the full classification matrix: the real 403 RBAC denial is the only true case; a 401-shaped `UNAUTHORIZED` with null or non-`ErrorAccessDenied` `BridgeErrorCode`, and every other code (`THROTTLED`, `NOT_FOUND`, `TRANSPORT_FAILURE`, `INVALID_REQUEST`, `CONFIGURATION_ERROR`, `INTERNAL_ERROR`) and the `Ok == true` case, all classify as not-a-denial and fail the boundary with a precise reason.
- [ ] Unit tests pin the pair-evaluation matrix: (in-scope allowed, out-of-scope denied) → `Succeeded = true` with null `FailureReason`; (denied, denied), (allowed, allowed), and (denied, allowed) → `Succeeded = false` with the precise reason string(s) naming the failing side(s), including the `"; "`-joined both-sides case and the wrong-error case quoting the observed `{ErrorCode}/{BridgeErrorCode}` classification.
- [ ] A narrow probe port `IMailboxScopeProbe` with mailbox-parameterized `ProbeMailboxReadAsync(mailboxUpn, ...)` is implemented by `GraphMailboxScopeProbe` in `src/OpenClaw.Core/CloudGraph/`, reusing the existing `GraphRequestExecutor` pipeline (bearer token, `client-request-id`, retry/backoff, D5 error mapping) and issuing `GET users/{escaped-upn}/messages?$top=1&$select=id`; the F13 `IHostAdapterClient` contract and the `OpenClaw.HostAdapter.Contracts` project are not modified.
- [ ] Contract tests at the Graph boundary (via the existing `FakeHttpHandler`) verify the probe's request shape (path, UPN escaping, `$top=1&$select=id`, bearer header, `client-request-id`) and the envelope-to-outcome projection: 200 with empty `value` → `Ok = true`; a 403 body with `error.code == "ErrorAccessDenied"` → `(false, "UNAUTHORIZED", "ErrorAccessDenied", ...)`; a 401 body → `UNAUTHORIZED` with the 401 Graph code; an unparseable error body → null `BridgeErrorCode`.
- [ ] A one-shot `IHostedService` (`ScopeBoundaryStartupValidator`) probes the configured in-scope mailbox first and the out-of-scope mailbox second (both always executed), logs the structured result as a single entry carrying every result field (`LogInformation` on pass, `LogCritical` on fail, never tokens or response bodies), and on failure throws `InvalidOperationException` naming the `FailureReason` so host startup hard-aborts; unit tests cover success, failure, and cancellation, and `ScopeBoundaryValidator` tests verify both probes are invoked with the configured mailboxes and outcomes are composed verbatim.
- [ ] The new config section `OpenClaw:ScopeValidation` (`Enabled` default `false`, `InScopeTestMailboxUpn`, `OutOfScopeTestMailboxUpn`) is bound with the `AddOptions/Bind/.Validate(...).ValidateOnStart()` pattern; when enabled, both UPNs are required, non-whitespace, and distinct (OrdinalIgnoreCase); disabled registers nothing; enabling with `OpenClaw:GraphAdapter:Enabled == false` throws at composition time; DI/selection tests cover all three registration branches.
- [ ] T1 test rigor holds for all new code: mocked-only per-commit suite (MSTest + Moq + FluentAssertions + `FakeHttpHandler`; no network, no temp files, no `Thread.Sleep`/wall-clock — time via `TimeProvider`/`FakeTimeProvider` only), at least one CsCheck property test per pure function (`IsAuthorizationDenial`, `Evaluate`, options validator), line coverage >= 85% and branch coverage >= 75% with no regression on changed lines, zero `dynamic`, every file under 500 lines, tests mirroring `src/` layout under `tests/`, and mutation testing (Stryker, >= 75%) scheduled in the pre-merge/nightly pipeline rather than the per-commit loop.
- [ ] Live-tenant verification is recorded as a `human_interaction` requirement with `response: exception` and `runbook_path: docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md` in orchestrator state (orchestrator-verified, mirroring F11 HI-1); the runbook covers execution prerequisites (F11 provisioning, CloudAuth credentials, Graph adapter enabled, ScopeValidation keys), the pass and negative-rehearsal procedures, the RBAC propagation-cache re-run guidance, and confirmation of the tenant-observed denial code (`ErrorAccessDenied`).

## Non-Goals

- **No change to the F13 `IHostAdapterClient` contract** or any file in `OpenClaw.HostAdapter.Contracts` (research Option 3b rejected: T2 breaking change, unimplementable on the Stage-0 local backend).
- **No fix for the F14/#117 `Meta.Bridge` gap** (`MessagePollingWorker.PersistPollResultAsync` requires `envelope.Meta.Bridge` non-null, which the Graph backend never sets). Explicitly out of scope per the research determination; #117 remains a queued follow-up, and F17 does not modify `MessagePollingWorker`.
- **No live-tenant execution in CI or in this environment**: no CI job attempts a Graph call; live verification is human-only via the runbook.
- **No soft-fail / warn-and-continue mode**: a failed or unproven boundary always aborts startup.
- **No separate CLI validation command**: the one-shot `IHostedService` satisfies master §13 Step 3's "at startup or in a dedicated validation command" disjunction.
- **No configurable expected-denial code**: `UNAUTHORIZED`/`ErrorAccessDenied` are named constants with pinned tests; tenant divergence is handled as a one-constant correction, not a config surface.
- **No `mailboxSettings` probe**: the harmless read is a messages read so the check proves the primary `Application Mail.Read` boundary, not `MailboxSettings.Read`.
- **No validation of the Stage-0 local backend**: the probe is Graph-only; enabling scope validation without the Graph adapter is a composition-time error, not a skipped check.
