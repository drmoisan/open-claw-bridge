# Policy Compliance Audit: evolve-hostadapter-graph-surface (#76)

**Audit Date:** 2026-06-12
**Code Under Test:** Branch `open-claw-bridge-wt-2026-06-12-22-19` @ `e3bc4506e1ebce0080e057306b91ffbbb77fd945` vs base `main` @ `3041d083691cd77b2b2e888580fc9f2ab8bc611f` (merge base identical to base tip).

Source files changed (C#):
- `src/OpenClaw.Core/CoreOptions.cs`
- `src/OpenClaw.Core/HostAdapterHttpClient.cs`
- `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`
- `src/OpenClaw.HostAdapter/HostAdapterOptions.cs`
- `src/OpenClaw.HostAdapter/HostAdapterRequestValidation.cs`
- `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj`
- `src/OpenClaw.HostAdapter/Program.cs`

Test files changed (C#):
- `tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs`
- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterAuthTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterEnvelopeTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterMappingTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterStatusCacheTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterValidationTests.cs`
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterVersionTests.cs` (new)

Docs/scoping changed: `spec.md`, `user-story.md`, `plan.2026-06-12T22-21.md`, and 22 evidence artifacts under the feature folder.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 7 source + 9 test | 252 (74 HostAdapter + 178 Core) | ✅ 252 pass, 0 fail | HostAdapter 84.99% line / 62.88% branch (whole-assembly); Core 89.32% line / 77.58% branch | HostAdapter package 97.08% line / 86.30% branch; Core package 98.58% line / 90.32% branch | Changed-code 98.85% line / 79.41% branch |

**Note:** Only C# (plus Markdown docs) has changed files in the branch diff. No Python, PowerShell, TypeScript, Bash, or JSON application code changed. Coverage verdicts for those languages are N/A because they have zero changed files on the branch.

### Coverage Evidence Checklist

- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- C# changed-code coverage artifact (executor analysis): `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/qa-gates/coverage-delta.2026-06-12T23-17.md`
- C# raw cobertura (HostAdapter.Tests): `tests/OpenClaw.HostAdapter.Tests/TestResults/8d218b5b-4811-4254-a89b-8c263055367c/coverage.cobertura.xml` — package `OpenClaw.HostAdapter` line-rate 0.9708, branch-rate 0.863 (independently inspected).
- C# raw cobertura (Core.Tests): `tests/OpenClaw.Core.Tests/TestResults/93de4c82-c23c-4300-93ab-8aa779dbcf9e/coverage.cobertura.xml` — package `OpenClaw.Core` line-rate 0.9858, branch-rate 0.9032 (independently inspected).
- Per-language comparison summary: Section 1.2.1 below.

**Coverage artifact path note:** The feature-review skill nominates `artifacts/csharp/coverage.xml` as the canonical C# coverage artifact path. That literal path is absent, but parseable cobertura reports exist under each test project's `TestResults/` directory and were inspected directly. This is a path-naming difference, not a missing-evidence condition; coverage verification was performed against the present cobertura reports and the executor coverage-delta artifact. Verdict therefore PASS, not the missing-artifact FAIL.

**Non-negotiable verdict rule:** Numeric baseline and post-change C# coverage metrics are included above; changed-code coverage (98.85% line / 79.41% branch) is recorded.

---

## Executive Summary

This change reshapes the `OpenClaw.HostAdapter` HTTP surface and the `IHostAdapterClient` T2 contract from a bespoke `/v1/*` route table to a Microsoft Graph-shaped surface (`/users/{id}/messages`, `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`, and a messages-filtered meeting-requests form), retaining the `/status` operational probe. It is a path-and-query change: the DTO/envelope schema is unchanged. The adapter version is bumped to `1.0.0` to signal the breaking contract change, a configurable `MailboxId` (default `"me"`) sources the `{id}` segment, and the Core `BaseUrl` default drops `/v1/`.

Independent verification confirmed the toolchain is clean: build with analyzers, nullable analysis, and warnings-as-errors produced 0 warnings / 0 errors; CSharpier reported no formatting changes across 149 files; the two affected test projects pass 252/252. Architecture boundaries are unchanged (no new `ProjectReference` edges). The T2 contract member parity holds (signatures unchanged; only XML-doc text changed), the implementation emits the Graph-shaped routes, and the version reports `1.0.0`. Changed-code coverage meets the uniform thresholds.

**Policy documents evaluated:**
- ✅ `.claude/rules/general-code-change.md`
- ✅ `.claude/rules/general-unit-test.md`
- ✅ `.claude/rules/quality-tiers.md` (T2 boundary, uniform coverage)
- ✅ `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- ✅ C# (`.claude/rules/csharp.md`): CSharpier format, .NET analyzers, nullable type analysis, MSTest, architecture-boundary assertions, contract/schema compatibility.
- N/A Python / PowerShell / Bash / JSON / TypeScript: no changed files on the branch.

**Temporary artifacts cleanup:**
- ✅ No temporary/one-time scripts were created during this review.
- ✅ No throwaway scripts remain.

---

## Rejected Scope Narrowing

The orchestrator prompt directed a full feature-vs-base audit and supplied locked design decisions and a toolchain note; it did not attempt to narrow audit scope to a plan/task/phase subset, to a subset of changed files, or to mark any in-scope language as out of scope. No scope-narrowing instruction was detected. No verbatim narrowing text is recorded because none was present.

Separately, the PR-context summary (`artifacts/pr_context.summary.txt`, "Changed files overview") classifies the change as "Core logic changes: 0 files" and "Docs/templates/agents/tooling: 25 files". This classification is inaccurate: the branch diff includes seven `src/**` C# source files and nine test files with material logic changes. This audit does not narrow scope to the summary classification; it audits the full branch diff resolved directly from `git diff <merge-base>..<head>`. Recorded here for traceability; this is a context-artifact classification defect, not a caller narrowing instruction.

---

## Evidence Location Compliance

- Branch diff scanned for files written under `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.
  - Command: `git diff --name-only 3041d08..e3bc450 | grep -E 'artifacts/(baselines?|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
  - Result: no matches. No forbidden evidence path was written by this branch.
- All feature evidence is written under the canonical `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/<kind>/` scheme (baseline, qa-gates, other).
- `validate_evidence_locations.py` is not present in this repository; the scan was performed by direct path inspection of the branch diff instead. No violations found.

Verdict: PASS. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events were required during this review.

---

## modified-workflow-needs-green-run

- Branch diff scanned for paths under `.github/workflows/**`, `scripts/benchmarks/**`, `.github/actions/**`.
  - Command: `git diff --name-only 3041d08..e3bc450 | grep -E '\.github/workflows/|scripts/benchmarks/|\.github/actions/'`
  - Result: no matches.
- The rule does not fire. No green-workflow-run evidence is required for this change. Verdict: PASS (rule not triggered).

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | ✅ PASS | MSTest classes use per-test `WebApplicationFactory`/`HttpClient` instances disposed with `using`; no shared mutable static state across tests. Both projects ran to green in a single `dotnet test` pass. |
| **Isolation** - Each test targets single behavior | ✅ PASS | Each added test targets one behavior: meeting-requests branch dispatch, plain-messages branch dispatch, and `DefaultAdapterVersion == "1.0.0"`. Updated tests assert one route/parameter shape each. |
| **Fast Execution** | ✅ PASS | HostAdapter.Tests 379 ms / 74 tests; Core.Tests 800 ms / 178 tests (independently observed). |
| **Determinism** | ✅ PASS | Tests use an in-memory `FakeHttpHandler` and an enqueued `ProcessRunner`; no wall-clock waits, no `Thread.Sleep`/`Task.Delay`, no network. Timestamps are fixed literals. |
| **Readability & Maintainability** | ✅ PASS | Descriptive method names (e.g., `HostAdapter_should_dispatch_meeting_requests_branch_when_filter_contains_meeting_message_type_predicate`) and XML-doc summaries on updated tests. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | `evidence/baseline/baseline-test.2026-06-12T22-30.md`: HostAdapter 84.99% line / 62.88% branch (whole-assembly), Core 89.32% line / 77.58% branch. Command: `dotnet test ... --collect:"XPlat Code Coverage"`. |
| **No Coverage Regression (changed lines)** | ✅ PASS | Changed/new lines in `Program.cs` and `HostAdapterHttpClient.cs` are at 100%; `HostAdapterRequestValidation.cs` 96.6% line; no changed line regressed. Source: `evidence/qa-gates/coverage-delta.2026-06-12T23-17.md`. |
| **Changed-code line >= 85% / branch >= 75%** | ✅ PASS | Changed-code aggregate 98.85% line / 79.41% branch (uniform thresholds per `quality-tiers.md`). Package-level cobertura independently inspected: HostAdapter 97.08% line / 86.30% branch; Core 98.58% line / 90.32% branch. |
| **Comprehensive Coverage** | ✅ PASS | New branch-selection logic (`FilterSelectsMeetingRequests`) covered by both meeting-requests and plain-messages dispatch tests; `$top`/`startDateTime`/`endDateTime` validation paths covered by updated validation tests; version helper covered by `HostAdapterVersionTests`. |
| **Positive Flows** | ✅ PASS | Mapping tests issue valid Graph-shaped requests and assert 200 + correct branch dispatch. |
| **Negative Flows** | ✅ PASS | Validation tests assert error messages now cite `receivedDateTime`, `startDateTime`/`endDateTime`, and `$top` for malformed/over-limit inputs. |
| **Edge Cases** | ✅ PASS | Over-`MaxLimit` `$top=251`, equal start/end window, and escaped path segments (`bridge%20id%2Bvalue`, spaces/slashes) covered. |
| **Error Handling** | ✅ PASS | Auth-failure and transport-failure paths retained and exercised against the new route strings. |
| **Concurrency** | N/A | No concurrency behavior introduced; route reshaping only. |
| **State Transitions** | N/A | No stateful component added. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 89.32% lines (Core whole-assembly) -> Post-change: 98.58% lines (Core package). Change: +9.26% lines. New/changed-code coverage: 98.85% lines / 79.41% branches. Disposition: PASS. Evidence: `evidence/qa-gates/coverage-delta.2026-06-12T23-17.md`; cobertura under `tests/OpenClaw.HostAdapter.Tests/TestResults/.../coverage.cobertura.xml` (HostAdapter package 97.08% lines / 86.30% branches) and `tests/OpenClaw.Core.Tests/TestResults/.../coverage.cobertura.xml`.
- Python / PowerShell / TypeScript / Bash / JSON: N/A — out of scope (zero changed files on the branch).

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | ✅ PASS | FluentAssertions `Should().Contain(...)`/`Should().Equal(...)` produce explicit diagnostics naming the expected route/parameter token. |
| **Arrange-Act-Assert Pattern** | ✅ PASS | Each test arranges factory + enqueued responses, acts via `client.GetAsync(...)`, then asserts status and invocation verbs. |
| **Document Intent** | ✅ PASS | Updated XML-doc summaries describe the Graph-shaped path under test. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | ✅ PASS | No real network/process; `FakeHttpHandler` and an enqueued in-process `ProcessRunner` substitute for the bridge. |
| **Use Mocks/Stubs** | ✅ PASS | HTTP handler fake and process-runner stub isolate the unit under test. |
| **Environment Stability** | ✅ PASS | In-memory configuration; no temporary files created by the changed tests. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | ✅ PASS | This audit, the accompanying code-review, and feature-audit artifacts constitute the required review. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | ✅ PASS | Objective stated in `spec.md`/`user-story.md` (#76); locked decisions D1–D6 confirmed by the orchestrator. |
| **Read existing change plans** | ✅ PASS | `plan.2026-06-12T22-21.md` present and tracked; phase-0 instructions-read evidence present. |
| **Document the plan** | ✅ PASS | Plan with P0–P3 tasks and acceptance gates is checked in. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | Messages and meeting-requests share one `/users/{id}/messages` handler that branches on a `$filter` predicate, removing the duplicated meeting-requests endpoint (Program.cs net -111/+... reduction). |
| **Reusability** | ✅ PASS | `ExtractReceivedDateTimeLowerBound` and `FilterSelectsMeetingRequests` are small reusable static helpers in `HostAdapterRequestValidation`. |
| **Extensibility** | ✅ PASS | `MailboxId` is a keyword-style option with a default; the `{id}` segment is parameterized for future per-mailbox routing. |
| **Separation of concerns** | ✅ PASS | Filter parsing/validation isolated in `HostAdapterRequestValidation`; route registration and dispatch in `Program.cs`; the typed client emits wire paths only. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | Changes stay within the HostAdapter HTTP surface, its options/validation, the typed client, and the contract doc. |
| **Under 500 lines** | ✅ PASS | All changed source files remain well under 500 lines (Program.cs shrank; validation, options, client, contract all small). |
| **Public vs internal** | ✅ PASS | `HostAdapterHttpClient` stays `internal`; the public T2 surface is `IHostAdapterClient` with unchanged signatures. |
| **No circular dependencies** | ✅ PASS | No new `ProjectReference` edge; dependency graph unchanged (see Section: architecture). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | `MailboxId`, `ExtractReceivedDateTimeLowerBound`, `FilterSelectsMeetingRequests`, `FormatAdapterVersion`; route segments `messageId`/`eventId` renamed from generic `bridgeId` to match Graph shape. |
| **Docs/docstrings** | ✅ PASS | `IHostAdapterClient` XML-doc updated to describe each Graph-shaped route; new options documented with `<summary>`. |
| **Comment why, not what** | ✅ PASS | `FormatAdapterVersion` comment explains why the 4-component assembly version is reduced to 3 components for `meta.adapterVersion`. |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | ✅ PASS | `csharpier check ./src ./tests` -> "Checked 149 files"; exit 0 (independently run, CSharpier 1.3.0). |
| **2. Linting** | ✅ PASS | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true` -> 0 Warning(s), 0 Error(s) (independently run). |
| **3. Type checking** | ✅ PASS | Nullable reference analysis is part of the same warnings-as-errors build; 0 nullable warnings. |
| **4. Architecture-boundary** | ✅ PASS | `grep -rn 'ProjectReference Include' src --include=*.csproj` confirms the six allowed edges and no new edge (see below). |
| **5. Testing** | ✅ PASS | HostAdapter.Tests 74/74, Core.Tests 178/178 (independently run with `--no-build`). |
| **6. Contract / schema** | ✅ PASS | T2 `IHostAdapterClient` member parity confirmed; DTO/envelope unchanged (see Section: contract). |
| **Full toolchain loop** | ✅ PASS | All stages pass in a single pass; executor recorded phase1/phase2/final qa-gate evidence, independently reconfirmed here. |
| **Explicit reporting** | ✅ PASS | Commands and results recorded in this audit and Appendix B. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | ✅ PASS | Summarized in this audit and the code-review artifact. |
| **Design choices explained** | ✅ PASS | D1–D6 locked decisions documented in spec; single-handler branch design noted in plan note N1. |
| **Update supporting documents** | ✅ PASS | `spec.md`, `user-story.md`, and `IHostAdapterClient` XML-doc updated. |
| **Provide next steps** | ✅ PASS | Recommendation recorded in Section 10. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3C#: C# Code Change Policy Compliance

#### 3C#.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with CSharpier** | ✅ PASS | `csharpier check ./src ./tests` -> "Checked 149 files"; exit 0. CSharpier 1.3.0 global tool (see toolchain note below). |
| **Linting with .NET analyzers** | ✅ PASS | `EnableNETAnalyzers=true` build: 0 warnings / 0 errors. |
| **Type checking (nullable)** | ✅ PASS | `Nullable enable` + `TreatWarningsAsErrors=true` build clean. |
| **Testing with MSTest** | ✅ PASS | 252/252 pass. |

**CSharpier version toolchain note (assessed):** The repository pins CSharpier `0.16.0` as a dotnet local tool (command id `dotnet-csharpier`), but `dotnet tool restore` for that pin failed in this worktree (`dotnet csharpier --version` -> "Run dotnet tool restore"). The executor and this review used the globally installed CSharpier `1.3.0` via `csharpier check`. Assessment: format verification is satisfied — the diff is formatting-clean under the available formatter and the build is clean. This is a residual non-blocking concern, not a violation: a major-version difference (0.16.0 vs 1.3.0) can in principle format identical source differently, so the green `csharpier check` under 1.3.0 does not prove the source is clean under the pinned 0.16.0. The risk is low here because the changes are route-string and small-helper edits with conventional formatting, and the build is warnings-as-errors clean. Recommended (non-blocking) follow-up: repair the dotnet-tool restore so the pinned formatter is the one exercised, or update the tool pin to match the installed major version. Recorded as a Minor finding in the code-review.

#### 3C#.2 C# Design & Typing

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Nullable safety** | ✅ PASS | `FormatAdapterVersion(Version?)` handles the null case explicitly; no nullable warnings. |
| **No `dynamic`/untyped escape hatches** | ✅ PASS (T1/T2 = 0) | No `dynamic` introduced; query values handled as `StringValues`/`string`. |
| **Public API discipline** | ✅ PASS | T2 `IHostAdapterClient` signatures unchanged; only XML-doc text changed. New options are keyword-style with defaults. |
| **Composition over inheritance** | ✅ PASS | No new inheritance; static helper functions and option properties. |

#### 3C#.3 C# Error Handling

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Fail fast / explicit** | ✅ PASS | Validation returns explicit `InvalidRequest` envelopes with specific messages naming the offending parameter. |
| **No silent error swallowing** | ✅ PASS | Absent `$filter` predicate yields empty `StringValues`, which surfaces the standard required-parameter error downstream (documented in the helper summary). |
| **Logging pattern** | ✅ PASS | Existing `RequestLoggingMiddleware` retained; no logging regression. |

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use MSTest** | ✅ PASS | `[TestClass]`/`[TestMethod]` with FluentAssertions and Moq across HostAdapter.Tests and Core.Tests. |
| **Coverage expectation** | ✅ PASS | Changed-code 98.85% line / 79.41% branch; package-level >= 85%/75%. |
| **Focused unit tests** | ✅ PASS | One behavior per test. |
| **Mocking sparingly** | ✅ PASS | Only the HTTP handler and process runner are faked. |
| **Organization** | ✅ PASS | Test files mirror the project structure under `tests/OpenClaw.*.Tests/`. |
| **Determinism infrastructure** | ✅ PASS | No `Thread.Sleep`/`Task.Delay`/real waits in the changed tests; fixed timestamps. |

---

## 5. Test Coverage Detail

### HostAdapter messages-branch dispatch (2 new tests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `HostAdapter_should_dispatch_meeting_requests_branch_when_filter_contains_meeting_message_type_predicate` | Positive (branch select) | ✅ |
| `HostAdapter_should_dispatch_plain_messages_branch_when_filter_has_no_meeting_message_type_predicate` | Positive (branch select) | ✅ |

**Coverage:** Exercises `FilterSelectsMeetingRequests` true/false paths and the `BuildListMeetingRequests`/`BuildListMessages` dispatch fork in `Program.cs`.

### HostAdapter version (1 new test)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `DefaultAdapterVersion_should_report_1_0_0_from_the_assembly_version` | Positive | ✅ |

**Coverage:** `FormatAdapterVersion` happy path (3-component reduction of the assembly version).

**Not covered:** Two defensive branches in `FormatAdapterVersion` (`version is null` and `version.Build < 0`) are unreachable for the loaded assembly (non-null version, Build >= 0). Justification: intentional defensive guards; aggregate changed-code branch coverage remains above threshold.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (affected projects) | 252 | ✅ |
| Tests Passed | 252 (100%) | ✅ |
| Tests Failed | 0 | ✅ |
| Execution Time | ~1.18 s total (0.379 s + 0.800 s) | ✅ Fast |
| Code Coverage (changed-code) | 98.85% line, 79.41% branch | ✅ |

---

## 7. Code Quality Checks

**For C#:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier Formatting | `csharpier check ./src ./tests` | Checked 149 files; exit 0 | ✅ |
| .NET Analyzers + Style | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` | 0 warnings / 0 errors | ✅ |
| Nullable type checking | `dotnet build ... -p:TreatWarningsAsErrors=true` | 0 warnings / 0 errors | ✅ |
| MSTest Tests | `dotnet test tests/OpenClaw.HostAdapter.Tests/...; dotnet test tests/OpenClaw.Core.Tests/...` | 74/74; 178/178 | ✅ |

**Notes:** CSharpier pinned-tool restore is unavailable in this worktree; the global 1.3.0 tool was used. See the toolchain note in Section 3C#.1. No other deviations from clean runs.

---

## 8. Gaps and Exceptions

### Identified Gaps
- **CSharpier tool pin mismatch (Minor, non-blocking):** Formatting was verified with global CSharpier 1.3.0 rather than the pinned 0.16.0 dotnet-tool, which could not be restored. Format-clean under the pinned formatter is therefore not directly proven. Low risk given the nature of the changes and the clean warnings-as-errors build.
- **PR-context summary misclassification (Info):** `artifacts/pr_context.summary.txt` reports "Core logic changes: 0 files" despite seven changed `src/**` source files. This is a context-artifact defect; the audit used the direct branch diff instead.

### Approved Exceptions
- **None.** No exceptions to policy thresholds were required; all gates pass on the merits.

### Removed/Skipped Tests
- **None.** No tests were removed or skipped. Updated tests assert stronger, Graph-shaped routes (not weakened).

---

## 9. Summary of Changes

### Range
- `3041d083691cd77b2b2e888580fc9f2ab8bc611f..e3bc4506e1ebce0080e057306b91ffbbb77fd945` (base `main` -> head).

### Files Modified (production)
1. `src/OpenClaw.HostAdapter/Program.cs` (MODIFIED) — six `/v1/*` route registrations reshaped to five Graph-shaped routes; messages and meeting-requests merged into one `/users/{id}/messages` handler branching on the `$filter` predicate; `/v1/status` -> `/status`; `bridgeId` route params renamed to `messageId`/`eventId`; `MailboxId` post-configure normalization.
2. `src/OpenClaw.HostAdapter/HostAdapterRequestValidation.cs` (MODIFIED) — added `ExtractReceivedDateTimeLowerBound` and `FilterSelectsMeetingRequests`; error messages updated to `$top`, `startDateTime`/`endDateTime`.
3. `src/OpenClaw.HostAdapter/HostAdapterOptions.cs` (MODIFIED) — added `MailboxId` (default `"me"`); added `FormatAdapterVersion` to render 3-component `1.0.0`.
4. `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj` (MODIFIED) — added `<Version>1.0.0</Version>`.
5. `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` (MODIFIED) — XML-doc only; signatures unchanged; `ListMeetingRequestsAsync` retained.
6. `src/OpenClaw.Core/HostAdapterHttpClient.cs` (MODIFIED) — six relative paths reshaped to Graph-shaped paths sourced from `MailboxId`.
7. `src/OpenClaw.Core/CoreOptions.cs` (MODIFIED) — `BaseUrl` default drops `/v1/`; added `MailboxId` mirror (default `"me"`) without referencing `OpenClaw.HostAdapter`.

### Files Modified (tests)
- 8 updated test files (route/parameter strings, base URL) + 1 new (`HostAdapterVersionTests.cs`).

---

## 10. Compliance Verdict

### Overall Status: ✅ FULLY COMPLIANT

The change satisfies the cross-language code-change and unit-test policies, the C# language policy, the T2 contract/version policy, the architecture-boundary rule, the evidence-location rule, and the coverage thresholds. All findings are non-blocking. Two residual items are recorded: a CSharpier tool-pin mismatch (Minor) and a PR-context summary misclassification (Info).

**Fail-closed reminder:** No required baseline, QA, coverage, or coverage-comparison artifact is missing; coverage metrics are numeric and present. The audit is not blocked.

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes
- ✅ Design Principles
- ✅ Module & File Structure
- ✅ Naming, Docs, Comments
- ✅ Toolchain Execution (7 stages clean in one pass)
- ✅ Summarize & Document

#### Language-Specific Code Change Policy (Section 3) — C#
- ✅ Tooling & Baseline (CSharpier note non-blocking)
- ✅ Design & Typing
- ✅ Error Handling

#### General Unit Test Policy (Section 1)
- ✅ Core Principles
- ✅ Coverage & Scenarios
- ✅ Test Structure
- ✅ External Dependencies
- ✅ Policy Audit

#### Language-Specific Unit Test Policy (Section 4) — C#
- ✅ Framework & Scope
- ✅ Test Style & Structure
- ✅ Naming & Readability
- ✅ Toolchain

### Metrics Summary
- ✅ 252/252 tests passing (100%)
- ✅ Changed-code coverage 98.85% line / 79.41% branch (>= 85% / 75%)
- ✅ Package-level coverage HostAdapter 97.08%/86.30%, Core 98.58%/90.32%
- ✅ Build 0 warnings / 0 errors (analyzers + nullable + warnings-as-errors)
- ✅ CSharpier format clean (149 files)
- ✅ No new architecture edges; T2 contract parity; version 1.0.0

### Recommendation

**Ready for merge.** No blocking or material-partial findings. Optional follow-up: repair or re-pin the CSharpier dotnet-tool so formatting is verified under the pinned version.

---

## Appendix A: Test Inventory

New and updated tests exercised by this change (affected projects):

1. OpenClaw.HostAdapter.Tests › HostAdapterMappingTests › `HostAdapter_should_dispatch_meeting_requests_branch_when_filter_contains_meeting_message_type_predicate` (new)
2. OpenClaw.HostAdapter.Tests › HostAdapterMappingTests › `HostAdapter_should_dispatch_plain_messages_branch_when_filter_has_no_meeting_message_type_predicate` (new)
3. OpenClaw.HostAdapter.Tests › HostAdapterVersionTests › `DefaultAdapterVersion_should_report_1_0_0_from_the_assembly_version` (new file)
4. OpenClaw.HostAdapter.Tests › HostAdapterMappingTests › existing messages mapping (route strings updated to `/users/me/messages?$filter=...&$top=...`)
5. OpenClaw.HostAdapter.Tests › HostAdapterValidationTests › timestamp, window, and over-limit cases (route/param strings updated to `receivedDateTime`, `startDateTime`/`endDateTime`, `$top`)
6. OpenClaw.HostAdapter.Tests › HostAdapterAuthTests › unauthorized/invalid-token (status route `/status`)
7. OpenClaw.HostAdapter.Tests › HostAdapterEndpointTests › escaped path segments (`/users/me/messages/...`, `/users/me/events/...`)
8. OpenClaw.HostAdapter.Tests › HostAdapterEnvelopeTests › status envelope (`/status`)
9. OpenClaw.HostAdapter.Tests › HostAdapterStatusCacheTests › cache reuse (route strings updated to Graph-shaped messages)
10. OpenClaw.Core.Tests › HostAdapterHttpClientTests › six route-shape/param assertions updated to Graph-shaped paths and `MailboxId`-sourced `/users/me/...`
11. OpenClaw.Core.Tests › CoreTestWebApplicationFactory base URL updated to drop `/v1/`

Full suite totals (affected projects): HostAdapter.Tests 74 tests, Core.Tests 178 tests, all passing.

---

## Appendix B: Toolchain Commands Reference

**For C#:**
```bash
# Formatting (global CSharpier 1.3.0; pinned 0.16.0 dotnet-tool unavailable in worktree)
csharpier check ./src ./tests

# Linting + style + nullable type checking
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true

# Architecture boundary inspection
grep -rn 'ProjectReference Include' src --include=*.csproj

# Route surface inspection
grep -rn '"/v1/' src/OpenClaw.HostAdapter/   # expect: no matches
grep -n 'MapGet' src/OpenClaw.HostAdapter/Program.cs

# Tests (with coverage via the runsettings used in execution)
dotnet test tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj -c Debug
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug
dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-06-12
**Policy Version:** Current (as of audit date)
