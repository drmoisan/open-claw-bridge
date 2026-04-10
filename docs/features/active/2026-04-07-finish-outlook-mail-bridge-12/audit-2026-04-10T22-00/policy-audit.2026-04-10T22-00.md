# Policy Audit

- Timestamp: 2026-04-10T22-00
- Feature: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Review mode: `full-feature`
- Base branch: `development` (explicitly provided in handoff)
- Head branch: `feature/finish-outlook-mail-bridge-12`
- Head commit: `d344f1810acdd2e9f583e4e4f23110aff271312d`
- Merge base: `886afaa7afdd2e60e7e9e25464f81cd6293bbf7e`
- Provenance: freshly collected `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`, direct code inspection, and fresh toolchain verification commands from this review run.
- Template source: Minimal fallback artifact generated because `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md` does not exist in this repository.
- Feature folder selection rule: Explicit user-provided folder `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`, confirmed by PR context scoping doc changes.

## Executive Summary

The feature branch delivers a functionally complete Outlook mail bridge with cache-backed RPC, COM boundary safety, and a mature acceptance suite. The C# toolchain (CSharpier, analyzers, nullable, tests) passes cleanly. PowerShell QA (format, analyze, tests) also passes. Three production/test files exceed the 500-line limit enforced by general code change policy §4, and one test harness violates the no-temporary-files constraint. These structural violations trigger remediation.

## Files Under Review

### In-scope production files (10 C# files)

- `src/OpenClaw.MailBridge/BridgeApplication.cs` (99 lines)
- `src/OpenClaw.MailBridge/BridgeStateStore.cs` (96 lines)
- `src/OpenClaw.MailBridge/CacheRepository.cs` (498 lines)
- `src/OpenClaw.MailBridge/ComActiveObject.cs` (125 lines)
- `src/OpenClaw.MailBridge/OutlookScanner.cs` (580 lines)
- `src/OpenClaw.MailBridge/OutlookStaExecutor.cs` (58 lines)
- `src/OpenClaw.MailBridge/PipeRpcWorker.cs` (404 lines)
- `src/OpenClaw.MailBridge/Program.cs` (6 lines)
- `src/OpenClaw.MailBridge/ResponseShaper.cs` (65 lines)
- `src/OpenClaw.MailBridge/ScanWorker.cs` (57 lines)
- `src/OpenClaw.MailBridge.Client/Program.cs` (~200 lines)
- `src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs`

### In-scope test files (9 C# files)

- `tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs` (162 lines)
- `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptHarness.cs` (432 lines)
- `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs` (123 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs` (183 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` (397 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs` (687 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs` (652 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Phase5.cs` (331 lines)
- `tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs` (143 lines)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs` (103 lines)

### In-scope PowerShell files (9 files)

- `scripts/install-mailbridge.ps1`
- `scripts/register-mailbridge-task.ps1`
- `scripts/test-mailbridge.ps1`
- `scripts/uninstall-mailbridge.ps1`
- `tests/scripts/install-mailbridge.Tests.ps1`
- `tests/scripts/register-mailbridge-task.Tests.ps1`
- `tests/scripts/runner-scripts.Tests.ps1`
- `tests/scripts/test-mailbridge.Tests.ps1`
- `tests/scripts/uninstall-mailbridge.Tests.ps1`

### In-scope documentation

- `docs/mailbridge-runbook.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/issue.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/plan.2026-04-07T07-55.md`

---

## 1. General Unit Test Policy Compliance

### 1.1 Independence and Isolation

- [✅] [PASS] Tests run independently and do not depend on execution order. Verified: `dotnet test` passes with 87 total tests (86 passed, 1 skipped).
- [✅] [PASS] Each test targets a single function, method, or behavior. Confirmed by inspection of test method structure (Arrange-Act-Assert pattern throughout).

### 1.2 Determinism and Speed

- [✅] [PASS] Tests are deterministic. Fresh run confirms identical results to evidence at T17-22. No flakiness observed.
- [✅] [PASS] Test execution completes in ~11 seconds for 87 tests.

### 1.3 Coverage

- [✅] [PASS] Overall C# line coverage is 89.4% (>= 80% threshold).
- [✅] [PASS] Changed/new line coverage is 100% (>= 80% threshold).
- [✅] [PASS] New production module coverage is 100% (>= 90% threshold).
- [⚠️] [PARTIAL] PowerShell coverage is 78.7% (218/277 commands), below the 80% repo minimum. However, the baseline measurement of 100% was an artifact of the MCP tool returning an empty `Files` array at Phase 0. The 78.7% represents the first real measurement. This is documented in `evidence/qa-gates/powershell-coverage-thresholds.2026-04-10T17-35.md`.

### 1.4 No External Dependencies

- [✅] [PASS] C# tests use in-memory SQLite and mock/stub patterns. No network, database, or external API calls.
- [✅] [PASS] PowerShell tests mock external commands (Pester `Mock`).

### 1.5 No Temporary Files

- [❌] [FAIL] `CodexWebSetupScriptHarness.cs` (line 16) uses `Path.GetTempPath()` and creates temporary directories with `Directory.CreateDirectory()`. This violates the general unit test policy §4: "Creation and use of temporary files on the local filesystem is expressly prohibited." No approved exception exists for this pattern. This file is in the feature diff.

### 1.6 Clear Failure Messages and Documentation

- [✅] [PASS] Test methods use descriptive names and FluentAssertions for clear failure messages. MSTest assertions are used sparingly where FluentAssertions is not practical.

---

## 2. General Code Change Policy Compliance

### 2.1 Design Principles

- [✅] [PASS] Simplicity: The cache-first architecture keeps the design straightforward with clear separation between scanning, caching, RPC, and response shaping.
- [✅] [PASS] Reusability: Shared helpers (`BridgeIdCodec`, `BodySanitizer`, `ResponseShaper`) are factored out.
- [✅] [PASS] Extensibility: Interfaces (`IBridgeRepository`, `IOutlookScanner`, `IOutlookStaExecutor`) support testability and future extension.
- [✅] [PASS] Separation of concerns: COM boundary (`ComActiveObject`, `OutlookStaExecutor`), persistence (`CacheRepository`), RPC (`PipeRpcWorker`), and response shaping (`ResponseShaper`) are cleanly separated.

### 2.2 Error Handling, Logging, and Contracts

- [✅] [PASS] Fail fast: `BuildPipeSecurity()` throws when SID resolution fails. `BridgeSettingsValidator` rejects invalid config before host starts.
- [✅] [PASS] No broad catch-alls without context. Exception handlers in `PipeRpcWorker` and `OutlookScanner` re-surface errors through bridge error codes or logging.
- [✅] [PASS] Logging uses `ILogger<T>` pattern at appropriate levels.

### 2.3 Module and File Structure

- [❌] [FAIL] `OutlookScanner.cs` is 580 lines, exceeding the 500-line limit (§4.1).
- [❌] [FAIL] `MailBridgeRuntimeTests.cs` is 687 lines, exceeding the 500-line limit (§4.1).
- [❌] [FAIL] `MailBridgeRuntimeTests.OutlookScanner.cs` is 652 lines, exceeding the 500-line limit (§4.1).
- [✅] [PASS] All other files are under 500 lines. `CacheRepository.cs` is at 498 lines (within limit).
- [✅] [PASS] Files are cohesive: each file has a single clear purpose.

### 2.4 Naming, Docs, and Comments

- [✅] [PASS] Public classes and methods have XML doc comments. Naming is descriptive and follows PascalCase/camelCase conventions.
- [✅] [PASS] Comments explain "why" where non-obvious (e.g., `WaitForPipeDrain` rationale in `WriteResponse`).

### 2.5 After Making Changes — Toolchain

- [✅] [PASS] CSharpier: Checked 37 files, 0 changes needed. (Verified: this review.)
- [✅] [PASS] MSBuild analyzers + EnforceCodeStyleInBuild: 0 warnings, 0 errors. (Verified: this review.)
- [✅] [PASS] MSBuild nullable + TreatWarningsAsErrors: 0 warnings, 0 errors. (Verified: this review.)
- [✅] [PASS] C# tests: 86 passed, 1 skipped (platform guard), 0 failed. (Verified: this review.)
- [✅] [PASS] PowerShell format: 0 files changed. (Evidence: `powershell-format.2026-04-10T17-22.md`.)
- [✅] [PASS] PowerShell analyze: 0 findings (scoped to `scripts/` and `tests/scripts/`). (Evidence: `powershell-analyze.2026-04-10T17-35.md`.)
- [✅] [PASS] PowerShell test: 19 tests, 0 failures. (Evidence: `powershell-test.2026-04-10T17-35.md`.)

---

## 3. Language-Specific Code Change Policy Compliance

### 3.1 C# Code Change Policy

- [✅] [PASS] Strong contracts: Public methods have explicit types. `var` used only where type is obvious.
- [✅] [PASS] Null safety: Nullable reference types enabled. Guard clauses and nullable annotations used throughout. Zero nullable warnings.
- [✅] [PASS] Composition: `BridgeApplication.BuildHost` composes via DI without deep inheritance.
- [✅] [PASS] Async/await: I/O-bound operations use async. `using`/`await using` for disposable resources.
- [✅] [PASS] Internal visibility: Production classes are `internal sealed` where appropriate.

### 3.2 PowerShell Code Change Policy

- [✅] [PASS] Scripts use `CmdletBinding()` and named parameters.
- [✅] [PASS] PSScriptAnalyzer reports zero findings for `scripts/` and `tests/scripts/`.
- [✅] [PASS] Formatter reports zero changes needed.
- [✅] [PASS] Scripts are compatible with PowerShell 7+ (enforced by PSScriptAnalyzer settings).

---

## 4. Language-Specific Unit Test Policy Compliance

### 4.1 C# Unit Test Policy

- [✅] [PASS] MSTest (`[TestClass]`, `[TestMethod]`) used throughout.
- [✅] [PASS] Moq used for mocking (e.g., `MailBridgeRuntimeTestDoubles`).
- [✅] [PASS] FluentAssertions used for assertions.
- [✅] [PASS] Arrange-Act-Assert pattern followed consistently.

### 4.2 PowerShell Unit Test Policy

- [✅] [PASS] Pester v5 used with repo config.
- [✅] [PASS] Tests organized in `tests/scripts/` mirroring `scripts/`.
- [✅] [PASS] Naming: `*.Tests.ps1` convention.
- [✅] [PASS] 19 tests, 0 failures, 0 skipped.

---

## 5. Test Coverage Detail

### C# Coverage

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Overall line coverage | 89.4% | >= 80% | PASS |
| Changed/new line coverage | 100% | >= 80% | PASS |
| New production coverage | 100% | >= 90% | PASS |
| Baseline regression | 89.4% < 100% (baseline) | N/A | Explained: baseline was trivial pre-feature |

### PowerShell Coverage

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Overall command coverage | 78.7% (218/277) | >= 80% | PARTIAL |
| Changed/new line coverage | 77.67% | >= 80% | PARTIAL |
| New production coverage | 100% | >= 90% | PASS |
| Baseline regression | 78.7% < 100% (baseline) | N/A | Explained: baseline was measurement artifact |

---

## 6. Test Execution Metrics

### C# Tests (verified this review)

- Total: 87
- Passed: 86
- Skipped: 1 (`Com_active_object_create_and_logon_should_throw_on_non_windows` — platform guard)
- Failed: 0
- Duration: ~11 seconds

### PowerShell Tests (evidence from T17-35)

- Total: 19
- Passed: 19
- Skipped: 0
- Failed: 0

---

## 7. Code Quality Checks

| Check | Command | Result | Verified |
|-------|---------|--------|----------|
| CSharpier | `csharpier check .` | PASS (37 files, 0 changes) | This review |
| MSBuild Analyzers | `dotnet build ... /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | PASS (0 warnings, 0 errors) | This review |
| MSBuild Nullable | `dotnet build ... /p:Nullable=enable /p:TreatWarningsAsErrors=true` | PASS (0 warnings, 0 errors) | This review |
| C# Tests | `dotnet test ... --no-build` | PASS (87 total, 0 failed) | This review |
| PowerShell Format | `mcp_drmcopilotext_run_poshqc_format` | PASS (0 changes) | Evidence T17-22 |
| PowerShell Analyze | `mcp_drmcopilotext_run_poshqc_analyze` | PASS (0 findings, scoped) | Evidence T17-35 |
| PowerShell Tests | `mcp_drmcopilotext_run_poshqc_test` | PASS (19 tests, 0 failed) | Evidence T17-35 |
| Framework Targets | project TFM + runtimeconfig validation | PASS (all net10.0-windows) | Evidence T17-35 |

---

## 8. Gaps and Exceptions

### 8.1 File Size Violations (FAIL)

Three files exceed the 500-line limit:

| File | Lines | Limit | Over By |
|------|-------|-------|---------|
| `OutlookScanner.cs` | 580 | 500 | 80 |
| `MailBridgeRuntimeTests.cs` | 687 | 500 | 187 |
| `MailBridgeRuntimeTests.OutlookScanner.cs` | 652 | 500 | 152 |

Remediation required: Extract helper methods from `OutlookScanner.cs` into a separate file. Split the two test files into additional partial class files or separate test classes.

### 8.2 Temporary File Usage in Test Harness (FAIL)

`CodexWebSetupScriptHarness.cs` creates and uses temporary directories via `Path.GetTempPath()` at line 16. The general unit test policy §4 prohibits this. No approved exception exists.

Remediation required: Either document an explicit exception for this harness (it tests a Codex bash setup script which requires filesystem interaction) or refactor to eliminate temp file usage.

### 8.3 PowerShell Coverage Below Threshold (PARTIAL)

PowerShell coverage at 78.7% is below the 80% repo minimum. However, the baseline comparison is invalid because the Phase 0 MCP tool returned 100% from an empty `Files` array. The 78.7% is the first real measurement. This is a known measurement artifact documented in `evidence/qa-gates/powershell-coverage-thresholds.2026-04-10T17-35.md`.

Disposition: This is a meaningful gap but has a documented cause. Recommend targeting 80% in a follow-up rather than blocking this feature.

### 8.4 Evidence Inconsistency (PARTIAL)

`evidence/qa-gates/feature-completion.2026-04-10T17-35.md` reports "95 tests, 0 failures, 96.6% line coverage" for C#, but fresh verification at this review confirms 87 tests and 89.4% coverage. The feature-completion artifact appears to contain an inaccurate test count and coverage figure.

Remediation required: Correct the feature-completion evidence artifact.

---

## 9. Summary of Changes

This feature branch completes the Outlook mail bridge from partial stub state to a functionally complete, cache-backed RPC surface. Key changes:

1. All projects retargeted to `net10.0-windows`.
2. Full Outlook scanner with COM acquire/release, Inbox/Calendar enumeration, and stale-cache state management.
3. Cache repository with message/event upsert and deterministic queries.
4. Response shaper with safe/enhanced mode privacy controls.
5. Complete RPC handler surface with deterministic validation and error codes.
6. Client pipe-name resolution from settings with `--pipe-name` override.
7. Named-pipe race condition fix and COM logon dialog fix.
8. Scripts: install, register-task, uninstall, and acceptance test suite with suites A-F.
9. 87 C# tests and 19 PowerShell Pester tests.

---

## 10. Compliance Verdict

**Needs revision.**

The implementation is functionally complete and the C# toolchain passes cleanly. Three files exceed the 500-line limit, one test harness uses prohibited temporary files, and one evidence artifact contains an inaccurate test count. These findings require remediation before merge.

| Category | Status |
|----------|--------|
| General Code Change Policy | ❌ FAIL (3 files exceed 500-line limit) |
| General Unit Test Policy | ❌ FAIL (temp file usage in test harness) |
| C# Code Change Policy | ✅ PASS |
| C# Unit Test Policy | ✅ PASS |
| PowerShell Code Change Policy | ✅ PASS |
| PowerShell Unit Test Policy | ⚠️ PARTIAL (coverage 78.7% < 80%) |
| Toolchain (format/lint/type/test) | ✅ PASS |
| Acceptance Criteria | ✅ PASS (10/10 verified) |
| Supporting Documentation | ⚠️ PARTIAL (evidence inconsistency) |

---

## Appendix A: Test Inventory

### C# Test Classes

| Test Class | File | Test Count | Focus |
|------------|------|------------|-------|
| MailBridgeRuntimeTests | MailBridgeRuntimeTests.cs | Multiple | Bridge application, config validation, state store, pipe ACL |
| MailBridgeRuntimeTests (OutlookScanner) | MailBridgeRuntimeTests.OutlookScanner.cs | Multiple | COM boundary orchestration, inbox/calendar scanning |
| MailBridgeRuntimeTests (Phase5) | MailBridgeRuntimeTests.Phase5.cs | Multiple | RPC validation, error mapping, stale-cache, client behavior |
| BridgeContractsCoverageTests | BridgeContractsCoverageTests.cs | Multiple | DTO/ID helpers, codec coverage |
| MailBridgeClientTests | MailBridgeClientTests.cs | Multiple | Client parse/build/send, exit-code mapping |
| ResponseShaperTests | ResponseShaperTests.cs | Multiple | Safe/enhanced mode shaping, preview sanitization |
| MailBridgeTests | MailBridgeTests.cs | Multiple | Legacy bridge tests |
| CodexWebSetupScriptTests | CodexWebSetupScriptTests.cs | Multiple | Codex web setup script validation |

### PowerShell Test Files

| File | Test Count | Focus |
|------|------------|-------|
| install-mailbridge.Tests.ps1 | Multiple | Install script preflight and layout |
| register-mailbridge-task.Tests.ps1 | Multiple | Scheduled task registration |
| runner-scripts.Tests.ps1 | Multiple | Build/Run/Test script behavior |
| test-mailbridge.Tests.ps1 | Multiple | Acceptance suite harness |
| uninstall-mailbridge.Tests.ps1 | Multiple | Uninstall script cleanup |

---

## Appendix B: Toolchain Commands Reference

### C# Toolchain (verified this review)

1. **Formatting:** `csharpier check .`
2. **Linting/Analyzers:** `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
3. **Type checking/Nullable:** `dotnet build OpenClaw.MailBridge.sln -c Debug /p:Nullable=enable /p:TreatWarningsAsErrors=true`
4. **Tests:** `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`

### PowerShell Toolchain (evidence-based, no production/test PS changes since)

1. **Formatting:** `mcp_drmcopilotext_run_poshqc_format`
2. **Analyzing:** `mcp_drmcopilotext_run_poshqc_analyze` (scoped to `scripts/` and `tests/scripts/`)
3. **Testing:** `mcp_drmcopilotext_run_poshqc_test` (scoped to `tests/scripts/`)
