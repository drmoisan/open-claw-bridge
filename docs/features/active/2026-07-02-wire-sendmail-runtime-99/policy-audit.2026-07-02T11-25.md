# Policy Compliance Audit: wire-sendmail-runtime (#99)

**Audit Date:** 2026-07-02
**Code Under Test:** C# only. 3 modified production `.cs` files in `src/OpenClaw.Core` (`Agent/Contracts/SendMailRequest.cs` doc-comment-only; `Agent/Runtime/HostAdapterSchedulingService.cs` replacing the throwing `SendMailAsync` with the delegating implementation; `Agent/Runtime/SchedulingDtoMapper.cs` adding `MapSendMailRequest` and `MapRecipients`), 3 modified test `.cs` files and 1 new test `.cs` file (`tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperPropertyTests.cs`, 107 lines, CsCheck property tests). Plus feature scoping/evidence Markdown (feature folder for issue #99) and agent-memory Markdown. No Python, PowerShell, TypeScript, Bash, or governed JSON files changed in the branch diff.

**Scope:** Full feature branch `feature/wire-sendmail-runtime-99` @ `d8a08879cacf23e8376e9ad4ed64c9b1a421a7d5` versus resolved base `main` @ merge-base `13f6f9390cbb634abca0c36eb7cdabe4acc2830e` (origin/main; the local `main` ref is stale per the caller inputs). Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-only): 7 `.cs`, 24 `.md` (31 files, +1117/-28). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 3 production `.cs` + 4 test `.cs` | 676 (solution) / 224 (Core.Tests) | 671 pass, 0 fail, 5 env-gated skips | 90.51% line, 79.95% branch (pooled solution) | 90.56% line, 80.05% branch (pooled solution) | SchedulingDtoMapper.cs 96.30% line / 87.23% branch with all new lines and both new branch points covered; HostAdapterSchedulingService.cs 100% of instrumented lines (async body uninstrumented per pre-existing runsettings attribute exclusion, behaviorally covered by 5 delegation tests); SendMailRequest.cs doc-only |

**Note:** Python, PowerShell, Bash, TypeScript, and governed-JSON rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/baseline/baseline-test-coverage.2026-07-02T10-54.md` (pooled 90.51% line / 79.95% branch; `OpenClaw.Core` package 98.61% line / 91.69% branch)
- C# post-change coverage artifact: `docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/qa-gates/final-qa-test-coverage.2026-07-02T11-20.md` and `evidence/qa-gates/coverage-comparison.2026-07-02T11-23.md` (pooled 90.56% line / 80.05% branch; `OpenClaw.Core` package 98.63% line / 91.82% branch)
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/qa-gates/coverage-review/{4d307440...,6931c62b...,8f31b492...}/coverage.cobertura.xml`; independently parsed pooled 90.56% line (4174/4609) / 80.05% branch (935/1168), identical to executor evidence. Reviewer evidence: `docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/qa-gates/coverage-review.2026-07-02T11-25.md`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura. The C# coverage gate is met (pooled line 90.56% >= 85%, branch 80.05% >= 75%; the materially modified mapper file is at 96.30% line / 87.23% branch; every instrumented changed line is covered; no regression — pooled coverage improved by +0.05pp line / +0.10pp branch).

---

## Executive Summary

This feature branch closes issue #99 (gap F5): `HostAdapterSchedulingService.SendMailAsync` no longer throws the stale `NotSupportedException` citing closed issues #74/#75; it now null-guards the request, maps the agent-side `OpenClaw.Core.Agent.SendMailRequest` to the wire `OpenClaw.HostAdapter.Contracts.SendMailRequest` via the new pure `SchedulingDtoMapper.MapSendMailRequest`, awaits `IHostAdapterClient.SendMailAsync` with the caller's cancellation token and `.ConfigureAwait(false)`, and converts a failure envelope into an `InvalidOperationException` carrying the API error code and message. Client exceptions (including `OperationCanceledException`) propagate unwrapped. No production change to `SchedulingWorker`, `IHostAdapterClient`, `HostAdapterHttpClient`, contract projects, HostAdapter routes, MailBridge, or schemas — verified against the full diff. The `SendEnabled` kill switch (default `false`) remains the safety boundary and is now exercised end-to-end in worker tests.

The mandatory toolchain was independently re-run by the reviewer against the branch head `d8a0887` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 205 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** NetArchTest suite in `OpenClaw.Core.Tests` — 2 passed, 0 failed.
- **Tests + coverage:** full solution `dotnet test` with `--collect:"XPlat Code Coverage"` — 671 passed, 0 failed, 5 environment-gated skips (same skips as baseline); pooled coverage 90.56% line / 80.05% branch, above the uniform gates; T1 property-test obligation for the new pure function satisfied by `SchedulingDtoMapperPropertyTests` (CsCheck, 1000 iterations, seed printed on failure per suite convention).
- **Regression evidence:** fail-before artifact (EXIT 1; the five new delegation tests failing against the pre-#99 `NotSupportedException` implementation) and pass-after artifact (EXIT 0) are present under `evidence/regression-testing/`.

No Blocking findings. No material PARTIAL findings. One informational observation on async-body instrumentation scope (pre-existing runsettings behavior, unchanged on this branch; Section 8). Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/csharp.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript / governed JSON (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is three production files, four test files, and documentation/evidence Markdown. The executor's raw cobertura intermediates under `artifacts/csharp/` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`13f6f93`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only. That categorization is inaccurate; the authoritative `git diff 13f6f93..d8a0887` contains 3 production C# files and 4 test C# files. The audit used the authoritative git diff file list, not the summary categorization. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 13f6f93..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
- Result: **NONE.** All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/<kind>/` locations (baseline, qa-gates, regression-testing). No files under `artifacts/` are tracked in the diff at all.
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, and #18 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/baseline/` and `artifacts/csharp/final/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Each service test builds its own `Mock<IHostAdapterClient>` and service instance; each worker test builds a fresh mock graph via `ServiceReturningContext()`/`CandidateSource()`; the property test instantiates its own mapper. No shared mutable state. 224/224 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | Five delegation tests each pin one seam behavior (success/once-with-token, envelope failure, unwrapped propagation, cancellation, request mapping); four mapper example tests each pin one mapping rule; worker tests pin one pipeline behavior each. |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 224 tests in ~1 s (reviewer run); all new tests are pure in-memory mock interactions. |
| **Determinism** | PASS | No wall-clock reads, sleeps, timers, network, or filesystem in any new test. The CsCheck property test follows the suite's established seeded-sample convention (failing seed printed on `Sample` failure, documented in the class XML doc), matching the five pre-existing `*PropertyTests` files. |
| **Readability & Maintainability** | PASS | Descriptive names (`SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage`, `RunCycle_SendFailure_LogsAndContinues`), FluentAssertions throughout, XML doc summaries on the new property-test class and new helpers, explanatory comments citing the spec/AC anchors. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 90.51% line, 79.95% branch; `OpenClaw.Core` package 98.61% line / 91.69% branch. Source: `evidence/baseline/baseline-test-coverage.2026-07-02T10-54.md`. |
| **No Coverage Regression** | PASS | Post-change pooled: 90.56% line, 80.05% branch (+0.05pp / +0.10pp); Core package 98.63% / 91.82% (+0.02pp / +0.13pp). Independently confirmed from reviewer cobertura (this audit). |
| **New Code Coverage** | PASS | Per-changed-file (reviewer cobertura, line AND branch): `SchedulingDtoMapper.cs` 96.30% line (130/135) / 87.23% branch (41/47) — the 5 uncovered lines and 5 partial conditions are all in pre-existing untouched code, equally uncovered at baseline; both new branch points (line 128 empty-CC ternary, line 148 whitespace-name ternary) are fully covered. `HostAdapterSchedulingService.cs` 100% of instrumented lines (4/4); the new async body is uninstrumented per the pre-existing runsettings attribute exclusion and is behaviorally covered by the five delegation tests (see Section 8 informational note). `SendMailRequest.cs` doc-only. New test files are excluded from coverage measurement per policy (`[*.Tests]*` exclude in `mailbridge.runsettings`). |
| **Comprehensive Coverage** | PASS | Send delegation success/failure/cancellation/mapping, mapper field translation and edge rules, worker gating on/off, per-message failure isolation, and cancellation stop are all exercised; existing read-path tests continue unchanged. |
| **Positive Flows** | PASS | Delegation success with token verification; full-field mapping test; worker send-enabled test with composed-request capture (recipient = normalized `MessageFrom`, subject `Re: {subject}`, `Proposed times:` body). |
| **Negative Flows** | PASS | Envelope `Ok: false` raises `InvalidOperationException` containing `BRIDGE_UNAVAILABLE` and `bridge offline`; null request throws `ArgumentNullException` (mapper and service null-guards); `HttpRequestException` propagates unwrapped (`ThrowExactlyAsync`). |
| **Edge Cases** | PASS | Empty CC list maps to null; empty/whitespace recipient name maps to null; `BccRecipients` always null; `SaveToSentItems` always true; property test samples 0-5 recipients including zero-length lists and whitespace names over 1000 iterations. |
| **Error Handling** | PASS | Envelope failure, transport exception, and cancellation paths each pinned at the service seam; worker failure isolation (`RunCycle_SendFailure_LogsAndContinues`) proves the cycle continues and the second candidate is processed; `RunCycle_SendCancellation_StopsCycle` proves `OperationCanceledException` stops the cycle before the second candidate is hydrated. |
| **Concurrency** | N/A | No new concurrency surface; the seam is a single awaited call. |
| **State Transitions** | N/A | `HostAdapterSchedulingService` remains stateless; `SchedulingWorker` production code is unchanged. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.51% line, 79.95% branch (pooled solution) -> Post-change: 90.56% line, 80.05% branch. Change: +0.05% line, +0.10% branch. New/changed-code coverage: SchedulingDtoMapper.cs 96.30% line / 87.23% branch with all new lines and both new branch points covered; HostAdapterSchedulingService.cs 100% of instrumented lines with the async body behaviorally covered by five delegation tests; SendMailRequest.cs doc-only; new test files excluded from measurement per policy. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: `evidence/baseline/baseline-test-coverage.2026-07-02T10-54.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T11-20.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T11-23.md`, reviewer re-run `evidence/qa-gates/coverage-review.2026-07-02T11-25.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions matchers with specific expectations (`Contain("BRIDGE_UNAVAILABLE").And.Contain("bridge offline")`, `ContainSingle().Which...`); property-test assertions compare full sequences so mismatches show both sides; CsCheck prints the failing seed. |
| **Arrange-Act-Assert Pattern** | PASS | All new tests follow the suite's arrange/act/assert flow; worker tests carry explanatory assert-section comments tied to spec conditions. |
| **Document Intent** | PASS | XML doc on the new property-test class states the T1 obligation, the invariants, and the seed-print behavior; inline comments in worker tests cite the per-message-isolation and cancellation ACs. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, filesystem, COM, or external process; all seams mocked with Moq at `IHostAdapterClient`/`ISchedulingService`. |
| **Use Mocks/Stubs** | PASS | Moq mocks per suite convention; `Capture.In` for request capture; the mapper (pure) is exercised directly. |
| **Environment Stability** | PASS | No temporary files; no mutable global state; no environment variables read. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #99, `spec.md` v0.2 (verified-facts section confirming both `SendMailRequest` records, the return-type mismatch, and the `SendEnabled` default), and gap-analysis research define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-02T10-37.md` present. |
| **Document the plan** | PASS | `plan.2026-07-02T10-37.md` with per-phase evidence under `evidence/**`; completed tasks recorded in the PR-context summary. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | The delegation is ~15 lines; the mapping is one pure method plus one private helper on the existing injected mapper; no new types, interfaces, or dependencies. |
| **Reusability** | PASS | Mapping lives on the shared `SchedulingDtoMapper` (the established agent/wire translation seam) rather than inline in the service; recipient translation factored into `MapRecipients` used for both To and Cc. |
| **Extensibility** | PASS | No public contract changes; `ISchedulingService`, `IHostAdapterClient`, and both `SendMailRequest` records unchanged; the wire `SaveToSentItems` default is set explicitly at the mapping seam. |
| **Separation of concerns** | PASS | Pure shape translation (mapper) is separate from the I/O delegation (service); envelope-to-exception conversion happens at the seam that owns the return-channel mismatch; worker orchestration untouched. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Changes confined to the runtime seam (`Agent/Runtime`) and one doc comment in `Agent/Contracts`; tests mirror the production layout. |
| **Under 500 lines** | PASS | `wc -l`: SendMailRequest.cs 20, HostAdapterSchedulingService.cs 143, SchedulingDtoMapper.cs 243, HostAdapterSchedulingServiceTests.cs 415, SchedulingDtoMapperPropertyTests.cs 107, SchedulingDtoMapperTests.cs 425, SchedulingWorkerTests.cs 310. All under the 500-line cap; matches executor evidence `evidence/qa-gates/file-size-check.2026-07-02T11-16.md`. |
| **Public vs internal** | PASS | The only public-surface addition is `SchedulingDtoMapper.MapSendMailRequest` (spec-sanctioned); `MapRecipients` is private static. |
| **No circular dependencies** | PASS | No project-reference changes; `OpenClaw.Core` already referenced `OpenClaw.HostAdapter.Contracts`; NetArchTest boundary suite passes (2/2, reviewer run). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `MapSendMailRequest`, `MapRecipients`, `WireSendMailRequest` alias (spec-prescribed disambiguation of the two record names); test names state scenario and expectation. |
| **Docs/docstrings** | PASS | New method XML doc documents purity, the always-true `SaveToSentItems`, the always-null BCC, and the intentional `InReplyToMessageId` drop (spec requirement); class-level docs of the service and mapper refreshed to describe the outbound direction; stale "deferred to #74/#75" sentences removed from both files. |
| **Comment why, not what** | PASS | The service's failure-envelope comment explains the fail-fast asymmetry; the mapper doc explains why the reply linkage is dropped (wire contract has no counterpart). |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 205 files, EXIT 0. Executor: `evidence/qa-gates/final-qa-format.2026-07-02T11-18.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. |
| **4. Architecture** | PASS | NetArchTest boundary tests: 2 passed, 0 failed (reviewer run). No COM, VSTO, or interop references added; the runtime seam references only already-referenced contract projects. |
| **5. Testing** | PASS | Reviewer: full solution test run — 671 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the T1 property-based test for the new pure function. |
| **6. Contract/schema checks** | N/A | No governed contract, DTO, route, or schema surface changed; both `SendMailRequest` records and `IHostAdapterClient` are byte-identical to base (verified in diff). |
| **7. Integration tests** | N/A | No adapter/external-system boundary changed; the integration-style condition (worker cycle composed-request verification) is covered at the mocked `ISchedulingService` seam per spec. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass Phase 5 loop (`final-qa-clean-pass.2026-07-02T11-22.md`). |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in `evidence/qa-gates/coverage-review.2026-07-02T11-25.md`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (three production files, no worker change); the commit message describes the wiring. |
| **Design choices explained** | PASS | The fail-fast envelope asymmetry, the `InReplyToMessageId` drop, and the out-of-scope stale worker catch block are all documented in spec.md with rationale. |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md`, `user-story.md`, and the `issue.md` mirror; stale doc comments in production files refreshed. |
| **Provide next steps** | PASS | Spec Rollout: merge with `SendEnabled` default-off; sequence the F6 idempotency feature immediately after (accepted interim duplicate-send risk documented). |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, TypeScript, and governed-JSON sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, #19, and #18 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; new code uses `ArgumentNullException.ThrowIfNull`, null-conditional access on `envelope.Error`, and explicit null-fallback messages; the test's single `null!` is the deliberate null-guard probe. |
| **Null-safety** | PASS | `envelope is not { Ok: true }` pattern plus `Error?.Code`/`Error?.Message` with explicit fallbacks; mapper null-guards its input; empty-name-to-null and empty-CC-to-null rules are explicit. |
| **Async / resource safety** | PASS | `await ... .ConfigureAwait(false)` per the file's existing pattern; caller token forwarded; no fire-and-forget, no blocking waits, no `async void`. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces throughout; `Async` suffix; PascalCase publics; the `WireSendMailRequest` using-alias resolves the duplicate record name exactly as the spec prescribes. |
| **Exceptions fail-fast** | PASS | Failure envelope converts to `InvalidOperationException` with error code and message (the seam has no return channel); no catch blocks added anywhere in production; client exceptions propagate unwrapped. |
| **No new suppressions** | PASS | The diff contains no pragma or suppression attributes and no banned-API usages (grep of the full C# diff for pragma, SuppressMessage, DateTime.Now/UtcNow, Random.Shared, Thread.Sleep, Task.Delay returned nothing). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq (all existing suites; divergence recorded in `.claude/agent-memory/prd-feature/project_test_framework_discrepancy.md`). The new and modified tests follow the established repo convention, consistent with the prior validated #70, #80, #19, and #18 audits. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq)** | PASS | `[TestClass]`/`[TestMethod]`; FluentAssertions matchers; Moq `Setup`/`Verify`/`Capture.In` per suite convention. |
| **Test file location** | PASS | All test changes under `tests/OpenClaw.Core.Tests/Agent/Runtime/`, mirroring `src/OpenClaw.Core/Agent/Runtime/`; the new property-test file sits beside the suite's existing `*PropertyTests` convention. No colocation in the production tree. |
| **Coverage expectation** | PASS | Pooled 90.56% line / 80.05% branch; mapper file 96.30% / 87.23%; every instrumented changed line covered; no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); the one new pure function (`MapSendMailRequest`) has a CsCheck property test (recipient count/address-sequence preservation for To and Cc, `SaveToSentItems` always true, input never mutated; 1000 iterations; generators include empty/whitespace names and 0-length lists). CsCheck 4.7.0 was already referenced by `OpenClaw.Core.Tests`; no new dependency. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80 T1 audit). |
| **Determinism (no sleeps, no wall clock)** | PASS | No `Thread.Sleep`/`Task.Delay`/`DateTime.Now`/timers in the diff; cancellation exercised via real `CancellationTokenSource` state, not timing; CsCheck prints the failing seed for reproducibility per suite convention. |
| **No temporary files** | PASS | Pure in-memory mocks; zero filesystem artifacts. |
| **Focused / isolated** | PASS | Fresh mock graph per test; helper factories (`SampleSendRequest`, `SetupSendMail`, `SetupAdditionalCandidate`) build per-test state only. |

---

## 5. Test Coverage Detail

### HostAdapterSchedulingServiceTests (5 new delegation tests replacing 1 removed deferred-behavior test)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `SendMailAsync_Success_DelegatesToClientOnceWithCallerToken` | Positive (delegation once, caller token forwarded) | PASS |
| `SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage` | Negative (envelope failure carries code + message) | PASS |
| `SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped` | Negative (`ThrowExactlyAsync<HttpRequestException>`, no wrapping) | PASS |
| `SendMailAsync_CanceledToken_PropagatesOperationCanceled` | Cancellation propagation | PASS |
| `SendMailAsync_MapsAgentRequestToWireRequest` | Mapping capture (subject, body, To/Cc translation, empty-CC null, SaveToSentItems) | PASS |

### SchedulingDtoMapperTests (4 new example tests) and SchedulingDtoMapperPropertyTests (1 new property test, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `MapSendMailRequest_MapsAllFieldsToWireShape` | Positive (full field mapping incl. BCC null) | PASS |
| `MapSendMailRequest_EmptyOrWhitespaceRecipientName_MapsToNullName` | Edge (name normalization) | PASS |
| `MapSendMailRequest_EmptyCcList_MapsToNull` | Edge (empty-CC rule) | PASS |
| `MapSendMailRequest_Null_Throws` | Negative (null guard) | PASS |
| `MapSendMailRequest_PreservesRecipientsSetsSaveAndNeverMutatesInput` | Property (T1 obligation; 1000 CsCheck iterations) | PASS |

### SchedulingWorkerTests (2 new tests; 1 existing test extended with argument capture)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `RunCycle_SendEnabled_InvokesSendMail` (extended) | Positive (send once; composed request: `Re:` subject, normalized recipient, `Proposed times:` body) | PASS |
| `RunCycle_SendDisabled_NeverInvokesSendMail` (pre-existing, unchanged) | Kill switch (default off) | PASS |
| `RunCycle_SendFailure_LogsAndContinues` | Failure isolation (second candidate still processed; cycle does not throw) | PASS |
| `RunCycle_SendCancellation_StopsCycle` | Cancellation stops cycle (second candidate never hydrated) | PASS |

**Coverage:** `SchedulingDtoMapper.cs` 96.30% line / 87.23% branch (new code fully covered incl. both new branch points); `HostAdapterSchedulingService.cs` 100% of instrumented lines, async body behaviorally covered. **Gap:** none attributable to this branch (remaining uncovered mapper lines are pre-existing untouched paths, equal at baseline).

**Fail-before / pass-after:** `evidence/regression-testing/expect-fail-sendmail-delegation.2026-07-02T10-58.md` (EXIT 1; the five delegation tests fail against the pre-#99 throwing implementation) and `evidence/regression-testing/pass-after-sendmail-delegation.2026-07-02T11-08.md` (EXIT 0), plus `mapper-tests-pass.2026-07-02T11-04.md` and `worker-gating-tests-pass.2026-07-02T11-13.md`.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 676 (671 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 224 passed / 224 | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~1 s | PASS |
| Pooled Code Coverage | 90.56% line, 80.05% branch | PASS |
| `OpenClaw.Core` package coverage (T1) | 98.63% line, 91.82% branch | PASS |
| Net new tests vs baseline | +11 (5 service + 4 mapper example + 1 property + 2 worker, minus 1 removed deferred test) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 205 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | `dotnet test tests/OpenClaw.Core.Tests/... --filter "FullyQualifiedName~ArchitectureBoundary"` | 2 passed, 0 failed | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | 671 passed, 0 failed, 5 skipped | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `d8a0887` on 2026-07-02. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** One informational observation, recorded (not a policy violation on this branch):

- **Async-body instrumentation exclusion (Informational).** The pre-existing `mailbridge.runsettings` coverlet setting `ExcludeByAttribute=CompilerGeneratedAttribute` excludes async state-machine bodies from instrumentation solution-wide. Converting the throwing (non-async) `SendMailAsync` to an async method therefore removed its body from the instrumented denominator (`HostAdapterSchedulingService.cs` dropped from 9 to 4 instrumented lines) — an instrumentation-scope shift, not a coverage regression. The new body's behavior is fully covered by the five delegation tests with fail-before/pass-after evidence. The runsettings file is byte-identical to base on this branch (empty diff), so no exclusion was added by this feature; the setting is an attribute-level filter, not a production-path `exclude` entry of the kind the coverage-exclusion policy prohibits. Recommended follow-up (out of scope here): evaluate removing `CompilerGeneratedAttribute` from `ExcludeByAttribute` so async method bodies contribute per-line coverage data. Detailed in the code review findings table.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, #19, and #18 audits. The format check ran to EXIT 0 over all 205 files.
- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #19 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **One test removed by design:** `SendMailAsync_Throws_DeferredNotSupported` asserted the pre-#99 deferred behavior that this feature deletes; its removal is an explicit acceptance criterion (AC-4) and it is replaced by five stronger delegation tests. No assertions were weakened; no other tests removed or skipped.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `13f6f93`)

Branch `feature/wire-sendmail-runtime-99`, head `d8a08879cacf23e8376e9ad4ed64c9b1a421a7d5` (single commit: "feat(core): wire HostAdapterSchedulingService.SendMailAsync to the live sendMail route"). Range: `13f6f9390cbb634abca0c36eb7cdabe4acc2830e..d8a08879cacf23e8376e9ad4ed64c9b1a421a7d5`.

### Files Modified (categories)

1. **`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`** (MODIFIED) — throwing `SendMailAsync` replaced with the async delegating implementation (null guard, mapper call, awaited client call with caller token, failure-envelope-to-exception conversion); class doc refreshed.
2. **`src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`** (MODIFIED) — new pure `MapSendMailRequest` plus private `MapRecipients`; `WireSendMailRequest` using-alias; class doc refreshed for the outbound direction.
3. **`src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs`** (MODIFIED) — stale "deferred to issues #74/#75; the runtime adapter throws" sentence removed; `SendEnabled` gating sentence kept. Doc-only.
4. **`tests/OpenClaw.Core.Tests/Agent/Runtime/`** — `HostAdapterSchedulingServiceTests.cs` (1 test removed, 5 added), `SchedulingDtoMapperTests.cs` (4 added), `SchedulingDtoMapperPropertyTests.cs` (NEW, 107 lines, CsCheck), `SchedulingWorkerTests.cs` (2 added, 1 extended with capture).
5. **`docs/features/active/2026-07-02-wire-sendmail-runtime-99/**`** (NEW, 19 files) — issue/spec/user-story/plan and canonical evidence (baseline, qa-gates, regression-testing).
6. **`.claude/agent-memory/**`** (5 files) — executor and prd-feature agent memory bookkeeping; no code or policy content.

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, architecture-boundary enforcement, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head. The T1 property-test obligation for the new pure function is satisfied. Fail-before/pass-after regression evidence is present and discriminates the removed deferred behavior exactly. No evidence-location or file-size violations. No new suppressions or banned APIs. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (minimal delegation + one pure mapping method, no new types)
- Module & File Structure: PASS (all files under 500 lines)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-fast envelope conversion; unwrapped propagation; no new catch blocks)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (90.56%/80.05% pooled; mapper 96.30%/87.23%; changed lines covered)
- Test Structure: PASS
- External Dependencies: PASS (Moq seams, no temp files)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq repo convention; tests/ mirror)
- Determinism: PASS (no sleeps/wall clock; seeded property sampling with seed printing)
- T1 obligations: PASS (property test present for the new pure function; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 671/671 runnable solution tests passing (5 pre-existing environment-gated skips)
- 90.56% pooled line coverage, 80.05% pooled branch coverage (gates: 85%/75%)
- T1 `OpenClaw.Core` package: 98.63% line / 91.82% branch
- Changed mapper file: 96.30% line / 87.23% branch; all new lines and both new branch points covered
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- Architecture boundary: 2/2 NetArchTest tests pass

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `d8a0887`. No remediation inputs are required. Post-merge operational note (from spec, not a gate): `SendEnabled` remains default-off; sequence the F6 idempotency feature next to close the accepted duplicate-send interim risk.

---

## Appendix A: Test Inventory

C# test changes in this feature (all in `tests/OpenClaw.Core.Tests/Agent/Runtime/`):

1. `HostAdapterSchedulingServiceTests.cs` — removed `SendMailAsync_Throws_DeferredNotSupported` (asserted the deleted pre-#99 deferred behavior; replacement mandated by AC-4); added 5 delegation tests: `SendMailAsync_Success_DelegatesToClientOnceWithCallerToken`, `SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage`, `SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped`, `SendMailAsync_CanceledToken_PropagatesOperationCanceled`, `SendMailAsync_MapsAgentRequestToWireRequest`.
2. `SchedulingDtoMapperTests.cs` — added 4 example tests: `MapSendMailRequest_MapsAllFieldsToWireShape`, `MapSendMailRequest_EmptyOrWhitespaceRecipientName_MapsToNullName`, `MapSendMailRequest_EmptyCcList_MapsToNull`, `MapSendMailRequest_Null_Throws`.
3. `SchedulingDtoMapperPropertyTests.cs` (NEW) — `MapSendMailRequest_PreservesRecipientsSetsSaveAndNeverMutatesInput` (CsCheck, 1000 iterations, seeded-sample convention with failing-seed printing).
4. `SchedulingWorkerTests.cs` — added `RunCycle_SendFailure_LogsAndContinues` and `RunCycle_SendCancellation_StopsCycle`; extended `RunCycle_SendEnabled_InvokesSendMail` with composed-request capture. Pre-existing `RunCycle_SendDisabled_NeverInvokesSendMail` unchanged.

Reviewer run: `OpenClaw.Core.Tests` 224 passed, 0 failed; solution total 671 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Architecture-boundary tests (subset)
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~ArchitectureBoundary"

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-wire-sendmail-runtime-99/evidence/qa-gates/coverage-review"

# Regression subsets (executor fail-before / pass-after evidence)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingDtoMapper"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerTests"

# Evidence-location scan
git diff --name-only 13f6f9390cbb634abca0c36eb7cdabe4acc2830e..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
