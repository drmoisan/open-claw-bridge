# Policy Compliance Audit — Bundle Install Script (Issue #36) — Refinement Cycle

- Component: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Publish.ps1` (modified), `scripts/Publish.Helpers.psm1` (modified), and Pester test peers
- Date: 2026-04-19
- Audit Timestamp: 2026-04-19T17-50
- Feature Folder: `docs/features/active/2026-04-18-bundle-install-script-36/`
- Feature Branch: `feature/bundle-install-script-36`
- Base Branch: `development`
- Merge-Base SHA: `7bd92a8cb772c8f41a85831416a5fec952a2330b` (timestamp 2026-04-18T20:16:13-05:00)
- HEAD SHA: `cda01a8e8e2f829f20e81dfe487ed82b579d1507`
- Commit range: `7bd92a8..cda01a8` (2 commits: `453343e` initial feature, `cda01a8` refinement)
- Work Mode (from `issue.md`): `full-feature`

## Scope

Full feature-vs-base diff (2 commits). Production and test files under audit:

Production (PowerShell):

- `scripts/Install.ps1` (added, 196 lines)
- `scripts/Uninstall.ps1` (added, 88 lines)
- `scripts/Install.Helpers.psm1` (added, 464 lines)
- `scripts/Publish.ps1` (modified, +7 / -1 lines)
- `scripts/Publish.Helpers.psm1` (modified, +50 / -11 lines)

Tests (Pester):

- `tests/scripts/Install.Tests.ps1` (added, 302 lines)
- `tests/scripts/Uninstall.Tests.ps1` (added, 163 lines)
- `tests/scripts/Install.Helpers.Tests.ps1` (added, 488 lines)
- `tests/scripts/Publish.Tests.ps1` (modified, +24 / -1 lines)
- `tests/scripts/Publish.Helpers.Tests.ps1` (modified, +38 / -4 lines)

Documentation:

- `README.md` (+11 / -1)
- `docs/mailbridge-runbook.md` (+76 / 0)

No Python, TypeScript, or C# production or test files changed in this branch diff.

## Executive Summary

All policy gates applicable to this feature PASS, based on the executor-produced evidence artifacts at `docs/features/active/2026-04-18-bundle-install-script-36/evidence/` and a direct review of the diff.

- PoshQC format (check-only): 0 diffs across `scripts/` and `tests/scripts/` (see `evidence/qa-gates/final-poshqc-format.refinement.2026-04-19T00-00.md`).
- PoshQC analyze: 0 diagnostics across all severities (see `evidence/qa-gates/final-poshqc-analyze.refinement.2026-04-19T00-00.md`).
- Pester: 150 / 150 tests pass in ~11.7 s with coverage artifacts emitted at `artifacts/pester/powershell-coverage.xml` and `artifacts/pester/coverage-final.refinement.xml` (see `evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md`).
- PowerShell coverage (the only language with changed files): repo-scoped across the five in-scope production files sits at 95.47 %; per-file coverage is 91.01 % / 95.92 % / 93.75 % / 97.14 % / 97.08 % on `Install.ps1`, `Install.Helpers.psm1`, `Uninstall.ps1`, `Publish.ps1`, `Publish.Helpers.psm1` respectively (see `evidence/qa-gates/coverage-delta.refinement.2026-04-19T00-00.md`).
- File-size policy (<= 500 lines): every new or modified production and test file is under the ceiling; maximum is `scripts/Install.Helpers.psm1` at 464 lines and `tests/scripts/Install.Helpers.Tests.ps1` at 488 lines.
- Retained scheduled-task scripts (`scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`) are unchanged on this branch (verified via `git diff --stat`).

Overall verdict: **PASS**. No remediation required.

## Rejected Scope Narrowing

No caller-supplied scope narrowing was detected in the inputs for this review cycle. The caller explicitly directed a full feature-vs-base audit across `7bd92a8..cda01a8`, which matches the non-negotiable scope invariant.

## 1. General Unit Test Policy Compliance

| Criterion | Status | Evidence |
|---|---|---|
| Tests are independent (no cross-test state) | PASS | `tests/scripts/Install.Helpers.Tests.ps1`, `Install.Tests.ps1`, `Uninstall.Tests.ps1` use Pester v5 `Describe` / `Context` / `It` with per-test `BeforeEach` arrange blocks; call-log globals are cleared in `BeforeEach`. |
| Isolation (one behavior per `It`) | PASS | Each `It` targets a single behavior (verified by direct review). |
| Fast execution | PASS | Full suite 150 tests in ~11.7 s (`evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md`). |
| Determinism | PASS | `Mock -ModuleName Install.Helpers` and `function global:docker` shims isolate every external dependency. No wall-clock or network dependencies. |
| Readability and maintainability | PASS | Context headings are behavior-named (e.g., `'stage ordering (happy path)'`, `'-SkipDocker path'`, `'bundle-root self-location'`). |
| Repo-wide line coverage >= 80% | PASS | Repo-scoped PowerShell coverage 95.47 % (>= 80 %). |
| New module/method coverage >= 90% | PASS | `Install.ps1` 91.01 %, `Install.Helpers.psm1` 95.92 %, `Uninstall.ps1` 93.75 %. |
| AAA structure | PASS | Arrange via `BeforeAll` / `BeforeEach`, act is the direct invocation, assert via `Should`-family assertions. |
| No temporary files in tests | PASS | Grep for `TestDrive`, `New-TemporaryFile`, `GetTempFileName` returns no hits in the new test files. |
| No external dependencies | PASS | Every docker, Appx, and filesystem call is mocked. |
| No mutable global state in production | PASS | `$global:` is used only in tests with explicit `PSAvoidGlobalVars` suppressions citing the shim requirement. |

Section verdict: **PASS**.

## 2. General Code Change Policy Compliance

| Criterion | Status | Evidence |
|---|---|---|
| Simplicity first | PASS | `Install.ps1` is a 196-line thin orchestrator; each stage is a numbered comment block with a single call path. |
| Reusability | PASS | Helpers factored into `Install.Helpers.psm1`. `Copy-InstallScriptsIntoBundle` (new in the refinement) is reused by the publish orchestrator's stage 5. |
| Extensibility | PASS | Public helpers expose named parameters with defaults; `-TimeoutSeconds` / `-PollIntervalSeconds` overrides on `Wait-ComposeHealthy`; `-ProjectName` default on compose wrappers. |
| Separation of concerns | PASS | Pure helpers (`Get-ManifestVersion`, `Test-ManifestIntegrity`, `New-ManifestEntry`) are read-only and testable without any filesystem mutation. I/O lives in distinct helpers. |
| Mandatory toolchain loop (format -> lint -> test) | PASS | `final-poshqc-format.refinement.2026-04-19T00-00.md` EXIT_CODE 0; `final-poshqc-analyze.refinement.2026-04-19T00-00.md` EXIT_CODE 0; `final-pester.refinement.2026-04-19T00-00.md` EXIT_CODE 0. Type-check N/A for PowerShell. |
| File size limit (<= 500 lines each) | PASS | Max production file: `scripts/Install.Helpers.psm1` 464 lines. Max test file: `tests/scripts/Install.Helpers.Tests.ps1` 488 lines. All other new files well under the ceiling. Modified `scripts/Publish.Helpers.psm1` sits at 495 lines (under 500). |
| Fail-fast error handling | PASS | `throw` on precondition failures (admin precheck, missing manifest, manifest integrity, Docker unavailable, missing MSIX, compose-up non-zero, health-poll timeout). Uninstall failure collector catches per-step errors, tags them, and re-throws a single terminating error. |
| Logging pattern | PASS | `Write-Information` with `-InformationAction Continue` and stage-prefixed messages (`[install:select]`, `[install:docker-check]`, `[uninstall:docker]`, `[compose:health]`, etc.). |
| Invariants at construction | PASS | `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at the top of every script and module. `[ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]` on `-Version` parameters in `Publish.ps1`, `Get-StampedAppxManifestXml`, `Invoke-VersionStamp`, `Write-PublishManifest`. |
| Naming conventions (approved verbs, PascalCase) | PASS | All new exported functions use approved PowerShell verbs (`Get-`, `Test-`, `Copy-`, `Initialize-`, `Invoke-`, `Wait-`, `Write-`, `Read-`, `New-`, `Find-`). One `PSUseSingularNouns` suppression on `Copy-BundleContents` with in-source justification; one on `Copy-DockerArtifact` (pre-existing, not introduced by this branch). |
| Public API stability / keyword params | PASS | Named parameters with `[Parameter(Mandatory=$true)]`; switch parameters for boolean flags; breaking change to `Install.ps1` (`-Version` removed) is intentional and is a pre-release change on a feature that has never shipped. No external callers exist. |
| Dependencies (approved libraries only) | PASS | Uses only built-in PowerShell 7+ cmdlets, the Windows `Appx` module (`Add-AppxPackage`, `Get-AppxPackage`, `Remove-AppxPackage`), and the external `docker` CLI. No new package dependencies. |
| I/O isolation | PASS | JSON shapes (`Write-InstallRecord` / `Read-InstallRecord`, `Write-PublishManifest`) are testable without the real filesystem via mocks. `New-ManifestEntry` is pure. `Get-StampedAppxManifestXml` is pure. |

Section verdict: **PASS**.

## 3. Language-Specific Code Change Policy Compliance

### PowerShell (`.claude/rules/powershell.md`)

| Criterion | Status | Evidence |
|---|---|---|
| Toolchain order (format -> analyze -> test) | PASS | Final gate artifacts for the refinement cycle all EXIT_CODE 0; Phase G plan executed format -> analyze -> test with restart-on-change semantics. |
| PowerShell 7+ compatibility | PASS | `#Requires -Version 7.0` on `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Publish.ps1`, and all new test files. Helpers rely on PowerShell 7+ cmdlet semantics (`ConvertFrom-Json -Depth`, string `ToLowerInvariant()`). |
| Advanced functions with `CmdletBinding()` | PASS | Every new and modified function carries `[CmdletBinding()]`; state-changing helpers additionally carry `SupportsShouldProcess=$true`. |
| `[Parameter(Mandatory = $true)]` / validation | PASS | All mandatory parameters marked. Version parameters carry `[ValidatePattern]`. |
| `SupportsShouldProcess` on state-changing actions | PASS | Applied to `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixRemove`, `Invoke-ComposeUp`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Copy-InstallScriptsIntoBundle`, `Write-PublishManifest`, and the `Install.ps1` / `Uninstall.ps1` orchestrators. |
| Avoid `Invoke-Expression`, plaintext secrets, hard-coded credentials | PASS | Grep of the production diff confirms zero `Invoke-Expression` use; no hard-coded credentials or secret values. `.env.example` guard copies only when `.env` is absent. |
| Approved verbs | PASS | `Get-ManifestVersion` (new), `Test-ManifestIntegrity` (modified), `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord`, `Copy-InstallScriptsIntoBundle` (new), `New-ManifestEntry`, `Write-PublishManifest`. All approved. |
| Line-count <= 500 | PASS | See section 7. |

Suppressions documented in `evidence/qa-gates/final-poshqc-analyze.refinement.2026-04-19T00-00.md`:

- `PSUseSingularNouns` on `Copy-BundleContents` in `scripts/Install.Helpers.psm1` — plan-mandated plural noun (copies multiple subtrees).
- `PSUseOutputTypeCorrectly` on `Get-StampedAppxManifestXml` in `scripts/Publish.Helpers.psm1` — static analysis sees `XmlNode` from `Clone()`; concrete runtime return type is `XmlDocument`.
- `PSUseShouldProcessForStateChangingFunctions` on `New-ManifestEntry` — pure function; no state change.
- `PSAvoidGlobalVars` on `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Helpers.Tests.ps1` — mock script-block shim pattern requires `$global:` to share state across scopes.

Section verdict: **PASS**.

### Python / TypeScript / C#

Zero changed files on this branch for these languages; coverage verdict therefore does not apply to them per the scope invariant.

## 4. Language-Specific Unit Test Policy Compliance

### PowerShell (Pester v5)

| Criterion | Status | Evidence |
|---|---|---|
| Pester v5.x used | PASS | Test files use `Describe`, `Context`, `It`, `BeforeAll`, `BeforeEach`, `AfterAll`, `AfterEach`, `Mock`, `Mock -ModuleName ...`, `Should` (v5 syntax). |
| Test file naming `*.Tests.ps1` | PASS | `Install.Tests.ps1`, `Uninstall.Tests.ps1`, `Install.Helpers.Tests.ps1`, `Publish.Tests.ps1`, `Publish.Helpers.Tests.ps1`. |
| Test layout mirrors code | PASS | All tests live under `tests/scripts/` mirroring `scripts/`. |
| One behavior per `It` | PASS | Direct review confirms single-behavior assertions. |
| Mock sparingly; prefer real code paths | PARTIAL with rationale | Tests mock every external dependency by necessity: Windows `Add-AppxPackage` / `Get-AppxPackage` / `Remove-AppxPackage` require a signed MSIX on the host, and `docker` requires a running daemon. This is the same pattern as the precedent `tests/scripts/Publish.Tests.ps1`. Pure helpers (`Get-ManifestVersion`, `Test-ManifestIntegrity`, `New-ManifestEntry`, `Get-StampedAppxManifestXml`) exercise realistic input objects without mocking their internal logic. Not a policy violation. |
| No external dependencies in unit tests | PASS | All outbound calls mocked. |
| Repo coverage >= 80% | PASS | 95.47 % on the five in-scope files (>= 80 %). |
| Per-new file coverage >= 90% | PASS | 91.01 % / 95.92 % / 93.75 % on the three new files. |
| No coverage regression on changed lines | PASS | `coverage-delta.refinement.2026-04-19T00-00.md` shows +0.21 pp repo-scoped delta (baseline 95.26 % -> 95.47 %); every refinement-changed file remains above the 90 % per-file floor. |

Section verdict: **PASS**.

## 5. Test Coverage Detail

Coverage artifact: `artifacts/pester/powershell-coverage.xml` (JaCoCo-format XML) and the refinement-specific artifact `artifacts/pester/coverage-final.refinement.xml`.

| File | New vs Modified | Coverage (line, post-refinement) | Threshold | Status |
|---|---|---|---|---|
| `scripts/Install.Helpers.psm1` | new | 95.92 % (188/196) | >= 90% | PASS |
| `scripts/Install.ps1` | new | 91.01 % (81/89) | >= 90% | PASS |
| `scripts/Uninstall.ps1` | new | 93.75 % (45/48) | >= 90% | PASS |
| `scripts/Publish.ps1` | modified | 97.14 % (68/70) | >= 80% (modified-file floor) and no regression vs baseline 97.06 % | PASS |
| `scripts/Publish.Helpers.psm1` | modified | 97.08 % (166/171) | >= 80% (modified-file floor) and no regression vs baseline 96.89 % | PASS |
| `README.md` | modified | N/A (Markdown) | N/A | N/A |
| `docs/mailbridge-runbook.md` | modified | N/A (Markdown) | N/A | N/A |
| Repo-wide (5 in-scope files) | — | 95.47 % (548/574) (baseline 95.26 %; delta +0.21 pp) | >= 80% | PASS |

Coverage artifact verification:

- File exists and is non-empty: `artifacts/pester/powershell-coverage.xml` (JaCoCo) and `artifacts/pester/coverage-final.refinement.xml`.
- Per-file coverage percentages are sourced from `evidence/qa-gates/coverage-delta.refinement.2026-04-19T00-00.md` and cross-referenced against `evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md`.
- Repo-wide PowerShell coverage uses the five in-scope production files as the denominator, matching the plan's targeted coverage scope. Repository is 100% PowerShell for production code changes in this branch.

Section verdict: **PASS** for every PowerShell file with changed lines. Python / TypeScript / C# are N/A because zero files changed.

## 6. Test Execution Metrics

| Metric | Value | Source |
|---|---|---|
| Test discovery | 150 tests | `evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md` |
| Passed | 150 | same |
| Failed | 0 | same |
| Skipped / Inconclusive / Not-run | 0 / 0 / 0 | same |
| Wall-clock duration | ~11.7 s | same |
| Baseline pass count (pre-refinement) | 143 | `evidence/qa-gates/final-pester.2026-04-18T00-00.md` |
| Delta | +7 new tests on the refinement | `evidence/qa-gates/coverage-delta.refinement.2026-04-19T00-00.md` |
| Regressions vs baseline | 0 | same |

Section verdict: **PASS**.

## 7. Code Quality Checks

### Formatting — PoshQC / Invoke-Formatter

- Command: `mcp__drmCopilotExtension__run_poshqc_format` (check-only; `Invoke-Formatter` comparison)
- Exit code: 0
- Result: 0 dirty files across `scripts/` and `tests/scripts/`.
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-poshqc-format.refinement.2026-04-19T00-00.md`.

Status: **PASS**.

### Linting — PSScriptAnalyzer via PoshQC

- Command: `mcp__drmCopilotExtension__run_poshqc_analyze` with Error / Warning / Information severities
- Exit code: 0
- Diagnostic count: 0
- Suppressions: four in-source suppressions, each with written justification (see section 3).
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-poshqc-analyze.refinement.2026-04-19T00-00.md`.

Status: **PASS**.

### Type checking

Not applicable for PowerShell per `.claude/rules/powershell.md`.

### Testing — Pester v5 via PoshQC

- Command: `mcp__drmCopilotExtension__run_poshqc_test`
- Exit code: 0
- Tests: 150 passed, 0 failed, 0 skipped
- Evidence: `docs/features/active/2026-04-18-bundle-install-script-36/evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md`.

Status: **PASS**.

### Line-count policy (500-line ceiling)

| File | Lines | Policy Ceiling |
|---|---|---|
| `scripts/Install.ps1` | 196 | 500 |
| `scripts/Uninstall.ps1` | 88 | 500 |
| `scripts/Install.Helpers.psm1` | 464 | 500 |
| `scripts/Publish.ps1` | 189 | 500 |
| `scripts/Publish.Helpers.psm1` | 495 | 500 |
| `tests/scripts/Install.Tests.ps1` | 302 | 500 |
| `tests/scripts/Uninstall.Tests.ps1` | 163 | 500 |
| `tests/scripts/Install.Helpers.Tests.ps1` | 488 | 500 |
| `tests/scripts/Publish.Tests.ps1` | 218 | 500 |
| `tests/scripts/Publish.Helpers.Tests.ps1` | 476 | 500 |

Status: **PASS** (see `evidence/qa-gates/end-state-line-counts.refinement.2026-04-19T00-00.md`).

## 8. Gaps and Exceptions

Four explicit analyzer suppressions (in-source with justification comments, echoed in `evidence/qa-gates/final-poshqc-analyze.refinement.2026-04-19T00-00.md`):

1. `PSUseSingularNouns` on `scripts/Install.Helpers.psm1::Copy-BundleContents`. Justification: the helper copies multiple subtrees (`executables/` and `docker/`); the plural noun is a planner-mandated API shape.
2. `PSUseOutputTypeCorrectly` on `scripts/Publish.Helpers.psm1::Get-StampedAppxManifestXml`. Justification: `XmlDocument.Clone()` is typed `XmlNode` in static analysis; concrete runtime return type is `XmlDocument` and the function always returns the cast `[xml]`.
3. `PSUseShouldProcessForStateChangingFunctions` on `scripts/Publish.Helpers.psm1::New-ManifestEntry`. Justification: pure function; constructs a new `pscustomobject` and has no side effects.
4. `PSAvoidGlobalVars` on `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Helpers.Tests.ps1`. Justification: Pester mock script blocks and `function global:docker` shims run in a foreign scope; `$global:` variables are required to share a call log across scopes. Test-only, not applied to production code.

Coverage gaps (lines flagged missed by coverage, each within the per-file acceptance envelope):

- `scripts/Install.ps1` uncovered lines are concentrated in the real `Test-IsElevatedAdmin` body (the Windows `WindowsPrincipal` / `WindowsBuiltInRole` calls), which tests intentionally override with a function shim so they do not invoke the real Windows API. File coverage 91.01 % > 90 % threshold.
- `scripts/Install.Helpers.psm1` uncovered lines are in the `Wait-ComposeHealthy` timeout error path and the JSON parse `catch` branch. File coverage 95.92 % > 90 % threshold.
- `scripts/Uninstall.ps1` uncovered lines are inside `Write-Information` progress output not asserted by tests. File coverage 93.75 % > 90 % threshold.

No gaps block the policy audit.

## 9. Summary of Changes

Production changes across `7bd92a8..cda01a8`:

- Added `scripts/Install.Helpers.psm1` (464 lines) exporting 13 helpers: `Get-ManifestVersion` (new in refinement), `Test-ManifestIntegrity` (refinement updated for `{ version, files }` schema), `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord`. The pre-refinement `Find-NewestPublishVersion` was retired in the refinement.
- Added `scripts/Install.ps1` (196 lines) orchestrator with parameters `-SourcePath`, `-AllowUnsigned`, `-SkipDocker`, `-Force`. `-Version` was removed in the refinement; bundle root self-locates via `$PSScriptRoot`.
- Added `scripts/Uninstall.ps1` (88 lines) orchestrator with no parameters (reads state from `install-record.json`). Unchanged by the refinement.
- Modified `scripts/Publish.ps1` (+7 / -1): added stage 5 (`Copy-InstallScriptsIntoBundle`) that stages the three install scripts into the bundle root so the bundle is self-installing. Stage 6 (`Write-PublishManifest`) runs after stage 5 so the manifest includes the install scripts.
- Modified `scripts/Publish.Helpers.psm1` (+50 / -11): added `Copy-InstallScriptsIntoBundle` helper; `Write-PublishManifest` now emits the `{ version, files }` schema.

Tests:

- Added `tests/scripts/Install.Helpers.Tests.ps1` (488 lines; export-surface + 12 per-helper contexts; 43 baseline tests then updated for the refinement schema).
- Added `tests/scripts/Install.Tests.ps1` (302 lines; parameter binding, admin precheck, stage ordering, -SkipDocker, -Force, manifest integrity, docker not running, bundle-root self-location, MSIX missing).
- Added `tests/scripts/Uninstall.Tests.ps1` (163 lines; missing record, stage ordering, skipDocker branching, partial-state tolerance, failure collection, preserves user config).
- Modified `tests/scripts/Publish.Tests.ps1` (+24 / -1): stage-5 ordering assertion for install-script staging.
- Modified `tests/scripts/Publish.Helpers.Tests.ps1` (+38 / -4): `Copy-InstallScriptsIntoBundle` helper tests and manifest-schema assertion.

Documentation:

- Updated `README.md` (+11 / -1): points operators to the bundle flow (`cd artifacts/publish/<version>; .\Install.ps1`) and updates the Repository Layout `scripts/` description.
- Updated `docs/mailbridge-runbook.md` (+76 / 0): new "Install Path D: Scripted Bundle Install" section; troubleshooting entries for manifest integrity failure, Docker not running, missing install record; preserves existing Paths A / B / C content (verified via `evidence/other/runbook-path-preservation.2026-04-18T00-00.md`).

Retained unchanged: `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1` (scheduled-task install path).

## 10. Compliance Verdict

Overall verdict: **PASS**.

| Area | Status |
|---|---|
| General unit test policy | PASS |
| General code change policy | PASS |
| PowerShell language-specific code change policy | PASS |
| PowerShell language-specific unit test policy | PASS |
| Test coverage (repo-wide + per-new-file + per-modified-file no regression) | PASS |
| Test execution metrics | PASS |
| Code quality (format / analyze / test / line-count) | PASS |
| Gaps and exceptions (documented, justified) | PASS |

No remediation required. The branch is ready for PR from a policy-compliance perspective.

## Appendix A: Test Inventory

| Test file | `Describe` / `Context` blocks | Approximate test count |
|---|---|---|
| `tests/scripts/Install.Helpers.Tests.ps1` | export surface, `Get-ManifestVersion`, `Test-ManifestIntegrity`, `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord` | ~43 |
| `tests/scripts/Install.Tests.ps1` | parameter binding, administrator precheck on -AllowUnsigned, stage ordering (happy path), -SkipDocker path, -Force over existing install, manifest integrity failure, docker not running, bundle-root self-location, MSIX missing | ~20 |
| `tests/scripts/Uninstall.Tests.ps1` | missing install record, stage ordering (happy path), skipDocker = true, partial state tolerance, failure collection, preserves user config | ~9 |
| `tests/scripts/Publish.Tests.ps1` | parameter validation, stage ordering including new stage 5, argument wiring | existing + refinement tests |
| `tests/scripts/Publish.Helpers.Tests.ps1` | each helper; `Copy-InstallScriptsIntoBundle` added; manifest-schema assertion added | existing + refinement tests |
| Grand total (refinement pass) | — | 150 |

## Appendix B: Toolchain Commands Reference

- Format (check-only): `mcp__drmCopilotExtension__run_poshqc_format`
- Lint (check-only): `mcp__drmCopilotExtension__run_poshqc_analyze`
- Tests + coverage: `mcp__drmCopilotExtension__run_poshqc_test`
- Coverage artifact path: `artifacts/pester/powershell-coverage.xml` (primary) and `artifacts/pester/coverage-final.refinement.xml` (refinement-specific).
- Git diff for this review cycle:
  - `git diff --name-status 7bd92a8cb772c8f41a85831416a5fec952a2330b..cda01a8e8e2f829f20e81dfe487ed82b579d1507`
  - `git diff --numstat 7bd92a8cb772c8f41a85831416a5fec952a2330b..cda01a8e8e2f829f20e81dfe487ed82b579d1507`
  - `git log 7bd92a8cb772c8f41a85831416a5fec952a2330b..cda01a8e8e2f829f20e81dfe487ed82b579d1507 --format="%H %s"`
- Coverage inspection: read `artifacts/pester/powershell-coverage.xml` (JaCoCo format).
- PR context refresh: the Python PR-context collector (`scripts.dev_tools.pr_context.collector`) is not present under the main worktree's `scripts/` directory. The PR context summary and appendix were regenerated manually via `git diff` + `git log` against the resolved base branch `development` and recorded at `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`.
