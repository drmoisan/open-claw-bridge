# negative-scope-smoke-test — Spec

- **Issue:** #120
- **Parent (optional):** Epic openclaw-vision (Epic C, F17); depends on F11 (#111) and F13 (#115)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-06
- **Status:** Ready
- **Version:** 1.0

## Overview

Master `docs/open-claw-approach.master.md` §13 Steps 2-3 requires that, before any business logic runs, the application proves its Exchange Online Application RBAC scope boundary: a harmless read against an in-scope mailbox must succeed, the same kind of read against an out-of-scope mailbox must be denied, and the result must be logged as part of startup validation. Gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` records this as F17: "No test exercises an in-scope-succeeds / out-of-scope-fails assertion" on the application-runtime side.

F11 (#111) delivered the administrator-side PowerShell boundary probe (`Test-OpenClawScopeBoundary` using `Test-ServicePrincipalAuthorization`, D5 structured result). F13 (#115) delivered the Graph-backed mailbox adapter the running application uses. F17 delivers the application-runtime counterpart: an opt-in startup validation that exercises the Graph read path against a configured in-scope and out-of-scope mailbox, asserts the expected authorization split, logs a structured result, and hard-aborts startup when the boundary does not hold. All tenant-dependent verification ships as mocked-Graph contract tests in the per-commit suite plus a human runbook recorded as a `human_interaction` exception (no Azure/Exchange credentials exist in this environment or in CI).

Authoritative design source: `docs/features/active/2026-07-06-negative-scope-smoke-test-120/research/2026-07-06T23-00-negative-scope-smoke-test-research.md` (Option 3c). This spec records that design; it does not re-open the option analysis.

## Design Decisions (recorded)

### D1 — Dedicated scope-boundary probe abstraction (research Option 3c)

The F13 port `IHostAdapterClient` reads a single fixed mailbox (`GraphAdapterOptions.PrincipalMailboxUpn` rendered into `users/{Principal}/...`); the target mailbox is not a per-call parameter. An in-scope/out-of-scope probe pair therefore cannot be expressed through the port as-is. F17 introduces a new narrow internal port with one mailbox-parameterized method, implemented against the existing `GraphRequestExecutor` pipeline, plus a pure evaluator that classifies the probe pair.

- The F13 `IHostAdapterClient` contract (`src/OpenClaw.HostAdapter.Contracts/`) is **not modified**. No member is added, changed, or removed.
- Rejected alternatives (recorded in the research artifact): 3a — two bespoke `GraphHostAdapterClient` instances (manual construction of internal types outside DI; DTO-mapping failure modes contaminate the boundary verdict); 3b — a mailbox parameter on `IHostAdapterClient` (T2 contract breaking change; unimplementable on the Stage-0 local backend; the probe is a Graph-runtime concern, not an agent capability).

Namespace/folder layout: host-neutral pieces in `src/OpenClaw.Core/ScopeValidation/`; the Graph-bound probe implementation in `src/OpenClaw.Core/CloudGraph/` beside the executor it reuses. Dependency direction: `ScopeValidation` defines the probe port; `CloudGraph` implements it. The pure evaluator has no dependency on `CloudGraph`, HTTP, DI, clock, or logging.

### D2 — Probe port and outcome record (I/O boundary)

```csharp
// src/OpenClaw.Core/ScopeValidation/MailboxProbeOutcome.cs
// Reduced, generic-free projection of one probe read's ApiEnvelope.
internal sealed record MailboxProbeOutcome(
    bool Ok,
    string? ErrorCode,        // ApiError.Code (null when Ok)
    string? BridgeErrorCode,  // ApiError.BridgeErrorCode (Graph error.code passthrough)
    string? ErrorMessage      // ApiError.Message (never a response body)
);

// src/OpenClaw.Core/ScopeValidation/IMailboxScopeProbe.cs
// One harmless read against an arbitrary mailbox; never throws on Graph failure.
internal interface IMailboxScopeProbe
{
    Task<MailboxProbeOutcome> ProbeMailboxReadAsync(
        string mailboxUpn,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
```

`GraphMailboxScopeProbe` (`src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs`, `internal sealed`) implements the port. Constructor seams mirror `GraphHostAdapterClient` exactly: `HttpClient`, `IOptions<GraphAdapterOptions>`, `IAppTokenProvider`, `TimeProvider`, `ILogger<GraphMailboxScopeProbe>`; it builds its own `GraphRequestExecutor`, so the probe inherits the production pipeline unchanged — app-only bearer token, `client-request-id` header, retry of 429/502/503/504 with `Retry-After` precedence, and D5 error mapping. Tokens and response bodies are never logged (existing executor guarantee).

**Harmless read:** `GET users/{Uri.EscapeDataString(mailboxUpn)}/messages?$top=1&$select=id`. Rationale: master §13 Step 3 requires "the same kind of read" as production; the ingestion path is a messages read, and `Application Mail.Read` is the primary scoped role granted by F11 (`<prefix>-MailRead`). A `mailboxSettings` probe would exercise `MailboxSettings.Read` and prove a different role's boundary. A 200 with an empty `value` array is success (the probe asserts authorization, not mailbox content); the success parser discards the body, so no DTO mapping and no `GraphMappingException` path exists in the probe. The probe maps `ApiEnvelope<T>` to `MailboxProbeOutcome(envelope.Ok, envelope.Error?.Code, envelope.Error?.BridgeErrorCode, envelope.Error?.Message)`.

### D3 — Out-of-scope denial classification rule (pure, fail-closed)

`ScopeBoundaryEvaluator` (`src/OpenClaw.Core/ScopeValidation/ScopeBoundaryEvaluator.cs`, `internal static`, pure: no I/O, no clock, no logging) defines the precise recognizer for a real Exchange RBAC denial, expressed against the F13 D5 envelope fields:

```csharp
internal const string ExpectedDenialErrorCode = "UNAUTHORIZED";        // D5 mapping of HTTP 403
internal const string ExpectedDenialGraphCode = "ErrorAccessDenied";   // Graph error.code passthrough

internal static bool IsAuthorizationDenial(MailboxProbeOutcome outcome) =>
    !outcome.Ok
    && string.Equals(outcome.ErrorCode, ExpectedDenialErrorCode, StringComparison.Ordinal)
    && string.Equals(outcome.BridgeErrorCode, ExpectedDenialGraphCode, StringComparison.Ordinal);
```

Classification matrix (all comparisons Ordinal; all three conjuncts required):

| Failure class | `Error.Code` | `Error.BridgeErrorCode` | Classified as denial? |
|---|---|---|---|
| Real 403 RBAC out-of-scope | `UNAUTHORIZED` | `ErrorAccessDenied` | **Yes** (the only accepted shape) |
| 401 bad/expired token | `UNAUTHORIZED` | `InvalidAuthenticationToken` or null | No (BridgeErrorCode mismatch) |
| Token acquisition failure | `CONFIGURATION_ERROR` | null | No |
| 404 mailbox not found | `NOT_FOUND` | e.g. `ErrorItemNotFound` | No |
| 429 retry-exhausted | `THROTTLED` | varies | No |
| 5xx / network | `TRANSPORT_FAILURE` | varies/null | No |
| 400 | `INVALID_REQUEST` | varies | No |
| Unmapped | `INTERNAL_ERROR` | varies | No |
| Successful read | (Ok) | — | No |

Because the F13 executor's `MapStatusError` folds HTTP 401 and 403 into the single code `UNAUTHORIZED`, `BridgeErrorCode == "ErrorAccessDenied"` is the only envelope-level discriminator between "authorization boundary held" and "authentication broke". The rule is deliberately fail-closed: any ambiguous or transient failure produces a failed validation with a reason, never a false pass. Both expected values are named constants; a tenant-observed divergence (see Constraints & Risks) is a one-constant, test-pinned correction.

### D4 — Structured result and pair evaluation (F11 D5 analog)

```csharp
// src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidationResult.cs
internal sealed record ScopeBoundaryValidationResult(
    string InScopeMailbox,
    string OutOfScopeMailbox,
    bool InScopeAllowed,
    bool OutOfScopeDenied,
    bool Succeeded,
    string? FailureReason,
    MailboxProbeOutcome InScopeOutcome,   // F11 D5 "InScopeDetails" analog
    MailboxProbeOutcome OutOfScopeOutcome // F11 D5 "OutOfScopeDetails" analog
);
```

`ScopeBoundaryEvaluator.Evaluate(inScopeMailbox, outOfScopeMailbox, inScopeOutcome, outOfScopeOutcome)` computes:

- `InScopeAllowed = inScopeOutcome.Ok`.
- `OutOfScopeDenied = IsAuthorizationDenial(outOfScopeOutcome)`.
- `Succeeded = InScopeAllowed && OutOfScopeDenied` (invariant; no other pass condition exists).
- `FailureReason` is null iff `Succeeded`; otherwise the precise reason(s), joined with `"; "` when both sides fail (mirroring F11 D5):
  - In-scope failure: `"in-scope mailbox read failed: {ErrorCode}/{BridgeErrorCode ?? "-"}: {ErrorMessage}"`.
  - Out-of-scope read succeeded (scope leak): `"out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold"`.
  - Out-of-scope failed for the wrong reason: `"out-of-scope mailbox read failed but not with the expected authorization denial (expected UNAUTHORIZED/ErrorAccessDenied; observed {ErrorCode}/{BridgeErrorCode ?? "-"})"`.

### D5 — Orchestrating validator and one-shot startup hosted service (fail-fast)

`ScopeBoundaryValidator` (`src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidator.cs`): injected `IMailboxScopeProbe` + `IOptions<ScopeValidationOptions>`. `ValidateAsync(CancellationToken)` probes the in-scope mailbox, then the out-of-scope mailbox (deterministic order; both always executed — no short-circuit, so the log always carries both sides), and returns `ScopeBoundaryEvaluator.Evaluate(...)`. Composition only; no logging and no verdict logic.

`ScopeBoundaryStartupValidator` (`src/OpenClaw.Core/ScopeValidation/ScopeBoundaryStartupValidator.cs`) is a one-shot `IHostedService` — not an `IStartupFilter` (synchronous seam, wrong for async I/O) and not inline `Program.cs` code (grows uncovered host-bound lines). In ASP.NET Core minimal hosting, user-registered hosted services run `StartAsync` before the server accepts requests, and an exception thrown from `StartAsync` aborts host startup — the master's "at startup ... assert" fail-fast semantics via the repository's established `AddHostedService` pattern. `StartAsync`:

1. Calls `ScopeBoundaryValidator.ValidateAsync`.
2. Logs the structured result (master §13 Step 2: "log the result as part of startup validation") as a single structured message-template entry carrying every result field: `LogInformation` on success, `LogCritical` on failure including `FailureReason` and both outcome summaries (`Ok`/`ErrorCode`/`BridgeErrorCode` only — never tokens, never response bodies).
3. On failure, throws `InvalidOperationException` naming the failure reason, aborting host startup. Hard fail-fast, always; no soft/warn-and-continue mode. Transient-failure verdicts (`THROTTLED`, `TRANSPORT_FAILURE`) also abort startup — the operator or container restart policy retries; startup validation never reports an unproven boundary as success.

`StopAsync` returns `Task.CompletedTask`. No separate CLI validation command is delivered: the host is a web application with no command plumbing, and the hosted-service form satisfies §13 Step 3's "at startup **or** in a dedicated validation command" disjunction with less surface.

### D6 — Configuration section `OpenClaw:ScopeValidation`

Options bag `ScopeValidationOptions` + pure static validator `ScopeValidationOptionsValidator` (full-violation-list, key-naming, value-non-echoing style), bound with `AddOptions/Bind/.Validate(...).ValidateOnStart()` — the exact `GraphAdapterOptions` pattern.

| Key | Type | Default | Env binding | Rule |
|---|---|---|---|---|
| `Enabled` | bool | `false` | `OpenClaw__ScopeValidation__Enabled` | Opt-in; all other rules apply only when true |
| `InScopeTestMailboxUpn` | string | `""` | `OpenClaw__ScopeValidation__InScopeTestMailboxUpn` | Required, non-whitespace when enabled (master §13 Step 1 `IN_SCOPE_TEST_MAILBOX`) |
| `OutOfScopeTestMailboxUpn` | string | `""` | `OpenClaw__ScopeValidation__OutOfScopeTestMailboxUpn` | Required, non-whitespace when enabled; must differ from in-scope (OrdinalIgnoreCase) (master §13 Step 1 `OUT_OF_SCOPE_TEST_MAILBOX`) |

Disabled implies always valid. No timeout key: the probe inherits the Graph retry budget from `GraphAdapterOptions` (`MaxAttempts`/`MaxDelaySeconds`), keeping one retry policy for all Graph traffic.

Registration: `ScopeValidationServiceCollectionExtensions.AddScopeBoundaryValidation(services, configuration)` (`src/OpenClaw.Core/ScopeValidation/`), called unconditionally from `Program.cs` immediately after the backend-selection block (a one-line change plus comment, keeping the host-bound file minimal per the coverage-exclusion policy):

- `OpenClaw:ScopeValidation:Enabled == false` → registers nothing (no-op; existing configurations unaffected).
- Enabled with `OpenClaw:GraphAdapter:Enabled == false` → throws `InvalidOperationException` at composition time. The probe is Graph-only; the Stage-0 local backend has no RBAC boundary, and a silently skipped validation would report false coverage.
- Enabled with Graph enabled → binds and validates options, registers `AddHttpClient<GraphMailboxScopeProbe>` with `BaseAddress = GraphAdapterOptions.BaseUrl`, maps it to `IMailboxScopeProbe`, registers `ScopeBoundaryValidator` and `AddHostedService<ScopeBoundaryStartupValidator>`. `IAppTokenProvider`/`TimeProvider` are already registered by `AddGraphHostAdapterClient`/`Program.cs`.

### D7 — Live-tenant verification as a human_interaction exception (F11 HI-1 mirror)

No Azure/Exchange credentials exist in this environment or in CI, and no CI job may attempt a live Graph call. Live-tenant execution of the startup validation (real in-scope + out-of-scope mailboxes, F11 RBAC roles applied, CloudAuth credentials configured) and confirmation of the tenant-observed denial code (`ErrorAccessDenied`) are delivered as a human runbook at `docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md` (authored in this feature per `.claude/skills/human-exception-runbook/SKILL.md`: Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation) and recorded in orchestrator state (`human_interaction.requirements[]`) with `response: exception` and that `runbook_path`, satisfying `.claude/rules/orchestrator-state.md` (exception requires non-empty `runbook_path`). This mirrors the F11 HI-1 precedent.

### D8 — F14/#117 Meta.Bridge gap is out of scope

`MessagePollingWorker.PersistPollResultAsync` requires `envelope.Meta.Bridge` non-null, which the Graph backend never sets. Research determination: this does not block F17 — the boundary probe calls the Graph pipeline directly, asserts on `Ok`/`Error.Code`/`Error.BridgeErrorCode`, and never calls `PersistPollResultAsync` or reads `Meta.Bridge`. #117 stays queued as its own feature; F17 must not modify `MessagePollingWorker`.

## Behavior

- A host-neutral, deterministic negative-scope evaluator (`ScopeBoundaryEvaluator`, pure) classifies a pair of mailbox-read outcomes: in-scope read succeeded AND out-of-scope read denied with the exact RBAC-denial shape (`UNAUTHORIZED`/`ErrorAccessDenied`, D3) → boundary holds; any other combination → boundary fails with a precise `FailureReason` naming the failing side(s) (D4).
- A narrow probe port (`IMailboxScopeProbe`) performs one harmless mailbox-parameterized read; the Graph implementation (`GraphMailboxScopeProbe`) reuses the F13 `GraphRequestExecutor` pipeline, so the D5 error semantics the classification depends on are identical to production by construction (D2). The F13 `IHostAdapterClient` contract is untouched.
- A one-shot `IHostedService` (`ScopeBoundaryStartupValidator`), registered only when `OpenClaw:ScopeValidation:Enabled == true`, probes both configured mailboxes at startup (in-scope first; both always run), logs the structured result (Information on pass, Critical on fail), and throws to hard-abort startup on any failure (D5).
- Configuration is the new `OpenClaw:ScopeValidation` section with `ValidateOnStart` semantics; enabling it without `OpenClaw:GraphAdapter:Enabled` fails at composition time (D6).
- All tenant-dependent verification ships as mocked-Graph/mocked-port tests in the per-commit suite plus the D7 human runbook recorded as a `human_interaction` exception.

### Behavior semantics (normative)

- **Pass condition (exactly one):** in-scope probe `Ok == true` AND out-of-scope probe matches `UNAUTHORIZED` + `ErrorAccessDenied`.
- **Fail matrix (F11 D5 mirror):** (denied, denied), (allowed, allowed), (denied, allowed) → `Succeeded = false` with a `FailureReason` naming the failing side(s); (allowed, wrong-error) → failed with the observed classification quoted.
- **Ordering:** in-scope probe first, out-of-scope second; both always run; outcomes composed into the result verbatim.
- **Edge cases:** empty in-scope mailbox (200 with empty `value`) → still allowed; out-of-scope mailbox does not exist (404 → `NOT_FOUND`) → wrong-error failure, not denial (runbook requires an existing out-of-scope mailbox); throttling/transport failure on either side → failed-with-reason, startup aborts, rerun; in-scope 403 shortly after provisioning → legitimate transient caused by the 30-minute-to-2-hour RBAC propagation cache (runbook states this with re-run guidance); cancellation → the token flows to the executor and startup shutdown cancels cleanly.

## Inputs / Outputs

- **Inputs:** configuration section `OpenClaw:ScopeValidation` (keys and env bindings per D6); existing `OpenClaw:GraphAdapter` and `OpenClaw:CloudAuth` sections supply the Graph pipeline, retry budget, and app-only credentials. No CLI flags, no files.
- **Outputs:** one structured startup log entry carrying every `ScopeBoundaryValidationResult` field (`LogInformation` on pass, `LogCritical` on fail); on failure, an `InvalidOperationException` that aborts host startup (process-level fail signal). No files written, no telemetry beyond the log entry. Tokens and response bodies are never logged.
- **Config keys and defaults:** per the D6 table; `Enabled` defaults to `false`, so the default (local/Stage-0) configuration is unaffected.
- **Versioning / backward compatibility:** no change to `OpenClaw.HostAdapter.Contracts` (T2 contract untouched, no version bump); all new types are `internal` to `OpenClaw.Core`; disabled-by-default registration preserves existing behavior byte-for-byte when the section is absent.

## API / CLI Surface

No public API and no CLI surface. All new types are `internal` (testable via the existing `InternalsVisibleTo("OpenClaw.Core.Tests")` grant). The operator-facing surface is configuration plus the startup log/abort behavior:

```text
# Enable (environment-variable form)
OpenClaw__GraphAdapter__Enabled=true
OpenClaw__ScopeValidation__Enabled=true
OpenClaw__ScopeValidation__InScopeTestMailboxUpn=in-scope-user@contoso.com
OpenClaw__ScopeValidation__OutOfScopeTestMailboxUpn=out-of-scope-user@contoso.com

# Expected outcomes
# Boundary holds  -> host starts; one LogInformation entry with Succeeded=true.
# Boundary broken -> one LogCritical entry with FailureReason; host startup aborts.
# Enabled without GraphAdapter -> InvalidOperationException at composition time.
# Missing/equal UPNs while enabled -> OptionsValidationException at startup (ValidateOnStart).
```

Contracts and validation rules: probe request shape `GET users/{escaped-upn}/messages?$top=1&$select=id` with bearer header and `client-request-id`; envelope-to-outcome projection per D2; denial classification per D3 (Ordinal, all conjuncts); options rules per D6.

## Data & State

- No persistence, no caching, no migration, no backfill. The feature reads two mailboxes (at most one message id each) and writes only a log entry.
- Data transformations: `ApiEnvelope<T>` → `MailboxProbeOutcome` (lossy projection: `Ok`, `Error.Code`, `Error.BridgeErrorCode`, `Error.Message` only) → `ScopeBoundaryValidationResult`.
- Invariants: `Succeeded == InScopeAllowed && OutOfScopeDenied`; `FailureReason == null` iff `Succeeded`; `IsAuthorizationDenial` is true only for the exact `!Ok` + `UNAUTHORIZED` + `ErrorAccessDenied` shape; both probes always execute; evaluator and classifier are pure and deterministic.
- No state change to `MessagePollingWorker` or any existing worker (D8).

## Constraints & Risks

- **No live tenant:** every tenant-dependent step ships mocked; no Azure/Exchange credentials in this environment or CI; no CI job may attempt a live Graph call. Live verification is the D7 runbook (human_interaction exception).
- **F13 contract preserved:** `IHostAdapterClient` and the `OpenClaw.HostAdapter.Contracts` project are not modified (D1). Auth/token path is touched only indirectly via the existing `IAppTokenProvider` (complexity floor signal `auth_or_token_handling`); no new credential handling is added.
- **Denial-classification precision:** the 401-vs-403 fold into `UNAUTHORIZED` means `BridgeErrorCode` is the only discriminator; the D3 rule is fail-closed so unrelated failures (auth, throttle, transport, not-found) can never satisfy the boundary.
- **Denial-code drift risk:** if a live tenant surfaces a Graph `error.code` other than `ErrorAccessDenied` for the RBAC denial, validation fails closed with the observed code quoted in `FailureReason`; the runbook's verification step captures the observed code, and the fix is one named constant plus its pinned tests. Fail-closed is the correct failure direction for a security boundary check.
- **RBAC propagation cache (30 minutes-2 hours)** can produce a legitimate transient in-scope denial right after provisioning (master §12 Step 7; Microsoft Learn Application RBAC "Limitations"); the runbook states this with re-run guidance.
- **Startup cost:** two Graph reads with the existing retry budget; incurred only when explicitly enabled; zero effect on the default configuration.
- **F14/#117 Meta.Bridge gap is explicitly OUT OF SCOPE** (queued follow-up per the research determination); F17 must not modify `MessagePollingWorker` (D8).
- **T1 rigor:** `OpenClaw.Core` is T1 in `quality-tiers.yml` — line >= 85%, branch >= 75%, >= 1 property test per pure function, mutation score >= 75% (pre-merge/nightly Stryker), zero untyped escape hatches (no `dynamic`).
- **Test-framework note:** the observed harness in `tests/OpenClaw.Core.Tests/` is MSTest + Moq + FluentAssertions + CsCheck + `FakeTimeProvider` + the existing `FakeHttpHandler` fixture; `.claude/rules/csharp.md` names xUnit + NSubstitute, a known pre-existing mismatch — new tests follow the observed repository harness.
- **File size:** every new production and test file stays under the 500-line cap; test files mirror the `src/` layout under `tests/`.

## Implementation Strategy

- **Scope of change (production, all `src/OpenClaw.Core`, T1):**

  | File | Content |
  |---|---|
  | `src/OpenClaw.Core/ScopeValidation/MailboxProbeOutcome.cs` | Outcome record (D2) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidationResult.cs` | Result record (D4) |
  | `src/OpenClaw.Core/ScopeValidation/IMailboxScopeProbe.cs` | Probe port (D2) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryEvaluator.cs` | Pure classifier + evaluator (D3, D4) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptions.cs` | Options bag (D6) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptionsValidator.cs` | Pure options validator (D6) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidator.cs` | Two-probe orchestration (D5) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryStartupValidator.cs` | One-shot IHostedService, logging + fail-fast (D5) |
  | `src/OpenClaw.Core/ScopeValidation/ScopeValidationServiceCollectionExtensions.cs` | DI registration + Graph-enabled guard (D6) |
  | `src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs` | Graph probe via `GraphRequestExecutor` (D2) |
  | `src/OpenClaw.Core/Program.cs` | One-line `AddScopeBoundaryValidation` call (D6) |

  Tests mirror under `tests/OpenClaw.Core.Tests/ScopeValidation/` and `tests/OpenClaw.Core.Tests/CloudGraph/`. Docs: the D7 runbook under this feature folder. No change to `OpenClaw.HostAdapter.Contracts`; no change to `MessagePollingWorker`.
- **New classes/functions:** per the table above; all `internal`; every file under the 500-line cap.
- **Dependency changes:** none. Options binding, hosted services, and the HTTP client factory are already referenced; test packages (MSTest, Moq, FluentAssertions, CsCheck, `Microsoft.Extensions.TimeProvider.Testing`) are already in use.
- **Logging:** one structured message-template entry in `ScopeBoundaryStartupValidator` (Information on pass, Critical on fail) carrying every result field; probe-level logging inherits the executor's guarantees (no tokens, no response bodies).
- **Rollout:** opt-in via `OpenClaw:ScopeValidation:Enabled` (default `false`); no staged deploy needed; fallback is leaving the flag off. Misconfiguration (enabled without Graph, missing/equal UPNs) fails fast at composition/startup rather than degrading silently.
- **Evidence:** baselines, QA gates, and coverage evidence are written under `docs/features/active/2026-07-06-negative-scope-smoke-test-120/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`.

## Acceptance Criteria

- [x] A pure, host-neutral `ScopeBoundaryEvaluator` exists in `src/OpenClaw.Core/ScopeValidation/` with no I/O, clock, logging, or `CloudGraph` dependency, and `Evaluate(...)` returns a `ScopeBoundaryValidationResult` record with fields `InScopeMailbox`, `OutOfScopeMailbox`, `InScopeAllowed`, `OutOfScopeDenied`, `Succeeded`, `FailureReason`, `InScopeOutcome`, `OutOfScopeOutcome`, where `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason` is null iff `Succeeded` (F11 D5 analog).
- [x] The out-of-scope denial classifier (`IsAuthorizationDenial`) accepts exactly the shape `!Ok && Error.Code == "UNAUTHORIZED" && Error.BridgeErrorCode == "ErrorAccessDenied"` (Ordinal comparisons), and unit tests pin the full classification matrix: the real 403 RBAC denial is the only true case; a 401-shaped `UNAUTHORIZED` with null or non-`ErrorAccessDenied` `BridgeErrorCode`, and every other code (`THROTTLED`, `NOT_FOUND`, `TRANSPORT_FAILURE`, `INVALID_REQUEST`, `CONFIGURATION_ERROR`, `INTERNAL_ERROR`) and the `Ok == true` case, all classify as not-a-denial and fail the boundary with a precise reason.
- [x] Unit tests pin the pair-evaluation matrix: (in-scope allowed, out-of-scope denied) → `Succeeded = true` with null `FailureReason`; (denied, denied), (allowed, allowed), and (denied, allowed) → `Succeeded = false` with the precise reason string(s) naming the failing side(s), including the `"; "`-joined both-sides case and the wrong-error case quoting the observed `{ErrorCode}/{BridgeErrorCode}` classification.
- [x] A narrow probe port `IMailboxScopeProbe` with mailbox-parameterized `ProbeMailboxReadAsync(mailboxUpn, ...)` is implemented by `GraphMailboxScopeProbe` in `src/OpenClaw.Core/CloudGraph/`, reusing the existing `GraphRequestExecutor` pipeline (bearer token, `client-request-id`, retry/backoff, D5 error mapping) and issuing `GET users/{escaped-upn}/messages?$top=1&$select=id`; the F13 `IHostAdapterClient` contract and the `OpenClaw.HostAdapter.Contracts` project are not modified.
- [x] Contract tests at the Graph boundary (via the existing `FakeHttpHandler`) verify the probe's request shape (path, UPN escaping, `$top=1&$select=id`, bearer header, `client-request-id`) and the envelope-to-outcome projection: 200 with empty `value` → `Ok = true`; a 403 body with `error.code == "ErrorAccessDenied"` → `(false, "UNAUTHORIZED", "ErrorAccessDenied", ...)`; a 401 body → `UNAUTHORIZED` with the 401 Graph code; an unparseable error body → null `BridgeErrorCode`.
- [x] A one-shot `IHostedService` (`ScopeBoundaryStartupValidator`) probes the configured in-scope mailbox first and the out-of-scope mailbox second (both always executed), logs the structured result as a single entry carrying every result field (`LogInformation` on pass, `LogCritical` on fail, never tokens or response bodies), and on failure throws `InvalidOperationException` naming the `FailureReason` so host startup hard-aborts; unit tests cover success, failure, and cancellation, and `ScopeBoundaryValidator` tests verify both probes are invoked with the configured mailboxes and outcomes are composed verbatim.
- [x] The new config section `OpenClaw:ScopeValidation` (`Enabled` default `false`, `InScopeTestMailboxUpn`, `OutOfScopeTestMailboxUpn`) is bound with the `AddOptions/Bind/.Validate(...).ValidateOnStart()` pattern; when enabled, both UPNs are required, non-whitespace, and distinct (OrdinalIgnoreCase); disabled registers nothing; enabling with `OpenClaw:GraphAdapter:Enabled == false` throws at composition time; DI/selection tests cover all three registration branches.
- [x] T1 test rigor holds for all new code: mocked-only per-commit suite (MSTest + Moq + FluentAssertions + `FakeHttpHandler`; no network, no temp files, no `Thread.Sleep`/wall-clock — time via `TimeProvider`/`FakeTimeProvider` only), at least one CsCheck property test per pure function (`IsAuthorizationDenial`, `Evaluate`, options validator), line coverage >= 85% and branch coverage >= 75% with no regression on changed lines, zero `dynamic`, every file under 500 lines, tests mirroring `src/` layout under `tests/`, and mutation testing (Stryker, >= 75%) scheduled in the pre-merge/nightly pipeline rather than the per-commit loop.
- [ ] Live-tenant verification is recorded as a `human_interaction` requirement with `response: exception` and `runbook_path: docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md` in orchestrator state (orchestrator-verified, mirroring F11 HI-1); the runbook covers execution prerequisites (F11 provisioning, CloudAuth credentials, Graph adapter enabled, ScopeValidation keys), the pass and negative-rehearsal procedures, the RBAC propagation-cache re-run guidance, and confirmation of the tenant-observed denial code (`ErrorAccessDenied`).

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments (default-disabled path byte-identical; enabled path per D5/D6)
- [ ] Tests updated/added (mocked unit, property, contract, DI/selection tests; no live-tenant tests — live verification is human-only per the D7 runbook)
- [ ] Edge cases and error handling covered by tests (full classification matrix, four-cell pair matrix, empty mailbox, 404 out-of-scope, transient failures, cancellation, options validation branches)
- [ ] Docs updated (runbook authored; feature-folder links current)
- [ ] Telemetry/logging added or updated (single structured startup log entry per D5)
- [ ] Toolchain pass completed (format → lint → type-check → architecture tests → unit tests → contract checks → integration tests, restart on any failure or auto-fix)

## Seeded Test Conditions (from potential)

- [ ] Unit: validator pass matrix — (in-scope allowed, out-of-scope denied) => succeeded; (denied, denied), (allowed, allowed), (denied, allowed) => failed with precise reason.
- [ ] Unit: out-of-scope denial classification distinguishes authorization-denied from other errors (401 vs 403 discrimination via `BridgeErrorCode`; every non-denial code row in the D3 matrix; case-sensitivity of both constants).
- [ ] Property (CsCheck): `IsAuthorizationDenial` is true only when all three conjuncts hold; `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason is null == Succeeded` for any outcome pair; options validator returns zero violations for any field values when `Enabled == false`.
- [ ] Integration/contract: startup service logs the structured result and produces the correct pass/fail signal against a mocked probe; `GraphMailboxScopeProbe` request-shape and error-body contract tests against `FakeHttpHandler`.
- [ ] DI/selection: disabled → nothing registered; enabled without Graph → composition-time throw; enabled with Graph → all services resolvable with `ValidateOnStart` wired.
- [ ] Mutation (nightly): classification-table rows kill constant/operator mutants in the classifier's string constants and boolean conjunctions.
- [ ] Human runbook: live-tenant verification procedure (out of CI), including negative rehearsal and denial-code confirmation.
