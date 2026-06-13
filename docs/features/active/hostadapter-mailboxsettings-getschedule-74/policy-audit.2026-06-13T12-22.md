# Policy Compliance Audit: hostadapter-mailboxsettings-getschedule (#74)

**Audit Date:** 2026-06-13
**Code Under Test:** C# source and tests in `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.Core`, and their test projects (full branch diff `48314bd..bbdce9f`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New/Changed-Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 26 source/test files | 89 (HostAdapter) + 193 (Core) + 219 (MailBridge) | ✅ 498 pass, 0 fail, 3 skipped (platform-gated) | Core 89.13% line / 77.59% branch; HostAdapter 84.10% line / 60.28% branch (per evidence) | Core package 98.41% line / 91.13% branch; HostAdapter package 98.52% line / 90.11% branch | 100% line / 100% branch on all changed files except `HostAdapterOptions.cs` (class-level 90.47% line / 50% branch; changed lines covered) |

**Note:** C# is the only language with changed files in the branch diff. No TypeScript, Python, PowerShell, or Bash files changed.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/test-coverage.md`
- C# post-change coverage artifact: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-test-coverage.md` and `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/coverage-delta.md`
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Reviewer-regenerated machine-readable artifacts: `coverage.cobertura.xml` produced by `dotnet test ... --collect:"XPlat Code Coverage"` during this audit (per-package and per-class rates parsed and confirmed)
- Per-language comparison summary: Section 1.2.1 below

**Verdict rule satisfied:** Numeric baseline and post-change coverage metrics are present for the single in-scope language (C#), with changed/new-code coverage independently verified from regenerated cobertura output.

---

## Executive Summary

This feature adds two Microsoft Graph-shaped routes to `OpenClaw.HostAdapter`
(`GET /users/{id}/mailboxSettings` and `GET /users/{id}/calendar/getSchedule`), adds two
additive methods to the `IHostAdapterClient` contract (a T2 boundary), implements them in
`HostAdapterHttpClient`, and replaces the two `NotSupportedException` stubs in
`HostAdapterSchedulingService` with delegating implementations. The three scheduling DTOs
(`MailboxSettingsDto`, `FreeBusyScheduleDto`, `BusyIntervalDto`) are relocated from
`OpenClaw.Core.Agent` to `OpenClaw.HostAdapter.Contracts`.

The reviewer independently ran the full C# toolchain against the branch head:
- CSharpier `check` — 165 files, 0 reformatting needed (PASS).
- `dotnet build -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true` — Build succeeded, 0 Warning(s), 0 Error(s) (lint + nullable type-check PASS).
- `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` — 498 passed, 0 failed, 3 skipped (PASS).

The previously-failing `AgentArchitectureBoundaryTests` (recorded in the scope-change
evidence) now passes; the architecture-boundary change is assessed as a legitimate correction
(see Section 8). No workflow, benchmark, or action files changed, so
`modified-workflow-needs-green-run` does not fire. No evidence-location violations were found.

**Policy documents evaluated:**
- ✅ `general-code-change.md`
- ✅ `general-unit-test.md`
- ✅ `csharp.md`
- ✅ `quality-tiers.md` and `architecture-boundaries.md`

**Language-specific policies evaluated:**
- ✅ C#: `csharp.md` (code-change + unit-test rules)
- N/A Python, PowerShell, Bash, JSON, TypeScript — no changed files in branch diff.

**Temporary artifacts cleanup:**
- ✅ No temporary or throwaway scripts were created by this feature.
- ✅ No production code or test code creates temporary files (verified by inspection of new test files).

---

## Rejected Scope Narrowing

The caller prompt included a neutral note describing the architecture-boundary scope-change
and asked for independent assessment. This was explicitly framed as "not a scope narrowing"
and did not attempt to limit the audit to a plan subset, file subset, or exclude any language.
No scope-narrowing instruction was detected. The audit scope is the full branch diff
`48314bd6cb9c1a9f0bae4ca0c775a95ec52f3a61..bbdce9f26ba3913de677f38a1a9db54fc1deddd6`.

One data-quality note (not a narrowing, recorded for transparency): the PR context summary
classifies the change as "Core logic changes: 0 files" and lists only docs. This
classification is inaccurate; the branch contains substantial C# source changes (verified via
`git diff --name-status`). The audit proceeds against the actual diff, not the summary's
file-category count.

---

## Evidence Location Compliance

The reviewer scanned the branch diff for files written under `artifacts/baselines/`,
`artifacts/qa/`, `artifacts/evidence/`, or `artifacts/coverage/`.

Command: `git diff --name-only 48314bd..bbdce9f | grep -E '^artifacts/(baselines|qa|evidence|coverage)/'`
Result: NONE.

All feature evidence is written under the canonical
`docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/<kind>/` path
(baseline, qa-gates, regression-testing, other). No evidence-location violations.

Note: the Python helper `validate_evidence_locations.py` is not present in this
C#/PowerShell repository; the canonical enforcement mechanism is the PreToolUse hook
`.claude/hooks/enforce-evidence-locations.ps1`. The diff-scan above is the substitute
verification and is clean.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** | ✅ PASS | MSTest tests use per-test Moq instances and `FakeTimeProvider`; no shared mutable state. Full-suite run passed deterministically. |
| **Isolation** | ✅ PASS | Each test targets one behavior (e.g., `Busy_event_status_2_is_projected_as_busy_interval`, `GetMailboxSettingsAsync_WhenEnvelopeNotOk_ReturnsDocumentedDefaults`). |
| **Fast execution** | ✅ PASS | HostAdapter.Tests 618 ms; Core.Tests ~1 s; MailBridge.Tests 11 s. |
| **Determinism** | ✅ PASS | `FakeTimeProvider` used for window derivation; no `Thread.Sleep`/`Task.Delay`/wall-clock reads in new tests; boundary seams (`IHostAdapterProcessRunner`, `IHostAdapterClient`) mocked. |
| **Readability & maintainability** | ✅ PASS | Arrange-Act-Assert structure, descriptive names, FluentAssertions throughout. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline coverage documented** | ✅ PASS | `evidence/baseline/test-coverage.md`: Core 89.13% line / 77.59% branch; HostAdapter 84.10% line / 60.28% branch. |
| **No coverage regression on changed lines** | ✅ PASS | Changed files moved from stub/absent to 100% covered; whole-project line/branch held or improved (Core +0.04 line; HostAdapter +2.71 line / +5.67 branch). |
| **New/changed-code coverage** | ✅ PASS | Reviewer-regenerated cobertura: `SchedulingContracts.cs`, `FreeBusyProjection.cs`, `MailboxSettingsOptions.cs`, `SchedulingRoutes.cs`, `HostAdapterHttpClient.cs`, `HostAdapterSchedulingService.cs` all 100% line / 100% branch. `HostAdapterOptions.cs` class-level 90.47% line / 50% branch — the new auto-property is covered; the uncovered branches pre-exist this feature. |
| **Positive flows** | ✅ PASS | Delegation success paths, busy-status projection, valid config parsing tested. |
| **Negative flows** | ✅ PASS | Envelope-not-OK degradation, invalid working-day names, invalid HH:mm, window validation failures tested. |
| **Edge cases** | ✅ PASS | Null busy-status treated as busy; empty event list yields empty intervals; tentative/OOF statuses. |
| **Error handling** | ✅ PASS | Downstream-failure envelope propagation in `getSchedule`; `SendMailAsync` still throws `NotSupportedException` (deferred to #75) with a test asserting that. |
| **Concurrency** | N/A | No new concurrency surface. |
| **State transitions** | N/A | No new stateful component. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 84.10% line / 60.28% branch (HostAdapter), 89.13% line / 77.59% branch (Core) -> Post-change: 98.52% line / 90.11% branch (HostAdapter package), 98.41% line / 91.13% branch (Core package). Change: +2.71% line / +5.67% branch (HostAdapter whole-project), +0.04% line / +0.00% branch (Core whole-project). New/changed-code coverage: 100% line / 100% branch on all changed files except HostAdapterOptions.cs (changed lines covered; class-level 50% branch pre-existing). Disposition: PASS. Evidence: evidence/qa-gates/coverage-delta.md, evidence/qa-gates/final-test-coverage.md, regenerated coverage.cobertura.xml.

Coverage attribution note: the HostAdapter test run's root `<coverage>` figure (86.81% line /
65.95% branch) is depressed because that assembly partially exercises
`OpenClaw.MailBridge.Contracts` (32.85% line / 0% branch in that run). That contracts surface
is fully covered in the MailBridge.Tests run (98.09% line / 93.65% branch). The depressed root
figure is a per-assembly attribution artifact, not a real gap in the in-scope projects, which
all exceed line >= 85% / branch >= 75% at the package level.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear failure messages** | ✅ PASS | FluentAssertions with descriptive `.Should().Be(...)` chains. |
| **Arrange-Act-Assert** | ✅ PASS | Explicit AAA comments in new tests. |
| **Document intent** | ✅ PASS | Self-documenting test names; class-level XML docs reference AC/decision IDs. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid external dependencies** | ✅ PASS | No network, no real Outlook, no live process; boundary seams mocked; `HostAdapterTestWebApplicationFactory` stubs `IHostAdapterProcessRunner`. |
| **Use mocks/stubs** | ✅ PASS | Moq for `IHostAdapterClient`/`IHostAdapterProcessRunner`. |
| **Environment stability** | ✅ PASS | No temporary files created in tests (verified by inspection). |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission review** | ✅ PASS | This artifact plus the code-review and feature-audit constitute the required review. |

---

## 2. General Code Change Policy Compliance

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | `FreeBusyProjection` is a small pure static projection; routes follow the existing `Program.cs` route lifecycle. |
| **Reusability** | ✅ PASS | Reuses `HostAdapterRequestValidation`, `HostAdapterResponses`, `ApiEnvelope<T>`, `IHostAdapterProcessRunner`. |
| **Extensibility** | ✅ PASS | `IHostAdapterClient` extended additively with keyword-style optional params; D2 client signature is the documented portability boundary. |
| **Separation of concerns** | ✅ PASS | Pure projection (`FreeBusyProjection`) separated from I/O (route handler, process runner); config parsing isolated in `MailboxSettingsOptions`. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | `SchedulingRoutes` extracted from `Program.cs` specifically to keep that file cohesive and under cap. |
| **Under 500 lines** | ✅ PASS | All changed files under 500: `Program.cs` 435, `SchedulingRoutes.cs` 230, `HostAdapterEndpointTests.cs` 455, `HostAdapterValidationTests.cs` 324, `HostAdapterSchedulingServiceTests.cs` 284, others <200. No file exceeds the cap; no pre-existing over-cap file was touched. |
| **Public vs internal** | ✅ PASS | DTOs and contract are `public`; `FreeBusyProjection`, `SchedulingRoutes`, `HostAdapterHttpClient` are `internal`. |
| **No circular dependencies** | ✅ PASS | Project graph unchanged; no new `ProjectReference` edge (verified `evidence/qa-gates/final-architecture.md` and build). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | PascalCase types/members; camelCase locals; `Async` suffix on async methods. |
| **Docs/docstrings** | ✅ PASS | XML docs on all new public APIs (DTOs, interface methods) including the D2 portability rationale. |
| **Comment why, not what** | ✅ PASS | Comments explain graceful-degradation rationale and the conservative null-busy-status default. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | ✅ PASS | `csharpier check .` — Checked 165 files, 0 changes. |
| **2. Linting** | ✅ PASS | `dotnet build ... -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` — 0 warnings. |
| **3. Type checking** | ✅ PASS | `dotnet build ... -p:TreatWarningsAsErrors=true` — 0 nullable/type warnings. |
| **4. Architecture** | ✅ PASS | Project graph unchanged; namespace-level boundary test now passes (see Section 8). |
| **5. Testing** | ✅ PASS | 498 passed / 0 failed / 3 skipped (platform-gated). |
| **6. Contract / schema** | ✅ PASS | `SchedulingDtoContractTests` extended for `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` round-trips; all pass. |
| **Full toolchain loop** | ✅ PASS | Single clean pass observed by the reviewer. |

---

## 3. Language-Specific Code Change Policy Compliance — C#

#### 3C.1 Type-Safety and Design

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strong contracts / explicit APIs** | ✅ PASS | `sealed record` DTOs; explicit interface signatures with typed window parameters. |
| **Null-safety** | ✅ PASS | Builds clean under `TreatWarningsAsErrors`; `ArgumentNullException.ThrowIfNull` guards in `FreeBusyProjection.Project`. |
| **Composition / focused types** | ✅ PASS | DTOs are immutable records; routes composed from existing helpers. |
| **Async / resource safety** | ✅ PASS | `async`/`await` with `.ConfigureAwait(false)` in the Core service; no new disposables leaked. |

#### 3C.2 Coding Standards

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Naming conventions** | ✅ PASS | Matches repo conventions. |
| **File-scoped namespaces** | ✅ PASS | All new files use file-scoped namespaces. |
| **Exceptions / error codes** | ✅ PASS | Downstream failures surfaced via `ApiError` codes (`DOWNSTREAM_FAILURE`), not raw exceptions; `SendMailAsync` retains explicit `NotSupportedException` for the deferred path. |
| **MSTest + Moq + FluentAssertions** | ✅ PASS | No xUnit/NUnit introduced; FakeTimeProvider used. |
| **File size** | ✅ PASS | All changed files <= 455 lines. |

---

## 4. Language-Specific Unit Test Policy Compliance — C#

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (MSTest)** | ✅ PASS | `[TestClass]`/`[TestMethod]` throughout. |
| **Coverage expectation** | ✅ PASS | Changed-code line >= 85% / branch >= 75% met (100% on new files). |
| **Mocking the boundary** | ✅ PASS | Moq for client/process-runner seams. |
| **Determinism (TimeProvider/FakeTimeProvider)** | ✅ PASS | `FakeTimeProvider` used to derive windows in service tests; projection has no clock dependency. |
| **No temporary files** | ✅ PASS | Verified by inspection. |

---

## 5. Test Coverage Detail

### FreeBusyProjection (5 tests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `Busy_event_status_2_is_projected_as_busy_interval` | Positive | ✅ |
| `Tentative_status_1_and_out_of_office_status_3_are_projected_as_busy` | Positive | ✅ |
| `Free_event_status_0_is_excluded` | Edge Case | ✅ |
| `Null_busy_status_is_treated_as_busy` | Edge Case | ✅ |
| `Empty_input_yields_empty_intervals` | Edge Case | ✅ |

**Coverage:** 100% line / 100% branch (`FreeBusyProjection.cs`).

### HostAdapterSchedulingService scheduling methods (4 new tests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `GetMailboxSettingsAsync_DelegatesToClient_ReturnsDto` | Positive | ✅ |
| `GetMailboxSettingsAsync_WhenEnvelopeNotOk_ReturnsDocumentedDefaults` | Negative / Error Handling | ✅ |
| `GetFreeBusyAsync_DelegatesToClient_ReturnsSchedule` | Positive | ✅ |
| `GetFreeBusyAsync_WhenEnvelopeNotOk_ReturnsEmptyIntervals` | Negative / Error Handling | ✅ |

**Coverage:** 100% line / 100% branch (`HostAdapterSchedulingService.cs`).

### Routes, client, and config

| Component | Verifying tests | Coverage | Status |
|-----------|-----------------|----------|--------|
| `SchedulingRoutes.cs` | `HostAdapterEndpointTests`, `HostAdapterValidationTests` | 100% / 100% | ✅ |
| `HostAdapterHttpClient.cs` | `HostAdapterHttpClientSchedulingTests` | 100% / 100% | ✅ |
| `MailboxSettingsOptions.cs` | route tests via endpoint factory | 100% / 100% | ✅ |
| `SchedulingContracts.cs` (DTOs) | `SchedulingDtoContractTests` round-trips | 100% / 100% | ✅ |
| `HostAdapterOptions.cs` | options binding via endpoint tests | 90.47% / 50% (changed lines covered) | ✅ |

**Not covered:** Pre-existing branches in `HostAdapterOptions` unrelated to this feature's
changed lines. No new untested code.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total tests | 510 (498 pass, 3 skip, plus filtered) | ✅ |
| Tests passed | 498 (100% of executed) | ✅ |
| Tests failed | 0 | ✅ |
| Tests skipped | 3 (platform-gated COM/publish) | ✅ |
| Execution time | HostAdapter 618 ms; Core ~1 s; MailBridge 11 s | ✅ Fast |
| Code coverage (in-scope packages) | Core 98.41% line / 91.13% branch; HostAdapter 98.52% line / 90.11% branch | ✅ |

---

## 7. Code Quality Checks

**For C#:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier formatting | `csharpier check .` | Checked 165 files, 0 changes | ✅ |
| .NET analyzers / code style | `dotnet build ... -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` | 0 warnings, 0 errors | ✅ |
| Nullable type-check | `dotnet build ... -p:TreatWarningsAsErrors=true` | 0 warnings, 0 errors | ✅ |
| MSTest tests + coverage | `dotnet test ... --collect:"XPlat Code Coverage"` | 498 pass, 0 fail, 3 skip | ✅ |

**Notes:** The 3 skipped tests are pre-existing platform-gated cases (non-Windows COM,
publish-output verification) unrelated to this feature. No pre-existing failures were observed.

---

## 8. Gaps and Exceptions

### Identified Gaps

- **None blocking.** One minor documentation discrepancy: `evidence/qa-gates/coverage-delta.md`
  lists every changed file at 100% line/branch and omits `HostAdapterOptions.cs`, which is
  actually 90.47% line / 50% branch at the class level. The changed lines (new
  `MailboxSettings` property) are covered and there is no regression, so the threshold is met;
  the evidence table is merely incomplete. Recorded as a non-blocking note.

### Approved Exceptions

- **None.**

### Removed/Skipped Tests

- **None.** The 3 skipped tests are pre-existing platform-gated cases (non-Windows COM,
  publish-output checks) unrelated to this feature.

---

## 9. Summary of Changes

### Commits in This Branch

- `bbdce9f` (HEAD) — feature implementation, 1 commit ahead of `main` (`48314bd`).

### Files Modified (C# scope)

- NEW: `src/OpenClaw.HostAdapter.Contracts/SchedulingContracts.cs`,
  `src/OpenClaw.HostAdapter/FreeBusyProjection.cs`,
  `src/OpenClaw.HostAdapter/MailboxSettingsOptions.cs`,
  `src/OpenClaw.HostAdapter/SchedulingRoutes.cs`,
  `tests/OpenClaw.Core.Tests/HostAdapterHttpClientSchedulingTests.cs`,
  `tests/OpenClaw.HostAdapter.Tests/FreeBusyProjectionTests.cs`.
- MODIFIED: `IHostAdapterClient.cs`, `HostAdapterHttpClient.cs`,
  `HostAdapterSchedulingService.cs`, `HostAdapterOptions.cs`, `Program.cs`,
  `ISchedulingService.cs`, `SlotProposer.cs`, `SlotProposer.Window.cs`,
  `SchedulingWorker.Pipeline.cs`, and test files (architecture-boundary, contract, endpoint,
  validation, web-factory).
- DELETED: `src/OpenClaw.Core/Agent/Contracts/MailboxSettingsDto.cs`,
  `src/OpenClaw.Core/Agent/Contracts/FreeBusyScheduleDto.cs` (relocated; field-for-field
  identical to the new `SchedulingContracts.cs` records, verified against baseline).
- Docs: `README.md`, `docs/api-reference.md` updated to document the new routes.

---

## 10. Compliance Verdict

### Overall Status: ✅ FULLY COMPLIANT

The change passes the full C# toolchain (format, lint, nullable type-check, architecture,
tests, contract round-trips) in a single clean pass verified independently by the reviewer.
Changed-code coverage meets the uniform thresholds (line >= 85%, branch >= 75%) with no
regression on changed lines. The relocation is field-for-field exact and additive at the
contract level. The architecture-boundary test change is a legitimate correction that
preserves the guard's protective intent (see code-review Section 8 / scope-change assessment).

**Fail-closed check:** All required baseline and post-change coverage artifacts are present and
were independently re-verified from regenerated cobertura output. No missing-artifact
condition exists.

### Recommendation

**Ready for merge.** No blocking findings. One non-blocking documentation note (the
coverage-delta evidence table omits `HostAdapterOptions.cs`) may be addressed at the author's
discretion.

---

## Appendix A: Test Inventory

New and extended tests verifying this feature:

- `FreeBusyProjectionTests` › `Busy_event_status_2_is_projected_as_busy_interval`
- `FreeBusyProjectionTests` › `Tentative_status_1_and_out_of_office_status_3_are_projected_as_busy`
- `FreeBusyProjectionTests` › `Free_event_status_0_is_excluded`
- `FreeBusyProjectionTests` › `Null_busy_status_is_treated_as_busy`
- `FreeBusyProjectionTests` › `Empty_input_yields_empty_intervals`
- `HostAdapterSchedulingServiceTests` › `GetMailboxSettingsAsync_DelegatesToClient_ReturnsDto`
- `HostAdapterSchedulingServiceTests` › `GetMailboxSettingsAsync_WhenEnvelopeNotOk_ReturnsDocumentedDefaults`
- `HostAdapterSchedulingServiceTests` › `GetFreeBusyAsync_DelegatesToClient_ReturnsSchedule`
- `HostAdapterSchedulingServiceTests` › `GetFreeBusyAsync_WhenEnvelopeNotOk_ReturnsEmptyIntervals`
- `HostAdapterHttpClientSchedulingTests` › mailboxSettings and getSchedule path-construction cases
- `HostAdapterEndpointTests` › mailboxSettings and getSchedule route cases
- `HostAdapterValidationTests` › getSchedule window/parameter validation cases
- `SchedulingDtoContractTests` › `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` round-trips
- `AgentArchitectureBoundaryTests` › `DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation` (new positive guard)

---

## Appendix B: Toolchain Commands Reference

```bash
# Formatting
csharpier check .

# Lint + nullable type-check (strict gate)
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true

# Tests + coverage
dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"

# Scope, evidence-location, and workflow-rule scans
git diff --name-status 48314bd6cb9c1a9f0bae4ca0c775a95ec52f3a61..bbdce9f26ba3913de677f38a1a9db54fc1deddd6
git diff --name-only 48314bd..bbdce9f | grep -E '^artifacts/(baselines|qa|evidence|coverage)/'
git diff --name-only 48314bd..bbdce9f | grep -E '\.github/workflows/|scripts/benchmarks/|\.github/actions/'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-06-13
**Policy Version:** Current (as of audit date)
