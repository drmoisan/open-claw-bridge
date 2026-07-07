# Code Review: negative-scope-smoke-test (#120)

**Review Date:** 2026-07-06
**Branch:** `feature/negative-scope-smoke-test-120` @ `99614291f2d81693e3e36e0a302b223d2849e58e` vs base `epic/openclaw-vision-integration` @ merge-base `d67dea0117984b980b093f1c942c9a4762b8b25f`
**Scope:** 10 new production C# files (9 `ScopeValidation/` + `CloudGraph/GraphMailboxScopeProbe.cs`), 2 modified production files (`Program.cs`, `OpenClaw.Core.csproj`), 9 new test files, 20 Markdown docs. No Python, PowerShell, Bash, or TypeScript changes.

## Executive Summary

The implementation is small, layered, and faithful to the recorded design (spec D1-D8). The security-relevant decision logic — recognizing a genuine Exchange RBAC denial — is a pure static function with Ordinal comparisons on two named constants, deliberately fail-closed, and pinned by an exhaustive directed matrix plus CsCheck properties; the Graph-bound probe adds no new HTTP/auth/retry code, delegating to the existing F13 executor so the D5 error semantics the classifier depends on are production-identical by construction. Startup behavior is hard fail-fast with a single structured log entry that never carries tokens or response bodies (asserted by test). The F13 contract project and `MessagePollingWorker` are untouched. Zero Blocking and zero Major findings; one Minor accessibility divergence from the spec prose and three informational notes.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Minor | src/OpenClaw.Core/ScopeValidation/ScopeValidationServiceCollectionExtensions.cs (also ScopeValidationOptions.cs, ScopeValidationOptionsValidator.cs) | Type declarations (lines 20, 10, 11 respectively) | CR-120-01: Three types are `public` while spec.md "API / CLI Surface" states "No public API ... All new types are `internal`". The only caller is the same-assembly `Program.cs`. | Either tighten the three types to `internal` in a follow-up (no external callers exist; DI and options binding work with internal types inside one assembly) or amend the spec sentence to sanction the public trio, matching how F13 documented `GraphAdapterOptions`/`GraphAdapterOptionsValidator`/`AddGraphHostAdapterClient`. | Keep the spec and the shipped public surface consistent so future contract audits do not have to re-derive the intent; the pattern itself is the established repo idiom (#115 precedent), so this is textual, not functional. | Reviewer read of the three files vs spec.md "API / CLI Surface"; `#115` policy audit Section 2.3 sanctioning the identical public trio for F13. |
| Info | src/OpenClaw.Core/OpenClaw.Core.csproj | Lines 12-16 | CR-120-02: `InternalsVisibleTo("DynamicProxyGenAssembly2")` grants internals access to Castle DynamicProxy so Moq can proxy the internal `IMailboxScopeProbe`. Because OpenClaw.Core is not strong-named, any assembly named `DynamicProxyGenAssembly2` gains internals visibility. | No action required. This is the standard Moq-internal-interface technique and is documented by an in-file comment. If the assembly is ever strong-named, add the well-known DynamicProxyGenAssembly2 public key to the attribute. | The alternative (making the port public) would widen the real API surface more than the test-only grant does; the risk is theoretical (an attacker who can load an assembly into the process already has broader capabilities). | Reviewer read of the csproj diff; Moq/Castle documented convention. |
| Info | src/OpenClaw.Core/ScopeValidation/ScopeBoundaryStartupValidator.cs | Lines 52-86 | CR-120-03: The success and failure log calls duplicate the same nine-placeholder message-template body, differing only in the leading verdict word and level. | Optional: extract the shared template suffix to a private const if the field list ever grows. Not worth churn at the current size. | Structured-logging message templates must be constant strings for template caching, so some duplication is inherent; the duplication is contained in one file and both branches are fully covered by tests. | Reviewer read; both branches at 100% line/branch coverage in the fresh cobertura. |
| Info | src/OpenClaw.Core/Program.cs | Whole file (364 lines) | CR-120-04: Under full (plain) instrumentation the composition root reports 56.06% branch coverage — entirely pre-existing endpoint-lambda/opt-in code from other features; this branch's changed lines (69-72) are covered in both instrumentation modes and measure 100%/100% under the repo-convention run used by prior audits. | No action for this branch. The standing guidance to keep the host-bound file minimal already applies; this feature added exactly one call plus a comment. | The uniform modified-file gate is graded on the measurement convention prior accepted audits used (runsettings), under which Program.cs is 258/258 line and 6/6 branch; the plain-run figure is disclosed for transparency per the evidence-first rule. | Reviewer cobertura parses under `evidence/qa-gates/coverage-review/` (plain: 264/304, 37/66; missed lines all outside 69-74) and `coverage-review-settings/` (258/258, 6/6). |

## Implementation Audit

### C# implementation audit

#### What changed well

- **Fail-closed classifier with exactly one accepted shape.** `IsAuthorizationDenial` requires all three conjuncts (`!Ok`, `ErrorCode == "UNAUTHORIZED"`, `BridgeErrorCode == "ErrorAccessDenied"`) with `StringComparison.Ordinal`; every ambiguous class (401 auth fault, throttle, transport, not-found, unmapped) fails the boundary with a precise reason instead of a false pass. The 401/403 fold in the F13 executor is documented in the XML doc as the reason `BridgeErrorCode` is the only discriminator.
- **Pipeline reuse instead of duplication.** `GraphMailboxScopeProbe` constructs the existing `GraphRequestExecutor` with the same five seams as `GraphHostAdapterClient`, so token acquisition, `client-request-id`, retry/backoff, and the D5 error matrix are inherited unchanged; the probe adds only URL composition and a four-field projection. The success parser is `static _ => true`, eliminating the DTO-mapping failure mode from the boundary verdict (spec D2 rationale, honored in code).
- **No-short-circuit two-probe orchestration.** `ScopeBoundaryValidator.ValidateAsync` always runs both probes in a deterministic order so the startup log always carries both sides; the test suite pins the order and the no-short-circuit behavior with recorded call order.
- **Hard fail-fast startup.** `ScopeBoundaryStartupValidator.StartAsync` logs exactly one structured entry (Information/Critical) carrying every result field and throws `InvalidOperationException` naming the `FailureReason` on any non-success — no warn-and-continue mode exists, matching the user-story requirement that transient failures also abort.
- **Three-branch registration with composition-time guard.** Disabled/absent registers nothing (default configuration behavior preserved — pinned by tests); enabled-without-Graph throws at composition time with an actionable message; the enabled path binds options with `Validate` + `ValidateOnStart` in the exact `GraphAdapterOptions` pattern.

#### Type safety and API notes

- Nullable annotations are honest: the outcome record's three error fields are nullable and the null/`"-"` substitution happens only at the message-formatting site; `FailureReason` nullability encodes the null-iff-Succeeded invariant, property-tested.
- All decision types are `internal`; the public surface is the options/validator/extension trio (Minor CR-120-01 above regarding the spec prose).
- Zero `dynamic`, zero suppressions, zero null-forgiving operators in the new production files (reviewer grep).

#### Error handling and logging

- The probe never throws on Graph failure — failures arrive as envelope data, so there are zero `catch` blocks in the new code; the only throw sites are argument guards, the composition-time Graph guard, and the intentional startup abort.
- Outcome summaries in the log are limited to `Ok`/`ErrorCode`/`BridgeErrorCode` via the private `Summarize` helper; a dedicated test asserts the rendered log contains neither the bearer token nor response-body markers.

## Test Quality Audit

### Reviewed test and QA artifacts

- 9 new test files (55 methods, 65 runtime cases) reviewed line-by-line; executor evidence `evidence/baseline/phase0-baseline-*.md` and `evidence/qa-gates/final-qa-*.md` cross-checked against the reviewer's independent re-runs.
- Reviewer re-ran the full toolchain (format, build, architecture, tests, coverage twice) at branch head; all results in `policy-audit.2026-07-06T23-55.md` Sections 5-7.

### Quality assessment

- **Matrix completeness:** the D3 classification matrix is exhaustively pinned (one true row; 401-null, 401-InvalidAuthenticationToken, six other codes, Ok==true, and both case variants false), and the D4 pair matrix covers all four cells plus the both-sides join and wrong-error quoting with exact string assertions — these are the mutant-killing rows the nightly Stryker stage depends on.
- **Property tests are genuine:** generators enumerate a vocabulary deliberately spanning the accepted constants, case variants, the 401 code, and null, so the conjunct-iff property exercises every classifier branch; the evaluator invariant pair and options-validator determinism/disabled properties are true universally-quantified statements, not re-labeled examples.
- **Determinism:** pure functions are clock-free; the probe tests use `FakeTimeProvider`; no sleeps, timers, temp files, or live endpoints anywhere (grep-verified).
- **Contract tests at the right boundary:** the probe suite asserts the outgoing request (path, escaping, `$top`/`$select`, bearer, `client-request-id`) and the envelope projection against `FakeHttpHandler`, exactly the seam the live tenant will exercise; the unparseable-body and 401-vs-403 rows guard the classifier's discriminator assumptions.
- **No weakened or modified existing tests:** the test diff is purely additive (reviewer-verified).

## Security / Correctness Checks

- **Boundary check cannot be fooled by unrelated failures:** verified by the classification matrix and by the fail-closed wrong-error reason path; a 404 (deleted out-of-scope mailbox) or 401 (expired credential) aborts startup with the observed classification quoted rather than passing.
- **No secret material in logs:** token never leaves the executor (pre-existing guarantee); the startup log's outcome summaries carry three classification fields only; test-asserted.
- **No new credential handling:** auth flows through the existing `IAppTokenProvider`; the feature adds no secret storage, no new configuration of credential material, and no `.env` files.
- **Default-path neutrality:** disabled/absent section registers nothing (tested); existing configurations are unaffected.
- **Architecture boundaries:** 3 new NetArchTest rules pin the pure core's independence from CloudGraph/HTTP/logging; no COM, VSTO, or Outlook-interop references (rules 1-7 of `architecture-boundaries.md` inapplicable or satisfied — mailbox access is Microsoft Graph only).

## Research Log

- Compared the probe's constructor seams and executor construction against `GraphHostAdapterClient.cs` (identical five-seam shape; field convention matches).
- Verified `quality-tiers.yml` maps `OpenClaw.Core` to T1 and that CsCheck 4.7.0 plus six pre-existing `*PropertyTests` classes establish the property-test harness in `OpenClaw.Core.Tests`.
- Confirmed via `git diff --name-only` that `OpenClaw.HostAdapter.Contracts` and `MessagePollingWorker` are untouched (spec D1/D8 non-goals).
- Confirmed the HI-1 `human_interaction` record in `artifacts/orchestration/orchestrator-state.json` (response `exception`, matching `runbook_path`) and that the runbook contains Cue, Prerequisites, Step-by-step (pass + negative rehearsal + propagation-cache guidance + denial-code capture), Verification, and Source and Citation sections.
- Re-measured per-file line and branch coverage from two fresh cobertura runs (plain and runsettings-convention), confirming the executor's committed per-file figures exactly and confirming — by inspecting instrumented line numbers — that all async bodies were measured on this branch (no CompilerGenerated masking, unlike #99-#117).

## Verdict

**Approve — Go for PR.** Zero Blocking and zero Major findings. One Minor (CR-120-01, spec-prose vs public accessibility of the options/validator/extension trio) and three informational notes; none require remediation before merge. Toolchain, coverage, architecture, and security checks all pass at branch head `9961429`.
