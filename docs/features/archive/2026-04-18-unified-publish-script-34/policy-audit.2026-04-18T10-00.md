# Policy Compliance Audit: unified-publish-script (Issue #34)

**Audit Date:** 2026-04-18
**Feature branch:** `feature/unified-publish-script-34`
**Head commit:** `c4bd410` (feat(#34): unified publish script replaces build-msix.ps1)
**Base branch:** `development`
**Merge-base SHA:** `500da7064a0ca3ae59c3d568235069b9c38b197b`
**Work Mode:** `full-feature` (from `issue.md`)
**Code Under Test:**
- `scripts/Publish.ps1` (NEW, 183 lines)
- `scripts/Publish.Helpers.psm1` (NEW, 456 lines)
- `tests/scripts/Publish.Tests.ps1` (NEW, 184 lines)
- `tests/scripts/Publish.Helpers.Tests.ps1` (NEW, 442 lines)
- `scripts/build-msix.ps1` (DELETED, -294 lines)
- `tests/scripts/build-msix.Tests.ps1` (DELETED, -173 lines)
- `.github/workflows/build-msix.yml` (DELETED, renamed)
- `.github/workflows/publish.yml` (NEW, 46 lines)
- `README.md` (MODIFIED, +25/-5)
- `docs/mailbridge-runbook.md` (MODIFIED, +29/-7)

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| PowerShell | 4 new prod/test + 2 deleted | 72 tests (51 new) | PASS (72/72) | 67.13% | 81.71% | 96.94% |
| YAML (CI) | 2 files (rename) | N/A | N/A | N/A | N/A | N/A |
| Markdown | 2 modified docs + scoping/evidence | N/A | N/A | N/A | N/A | N/A |
| Python | 0 | N/A | N/A | N/A | N/A | N/A |
| C# | 0 | N/A | N/A | N/A | N/A | N/A |
| TypeScript | 0 | N/A | N/A | N/A | N/A | N/A |

---

## Executive Summary

The feature branch introduces `scripts/Publish.ps1` and `scripts/Publish.Helpers.psm1` as a unified publish entry point that replaces `scripts/build-msix.ps1`. The change retires two legacy files (`build-msix.ps1`, `build-msix.Tests.ps1`), renames `.github/workflows/build-msix.yml` to `.github/workflows/publish.yml` with triggers preserved, updates `README.md` and `docs/mailbridge-runbook.md` to document the new entry point, and adds 51 new Pester tests.

All toolchain checks passed in the existing executor evidence artifacts:
- PoshQC Format: PASS, zero files changed, zero findings.
- PoshQC Analyze (PSScriptAnalyzer): PASS, 0 diagnostics.
- Pester test run: PASS, 72/72 tests green, 0 failures, 0 regressions from baseline.
- Coverage: repo-wide 81.71% (baseline 67.13%, +14.58pp), targeted new-code 96.94%.

No Python, C#, or TypeScript source files are in the branch diff, so their coverage artifacts are not required.

**Policy documents evaluated:**
- PASS `CLAUDE.md` (repo standing instructions)
- PASS `.claude/rules/general-code-change.md`
- PASS `.claude/rules/general-unit-test.md`
- PASS `.claude/rules/powershell.md`
- PASS `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- N/A Python — zero Python files in branch diff.
- PASS PowerShell (`.claude/rules/powershell.md`) — 4 PowerShell files (2 production + 2 test).
- N/A C# — zero C# files in branch diff.
- N/A TypeScript — zero TypeScript files in branch diff.

**Temporary artifacts cleanup:**
- PASS No temporary/throwaway scripts in the change. The retirement of `scripts/build-msix.ps1` is planned (not incidental).

**PR context artifact refresh note:**
The on-disk `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` at the start of this review referenced a prior feature branch (`feature/openclaw-agent-docker-30` / issue #30) and were stale. The summary artifact has been refreshed to reflect the current branch (`feature/unified-publish-script-34`, head `c4bd410`, base `development`, merge-base `500da70`). The appendix file has not been regenerated for this review; evidence was taken directly from the branch diff and the executor-produced evidence artifacts under `docs/features/active/2026-04-18-unified-publish-script-34/evidence/`.

## Rejected Scope Narrowing

No caller narrowing detected. The caller prompt requested the full feature-vs-base audit; scope is the full branch diff against `origin/development` at merge-base `500da7064a0ca3ae59c3d568235069b9c38b197b`. No language with changed files in the diff has been marked N/A or UNVERIFIED — PowerShell has explicit PASS verdicts below, and Python/C#/TypeScript carry explicit N/A verdicts tied to zero changed files.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** | PASS | Each Pester `It` block performs its own mock setup in `BeforeEach`; state variables (`$script:MakePriCallCount`, etc.) are reset in `BeforeEach`. Order-insensitive by inspection of `Publish.Helpers.Tests.ps1` and `Publish.Tests.ps1`. |
| **Isolation** | PASS | Tests are grouped per helper function with one `Context` per function and one `It` per behavior. Each `It` asserts a single outcome. |
| **Fast Execution** | PASS | Final Pester run: 72 tests total, evidence `evidence/qa-gates/final-pester.2026-04-18T00-00.md`. Helpers use mocks for SDK tools, `Copy-Item`, `Set-Content`, etc.; no file I/O. |
| **Determinism** | PASS | External tools (`dotnet`, `makepri`, `makeappx`, `signtool`) are replaced by global function shims set up in `BeforeAll` of `Publish.Helpers.Tests.ps1`. `Get-FileHash` is mocked to a deterministic value. |
| **Readability & Maintainability** | PASS | Test names describe the behavior under test (for example, `'rejects a 3-part version via ValidatePattern'`, `'emits Write-Warning and does not copy when a secrets/ dir exists'`). AAA pattern observable in `BeforeEach`/`It`/`Assert-MockCalled`. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline repo-wide coverage 67.13% captured in `evidence/baseline/baseline-pester.2026-04-18T00-00.md`. Command: `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection. |
| **No Coverage Regression** | PASS | Post-change repo-wide: 81.71% (+14.58pp). Evidence: `evidence/qa-gates/coverage-delta.2026-04-18T00-00.md`. |
| **New Code Coverage ≥90%** | PASS | Targeted new-code coverage for `Publish.ps1` + `Publish.Helpers.psm1`: 96.94% (222/229 analyzed commands). Evidence: `evidence/qa-gates/final-pester.2026-04-18T00-00.md`. |
| **Comprehensive Coverage** | PASS | All 11 exported helpers have dedicated `Context` blocks with positive, negative, and `-WhatIf` tests. `Publish.ps1` orchestrator coverage includes parameter validation, stage ordering, per-project publish flags, skip-sign path, and output paths. |
| **Positive Flows** | PASS | For example: `stamps the 4-part version into Package.Identity.Version`, `copies both compose files when present`, `invokes makepri createconfig then new`. |
| **Negative Flows** | PASS | For example: `rejects a 3-part version via ValidatePattern`, `throws when bridge publish dir is missing`, `throws on non-zero exit` for each SDK-tool wrapper. |
| **Edge Cases** | PASS | `.env.example` absent vs present; `secrets/` directory detected; MSIX path composition; manifest `manifest.json` exclusion from `files[]`. |
| **Error Handling** | PASS | Non-zero exit codes from `dotnet`, `makepri`, `makeappx`, `signtool` each covered with an `It 'throws on non-zero exit'`. |
| **Concurrency** | N/A | Publish is a single-operator sequential script. |
| **State Transitions** | N/A | No explicit state machine; stage ordering is tested instead via `Publish.Tests.ps1` Context `stage ordering`. |

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | Pester `Should -Throw -ExpectedMessage` patterns include wildcard matches on the exact thrown strings (for example, `'*Bridge publish directory not found*'`). |
| **Arrange-Act-Assert Pattern** | PASS | `BeforeEach` performs arrange; the `It` body calls the function (act) then asserts via `Should` or `Assert-MockCalled`. |
| **Document Intent** | PASS | Each `Describe`/`Context`/`It` name reads as a sentence describing behavior. No docstrings required beyond the names given Pester idioms. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, no database, no process spawning in the test files. Global function shims intercept `dotnet`, `makepri`, `makeappx`, `signtool`. |
| **Use Mocks/Stubs** | PASS | Pester `Mock -ModuleName Publish.Helpers` used for `Test-Path`, `Get-ChildItem`, `Get-Command`, `Get-Content`, `Set-Content`, `Copy-Item`, `Remove-Item`, `New-Item`, `Get-Item`, `Get-FileHash`, `Resolve-Path`. Shim functions in `$global:` scope intercept tool invocations. |
| **Environment Stability** | PASS | No temporary files created by the tests; assertion checked by inspection of `Publish.Helpers.Tests.ps1` and `Publish.Tests.ps1` — neither file calls `New-TemporaryFile`, `[System.IO.Path]::GetTempFileName()`, or writes outside the mocked paths. The `$global:PublishTestCalls` ArrayList is documented as a cross-scope call log with a justified `PSAvoidGlobalVars` suppression. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #34 + `spec.md` + `user-story.md` in the active feature folder. |
| **Read existing change plans** | PASS | `plan.2026-04-18T00-00.md` consumes spec, user-story, and issue. Plan contains PREFLIGHT section with 17 passing gates. |
| **Document the plan** | PASS | Atomic plan with Phase 0–6 captured at `docs/features/active/2026-04-18-unified-publish-script-34/plan.2026-04-18T00-00.md`. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | `Publish.ps1` orchestrator is 183 lines; it composes 10 helper invocations. Helpers are each small and purpose-specific. |
| **Reusability** | PASS | The helper module split eliminates duplicated MSIX logic. `Find-WindowsSdkTool`, `New-ManifestEntry`, `Copy-DockerArtifact` are all reusable units. |
| **Extensibility** | PASS | Orchestrator drives publish via a `$PublishMatrix` array of `pscustomobject` entries; adding a new project requires adding one matrix entry rather than modifying stage code. Helpers use `[CmdletBinding(SupportsShouldProcess = $true)]` with named parameters and defaults. |
| **Separation of concerns** | PASS | `Publish.ps1` handles parameter binding, stage orchestration, and progress output only. All I/O, tool invocation, hashing, XML stamping, and artifact copying live in `Publish.Helpers.psm1`. `New-ManifestEntry` and `Get-StampedAppxManifestXml` are pure helpers with no side effects. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | `Publish.ps1` is the orchestrator; `Publish.Helpers.psm1` is the helper surface. Each helper has a single responsibility. |
| **Under 500 lines** | PASS | `Publish.ps1` 183, `Publish.Helpers.psm1` 456, `Publish.Tests.ps1` 184, `Publish.Helpers.Tests.ps1` 442. All under 500. Evidence: `evidence/qa-gates/end-state-line-counts.2026-04-18T00-00.md`. |
| **Public vs internal** | PASS | `Publish.Helpers.psm1` uses an explicit `Export-ModuleMember -Function` list of exactly 11 functions (verified by a test). |
| **No circular dependencies** | PASS | `Publish.ps1` imports `Publish.Helpers.psm1`; `Publish.Helpers.psm1` imports nothing from the repo. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | Functions use approved PowerShell verbs (`Invoke-`, `Copy-`, `Find-`, `Get-`, `New-`, `Write-`) plus descriptive nouns. Parameters are descriptive (`BridgePublishDir`, `StagingDir`, `OutputMsixPath`). |
| **Docs/docstrings** | PASS | `Publish.ps1` and `Publish.Helpers.psm1` both open with comment-based help blocks. Each helper function has its own `<# .SYNOPSIS .DESCRIPTION #>` block. |
| **Comment why, not what** | PASS | For example, the `Import-Module` line in `Publish.ps1` includes a comment explaining why `-Force` is omitted (Pester mock preservation). `Write-PublishManifest` explains why `Sort-Object -Culture 'en-US'` is used (deterministic ordering across locales). |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | `Invoke-PoshQCFormat -Root . -ScanFolders @('scripts','tests')`; evidence `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`. Zero files changed. |
| **2. Linting** | PASS | `Invoke-PoshQCAnalyze -Root . -ScanFolders @('scripts','tests')`; evidence `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`. Zero findings. |
| **3. Type checking** | N/A | Not applicable for PowerShell per `.claude/rules/powershell.md`. |
| **4. Testing** | PASS | `Invoke-PoshQCTest -Root . -ScanFolders @('scripts','tests')`; evidence `evidence/qa-gates/final-pester.2026-04-18T00-00.md`. 72/72 passing, 0 failures, 0 skipped. |
| **Full toolchain loop** | PASS | Evidence artifacts cover a single-pass clean run: format emitted zero "Formatted ..." lines; analyze emitted 0 diagnostics; Pester run green. No loop restart was triggered. |
| **Explicit reporting** | PASS | All toolchain commands are documented in this audit and in the per-phase evidence artifacts. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Definition of Done and `plan.2026-04-18T00-00.md` summarize the change set. Commit `c4bd410` message prefix `feat(#34)` describes intent. |
| **Design choices explained** | PASS | Planner resolutions (Q1 strict `ValidatePattern`, Q2 `msix.pubxml` retention, Q3 structural stability) captured in both `plan.md` and the `Publish.ps1` header. |
| **Update supporting documents** | PASS | `README.md` release-build section and `docs/mailbridge-runbook.md` Section 3 updated. Evidence: `evidence/qa-gates/final-build-msix-refs.2026-04-18T00-00.md` confirms no residual `build-msix` references in `README.md` or the runbook. |
| **Provide next steps** | PASS | Plan Phase 6 reconciles Definition of Done; reconciliation recorded in `evidence/qa-gates/definition-of-done-reconciliation.2026-04-18T00-00.md`. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3B: PowerShell Code Change Policy Compliance

#### 3B.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with Invoke-Formatter** | PASS | `Invoke-PoshQCFormat` run; zero files changed. Evidence: `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`. |
| **Linting with PSScriptAnalyzer** | PASS | `Invoke-PoshQCAnalyze` run; 0 diagnostics. Evidence: `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`. |
| **Fix all findings** | PASS | No findings to fix. Two targeted `SuppressMessageAttribute` decorations are present in the source with documented justification: `PSUseOutputTypeCorrectly` on `Get-StampedAppxManifestXml` (XmlDocument return-type explanation) and `PSUseShouldProcessForStateChangingFunctions` on `New-ManifestEntry` (pure function). |
| **PowerShell 7+ compatible** | PASS | Both files carry `#Requires -Version 7.0`. |

#### 3B.2 PowerShell Design & Safety

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Advanced functions** | PASS | All exported helpers use `[CmdletBinding(...)]`. `Publish.ps1` uses `[CmdletBinding(SupportsShouldProcess = $true)]`. |
| **Parameter validation** | PASS | `[Parameter(Mandatory = $true)]` applied where required; `[ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]` applied to every `-Version` parameter (orchestrator, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Write-PublishManifest`); `[ValidateSet('Debug','Release')]` on `-Configuration`. |
| **Avoid global state** | PASS | Production code uses only parameters and local variables. `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at module top. Test file has one documented `$global:PublishTestCalls` with a justified suppression. |
| **Error handling** | PASS | Every external-tool invocation checks `$LASTEXITCODE` and `throw`s with the tool name, exit code, and captured output. Pre-condition checks (`Test-Path`) in `Invoke-LayoutAssembly` throw with the missing path in the message. |

#### 3B.3 Structure, Naming, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive and under 500 lines** | PASS | 183 / 456 / 184 / 442 lines for the four files. All under 500. |
| **Approved verbs** | PASS | Function names: `Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`, `Invoke-DotnetPublish`, `Copy-DockerArtifact`, `New-ManifestEntry`, `Write-PublishManifest`. All verbs are on the PowerShell approved list. |
| **Comment why** | PASS | Inline comments in `Publish.ps1` and `Publish.Helpers.psm1` explain rationale (mock preservation, deterministic sort, `PSUseOutputTypeCorrectly` suppression, pure-function justification). |

#### 3B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Step 1: Format** | PASS | Zero file changes; see `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`. |
| **Step 2: Analyze** | PASS | 0 diagnostics; see `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`. |
| **Step 3: Type check** | N/A | PowerShell. |
| **Step 4: Test** | PASS | 72/72 passing; see `evidence/qa-gates/final-pester.2026-04-18T00-00.md`. |
| **Rerun loop if needed** | PASS | Single pass clean; no restart needed. |

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4B: PowerShell Unit Test Policy Compliance

#### 4B.1 Framework and Scope

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use Pester v5.x** | PASS | `BeforeAll`, `BeforeEach`, `AfterAll`, `Describe`, `Context`, `It`, `Should` (modern syntax) used throughout. |
| **Use PoshQC Configuration** | PASS | `Invoke-PoshQCTest` run with coverage collection. Repo config path referenced by the tool by default. |
| **PowerShell 7+ Compatible** | PASS | `#Requires -Version 7.0` in both test files. |

#### 4B.2 Test Style and Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Focused Unit Tests** | PASS | One `It` asserts one behavior; mocks scoped per `It` via `Mock -ModuleName Publish.Helpers`. |
| **Test Behavior Over Implementation** | PASS | Tests assert observable contracts (arg order to `signtool`, exit-code handling, structure of manifest entries) rather than private implementation details. |
| **Mocking Used Sparingly** | PASS | External effects (filesystem, SDK tools, hashing) are mocked. Pure helpers (`Get-StampedAppxManifestXml`, `New-ManifestEntry` assertions) rely on real code paths with mocked input sources only. |
| **Organization** | PASS | Test file paths mirror code paths: `scripts/Publish.ps1` → `tests/scripts/Publish.Tests.ps1`; `scripts/Publish.Helpers.psm1` → `tests/scripts/Publish.Helpers.Tests.ps1`. |

#### 4B.3 Naming and Readability

| Requirement | Status | Evidence |
|------------|--------|----------|
| **File Naming** | PASS | `Publish.Tests.ps1`, `Publish.Helpers.Tests.ps1`. |
| **Describe/Context/It Structure** | PASS | Top-level `Describe`; `Context` per function or concern; `It` per behavior. |
| **Logical Grouping** | PASS | Helpers grouped by function name; orchestrator grouped by `parameter validation`, `stage ordering`, `per-project publish flags`, `skip-sign path`, `output paths`. |
| **Docstrings/Comments** | PASS | Test-file comment-based help blocks explain purpose and mock strategy, including the rationale for the global call log in `Publish.Tests.ps1`. |

#### 4B.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use PoshQCTest Command** | PASS | `Invoke-PoshQCTest -Root .` in both baseline and final gates. |
| **No Alternative Test Runners** | PASS | Only Pester via PoshQC is used. |

---

## 5. Test Coverage Detail

### Publish.Helpers module exports (1 test)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| exports the expected 11 helper functions | Positive | PASS |

### Find-WindowsSdkTool (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| returns the SDK-bin match when ProgramFiles(x86) has a matching x64 binary | Positive | PASS |
| falls back to Get-Command when the SDK bin root is missing | Edge Case | PASS |
| throws when the tool cannot be located anywhere | Negative / Error Handling | PASS |

### Get-StampedAppxManifestXml (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| stamps the 4-part version into Package.Identity.Version | Positive | PASS |
| preserves other Identity attributes unchanged | Positive / Invariant | PASS |
| rejects a 3-part version via ValidatePattern | Negative | PASS |

### Invoke-VersionStamp (2 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| writes stamped XML to `<stagingDir>/AppxManifest.xml` | Positive | PASS |
| -WhatIf leaves the staging file absent | Safety | PASS |

### Invoke-LayoutAssembly (4 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| throws when bridge publish dir is missing | Negative | PASS |
| throws when client publish dir is missing | Negative | PASS |
| calls Copy-Item for bridge, client, and assets on success | Positive | PASS |
| -WhatIf skips all copies | Safety | PASS |

### Invoke-MakePri (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| invokes makepri createconfig then new | Positive | PASS |
| throws on non-zero exit | Error Handling | PASS |
| -WhatIf does not invoke the tool | Safety | PASS |

### Invoke-MakeAppx (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| passes /d /p /nv /o exactly and preserves OutputMsixPath | Positive | PASS |
| throws on non-zero exit | Error Handling | PASS |
| -WhatIf does not invoke the tool | Safety | PASS |

### Invoke-SignTool (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| passes /sha1, /fd SHA256, /tr, /td SHA256 with msix path last | Positive | PASS |
| throws on non-zero exit | Error Handling | PASS |
| -WhatIf does not invoke the tool | Safety | PASS |

### Invoke-DotnetPublish (3 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| passes -c -o /p:Deterministic=true verbatim | Positive | PASS |
| appends ExtraArgs after required args | Positive / Edge Case | PASS |
| throws on non-zero exit | Error Handling | PASS |

### Copy-DockerArtifact (6 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| copies both compose files when present | Positive | PASS |
| copies .env.example when present | Positive | PASS |
| skips .env.example silently when absent | Edge Case | PASS |
| recursively copies deploy/docker/** | Positive | PASS |
| emits Write-Warning and does not copy when a secrets/ dir exists | Negative / Security | PASS |
| never copies secrets/.env.anthropic even if present | Negative / Security | PASS |

### New-ManifestEntry (4 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| returns path as forward-slash-relative string | Positive | PASS |
| returns size as non-negative integer | Positive | PASS |
| returns sha256 as 64-character lowercase hex string | Positive | PASS |
| calls Get-FileHash with -Algorithm SHA256 | Positive / Contract | PASS |

### Write-PublishManifest (4 tests)

| Test Name | Scenario Type | Status |
|-----------|---------------|--------|
| writes JSON with version, generatedAt, files and excludes manifest.json | Positive | PASS |
| sorts files ascending by path | Positive | PASS |
| each file entry has exactly path, size, sha256 | Contract | PASS |
| structural stability only (Q3) - 64-char lowercase hex, present path, non-negative size | Contract | PASS |

### scripts/Publish.ps1 orchestrator (12 tests)

| Context | Test Name | Scenario Type | Status |
|---------|-----------|---------------|--------|
| parameter validation | throws when neither -SkipSign nor -CertThumbprint is provided | Negative / Fail-fast | PASS |
| parameter validation | accepts -SkipSign alone | Positive | PASS |
| parameter validation | accepts -CertThumbprint alone | Positive | PASS |
| parameter validation | rejects a 3-part version (Q1 strict validation) | Negative | PASS |
| stage ordering | calls helpers in the correct order with -SkipSign | Positive | PASS |
| stage ordering | inserts Invoke-SignTool between Invoke-MakeAppx and Write-PublishManifest when signed | Positive | PASS |
| per-project publish flags | passes --self-contained true -r win-x64 for Core and HostAdapter | Positive | PASS |
| per-project publish flags | passes /p:PublishProfile=msix for MailBridge and MailBridge.Client | Positive | PASS |
| skip-sign path | -SkipSign does NOT invoke Invoke-SignTool | Positive | PASS |
| skip-sign path | -CertThumbprint invokes Invoke-SignTool with the supplied thumbprint | Positive | PASS |
| output paths | writes the MSIX to `<OutputDir>/<Version>/msix/OpenClaw.MailBridge_<Version>_x64.msix` | Positive | PASS |
| output paths | writes manifest.json under `<OutputDir>/<Version>/` | Positive | PASS |

**Coverage:** 96.94% targeted new-code line coverage across `scripts/Publish.ps1` + `scripts/Publish.Helpers.psm1` (222/229 analyzed commands). Repo-wide: 81.71%.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | 72 | PASS |
| Tests Passed | 72 (100%) | PASS |
| Tests Failed | 0 | PASS |
| Tests Skipped | 0 | PASS |
| Execution Time | Not individually reported in final artifact; baseline was 1.86s for 28 tests, so 72 tests fit the fast-execution envelope. | PASS |
| Functions/Classes Tested | 11/11 helpers (100%) + orchestrator | PASS |
| Test File Sizes | 184, 442 lines | PASS (under 500) |
| Repo-wide Line Coverage | 81.71% | PASS (>= 80%) |
| New-code Line Coverage | 96.94% | PASS (>= 90%) |

---

## 7. Code Quality Checks

**For PowerShell:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Invoke-Formatter | `Invoke-PoshQCFormat -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')` | 20 files scanned, all "Already formatted"; 0 changes | PASS |
| PSScriptAnalyzer | `Invoke-PoshQCAnalyze -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')` | 0 diagnostics | PASS |
| Pester Tests | `Invoke-PoshQCTest -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')` | 72/72 passing, coverage 81.71% repo-wide / 96.94% new-code | PASS |

**Notes:**
Evidence artifacts (`evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`, `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`, `evidence/qa-gates/final-pester.2026-04-18T00-00.md`) are the source of truth for these results. The review inspected the existing artifacts rather than re-running the toolchain (coverage-verification policy: inspect pre-existing artifacts from the executor run).

---

## 8. Gaps and Exceptions

### Identified Gaps

- **Live end-to-end MSIX regression** — The user-story AC item `An MSIX produced by Publish.ps1 installs, launches the bridge startup task on next logon, and uninstalls cleanly (regression of feature #17 behavior)` remains unchecked in `user-story.md`. The executor's `definition-of-done-reconciliation.2026-04-18T00-00.md` documents this as out of the unit-test surface for this feature and as a regression condition for a follow-on cycle. This is a documented scope decision, not a defect in delivered work; however, it is a PARTIAL status on the user-story AC set.
- **PR context appendix staleness** — `artifacts/pr_context.appendix.txt` still references the previous feature branch (`feature/openclaw-agent-docker-30`) and was not regenerated for this review. The summary file was refreshed. The branch diff and executor evidence artifacts are authoritative for this review; the stale appendix was not consulted.

### Approved Exceptions

- `PSUseOutputTypeCorrectly` suppressed on `Get-StampedAppxManifestXml` with justification: `XmlDocument.Clone` returns `XmlNode` at static-analysis level, but the concrete runtime type is `XmlDocument`. Documented inline in the source.
- `PSUseShouldProcessForStateChangingFunctions` suppressed on `New-ManifestEntry` with justification: the function is pure and returns a new object without changing system state. Documented inline.
- `PSAvoidGlobalVars` suppressed at file scope in `Publish.Tests.ps1` with justification: Pester `Mock` script blocks execute in the orchestrator's caller scope; a `$global:` call log is required to share state across scopes. Documented inline.

### Removed/Skipped Tests

- `tests/scripts/build-msix.Tests.ps1` (173 lines, 7 tests) was deleted as part of the retirement of `scripts/build-msix.ps1`. Those 7 tests exercised the retired script; equivalent behaviors are now covered by the 51 new tests across `Publish.Tests.ps1` and `Publish.Helpers.Tests.ps1`. Net test count: +44 tests.

---

## 9. Summary of Changes

### Commits in This PR/Branch

1. `c4bd410` — feat(#34): unified publish script replaces build-msix.ps1

### Files Modified

1. `scripts/Publish.ps1` (NEW, 183 lines) — Unified publish orchestrator; validates parameters, orchestrates five stages (clean, publish, docker copy, MSIX, manifest), delegates to `Publish.Helpers.psm1`.
2. `scripts/Publish.Helpers.psm1` (NEW, 456 lines) — Shared helper module exporting 11 functions: `Find-WindowsSdkTool`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`, `Invoke-DotnetPublish`, `Copy-DockerArtifact`, `New-ManifestEntry`, `Write-PublishManifest`.
3. `tests/scripts/Publish.Tests.ps1` (NEW, 184 lines) — Pester tests for the orchestrator; 12 tests.
4. `tests/scripts/Publish.Helpers.Tests.ps1` (NEW, 442 lines) — Pester tests for helpers; 39 tests.
5. `scripts/build-msix.ps1` (DELETED, 294 lines) — Retired in favor of `Publish.ps1` + `Publish.Helpers.psm1`.
6. `tests/scripts/build-msix.Tests.ps1` (DELETED, 173 lines) — Retired with its subject.
7. `.github/workflows/build-msix.yml` (DELETED, 54 lines) — Renamed.
8. `.github/workflows/publish.yml` (NEW, 46 lines) — Renamed workflow body; preserves `push` on `v*` tags and `workflow_dispatch` input `version`; invokes `Publish.ps1` with `CertThumbprint` (falling back to `New-MsixDevCert.ps1` when the secret is absent).
9. `README.md` (MODIFIED, +25/-5) — Release-build section now documents `Publish.ps1`.
10. `docs/mailbridge-runbook.md` (MODIFIED, +29/-7) — Section 3 now documents `Publish.ps1` and the bundle layout.
11. Feature documentation and evidence: 19 new files under `docs/features/active/2026-04-18-unified-publish-script-34/` + one frozen promoted snapshot.

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

All PowerShell toolchain steps pass. All delivered acceptance criteria within the feature's declared unit-test scope pass. Coverage thresholds met or exceeded. File-size policy met. No residual `build-msix` references outside historical planning docs and this feature's own evidence artifacts.

One AC item (live MSIX install/uninstall regression) is documented as out of the unit-test scope for this feature and is a follow-on verification; this is a documented scope decision rather than a defect.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- PASS Before Making Changes
- PASS Design Principles
- PASS Module & File Structure
- PASS Naming, Docs, Comments
- PASS Toolchain Execution
- PASS Summarize & Document

#### Language-Specific Code Change Policy (Section 3)

**For PowerShell:**
- PASS Tooling & Baseline
- PASS PowerShell Design & Safety
- PASS Structure & Naming
- PASS Toolchain

#### General Unit Test Policy (Section 1)
- PASS Core Principles
- PASS Coverage & Scenarios
- PASS Test Structure
- PASS External Dependencies
- PASS Policy Audit

#### Language-Specific Unit Test Policy (Section 4)

**For PowerShell:**
- PASS Framework & Scope
- PASS Test Style & Structure
- PASS Naming & Readability
- PASS Toolchain

---

### Metrics Summary

- PASS 72/72 tests passing (100%)
- PASS 11/11 helper functions covered
- PASS 81.71% repo-wide line coverage (>= 80% floor; +14.58pp vs baseline)
- PASS 96.94% new-code line coverage (>= 90% floor)
- PASS All four new files under 500 lines
- PASS PoshQC format/analyze/test all green
- PASS No new external dependencies introduced

---

### Recommendation

**Ready for merge.**

The feature delivers all in-scope acceptance criteria with passing toolchain and coverage evidence. The single unchecked user-story AC (`An MSIX produced by Publish.ps1 installs, launches the bridge startup task on next logon, and uninstalls cleanly`) is a live-environment regression scenario explicitly scoped out of the unit-test surface for this feature; it does not block merge.

Optional follow-on work:
- Regenerate `artifacts/pr_context.appendix.txt` for this branch before opening the PR to keep the appendix aligned with the refreshed summary.
- Schedule the live MSIX install/uninstall regression as a separate verification cycle (the spec lists it as a seeded test condition for a follow-on smoke).

---

## Appendix A: Test Inventory

### Complete Test List (72 tests)

- `Publish.Helpers.psm1` › Module exports › exports the expected 11 helper functions
- `Publish.Helpers.psm1` › Find-WindowsSdkTool › returns the SDK-bin match when ProgramFiles(x86) has a matching x64 binary
- `Publish.Helpers.psm1` › Find-WindowsSdkTool › falls back to Get-Command when the SDK bin root is missing
- `Publish.Helpers.psm1` › Find-WindowsSdkTool › throws when the tool cannot be located anywhere
- `Publish.Helpers.psm1` › Get-StampedAppxManifestXml › stamps the 4-part version into Package.Identity.Version
- `Publish.Helpers.psm1` › Get-StampedAppxManifestXml › preserves other Identity attributes unchanged
- `Publish.Helpers.psm1` › Get-StampedAppxManifestXml › rejects a 3-part version via ValidatePattern
- `Publish.Helpers.psm1` › Invoke-VersionStamp › writes stamped XML to `<stagingDir>/AppxManifest.xml`
- `Publish.Helpers.psm1` › Invoke-VersionStamp › -WhatIf leaves the staging file absent
- `Publish.Helpers.psm1` › Invoke-LayoutAssembly › throws when bridge publish dir is missing
- `Publish.Helpers.psm1` › Invoke-LayoutAssembly › throws when client publish dir is missing
- `Publish.Helpers.psm1` › Invoke-LayoutAssembly › calls Copy-Item for bridge, client, and assets on success
- `Publish.Helpers.psm1` › Invoke-LayoutAssembly › -WhatIf skips all copies
- `Publish.Helpers.psm1` › Invoke-MakePri › invokes makepri createconfig then new
- `Publish.Helpers.psm1` › Invoke-MakePri › throws on non-zero exit
- `Publish.Helpers.psm1` › Invoke-MakePri › -WhatIf does not invoke the tool
- `Publish.Helpers.psm1` › Invoke-MakeAppx › passes /d /p /nv /o exactly and preserves OutputMsixPath
- `Publish.Helpers.psm1` › Invoke-MakeAppx › throws on non-zero exit
- `Publish.Helpers.psm1` › Invoke-MakeAppx › -WhatIf does not invoke the tool
- `Publish.Helpers.psm1` › Invoke-SignTool › passes /sha1, /fd SHA256, /tr, /td SHA256 with msix path last
- `Publish.Helpers.psm1` › Invoke-SignTool › throws on non-zero exit
- `Publish.Helpers.psm1` › Invoke-SignTool › -WhatIf does not invoke the tool
- `Publish.Helpers.psm1` › Invoke-DotnetPublish › passes -c -o /p:Deterministic=true verbatim
- `Publish.Helpers.psm1` › Invoke-DotnetPublish › appends ExtraArgs after required args
- `Publish.Helpers.psm1` › Invoke-DotnetPublish › throws on non-zero exit
- `Publish.Helpers.psm1` › Copy-DockerArtifact › copies both compose files when present
- `Publish.Helpers.psm1` › Copy-DockerArtifact › copies .env.example when present
- `Publish.Helpers.psm1` › Copy-DockerArtifact › skips .env.example silently when absent
- `Publish.Helpers.psm1` › Copy-DockerArtifact › recursively copies deploy/docker/**
- `Publish.Helpers.psm1` › Copy-DockerArtifact › emits Write-Warning and does not copy when a secrets/ dir exists
- `Publish.Helpers.psm1` › Copy-DockerArtifact › never copies secrets/.env.anthropic even if present
- `Publish.Helpers.psm1` › New-ManifestEntry › returns path as forward-slash-relative string
- `Publish.Helpers.psm1` › New-ManifestEntry › returns size as non-negative integer
- `Publish.Helpers.psm1` › New-ManifestEntry › returns sha256 as 64-character lowercase hex string
- `Publish.Helpers.psm1` › New-ManifestEntry › calls Get-FileHash with -Algorithm SHA256
- `Publish.Helpers.psm1` › Write-PublishManifest › writes JSON with version, generatedAt, files and excludes manifest.json
- `Publish.Helpers.psm1` › Write-PublishManifest › sorts files ascending by path
- `Publish.Helpers.psm1` › Write-PublishManifest › each file entry has exactly path, size, sha256
- `Publish.Helpers.psm1` › Write-PublishManifest › structural stability only (Q3)
- `scripts/Publish.ps1` › parameter validation › throws when neither -SkipSign nor -CertThumbprint is provided
- `scripts/Publish.ps1` › parameter validation › accepts -SkipSign alone
- `scripts/Publish.ps1` › parameter validation › accepts -CertThumbprint alone
- `scripts/Publish.ps1` › parameter validation › rejects a 3-part version (Q1 strict validation)
- `scripts/Publish.ps1` › stage ordering › calls helpers in the correct order with -SkipSign
- `scripts/Publish.ps1` › stage ordering › inserts Invoke-SignTool between Invoke-MakeAppx and Write-PublishManifest when signed
- `scripts/Publish.ps1` › per-project publish flags › passes --self-contained true -r win-x64 for Core and HostAdapter
- `scripts/Publish.ps1` › per-project publish flags › passes /p:PublishProfile=msix for MailBridge and MailBridge.Client
- `scripts/Publish.ps1` › skip-sign path › -SkipSign does NOT invoke Invoke-SignTool
- `scripts/Publish.ps1` › skip-sign path › -CertThumbprint invokes Invoke-SignTool with the supplied thumbprint
- `scripts/Publish.ps1` › output paths › writes the MSIX to `<OutputDir>/<Version>/msix/OpenClaw.MailBridge_<Version>_x64.msix`
- `scripts/Publish.ps1` › output paths › writes manifest.json under `<OutputDir>/<Version>/`

(Plus 21 legacy PowerShell tests under `tests/scripts/` for `install-mailbridge`, `New-MsixDevCert`, `register-mailbridge-task`, `runner-scripts`, `test-mailbridge`, `uninstall-mailbridge` — unchanged by this feature; 72 total.)

---

## Appendix B: Toolchain Commands Reference

**PowerShell (canonical — as used in this feature's evidence artifacts):**

```powershell
# Formatting (MCP)
mcp__drmCopilotExtension__run_poshqc_format
# or equivalent invocation:
Invoke-PoshQCFormat -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')

# Linting (MCP)
mcp__drmCopilotExtension__run_poshqc_analyze
# or equivalent:
Invoke-PoshQCAnalyze -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')

# Testing with coverage (MCP)
mcp__drmCopilotExtension__run_poshqc_test
# or equivalent:
Invoke-PoshQCTest -Root 'C:/Users/DanMoisan/repos/open-claw-bridge' -ScanFolders @('scripts','tests')
```

**Evidence reference:**
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/final-pester.2026-04-18T00-00.md`
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/coverage-delta.2026-04-18T00-00.md`

**Coverage artifact:** `artifacts/pester/powershell-coverage.xml` (present; 81.71% repo-wide line coverage).

---

**Audit Completed By:** Claude (feature-review-workflow)
**Audit Date:** 2026-04-18
**Policy Version:** Current as of 2026-04-18
