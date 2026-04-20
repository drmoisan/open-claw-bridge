# Policy Compliance Audit — Bundle Install Script (Issue #36)

- Component: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1` (and Pester test peers)
- Date: 2026-04-19
- Audit Timestamp: 2026-04-19T03-21
- Feature Folder: `docs/features/active/2026-04-18-bundle-install-script-36/`
- Feature Branch: `feature/unified-publish-script-34` (HEAD) — contains commit `feat(#36): bundle install/uninstall scripts consume Publish.ps1 output`
- Base Branch: `development`
- Merge-Base SHA: `7bd92a8cb772c8f41a85831416a5fec952a2330b` (timestamp 2026-04-18T20:16:13-05:00)
- HEAD SHA: `453343e77121d4592e7179dda731a117b3d2b601`

## Scope

Full feature-vs-base diff (1 commit). Files under test (production):

- `scripts/Install.ps1` (added, 210 lines)
- `scripts/Uninstall.ps1` (added, 88 lines)
- `scripts/Install.Helpers.psm1` (added, 448 lines)

Files under test (tests):

- `tests/scripts/Install.Tests.ps1` (added, 268 lines)
- `tests/scripts/Uninstall.Tests.ps1` (added, 163 lines)
- `tests/scripts/Install.Helpers.Tests.ps1` (added, 488 lines)

Documentation changes: `README.md` (+8 / -1), `docs/mailbridge-runbook.md` (+75 / 0).

No Python, TypeScript, or C# production or test files changed in this branch diff.

## Executive Summary

All policy gates applicable to this feature PASS, based on existing evidence artifacts produced during execution and the branch diff. Formatting, linting, and testing toolchain runs returned zero diagnostics. Coverage artifacts at `artifacts/pester/powershell-coverage.xml` confirm the repo-wide PowerShell coverage of 86.39% and per-file coverage of 96.32% / 90.29% / 93.75% on the three new production files. Every production and test file is at or under the 500-line policy ceiling. Retained scheduled-task scripts (`scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`) are unchanged on this branch.

Overall verdict: **PASS**. No remediation required.

## 1. General Unit Test Policy Compliance

| Criterion | Status | Evidence |
|---|---|---|
| Tests are independent (no cross-test state) | PASS | `tests/scripts/Install.Helpers.Tests.ps1`, `Install.Tests.ps1`, `Uninstall.Tests.ps1` use Pester v5 `Describe`/`Context`/`It` with `BeforeAll`/`BeforeEach` setup and no persisted cross-test state. |
| Isolation (one behavior per `It`) | PASS | Each `It` block targets a single behavior per review of the three test files. |
| Fast execution | PASS | Full suite duration 11.31s for 143 tests (`evidence/qa-gates/final-pester.2026-04-18T00-00.md`). |
| Determinism | PASS | Helpers mocked with `function global:docker` shims and `Mock` at module scope; no filesystem or network dependencies. |
| Readability and maintainability | PASS | Clear `Describe` / `Context` headings named after behaviors (e.g., `'manifest integrity failure'`, `'stage ordering (happy path)'`, `'-SkipDocker path'`). |
| Repository-wide line coverage >= 80% | PASS | Post-change 86.39% (`evidence/qa-gates/coverage-delta.2026-04-18T00-00.md`). |
| New module/class/method coverage >= 90% | PASS | `Install.Helpers.psm1` 96.32%, `Install.ps1` 90.29%, `Uninstall.ps1` 93.75%. |
| Arrange / Act / Assert structure | PASS | Test bodies follow AAA implicitly: `BeforeAll`/`BeforeEach` for arrange, the act line is the direct call, and `Should`-family assertions follow. |
| No temporary files in tests | PASS | Tests use Pester mocks only; no `[IO.Path]::GetTempFileName()`, `New-TemporaryFile`, or `TestDrive` writes observed. |
| No external dependencies | PASS | All `docker`, `Add-AppxPackage`, `Get-AppxPackage`, `Remove-AppxPackage`, `Get-FileHash`, `Copy-Item`, `Get-Content`, `Set-Content`, `Get-ChildItem`, `Test-Path`, `New-Item`, `Remove-Item` invocations are mocked. |
| No mutable global state | PASS | Two intentional `$global:` shim variables (`$global:dockerCalls`, `$global:dockerFails`) in tests only, with `PSAvoidGlobalVars` suppression documented in `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`. Production code avoids `$global:`. |

Section verdict: **PASS**.

## 2. General Code Change Policy Compliance

| Criterion | Status | Evidence |
|---|---|---|
| Simplicity first | PASS | `Install.ps1` is a thin orchestrator (210 lines) that delegates to helpers; stage pattern is linear with `[install:<stage>]` progress tags. |
| Reusability | PASS | Shared helpers factored into `Install.Helpers.psm1`; no copy-paste between `Install.ps1` and `Uninstall.ps1`; pattern mirrors the `Publish.ps1` + `Publish.Helpers.psm1` precedent. |
| Extensibility | PASS | Public helpers use `CmdletBinding`, named parameters with `[Parameter(Mandatory=$true)]`, `SupportsShouldProcess`, and `-TimeoutSeconds` / `-PollIntervalSeconds` overrides on `Wait-ComposeHealthy`. |
| Separation of concerns | PASS | Pure discovery (`Find-NewestPublishVersion`), validation (`Test-ManifestIntegrity`), I/O (`Copy-BundleContents`, `Initialize-DotEnv`, `Write-InstallRecord`, `Read-InstallRecord`), MSIX wrappers, and docker wrappers live in distinct helpers. |
| Mandatory toolchain loop (format -> lint -> test) | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` EXIT_CODE 0; `final-poshqc-analyze` EXIT_CODE 0 (0 diagnostics); `final-pester` EXIT_CODE 0 (143/143 pass). Type-check step is N/A for PowerShell. |
| File size limit (<= 500 lines each) | PASS | Max = `tests/scripts/Install.Helpers.Tests.ps1` 488 lines. Every new file under 500. |
| Fail-fast error handling | PASS | `throw` is used on precondition failures (manifest integrity, docker unavailable, MSIX missing, admin precheck). No catch-all swallowing in production code; the uninstall failure-collection loop catches, tags, and re-throws. |
| Logging (appropriate pattern) | PASS | Uses `Write-Information` with `-InformationAction Continue` and stage-prefixed messages (`[install:select]`, `[uninstall:docker]`, etc.). |
| Invariants at construction | PASS | Parameters declared mandatory or with `ValidatePattern`; `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at the top of every script and the helper module. |
| Naming conventions (approved verbs, PascalCase) | PASS | All public helpers use approved PowerShell verbs (`Find-`, `Test-`, `Copy-`, `Initialize-`, `Invoke-`, `Wait-`, `Write-`, `Read-`). One `PSUseSingularNouns` suppression on `Copy-BundleContents` with explicit justification (plan-mandated plural noun). |
| Public API stability / keyword params | PASS | Named parameters with defaults; switch parameters for boolean flags; no breaking changes to existing scripts. |
| Dependencies (approved libraries only) | PASS | Uses only built-in PowerShell 7+ cmdlets, the Windows `Appx` module, and the `docker` CLI. No new package dependencies. |
| I/O isolation | PASS | Domain logic (`Find-NewestPublishVersion`, JSON shapes in `Write-InstallRecord` / `Read-InstallRecord`) is testable without the real filesystem via mocks. |

Section verdict: **PASS**.

## 3. Language-Specific Code Change Policy Compliance

### PowerShell (`.claude/rules/powershell.md`)

| Criterion | Status | Evidence |
|---|---|---|
| Toolchain order (format -> analyze -> test) | PASS | `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`, `final-poshqc-analyze`, `final-pester` all EXIT_CODE 0. Plan Phase 5 executed in this order with restart-on-change. |
| PowerShell 7+ compatibility | PASS | `#Requires -Version 7.0` declared at the top of `Install.ps1` and `Uninstall.ps1`; helper module relies on PS7 cmdlets (`ConvertFrom-Json -Depth`, `.ToLowerInvariant()` on strings). |
| Advanced functions with `CmdletBinding()` | PASS | Every exported helper and the two orchestrators declare `[CmdletBinding()]`. |
| `[Parameter(Mandatory=$true)]` / validation | PASS | All mandatory parameters marked; `Install.ps1 -Version` has `[ValidatePattern('^(\d+\.\d+\.\d+\.\d+)?$')]`. |
| `SupportsShouldProcess` on state-changing actions | PASS | Applied to `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixRemove`, `Invoke-ComposeUp`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Install.ps1`, `Uninstall.ps1`. |
| Avoid `Invoke-Expression`, plaintext secrets, hard-coded credentials | PASS | Grep confirms no `Invoke-Expression`; no hard-coded credentials. `.env.example` guard copies only when absent. |
| Approved verbs | PASS | All exported functions use `Find-`, `Test-`, `Copy-`, `Initialize-`, `Invoke-`, `Wait-`, `Write-`, `Read-`. |
| `<=` 500 lines per script | PASS | See section 6. |

Suppressions (per `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`):
- `Copy-BundleContents` carries a `PSUseSingularNouns` suppression with in-source justification citing the plan decision.
- `tests/scripts/Install.Tests.ps1` and `Install.Helpers.Tests.ps1` carry `PSAvoidGlobalVars` suppressions with in-source justification (mock call-log shims).

Section verdict: **PASS**.

### Python / TypeScript / C#

Not applicable — zero changed files on this branch for these languages.

## 4. Language-Specific Unit Test Policy Compliance

### PowerShell (Pester v5)

| Criterion | Status | Evidence |
|---|---|---|
| Pester v5.x used | PASS | Test files use `Describe`, `Context`, `It`, `BeforeAll`, `BeforeEach`, `Mock`, `Should` (v5 syntax). |
| Test file naming `*.Tests.ps1` | PASS | `Install.Tests.ps1`, `Uninstall.Tests.ps1`, `Install.Helpers.Tests.ps1`. |
| Test layout mirrors code | PASS | Tests live under `tests/scripts/` mirroring `scripts/`. |
| One behavior per `It` | PASS | Verified during code review. |
| Mock sparingly; prefer real code paths | PARTIAL with rationale | Tests mock heavily (13 helpers + the `docker` CLI + `Add-AppxPackage` family), as required by the I/O boundary. This is the same pattern used by the precedent feature `#34` (`tests/scripts/Publish.Tests.ps1`). Mocking is unavoidable because the real calls hit Windows-only Appx APIs and the external `docker` daemon. Not a policy violation. |
| No external dependencies in unit tests | PASS | All outbound calls mocked. |
| Repo coverage >= 80% | PASS | 86.39%. |
| Per-new file coverage >= 90% | PASS | 96.32% / 90.29% / 93.75%. |
| No coverage regression on changed lines | PASS | `+4.68 pp` improvement vs baseline 81.71%. |

Section verdict: **PASS**.

## 5. Test Coverage Detail

Artifact: `artifacts/pester/powershell-coverage.xml` (JaCoCo-format XML, ~64 KB, generated 2026-04-18).

| File | New vs Modified | Coverage | Threshold | Status |
|---|---|---|---|---|
| `scripts/Install.Helpers.psm1` | new | 96.32% (183/190 instruction, LINE counter 4 missed / 126 covered = 96.92% line) | >= 90% | PASS |
| `scripts/Install.ps1` | new | 90.29% (93/103 instruction, LINE counter 7 missed / 75 covered = 91.46% line) | >= 90% | PASS |
| `scripts/Uninstall.ps1` | new | 93.75% (45/48 instruction, LINE counter 1 missed / 28 covered = 96.55% line) | >= 90% | PASS |
| `README.md` | modified | N/A (Markdown, not a code-coverage target) | N/A | N/A |
| `docs/mailbridge-runbook.md` | modified | N/A (Markdown, not a code-coverage target) | N/A | N/A |
| Repo-wide PowerShell | baseline 81.71%, post-change 86.39% | >= 80% | PASS |

Coverage artifact verification:

- File exists: `artifacts/pester/powershell-coverage.xml` (authoritative; also mirrored at `artifacts/pester/powershell-coverage.koverage.xml` and per-module `install-helpers-cov.xml`).
- Format: JaCoCo XML with `<package>` / `<sourcefile>` / `<line>` / `<counter>` elements.
- Per-file instruction counts extracted at XML lines 624, 712, 1178 for Install.Helpers.psm1 / Install.ps1 / Uninstall.ps1 respectively.

Section verdict: **PASS**.

## 6. Test Execution Metrics

| Metric | Value | Source |
|---|---|---|
| Test discovery | 143 tests across 11 `*.Tests.ps1` files | `evidence/qa-gates/final-pester.2026-04-18T00-00.md` |
| Passed | 143 | same |
| Failed | 0 | same |
| Skipped / Inconclusive / Not-run | 0 / 0 / 0 | same |
| Wall-clock duration | 11.31 s | same |
| Baseline pass count (Phase 0) | 73 | `evidence/baseline/baseline-pester.2026-04-18T00-00.md` |
| Delta | +70 new tests (43 Helpers + 18 Install + 9 Uninstall) | same |
| Regressions vs baseline | 0 | same |

Section verdict: **PASS**.

## 7. Code Quality Checks

### Formatting — PoshQC / Invoke-Formatter

- Command: `Invoke-PoshQCFormat -Root <repo> -ScanFolders @('scripts','tests')`
- Exit code: 0
- Result: 26/26 files report "Already formatted". No diffs produced.
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md`.

Status: **PASS**.

### Linting — PSScriptAnalyzer via PoshQC

- Command: `Invoke-PoshQCAnalyze -Root <repo> -ScanFolders @('scripts','tests')`
- Exit code: 0
- Diagnostic count: 0
- Suppressions: 2 rule suppressions with explicit justification (see section 3).
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`.

Status: **PASS**.

### Type checking

Not applicable for PowerShell.

### Testing — Pester v5 via PoshQC

- Command: `Invoke-PoshQCTest -Root <repo> -ScanFolders @('scripts','tests')`
- Exit code: 0
- Tests: 143 passed, 0 failed, 0 skipped
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-pester.2026-04-18T00-00.md`.

Status: **PASS**.

### Line-count policy

| File | Lines | Policy Ceiling |
|---|---|---|
| `scripts/Install.ps1` | 210 | 500 |
| `scripts/Uninstall.ps1` | 88 | 500 |
| `scripts/Install.Helpers.psm1` | 448 | 500 |
| `tests/scripts/Install.Tests.ps1` | 268 | 500 |
| `tests/scripts/Uninstall.Tests.ps1` | 163 | 500 |
| `tests/scripts/Install.Helpers.Tests.ps1` | 488 | 500 |

Status: **PASS** (see `evidence/qa-gates/end-state-line-counts.2026-04-18T00-00.md`).

## 8. Gaps and Exceptions

Two explicit suppressions, both documented in source and in `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md`:

1. `PSUseSingularNouns` on `scripts/Install.Helpers.psm1::Copy-BundleContents`. Justification: the spec and plan mandate the plural noun "Contents" because the helper copies multiple subtrees (`executables/` and `docker/`). Renaming to a singular noun would breach the planner-approved API surface. Not a policy violation.
2. `PSAvoidGlobalVars` on `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Helpers.Tests.ps1`. Justification: mock script blocks and the `function global:docker` shim run in the orchestrator script scope; `$global:` is required to share call-log variables across scopes. Mirrors the `tests/scripts/Publish.Tests.ps1` precedent. Test-only, not applied to production code.

Coverage gaps (lines flagged missed by coverage):

- `scripts/Install.ps1` lines 82, 83 (the real `IsInRole` call inside `Test-IsElevatedAdmin`) are not exercised by tests because tests override `Test-IsElevatedAdmin` at function scope. This is acceptable: the uncovered lines are a thin wrapper around a documented Windows API. File-level coverage still exceeds 90%.
- `scripts/Install.Helpers.psm1` missed lines are concentrated in the `Wait-ComposeHealthy` timeout error path and the JSON parse catch block. File coverage 96.32% > 90% threshold.
- `scripts/Uninstall.ps1` single missed line is inside `Write-Information` progress output that is not asserted by tests. File coverage 93.75% > 90% threshold.

No gaps that block the policy audit.

## 9. Summary of Changes

Production:

- Added `scripts/Install.Helpers.psm1` with 13 exported functions: `Find-NewestPublishVersion`, `Test-ManifestIntegrity`, `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord`.
- Added `scripts/Install.ps1` orchestrator with parameters `-SourcePath`, `-Version`, `-AllowUnsigned`, `-SkipDocker`, `-Force`.
- Added `scripts/Uninstall.ps1` orchestrator with no parameters (reads state from install-record.json).

Tests:

- Added `tests/scripts/Install.Helpers.Tests.ps1` (43 tests).
- Added `tests/scripts/Install.Tests.ps1` (18 tests).
- Added `tests/scripts/Uninstall.Tests.ps1` (9 tests).

Documentation:

- Updated `README.md` (+8 / -1): new bullet pointing to `Install.ps1` under "What It Does" and expanded `scripts/` row in the Repository Layout section.
- Updated `docs/mailbridge-runbook.md` (+75 / 0): new "Install Path D: Scripted Bundle Install" section, troubleshooting entries, and cross-references from Paths B and C.

Feature scoping and evidence:

- Added `docs/features/active/2026-04-18-bundle-install-script-36/` with `issue.md`, `spec.md`, `user-story.md`, `plan.2026-04-18T00-00.md`, and 17 evidence artifacts under `evidence/baseline/`, `evidence/other/`, `evidence/qa-gates/`.
- Added `docs/features/potential/promoted/2026-04-18-bundle-install-script.md` mirror.

Retained unchanged: `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1` (verified via `git diff --stat` returning empty output).

## 10. Compliance Verdict

Overall verdict: **PASS**.

| Area | Status |
|---|---|
| General unit test policy | PASS |
| General code change policy | PASS |
| PowerShell language-specific code change policy | PASS |
| PowerShell language-specific unit test policy | PASS |
| Test coverage (repo-wide + per-new-file) | PASS |
| Test execution metrics | PASS |
| Code quality (format / analyze / test / line-count) | PASS |
| Gaps and exceptions (documented, justified) | PASS |

No remediation required. The branch is ready for PR from a policy-compliance perspective.

## Appendix A: Test Inventory

| Test file | `Describe` / `Context` blocks | Count |
|---|---|---|
| `tests/scripts/Install.Helpers.Tests.ps1` | `Install.Helpers.psm1 — export surface`, `Find-NewestPublishVersion`, `Test-ManifestIntegrity`, `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord` | 43 |
| `tests/scripts/Install.Tests.ps1` | `parameter binding`, `administrator precheck on -AllowUnsigned`, `stage ordering (happy path)`, `-SkipDocker path`, `-Force over existing install`, `manifest integrity failure`, `docker not running`, `MSIX missing` | 18 |
| `tests/scripts/Uninstall.Tests.ps1` | `missing install record`, `stage ordering (happy path)`, `skipDocker = true`, `partial state tolerance`, `failure collection`, `preserves user config` | 9 |
| Total (new) | — | 70 |
| Legacy tests still passing | 8 files | 73 |
| Grand total | — | 143 |

## Appendix B: Toolchain Commands Reference

- Format: `mcp__drmCopilotExtension__run_poshqc_format` (check-only)
- Lint: `mcp__drmCopilotExtension__run_poshqc_analyze` (check-only)
- Test + coverage: `mcp__drmCopilotExtension__run_poshqc_test`
- Git diff for this review:
  - `git diff --name-status 7bd92a8cb772c8f41a85831416a5fec952a2330b..HEAD`
  - `git diff --numstat 7bd92a8cb772c8f41a85831416a5fec952a2330b..HEAD`
  - `git log 7bd92a8cb772c8f41a85831416a5fec952a2330b..HEAD --format="%H %s"`
- Coverage inspection: read `artifacts/pester/powershell-coverage.xml` (JaCoCo format).
- PR context refresh: the Python PR-context collector (`scripts.dev_tools.pr_context.collector`) is not present in this repository tree; the PR context summary and appendix were regenerated via `git diff` + `git log` against the resolved base branch `development`.
