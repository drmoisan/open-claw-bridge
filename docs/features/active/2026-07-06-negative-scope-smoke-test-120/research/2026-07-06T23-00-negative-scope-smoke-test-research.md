# F17 negative-scope-smoke-test (#120) — Research

- **Issue:** #120 (epic openclaw-vision, Epic C, F17)
- **Date:** 2026-07-06T23-00
- **Branch:** feature/negative-scope-smoke-test-120
- **Master reference:** `docs/open-claw-approach.master.md` §13 Steps 2-3 (lines 1466-1486): "Read an in-scope mailbox successfully; fail against an out-of-scope mailbox; log the result as part of startup validation" and "Your first automated smoke test proves the scope boundary. At startup or in a dedicated validation command: call a harmless read against an in-scope mailbox; call the same kind of read against an out-of-scope mailbox; assert that the in-scope call succeeds and the out-of-scope call fails."
- **Companion features:** F11 (#111) admin-side `Test-OpenClawScopeBoundary` (D5 structured result, HI-1 runbook precedent); F13 (#115) Graph-backed `IHostAdapterClient`.

## 1. Current State Analysis (verified by direct code read)

All facts below were verified in this worktree on 2026-07-06.

1. **Port.** `IHostAdapterClient` (`src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`) is the agent-facing contract. All read methods return `ApiEnvelope<T>` and do not throw on Graph failures. Envelope shape (`src/OpenClaw.HostAdapter.Contracts/ApiEnvelope.cs`, `ApiError.cs`, `ApiMeta.cs`):
   - `ApiEnvelope<T>(bool Ok, T? Data, ApiMeta Meta, ApiError? Error)`
   - `ApiError(string Code, string Message, string? BridgeErrorCode = null, bool Retryable = false)`
   - `ApiMeta(string RequestId, string AdapterVersion, BridgeStatusDto? Bridge)`
2. **Graph implementation.** `GraphHostAdapterClient : IHostAdapterClient` (`internal sealed partial`, namespace `OpenClaw.Core.CloudGraph`), split across `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient*.cs`. All requests flow through the shared `GraphRequestExecutor` (`src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs`): app-only bearer token via `IAppTokenProvider`, `client-request-id` header, retry of 429/502/503/504 with `Retry-After` precedence over exponential backoff driven by injected `TimeProvider`, D5 error mapping, envelope synthesis with `ApiMeta(requestId, "cloudgraph", null)` — `Meta.Bridge` is always `null` on the Graph backend.
3. **Fixed-mailbox constraint (confirmed).** The read routes render `users/{Principal}/...` where `Principal` is `Uri.EscapeDataString(options.PrincipalMailboxUpn)` (`GraphHostAdapterClient.cs` line 98). The target mailbox is not a per-call parameter on any `IHostAdapterClient` method. An in-scope/out-of-scope probe pair therefore cannot be expressed through the port as-is.
4. **Error mapping (confirmed).** `GraphRequestExecutor.MapStatusError` maps 401 and 403 both to `Code == "UNAUTHORIZED"`; the Graph `error.code` string (e.g. `ErrorAccessDenied`) is parsed from the error body by `TryReadGraphErrorCode` and passed through as `ApiError.BridgeErrorCode` (null when the body is unparseable or absent). Other codes: 400 → `INVALID_REQUEST`, 404 → `NOT_FOUND`, 429 → `THROTTLED` (after retry exhaustion), 502/503/504 → `TRANSPORT_FAILURE`, other → `INTERNAL_ERROR`; token-acquisition failure → `CONFIGURATION_ERROR`; network exception → `TRANSPORT_FAILURE`. The existing error-matrix test (`tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorErrorMatrixTests.cs` lines 82-84) pins 401 and 403 both to `UNAUTHORIZED`.
5. **Composition root.** `src/OpenClaw.Core/Program.cs` (top-level statements): `OpenClaw:GraphAdapter:Enabled == true` selects `AddGraphHostAdapterClient(configuration)` (`src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs`), which registers CloudAuth (`AddCloudAuth`), binds `GraphAdapterOptions` from `OpenClaw:GraphAdapter` with `.Validate(...).ValidateOnStart()`, and registers the typed `IHostAdapterClient` HTTP client. Hosted services in use: `MessagePollingWorker`, `CalendarPollingWorker`, `SchedulingWorker` (all `AddHostedService`).
6. **Options pattern.** Plain options bag + pure static validator returning the full violation list (`GraphAdapterOptions` / `GraphAdapterOptionsValidator`; `CloudAuthOptions` / `CloudAuthOptionsValidator`), bound with `AddOptions/Bind/Validate/ValidateOnStart`. Validation messages name keys and never echo values. There is no existing IN_SCOPE / OUT_OF_SCOPE test-mailbox key anywhere in configuration — F17 introduces it (master §13 Step 1 requires `IN_SCOPE_TEST_MAILBOX` and `OUT_OF_SCOPE_TEST_MAILBOX`).
7. **Tiers.** `quality-tiers.yml`: `OpenClaw.Core` = T1, `OpenClaw.HostAdapter.Contracts` = T2, `OpenClaw.Core.Tests` = T1. T1 obligations: line >= 85%, branch >= 75%, >= 1 property test per pure function, mutation >= 75% (pre-merge/nightly Stryker), zero untyped escape hatches.
8. **Test conventions (observed, authoritative over the rule text).** `tests/OpenClaw.Core.Tests/CloudGraph/` uses MSTest (`[TestClass]`/`[TestMethod]`/`[DataTestMethod]`) + Moq + FluentAssertions + CsCheck (property tests) + `FakeTimeProvider` + a `FakeHttpHandler` fixture; `.claude/rules/csharp.md` names xUnit+NSubstitute, but the repository's actual harness is MSTest+Moq (known pre-existing mismatch). `OpenClaw.Core` grants `InternalsVisibleTo("OpenClaw.Core.Tests")` (`src/OpenClaw.Core/OpenClaw.Core.csproj` line 9), so internal types are directly testable.
9. **F11 precedent.** `Test-OpenClawScopeBoundary` (spec `docs/features/active/2026-07-02-exchange-rbac-scripts-111/spec.md`, D5) returns a structured object `{ InScopeMailbox, OutOfScopeMailbox, InScopeAllowed, OutOfScopeDenied, Succeeded, FailureReason, InScopeDetails, OutOfScopeDetails }` with `Succeeded = InScopeAllowed AND OutOfScopeDenied` and a precise `FailureReason` naming the failing side(s). Live-tenant execution is recorded as `human_interaction` requirement HI-1 with `response: exception` and `runbook_path: docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (runbook verified present on disk).

### F14/#117 adjacency determination

`MessagePollingWorker.PersistPollResultAsync` (`src/OpenClaw.Core/MessagePollingWorker.cs` line 158) takes the success branch only when `envelope.Ok && envelope.Meta.Bridge is not null`. The Graph executor always sets `Meta.Bridge = null` (`GraphRequestExecutor.cs` line 65), so Graph-backed polls always record as failures. **Determination: this does not block F17.** The F17 boundary probe calls the Graph pipeline directly and asserts on the envelope (`Ok`, `Error.Code`, `Error.BridgeErrorCode`); it never calls `PersistPollResultAsync` and never reads `Meta.Bridge`. Keep #117 queued as its own feature; F17 must not modify `MessagePollingWorker`.

## 2. Candidate Approaches and Recommendation

The delegation constraint (fact 3 above) admits three designs for producing an in-scope/out-of-scope probe pair.

### Option 3a — Two adapter instances with different `GraphAdapterOptions`

Construct two extra `GraphHostAdapterClient` instances whose `PrincipalMailboxUpn` is the in-scope and out-of-scope test mailbox respectively, and call `ListMessagesAsync` on each.

- Advantages: exercises the exact production read path end to end.
- Limitations: `GraphHostAdapterClient` is `internal` and registered as a single typed HTTP client bound to `IHostAdapterClient`; two more instances require bespoke manual construction (HttpClient, token provider, options snapshot) outside the DI pattern. The probe would also inherit the full DTO mapping pipeline, where an unrelated `GraphMappingException` (→ `INTERNAL_ERROR`) or a mapper defect could contaminate the boundary verdict. Heavyweight for a probe that needs only "did the read succeed / how was it denied".

### Option 3b — Mailbox-parameterized read seam on the port

Add a `mailboxUpn` parameter (or a new method) to `IHostAdapterClient`.

- Advantages: smallest amount of new Graph code.
- Limitations: breaks the F13/Stage-0 contract-parity design. `IHostAdapterClient` is the T2 agent-facing contract implemented by both the local Stage-0 adapter (single signed-in mailbox; cannot honor an arbitrary UPN) and the Graph backend. A contract change is a breaking change requiring a major bump per the T2 gate matrix and forces a meaningless implementation into the local backend. The scope probe is a Graph-runtime concern, not an agent capability; it does not belong on the agent-facing port.

### Option 3c — Dedicated scope-boundary probe abstraction (RECOMMENDED)

A new narrow internal port with one method that performs a harmless mailbox-parameterized read, implemented against the existing `GraphRequestExecutor` pipeline, plus a pure evaluator that classifies the probe pair. The F13 contract is untouched; the probe reuses the exact auth/retry/error-mapping pipeline production reads use, so the D5 error semantics the classification depends on are identical by construction.

**Recommendation: Option 3c.** It is the simplest design that satisfies the I/O-boundary rule (`.claude/rules/general-code-change.md`: pure logic separated from I/O), respects T1 rigor (the decision logic is a pure function with property tests), and does not break the F13 contract. Rejected alternatives: 3a (bespoke construction of internal types, DTO-mapping noise in the verdict), 3b (T2 contract break, forces an unimplementable member onto the Stage-0 backend).

## 3. Recommended Design

Namespace/folder: `src/OpenClaw.Core/ScopeValidation/` for the host-neutral pieces; the Graph-bound probe implementation lives in `src/OpenClaw.Core/CloudGraph/` beside the executor it reuses. Dependency direction: `ScopeValidation` (pure + orchestration) defines the probe port; `CloudGraph` implements it. The pure evaluator has no dependency on `CloudGraph`, HTTP, or DI.

### 3.1 Probe port and outcome record (I/O boundary)

```csharp
// src/OpenClaw.Core/ScopeValidation/MailboxProbeOutcome.cs
/// Reduced, generic-free projection of one probe read's ApiEnvelope.
internal sealed record MailboxProbeOutcome(
    bool Ok,
    string? ErrorCode,        // ApiError.Code (null when Ok)
    string? BridgeErrorCode,  // ApiError.BridgeErrorCode (Graph error.code passthrough)
    string? ErrorMessage      // ApiError.Message (never a response body)
);

// src/OpenClaw.Core/ScopeValidation/IMailboxScopeProbe.cs
/// One harmless read against an arbitrary mailbox; never throws on Graph failure.
internal interface IMailboxScopeProbe
{
    Task<MailboxProbeOutcome> ProbeMailboxReadAsync(
        string mailboxUpn,
        string? requestId = null,
        CancellationToken cancellationToken = default
    );
}
```

### 3.2 Graph probe implementation

`GraphMailboxScopeProbe` (`src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs`, `internal sealed`, implements `IMailboxScopeProbe`). Constructor seams mirror `GraphHostAdapterClient` exactly: `HttpClient`, `IOptions<GraphAdapterOptions>`, `IAppTokenProvider`, `TimeProvider`, `ILogger<GraphMailboxScopeProbe>`; it builds its own `GraphRequestExecutor`.

**Harmless read:** `GET users/{Uri.EscapeDataString(mailboxUpn)}/messages?$top=1&$select=id`.

- Rationale: master §13 Step 3 requires "the same kind of read" against both mailboxes; the production ingestion path is a messages read, and `Application Mail.Read` is the primary scoped role (F11 grants it as `<prefix>-MailRead`). A `mailboxSettings` probe (the D2 status substitute) would exercise `MailboxSettings.Read` instead and prove a different role's boundary.
- A 200 with an empty `value` array is still success (the probe asserts authorization, not mailbox content), so the success parser discards the body and returns a marker; no DTO mapping and no `GraphMappingException` path exists in the probe.
- The executor supplies retry/backoff (429/502/503/504), the `client-request-id`, and D5 error mapping unchanged. Tokens and response bodies are never logged (existing executor guarantee).

The probe method maps `ApiEnvelope<T>` → `MailboxProbeOutcome(envelope.Ok, envelope.Error?.Code, envelope.Error?.BridgeErrorCode, envelope.Error?.Message)`.

### 3.3 Pure decision logic (host-neutral, deterministic)

`ScopeBoundaryEvaluator` (`src/OpenClaw.Core/ScopeValidation/ScopeBoundaryEvaluator.cs`, `internal static`, pure: no I/O, no clock, no logging). Two pure functions:

**(a) Out-of-scope denial classification rule** — the precise recognizer for a real Exchange RBAC denial, expressed against the envelope fields:

```csharp
internal const string ExpectedDenialErrorCode = "UNAUTHORIZED";        // D5 mapping of HTTP 403
internal const string ExpectedDenialGraphCode = "ErrorAccessDenied";   // Graph error.code passthrough

internal static bool IsAuthorizationDenial(MailboxProbeOutcome outcome) =>
    !outcome.Ok
    && string.Equals(outcome.ErrorCode, ExpectedDenialErrorCode, StringComparison.Ordinal)
    && string.Equals(outcome.BridgeErrorCode, ExpectedDenialGraphCode, StringComparison.Ordinal);
```

Why both conjuncts are required:

| Failure class | `Error.Code` | `Error.BridgeErrorCode` | Classified as denial? |
|---|---|---|---|
| Real 403 RBAC out-of-scope | `UNAUTHORIZED` | `ErrorAccessDenied` | **Yes** (the only accepted shape) |
| 401 bad/expired token | `UNAUTHORIZED` | `InvalidAuthenticationToken` or null | No (BridgeErrorCode mismatch) |
| Token acquisition failure | `CONFIGURATION_ERROR` | null | No |
| 404 mailbox not found | `NOT_FOUND` | e.g. `ErrorItemNotFound`/`ResourceNotFound` | No |
| 429 exhausted | `THROTTLED` | varies | No |
| 5xx / network | `TRANSPORT_FAILURE` | varies/null | No |
| Successful read | (Ok) | — | No |

Because `MapStatusError` folds 401 and 403 into one `UNAUTHORIZED` code, `BridgeErrorCode == "ErrorAccessDenied"` is the only envelope-level discriminator between "authorization boundary held" and "authentication broke"; the rule is deliberately fail-closed — any ambiguous or transient failure produces a failed validation with a reason, never a false pass. `ErrorAccessDenied` as the tenant-observed denial code is the caller-established fact for the RBAC boundary (consistent with the documented Application Access Policies denial behavior); the live-tenant runbook's verification step confirms the observed code, and because it is a named constant, a tenant divergence is a one-line, test-pinned correction (see Risks).

**(b) Pair evaluation** — the D5-analog verdict:

```csharp
internal static ScopeBoundaryValidationResult Evaluate(
    string inScopeMailbox,
    string outOfScopeMailbox,
    MailboxProbeOutcome inScopeOutcome,
    MailboxProbeOutcome outOfScopeOutcome
);
```

- `InScopeAllowed = inScopeOutcome.Ok`.
- `OutOfScopeDenied = IsAuthorizationDenial(outOfScopeOutcome)`.
- `Succeeded = InScopeAllowed && OutOfScopeDenied`.
- `FailureReason`: null iff `Succeeded`; otherwise the precise reason(s), joined with `"; "` when both sides fail (mirroring F11 D5):
  - In-scope failure: `"in-scope mailbox read failed: {ErrorCode}/{BridgeErrorCode ?? "-"}: {ErrorMessage}"`.
  - Out-of-scope read succeeded (scope leak — most severe): `"out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold"`.
  - Out-of-scope failed for the wrong reason: `"out-of-scope mailbox read failed but not with the expected authorization denial (expected UNAUTHORIZED/ErrorAccessDenied; observed {ErrorCode}/{BridgeErrorCode ?? "-"})"`.

### 3.4 Structured result shape (D5 analog)

```csharp
// src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidationResult.cs
internal sealed record ScopeBoundaryValidationResult(
    string InScopeMailbox,
    string OutOfScopeMailbox,
    bool InScopeAllowed,
    bool OutOfScopeDenied,
    bool Succeeded,
    string? FailureReason,
    MailboxProbeOutcome InScopeOutcome,   // D5 "InScopeDetails" analog
    MailboxProbeOutcome OutOfScopeOutcome // D5 "OutOfScopeDetails" analog
);
```

### 3.5 Orchestrating validator (thin I/O)

`ScopeBoundaryValidator` (`src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidator.cs`): injected `IMailboxScopeProbe` + `IOptions<ScopeValidationOptions>`. `ValidateAsync(CancellationToken)`:

1. Probes the in-scope mailbox, then the out-of-scope mailbox (deterministic order; both always executed — no short-circuit, so the log always carries both sides).
2. Returns `ScopeBoundaryEvaluator.Evaluate(...)`.

No logging and no verdict logic here; it is composition only.

### 3.6 Startup-command / fail-fast wiring recommendation

**Mechanism: a one-shot `IHostedService` (`ScopeBoundaryStartupValidator`), not `IStartupFilter` and not inline code in `Program.cs`.**

- `IStartupFilter` configures the middleware pipeline synchronously; it is the wrong seam for async I/O validation.
- Inline `await` between `builder.Build()` and `app.Run()` works but grows the host-bound `Program.cs` (whose uncovered lines are a real coverage cost per the coverage-exclusion policy) and is harder to unit-test.
- In ASP.NET Core minimal hosting, user-registered hosted services run `StartAsync` before the server begins accepting requests, and an exception thrown from `StartAsync` aborts host startup — exactly the master's "at startup … assert" fail-fast semantics, using the repository's established `AddHostedService` pattern (three existing workers).

`ScopeBoundaryStartupValidator.StartAsync`:

1. Calls `ScopeBoundaryValidator.ValidateAsync`.
2. **Logs the structured result** (master §13 Step 2 "log the result as part of startup validation") with a single structured message-template entry carrying every result field: `LogInformation` on success; `LogCritical` on failure including `FailureReason` and both outcome summaries (`Ok`/`ErrorCode`/`BridgeErrorCode` only — never tokens, never response bodies, consistent with the executor's logging guarantees).
3. On failure, throws `InvalidOperationException` naming the failure reason → host startup aborts (hard fail-fast, always; no soft mode). Rationale: master §13 Step 3 exists so nothing is built or run on a mis-scoped authorization model; a warn-and-continue mode would reintroduce the risk the check exists to remove. Transient-failure verdicts (`THROTTLED`/`TRANSPORT_FAILURE`) also fail startup — the operator or the container restart policy retries; startup validation must never report an unproven boundary as success.

`StopAsync` returns `Task.CompletedTask`. A separate CLI "validation command" is not recommended: the host is a web application with no command plumbing, and the hosted-service form satisfies §13 Step 3's "at startup **or** in a dedicated validation command" disjunction with less surface.

### 3.7 Configuration surface (new)

Section **`OpenClaw:ScopeValidation`** (options bag `ScopeValidationOptions`, pure validator `ScopeValidationOptionsValidator`, bound with `AddOptions/Bind/Validate/ValidateOnStart` — the exact `GraphAdapterOptions` pattern):

| Key | Type | Default | Env binding | Rule |
|---|---|---|---|---|
| `Enabled` | bool | `false` | `OpenClaw__ScopeValidation__Enabled` | Opt-in; all other rules apply only when true |
| `InScopeTestMailboxUpn` | string | `""` | `OpenClaw__ScopeValidation__InScopeTestMailboxUpn` | Required, non-whitespace (master §13 Step 1 `IN_SCOPE_TEST_MAILBOX`) |
| `OutOfScopeTestMailboxUpn` | string | `""` | `OpenClaw__ScopeValidation__OutOfScopeTestMailboxUpn` | Required, non-whitespace; must differ from in-scope (OrdinalIgnoreCase) (master §13 Step 1 `OUT_OF_SCOPE_TEST_MAILBOX`) |

Validator rules (full-list, key-naming, value-non-echoing style): disabled → always valid; enabled → both UPNs required and distinct. No timeout key: the probe inherits the Graph retry budget from `GraphAdapterOptions` (`MaxAttempts`/`MaxDelaySeconds`), keeping one retry policy for all Graph traffic.

**Registration** — `ScopeValidationServiceCollectionExtensions.AddScopeBoundaryValidation(services, configuration)` (`src/OpenClaw.Core/ScopeValidation/`), called unconditionally from `Program.cs` immediately after the backend-selection block:

- `OpenClaw:ScopeValidation:Enabled == false` → registers nothing (no-op; existing checkpoints/configs unaffected).
- Enabled with `OpenClaw:GraphAdapter:Enabled == false` → throws `InvalidOperationException` at composition time (the probe is Graph-only; the Stage-0 local backend has no RBAC boundary — a silently skipped validation would be a false sense of coverage; misconfiguration fails fast).
- Enabled with Graph enabled → binds+validates options, registers `AddHttpClient<GraphMailboxScopeProbe>` with `BaseAddress = GraphAdapterOptions.BaseUrl`, maps it to `IMailboxScopeProbe`, registers `ScopeBoundaryValidator` and `AddHostedService<ScopeBoundaryStartupValidator>`. `IAppTokenProvider`/`TimeProvider` are already registered by `AddGraphHostAdapterClient`/`Program.cs`.

`Program.cs` change is one line plus a comment (keeps the host-bound file minimal per the coverage-exclusion policy).

## 4. Behavior Semantics

- **Pass condition (exactly one):** in-scope probe `Ok == true` AND out-of-scope probe matches `UNAUTHORIZED` + `ErrorAccessDenied`.
- **Fail matrix (F11 D5 mirror):** (denied, denied), (allowed, allowed), (denied, allowed) → `Succeeded = false` with a `FailureReason` naming the failing side(s); (allowed, wrong-error) → failed with the observed classification quoted.
- **Ordering:** in-scope probe first, out-of-scope second; both always run.
- **Edge cases:**
  - Empty in-scope mailbox (zero messages): 200 with empty `value` → still allowed (probe asserts authorization, not content).
  - In-scope 403 immediately after provisioning: RBAC cache propagation is 30 minutes-2 hours (master §12 Step 7 note; Microsoft Learn Application RBAC "Limitations"); the runbook must state this and instruct a re-run, since only `Test-ServicePrincipalAuthorization` bypasses the cache.
  - Out-of-scope mailbox does not exist: 404 → classified as wrong-error failure, not denial. The runbook must require an existing, real out-of-scope mailbox (F11 §12 Step 8 handoff already requires one).
  - Throttling/transport failure on either side: verdict is failed-with-reason; startup aborts; rerun.
  - Cancellation: `CancellationToken` flows through to the executor; startup shutdown token cancels cleanly.

## 5. Requirements Mapping (proposed file changes)

Production (all `src/OpenClaw.Core`, T1; every file well under the 500-line cap):

| File | Content |
|---|---|
| `src/OpenClaw.Core/ScopeValidation/MailboxProbeOutcome.cs` | Outcome record |
| `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidationResult.cs` | Result record |
| `src/OpenClaw.Core/ScopeValidation/IMailboxScopeProbe.cs` | Probe port |
| `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryEvaluator.cs` | Pure classifier + evaluator |
| `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptions.cs` | Options bag |
| `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptionsValidator.cs` | Pure options validator |
| `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidator.cs` | Two-probe orchestration |
| `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryStartupValidator.cs` | One-shot IHostedService, logging + fail-fast |
| `src/OpenClaw.Core/ScopeValidation/ScopeValidationServiceCollectionExtensions.cs` | DI registration + Graph-enabled guard |
| `src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs` | Graph probe via `GraphRequestExecutor` |
| `src/OpenClaw.Core/Program.cs` | One-line `AddScopeBoundaryValidation` call |

Tests mirror under `tests/OpenClaw.Core.Tests/ScopeValidation/` and `tests/OpenClaw.Core.Tests/CloudGraph/` (section 6). Docs: runbook under this feature folder (section Automation Feasibility). No change to `OpenClaw.HostAdapter.Contracts`, no change to `MessagePollingWorker` (#117 stays queued), no new package dependencies (everything used — options, hosted services, HTTP client factory — is already referenced).

## 6. Testing Implications (strategy only; no test code here)

Framework: MSTest + Moq + FluentAssertions + CsCheck + `FakeTimeProvider` + the existing `FakeHttpHandler`, matching `tests/OpenClaw.Core.Tests/CloudGraph/` conventions. All per-commit tests are fully mocked; no network.

1. **Pure classifier (`IsAuthorizationDenial`)** — `[DataTestMethod]` rows for the full classification table in section 3.3 (real denial → true; 401-shaped `UNAUTHORIZED` with null/`InvalidAuthenticationToken` BridgeErrorCode → false; `NOT_FOUND`/`THROTTLED`/`TRANSPORT_FAILURE`/`INTERNAL_ERROR`/`CONFIGURATION_ERROR` → false; `Ok == true` → false; case-sensitivity of both constants).
2. **Pure evaluator (`Evaluate`)** — the four-cell matrix: (allowed, denied) → `Succeeded = true`, `FailureReason = null`; (denied, denied), (allowed, allowed), (denied, allowed) → `Succeeded = false` with the precise reason string(s), including the joined both-sides case.
3. **Property tests (T1: >= 1 per pure function, CsCheck):**
   - For any generated `MailboxProbeOutcome`, `IsAuthorizationDenial` returns true only when all three conjuncts hold (never true when `BridgeErrorCode != "ErrorAccessDenied"`).
   - For any outcome pair, `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason is null == Succeeded`.
   - Options validator: `Enabled == false` implies zero violations for any field values.
4. **Contract tests at the Graph boundary (`GraphMailboxScopeProbe` + `FakeHttpHandler`):** request shape (`users/{escaped-upn}/messages?$top=1&$select=id`, UPN escaping, bearer header, `client-request-id`); 200-with-empty-list → `Ok = true`; Graph 403 error body `{"error":{"code":"ErrorAccessDenied",...}}` → outcome `(false, "UNAUTHORIZED", "ErrorAccessDenied", ...)`; 401 body → `UNAUTHORIZED` with the 401 graph code; unparseable error body → null BridgeErrorCode. These double as the host-service-boundary contract tests required by `general-unit-test.md`.
5. **Orchestration (`ScopeBoundaryValidator`, mocked `IMailboxScopeProbe`):** both probes always invoked with the configured mailboxes (in-scope first), outcomes composed into the result verbatim.
6. **Startup service (`ScopeBoundaryStartupValidator`):** success → completes and logs the structured result; failure → throws with `FailureReason` in the message and logs at critical; cancellation propagates.
7. **DI/selection tests (pattern: `GraphBackendSelectionTests`):** disabled → no `IMailboxScopeProbe`/hosted service registered; enabled without Graph → composition-time throw; enabled with Graph → all services resolvable; options validator wired with `ValidateOnStart`.
8. **Mutation (T1):** Stryker in pre-merge/nightly; the classifier's string constants and boolean conjunctions are the primary mutation surface — the classification-table rows are designed to kill operator/constant mutants.

Coverage: all new logic is host-neutral and mocked-testable; the only thin host-bound lines are the one-line `Program.cs` call and `StartAsync` wiring (directly unit-testable since the service is a plain class).

## 7. Constraints & Risks

- **Denial-code drift:** if a live tenant surfaces a different Graph `error.code` for the RBAC denial than `ErrorAccessDenied`, validation fails closed with the observed code quoted in `FailureReason`; the runbook's verification step captures the observed code, and the fix is a change to one named constant plus its pinned tests. Fail-closed is the correct failure direction for a security boundary check.
- **RBAC propagation cache (30 min-2 h)** can produce a legitimate transient in-scope denial right after provisioning; runbook re-run guidance mitigates.
- **Startup cost:** two Graph reads with the existing retry budget; runs only when explicitly enabled; no effect on the default (local/Stage-0) configuration.
- **Floor signal:** the feature touches the auth/token path indirectly (`auth_or_token_handling` complexity floor signal noted in the spec) but adds no new credential handling; the probe consumes the existing `IAppTokenProvider`.

## Automation Feasibility

Required assessment of every tenant-dependent step (no Azure/Exchange credentials exist in this environment or in CI; no CI job may attempt a live Graph call).

| # | Step | Classification | Response | Evidence / runbook |
|---|---|---|---|---|
| AF-1 | Authoring the validator, probe, options, startup service, and all mocked tests | Fully automatable in-repo | Normal feature work; per-commit suite | Mocked MSTest/CsCheck tests per section 6 |
| AF-2 | Executing the startup validation against a live tenant (real in-scope + out-of-scope mailboxes, RBAC roles applied per F11, CloudAuth credentials configured) | Automatable-with-credentials; **not runnable here or in CI** | `human_interaction` requirement with `response: exception` and a runbook (mirrors F11 HI-1) | New runbook: `docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md` (to be authored in this feature per `.claude/skills/human-exception-runbook/SKILL.md`: Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation) |
| AF-3 | Confirming the tenant-observed Graph `error.code` for the out-of-scope denial equals `ErrorAccessDenied` | Automatable-with-credentials; human-verified | Folded into the AF-2 runbook's Verification section (record the observed `FailureReason`/log fields; escalate a divergence as a one-constant fix) | Same runbook |

Runbook prerequisites (AF-2): F11 provisioning executed per `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (HI-1), including the §12 Step 8 handoff package naming one in-scope and one existing out-of-scope mailbox; `OpenClaw:CloudAuth` credentials configured; `OpenClaw:GraphAdapter:Enabled=true`; `OpenClaw:ScopeValidation` keys set. Verification: the application starts (boundary holds) and the structured `LogInformation` entry shows `Succeeded=true`; the negative rehearsal (temporarily pointing `InScopeTestMailboxUpn` at an out-of-scope mailbox) aborts startup with the logged reason. The RBAC propagation window (30 minutes-2 hours; Microsoft Learn Application RBAC "Limitations", `updated_at` 2026-03-16) must be stated with re-run guidance. The gap-analysis Automation Feasibility table (`docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, "`Test-ServicePrincipalAuthorization` positive/negative smoke test" row) already prescribes exactly this split: mocked decision-logic tests in the per-commit suite, live verification as an `exception` with a runbook.

Orchestrator obligation: record the AF-2/AF-3 requirement in `artifacts/orchestration/orchestrator-state.json` `human_interaction.requirements[]` with `response: "exception"` and the runbook path above, satisfying the `.claude/rules/orchestrator-state.md` invariants (exception requires non-empty `runbook_path`).

## Rejected Alternatives (summary)

- **3a (two adapter instances):** bespoke construction of internal types outside DI; DTO-mapping failure modes contaminate the boundary verdict.
- **3b (mailbox parameter on `IHostAdapterClient`):** T2 contract breaking change; unimplementable on the Stage-0 local backend; probe is not an agent capability.
- **Inline `Program.cs` validation / `IStartupFilter`:** grows uncovered host-bound code / wrong seam for async I/O (section 3.6).
- **Soft-fail (log-only) mode:** contradicts master §13 Step 3's purpose; fail-closed only.
- **`mailboxSettings` as the harmless read:** proves `MailboxSettings.Read`, not the primary `Mail.Read` boundary the ingestion path depends on.
- **Configurable expected-denial code:** unnecessary surface; a named constant with pinned tests is simpler and safer.

## References

- `docs/open-claw-approach.master.md` §12 Steps 7-8 (lines 1411-1447), §13 Steps 1-3 (lines 1453-1486).
- `docs/features/active/2026-07-02-exchange-rbac-scripts-111/spec.md` (D5 result shape, HI-1 precedent) and `.../runbooks/exchange-rbac-setup.runbook.md` (verified present).
- `docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md` (F13 adapter contract, D5 error matrix).
- `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` items 17 and the Automation Feasibility table.
- Code: `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`, `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs` (MapStatusError lines 226-247), `GraphHostAdapterClient.cs`, `GraphAdapterOptions(.Validator).cs`, `GraphServiceCollectionExtensions.cs`, `src/OpenClaw.Core/Program.cs`, `src/OpenClaw.Core/MessagePollingWorker.cs` (line 158), `quality-tiers.yml`.
- Microsoft Learn, "Role Based Access Control for Applications in Exchange Online" (`https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac`, `updated_at` 2026-03-16): scoped application roles, `Test-ServicePrincipalAuthorization` semantics, 30-minute-2-hour permission-cache limitation, nested-group exclusion. The article does not state the wire-level denial error code; the `ErrorAccessDenied` expectation is the caller-established fact, verified in-tenant per AF-3.
