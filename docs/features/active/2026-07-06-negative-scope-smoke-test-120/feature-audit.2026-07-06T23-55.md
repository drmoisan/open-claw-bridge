# Feature Audit: negative-scope-smoke-test (#120)

**Audit Date:** 2026-07-06
**Feature:** Application-runtime negative-scope smoke test (F17, Epic openclaw-vision Epic C) — opt-in startup validation proving the Exchange Online Application RBAC scope boundary (in-scope read allowed, out-of-scope read denied with the exact RBAC-denial shape) with hard fail-fast on any failure.

## Scope and Baseline

- **Branch:** `feature/negative-scope-smoke-test-120` @ head `99614291f2d81693e3e36e0a302b223d2849e58e`
- **Resolved base branch:** `epic/openclaw-vision-integration` (supplied by the caller; reviewer-confirmed local and origin refs resolve the identical merge-base)
- **Merge-base:** `d67dea0117984b980b093f1c942c9a4762b8b25f` (2026-07-06T22:49:06-04:00)
- **Diff:** 41 files, +3355/-3 — 10 new production `.cs`, 2 modified production files (`Program.cs`, `OpenClaw.Core.csproj`), 9 new test `.cs`, 20 Markdown (feature docs/evidence + 2 agent-memory records)
- **Work mode:** `full-feature` (persisted `- Work Mode: full-feature` marker in `issue.md`); acceptance-criteria sources are `spec.md` **and** `user-story.md` (the two files carry an identical nine-item AC list; each is tracked independently per `acceptance-criteria-tracking`)
- **Evidence:** reviewer-independent toolchain re-run at branch head (format, build+analyzers+nullable, architecture suites, full tests, coverage under two instrumentation modes); details and per-file coverage in `policy-audit.2026-07-06T23-55.md` Sections 5-7; PR context regenerated from the authoritative git range into `artifacts/pr_context.summary.txt` / `artifacts/pr_context.appendix.txt`.

## Acceptance Criteria Inventory

Nine acceptance criteria, identical in `spec.md` (## Acceptance Criteria) and `user-story.md` (## Acceptance Criteria). AC-1 through AC-8 were checked off (`[x]`) by the executor; AC-9 was unchecked (`[ ]`) at review start.

1. AC-1 — Pure, host-neutral `ScopeBoundaryEvaluator` in `src/OpenClaw.Core/ScopeValidation/` (no I/O/clock/logging/CloudGraph dependency); `Evaluate(...)` returns the eight-field `ScopeBoundaryValidationResult` with `Succeeded == InScopeAllowed && OutOfScopeDenied` and `FailureReason` null iff `Succeeded`.
2. AC-2 — `IsAuthorizationDenial` accepts exactly `!Ok && Code == "UNAUTHORIZED" && BridgeErrorCode == "ErrorAccessDenied"` (Ordinal); unit tests pin the full classification matrix (one true row; every other code, 401 shapes, Ok==true false).
3. AC-3 — Unit tests pin the four-cell pair-evaluation matrix with precise reason strings, including the `"; "`-joined both-sides case and wrong-error quoting.
4. AC-4 — Narrow port `IMailboxScopeProbe` implemented by `GraphMailboxScopeProbe` in `CloudGraph/`, reusing the `GraphRequestExecutor` pipeline, issuing `GET users/{escaped-upn}/messages?$top=1&$select=id`; F13 contract and `OpenClaw.HostAdapter.Contracts` unmodified.
5. AC-5 — Contract tests via `FakeHttpHandler` verify request shape (path, escaping, `$top=1&$select=id`, bearer, `client-request-id`) and the envelope-to-outcome projection rows (200 empty value; 403 ErrorAccessDenied; 401; unparseable body → null BridgeErrorCode).
6. AC-6 — One-shot `IHostedService` probes in-scope first, out-of-scope second (both always), logs a single structured entry with every field (Information/Critical, never tokens/bodies), throws `InvalidOperationException` naming the reason; tests cover success/failure/cancellation and validator composition.
7. AC-7 — `OpenClaw:ScopeValidation` section bound with `AddOptions/Bind/Validate/ValidateOnStart`; enabled requires both UPNs non-whitespace and distinct (OrdinalIgnoreCase); disabled registers nothing; enabled-without-Graph throws at composition time; DI tests cover all three branches.
8. AC-8 — T1 rigor: mocked-only per-commit suite; >= 1 CsCheck property per pure function; line >= 85% / branch >= 75% with no changed-line regression; zero `dynamic`; files < 500 lines; mirrored test layout; mutation (Stryker >= 75%) scheduled nightly/pre-merge.
9. AC-9 — Live-tenant verification recorded as a `human_interaction` requirement (`response: exception`, `runbook_path` = the committed runbook) in orchestrator state; the runbook covers prerequisites, pass and negative-rehearsal procedures, propagation-cache re-run guidance, and denial-code confirmation.

## Acceptance Criteria Evaluation

| AC | Criterion (abbreviated) | Verdict | Evidence |
|----|--------------------------|---------|----------|
| AC-1 | Pure host-neutral evaluator + result invariants | **PASS** | Reviewer read: `ScopeBoundaryEvaluator.cs` is `internal static`, clock/I/O/logging/DI-free with only BCL usings; the D1 independence from CloudGraph/HTTP/logging is pinned by 3 NetArchTest rules (`ScopeValidationArchitectureBoundaryTests`, in the 781/781 pass). `ScopeBoundaryValidationResult` carries all eight fields. The invariants are enforced in code (`succeeded = inScopeAllowed && outOfScopeDenied`; ternary null reason) and property-tested universally (`Evaluate_SucceededAndFailureReasonInvariantsHold`). File coverage 56/56 line, 16/16 branch. |
| AC-2 | Exact fail-closed denial classifier + full matrix pinned | **PASS** | Implementation uses `StringComparison.Ordinal` on both named constants with all three conjuncts. Tests: the real 403 row true; 401-null and 401-`InvalidAuthenticationToken` false; six-code DataRow matrix (`CONFIGURATION_ERROR`, `NOT_FOUND`, `THROTTLED`, `TRANSPORT_FAILURE`, `INVALID_REQUEST`, `INTERNAL_ERROR`) false; `Ok == true` false; both case-variant constants false (pinning Ordinal). CsCheck property asserts true-iff-all-three-conjuncts over a generated space including the case variants and null. Wrong-error pairs fail the boundary with the precise reason (AC-3 evidence). |
| AC-3 | Pair-evaluation matrix with precise reasons | **PASS** | `ScopeBoundaryEvaluatorTests`: (allowed, denied) → `Succeeded=true`, null reason; (denied, denied) → in-scope reason only, join-absence asserted; (allowed, allowed) → exact scope-leak string; (denied, allowed) → exact `"; "`-joined both-sides string; (allowed, wrong-error) → exact string quoting `NOT_FOUND/ErrorItemNotFound`; null-BridgeErrorCode renders `-`. All assertions are exact `Be(...)` string matches. |
| AC-4 | Probe port + Graph implementation reusing the F13 pipeline; contract untouched | **PASS** | `IMailboxScopeProbe.ProbeMailboxReadAsync(mailboxUpn, requestId, ct)` implemented by `GraphMailboxScopeProbe` in `CloudGraph/`, constructing `GraphRequestExecutor` with the same five seams as `GraphHostAdapterClient` (bearer, `client-request-id`, retry/backoff, D5 mapping inherited); URL is `users/{Uri.EscapeDataString(upn)}/messages?$top=1&$select=id`. Reviewer-verified empty diff for `src/OpenClaw.HostAdapter.Contracts` and no `IHostAdapterClient` change anywhere in the diff. |
| AC-5 | Graph-boundary contract tests | **PASS** | `GraphMailboxScopeProbeTests` (8 tests, `FakeHttpHandler`): GET path with `$top=1`/`$select=id`; reserved-character UPN percent-encoding (`test%20user%2Btag%40contoso.com`); Bearer header value and single `client-request-id`; 200 empty `value` → `Ok=true`; 403 `ErrorAccessDenied` body → `(false, "UNAUTHORIZED", "ErrorAccessDenied", ...)`; 401 body → `UNAUTHORIZED` + `InvalidAuthenticationToken`; unparseable HTML error body → null `BridgeErrorCode`; cancellation flow-through. File coverage 37/37 line, 6/6 branch (async body instrumented). |
| AC-6 | Hosted-service semantics + composition tests | **PASS** | `ScopeBoundaryStartupValidator` registered via `AddHostedService` only on the enabled path; logs one entry (Information on pass, Critical on fail with `FailureReason`), summaries limited to Ok/ErrorCode/BridgeErrorCode, throws `InvalidOperationException` naming the reason. Tests cover success (single entry, every field name asserted), failure (single Critical entry + throw message), token/body log-absence, pre-cancelled-token propagation, and `StopAsync`. `ScopeBoundaryValidatorTests` pin both-probes-invoked with configured mailboxes, in-scope-first order, no short-circuit, verbatim composition (`BeSameAs`), and token flow to both calls. |
| AC-7 | Config section, ValidateOnStart, three DI branches | **PASS** | `ScopeValidationOptions` (`Enabled` default false, two UPN keys) bound via `AddOptions/Bind/.Validate(...).ValidateOnStart()`; validator rules: both UPNs required non-whitespace when enabled, distinct OrdinalIgnoreCase (case-variant equality rejected — tested), disabled always valid (directed + property tests). DI tests cover disabled and absent-section (nothing registered), enabled-without-Graph (composition-time `InvalidOperationException`), enabled-with-Graph (probe/validator/hosted service resolvable, options bound per key), plus `OptionsValidationException` fail-closed DataRows and null guards. |
| AC-8 | T1 rigor bundle | **PASS** | Mocked-only suite (Moq + `FakeHttpHandler`; in-memory configuration; zero network/temp-file/sleep/wall-clock usages — reviewer grep). Property density: 4 genuine CsCheck properties covering all three pure functions (`IsAuthorizationDenial`, `Evaluate`, options `Validate`). Coverage: every instrumented new file 100.00% line and 100.00% branch under full instrumentation (async bodies measured); OpenClaw.Core package 92.82%/81.40% vs baseline 92.49%/80.90% — no regression; changed Program.cs lines covered. Zero `dynamic`. All files under 500 lines (new production max 124). Tests mirror `src/` under `tests/`. Mutation: nightly/pre-merge stage per policy (same disposition as the #80-#117 T1 audits); the exact-string matrix tests are the mutant-killing rows. |
| AC-9 | HI-1 `human_interaction` exception + runbook content | **PASS** | Reviewer independently read `artifacts/orchestration/orchestrator-state.json`: `human_interaction.requirements[]` contains exactly one entry (HI-1) with `response: "exception"` and `runbook_path` exactly equal to `docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md`; the file exists on the branch and the block satisfies all three `human_interaction` invariants in `.claude/rules/orchestrator-state.md` (F11 HI-1 precedent, #111 review pattern). Runbook content verified: Cue; Prerequisites (F11 provisioning + Step 6 boundary check, roles, two existing test mailboxes, CloudAuth credential guidance, Graph adapter enabled); Step 2 ScopeValidation keys; Step 3 pass procedure; Step 4 negative rehearsal; Step 5 propagation-cache (30 min-2 h) re-run guidance; Step 6 denial-code (`ErrorAccessDenied`) confirmation and drift handling; Verification; Source and Citation. Checked off by this review in both AC source files (see Check-off below). |

## Summary

- **All nine acceptance criteria: PASS.** The delivered code matches the recorded design (D1-D8) precisely; the reviewer independently re-ran the full toolchain and re-measured per-file coverage at branch head, confirming the executor's committed figures exactly.
- **Non-goals honored:** `IHostAdapterClient`/`OpenClaw.HostAdapter.Contracts` unmodified; `MessagePollingWorker` unmodified (#117 gap stays queued); no soft-fail mode; no CLI command; no configurable denial code; no `mailboxSettings` probe; Stage-0 local backend not validated (composition-time error instead).
- **Findings:** zero Blocking, zero Major. One Minor (CR-120-01: public accessibility of the options/validator/extension trio vs the spec's "all internal" prose — pattern-consistent with F13, textual divergence) and three Info notes in `code-review.2026-07-06T23-55.md`.
- **Remediation:** not required. No `remediation-inputs` artifact was produced.
- **Go/No-Go:** **Go** — ready for PR against `epic/openclaw-vision-integration`.

## Acceptance Criteria Check-off

Work mode `full-feature`: authoritative AC sources are `spec.md` and `user-story.md`, tracked independently.

- AC-1 .. AC-8: already checked off (`[x]`) by the executor in both `spec.md` and `user-story.md`; reviewer verification above confirms each; no source edit needed.
- AC-9: evaluated **PASS** by this review (orchestrator-state record + runbook independently verified); newly checked off by the reviewer in **both** `spec.md` and `user-story.md` (change `- [ ]` → `- [x]`, criterion text preserved).

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-06-negative-scope-smoke-test-120/spec.md` and `docs/features/active/2026-07-06-negative-scope-smoke-test-120/user-story.md`
- Total AC items: 9 (per source file; identical lists)
- Checked off (delivered): 9
- Remaining (unchecked): 0
- Items remaining: none
