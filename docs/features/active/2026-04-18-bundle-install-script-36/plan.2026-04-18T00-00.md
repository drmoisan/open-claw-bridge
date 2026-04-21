# Plan — Bundle Install Script (Issue #36)

- **Feature folder:** `docs/features/active/2026-04-18-bundle-install-script-36/`
- **Feature branch:** `feature/bundle-install-script-36` (base: `development`)
- **Work Mode:** `full-feature`
- **Plan Timestamp:** 2026-04-18T00-00
- **Acceptance-criteria sources (authoritative):**
  - `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` (Definition of Done + Behavior + Seeded Test Conditions)
  - `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` (Acceptance Criteria)
- **Supporting inputs:**
  - `docs/features/active/2026-04-18-bundle-install-script-36/issue.md`
  - `artifacts/research/2026-04-18-bundle-install-script.md`
  - `docs/features/active/2026-04-18-unified-publish-script-34/plan.2026-04-18T00-00.md` (precedent plan)
  - `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1` (PowerShell + Pester pattern)
  - `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1` (Pester v5 mock patterns)
  - `.claude/rules/powershell.md`
  - `.claude/rules/general-code-change.md`
  - `.claude/rules/general-unit-test.md`
  - `.claude/rules/tonality.md`

## Design Summary

This plan implements Research Design A: two thin orchestrators (`scripts/Install.ps1` and `scripts/Uninstall.ps1`) plus a single shared helpers module (`scripts/Install.Helpers.psm1`). The split mirrors the `Publish.ps1` + `Publish.Helpers.psm1` pattern established in feature #34 and keeps every file under the 500-line policy ceiling.

The feature delivers:

1. `scripts/Install.Helpers.psm1` — all near-pure helpers (version discovery, manifest integrity, bundle copy, `.env` guard, MSIX shims, docker shims, install-record I/O).
2. `scripts/Install.ps1` — thin orchestrator for install.
3. `scripts/Uninstall.ps1` — thin orchestrator for uninstall.
4. `tests/scripts/Install.Helpers.Tests.ps1` — Pester v5 tests for each helper.
5. `tests/scripts/Install.Tests.ps1` — Pester v5 tests for the install orchestrator (dot-source with full mock injection, stage-ordering assertions).
6. `tests/scripts/Uninstall.Tests.ps1` — Pester v5 tests for the uninstall orchestrator (missing record, stage ordering, failure collection).
7. Documentation updates to `README.md` and `docs/mailbridge-runbook.md` without displacing Path A content.

No existing scripts are deleted. `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain unchanged and continue to serve runbook Path A.

## Owner Decisions Captured (Spec-level — binding)

These came from the spec's "Owner Decisions (resolved)" block and are not renegotiable:

- **Newest-bundle detection:** Auto-detect highest `[System.Version]` subdirectory under `artifacts/publish/`; `-SourcePath` overrides.
- **Destination:** `%LOCALAPPDATA%\OpenClaw\<version>\`.
- **Compose invocation:** Copy `docker/` to destination, then `docker compose --project-name openclaw --project-directory <dest-docker-dir> -f docker-compose.yml up -d openclaw-core openclaw-agent`; verify both services reach running/healthy within a bounded timeout.
- **Install record:** Single-record JSON at `%LOCALAPPDATA%\OpenClaw\install-record.json`, overwritten per install.
- **`-Force` semantics:** Full uninstall-then-install (not in-place overwrite).
- **`.env` guard:** Copy `.env.example` to `.env` only when `.env` is absent.
- **Uninstall order:** `docker compose down` -> `Remove-AppxPackage` -> `Remove-Item` destination -> delete install record. All steps run regardless of individual failures; failures collected and reported as a single terminating error.
- **File split:** `Install.ps1` + `Uninstall.ps1` + `Install.Helpers.psm1`.
- **Language and platform:** PowerShell 7+, Windows-only. Docker Desktop running is a precondition for the docker stage.
- **Scheduled-task scripts retained:** `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` unchanged.

## Planner Decisions (Spec Open Questions)

The spec delegates three items to the planner. Decisions below are binding for this plan and must be reflected in script header comments and Pester tests.

### Q1 — Compose health-poll timeout and interval

- **Decision:** Total bounded timeout of **90 seconds**, poll interval of **3 seconds** (30 poll cycles maximum).
- **Rationale:** The compose file (`docker-compose.yml`) defines `start_period: 20s` for `openclaw-core` and `start_period: 30s` for `openclaw-agent`. A 90-second ceiling provides a 60-second safety margin above the longer of the two `start_period` values to accommodate cold-start image pulls, volume initialization, and health probe retries on slower hosts. A 3-second poll interval gives at least 10 poll cycles after the longer `start_period` expires, reducing the chance of a false negative from a single transient poll while keeping operator feedback cadence under 5 seconds. Both values are exposed via `-TimeoutSeconds` and `-PollIntervalSeconds` parameters on `Wait-ComposeHealthy` with the defaults captured in the helper signature, the script header comment, and one `It` block assertion per parameter.
- **Risk / follow-up:** If observed cold-start times exceed 90 seconds on CI runners, the defaults can be raised in a targeted follow-up without a schema change.

### Q2 — Administrator-privilege precheck for `-AllowUnsigned`

- **Decision:** **Precheck is performed** only when `-AllowUnsigned` is supplied. The check uses `[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)`. When the current process is not elevated, `Install.ps1` aborts before any filesystem side effects with the following terminating error:

  ```
  -AllowUnsigned requires the current PowerShell session to run as administrator when the MSIX contains executable content. Relaunch PowerShell as administrator and retry, or install the signing certificate to Cert:\LocalMachine\TrustedPeople and omit -AllowUnsigned. See https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package for details.
  ```

- **Rationale:** Per Microsoft Learn's unsigned-package guidance, `Add-AppxPackage -AllowUnsigned` requires administrator privileges for packages containing executable content (which the MailBridge MSIX does). Failing fast with a precise remediation message avoids a partial install state where the bundle has been copied but the MSIX step produces a generic Appx error. The precheck runs only on the `-AllowUnsigned` path so non-admin signed installs are not blocked.
- **Risk / follow-up:** Rare packages that contain only non-executable content would not require elevation, but the MailBridge MSIX does not fall into that category. Hardening the check to inspect package contents is out of scope.

### Q3 — Runbook structure for the new path

- **Decision:** Add a **new "Install Path D: Scripted Bundle Install"** section to `docs/mailbridge-runbook.md`, placed after the existing "Install Path C: Additive HostAdapter Plus Docker Core" section and before "Optional OpenClaw Assistant Service". **Path A, Path B, and Path C content is preserved verbatim.** Path B and Path C are updated only by adding a single cross-reference line at the end of each pointing operators to Path D for the scripted bundle flow; the manual steps remain authoritative for those paths. `README.md` gains a new bullet under "What It Does" listing `Install.ps1` as the scripted bundle path alongside the existing scheduled-task path.
- **Rationale:** The scripted bundle flow is a distinct install mode driven by a new script pair; conflating it with Path B (manual MSIX) or Path C (additive HostAdapter + Docker) would reduce operator clarity. A new Path D section with its own prerequisites, invocations, and troubleshooting entries mirrors the structure of Path A/B/C. The scope decision explicitly mandates Path A retention; placing Path D last preserves all existing section numbering and anchor links for Paths A-C.
- **Risk / follow-up:** Operators who search for "bundle" in the runbook will find Path D. If a future feature supersedes Paths B or C, those can be retired in a separate change without touching Path D.

---

## Phase 0 — Baseline Capture

Capture the exact toolchain and repo state before any change. All artifacts land under `docs/features/active/2026-04-18-bundle-install-script-36/evidence/baseline/` with timestamps in `yyyy-MM-ddTHH-mm` format. Each artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.

- [x] [P0-T1] Read `AGENTS.md` (repo-root standing instructions) and record the file list in `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md` with `Timestamp:`, `Policy Order:`, and explicit list of files read.
- [x] [P0-T2] Read `.claude/rules/general-code-change.md` and append its path and one-line purpose to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T3] Read `.claude/rules/general-unit-test.md` and append its path and one-line purpose to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T4] Read `.claude/rules/powershell.md` and append its path and one-line purpose to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T5] Read `.claude/rules/tonality.md` and append its path and one-line purpose to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T6] Read spec at `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` and append confirmation plus AC count to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T7] Read user story at `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` and append confirmation to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T8] Read issue at `docs/features/active/2026-04-18-bundle-install-script-36/issue.md` and append confirmation to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T9] Read research artifact at `artifacts/research/2026-04-18-bundle-install-script.md` and append confirmation to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T10] Read the precedent plan at `docs/features/active/2026-04-18-unified-publish-script-34/plan.2026-04-18T00-00.md` and append confirmation to `evidence/baseline/phase0-instructions-read.2026-04-18T00-00.md`.
- [x] [P0-T11] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/` and `tests/` in check-only mode; persist full output to `evidence/baseline/baseline-poshqc-format.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail plus any files flagged for formatting).
- [x] [P0-T12] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `scripts/` and `tests/`; persist output to `evidence/baseline/baseline-poshqc-analyze.2026-04-18T00-00.md` with all four schema fields including rule-violation counts in `Output Summary:`.
- [x] [P0-T13] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled; persist output to `evidence/baseline/baseline-pester.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that includes baseline pass/fail counts AND numeric repo-wide line-coverage percentage.
- [x] [P0-T14] Record docker-compose `start_period` values for `openclaw-core` and `openclaw-agent` (reading `docker-compose.yml` lines for each service) to `evidence/baseline/baseline-compose-start-periods.2026-04-18T00-00.md` with all four schema fields. This is the authoritative reference for the Q1 health-poll ceiling (90s timeout, 3s interval).
- [x] [P0-T15] Confirm non-collision of new files: grep `scripts/` for existing `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1` and `tests/scripts/` for existing `Install.Tests.ps1`, `Uninstall.Tests.ps1`, `Install.Helpers.Tests.ps1`. Persist results to `evidence/baseline/baseline-new-file-non-collision.2026-04-18T00-00.md` with all four schema fields. Acceptance: zero matches for each target path.

---

## Phase 1 — Install.Helpers.psm1 per-Helper Batches

Introduce `scripts/Install.Helpers.psm1` and its Pester test file in five batches of at most three production functions plus three test `Describe` blocks each. After every batch, run the PowerShell QA loop (format -> analyze -> test + coverage). Restart the batch if any step fails or auto-fixes files. The module stays strictly under 500 lines; a line-count guard runs at the end of the phase.

### Phase 1 — Batch 1 (skeleton + version discovery + manifest integrity)

- [x] [P1-T1] Create `scripts/Install.Helpers.psm1` with a professional-tone file header comment block, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, and a single `Export-ModuleMember -Function` declaration that lists the final 13 function names as inline comments (Find-NewestPublishVersion, Test-ManifestIntegrity, Copy-BundleContents, Initialize-DotEnv, Invoke-MsixInstall, Invoke-MsixCapture, Invoke-MsixRemove, Test-DockerAvailable, Invoke-ComposeUp, Wait-ComposeHealthy, Invoke-ComposeDown, Write-InstallRecord, Read-InstallRecord) but only exports the two added in this batch. File ends with a trailing newline.
- [x] [P1-T2] Add `Find-NewestPublishVersion` to `scripts/Install.Helpers.psm1`. Signature: `[CmdletBinding()] [OutputType([pscustomobject])] param([Parameter(Mandatory=$true)][string]$PublishRoot)`. Body enumerates child directories, filters those where `[System.Version]::TryParse($_.Name, [ref]$v)` succeeds, sorts descending by the parsed version, and returns `[pscustomobject]@{ Version = <System.Version>; Path = <string> }`. Throws when no parseable directory is found with a message that names `$PublishRoot`.
- [x] [P1-T3] Add `Test-ManifestIntegrity` to `scripts/Install.Helpers.psm1`. Signature: `[CmdletBinding()] param([Parameter(Mandatory=$true)][string]$BundleRoot)`. Reads `<BundleRoot>/manifest.json`; for each entry, verifies file existence, size (as `[long]`), and `(Get-FileHash -LiteralPath <path> -Algorithm SHA256).Hash.ToLowerInvariant()`. Also enumerates files on disk (excluding `manifest.json`) and reports any not listed in the manifest. Accumulates every discrepancy into a single array and throws one terminating error listing all discrepancies when non-empty. Paths use `Join-Path $BundleRoot ($entry.path -replace '/', [IO.Path]::DirectorySeparatorChar)`.
- [x] [P1-T4] Update the `Export-ModuleMember` list in `scripts/Install.Helpers.psm1` to export `Find-NewestPublishVersion` and `Test-ManifestIntegrity`.
- [x] [P1-T5] Create `tests/scripts/Install.Helpers.Tests.ps1` with a Pester v5 `Describe 'scripts/Install.Helpers.psm1 — export surface'` block that imports the module via `Import-Module <repo>/scripts/Install.Helpers.psm1 -Force` in `BeforeAll` and asserts that `Get-Command -Module Install.Helpers` contains exactly the two currently-exported names from P1-T4.
- [x] [P1-T6] Add `Describe 'Find-NewestPublishVersion'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) returns the highest-version directory when multiple parseable names exist; (b) filters out non-parseable directory names (`bridge`, `client`); (c) throws with a message containing the publish root when no parseable directory exists. Mock `Get-ChildItem` to return a controlled set of directory entries.
- [x] [P1-T7] Add `Describe 'Test-ManifestIntegrity'` to `tests/scripts/Install.Helpers.Tests.ps1` with four `It` blocks: (a) passes silently when every manifest entry matches disk; (b) throws a single terminating error listing every hash mismatch; (c) throws when an on-disk file under the bundle root is absent from the manifest; (d) throws when a manifest entry points to a missing file. Mock `Get-FileHash`, `Get-Item`, `Get-ChildItem`, `Test-Path`, and `Get-Content` to return controlled fixtures. No temporary files.
- [x] [P1-T8] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Install.Helpers.psm1` and `tests/scripts/Install.Helpers.Tests.ps1`. If any file changes, restart Phase 1 Batch 1 QA from P1-T8.
- [x] [P1-T9] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same two files. Fail the task if any diagnostic is emitted.
- [x] [P1-T10] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage for `tests/scripts/Install.Helpers.Tests.ps1`; assert Pester passes, zero test regressions vs Phase 0 baseline, and coverage for the two new helpers in `scripts/Install.Helpers.psm1` is `>= 90%`.

### Phase 1 — Batch 2 (bundle copy + `.env` guard + MSIX install)

- [x] [P1-T11] Add `Copy-BundleContents` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-SourceBundleRoot`, `-DestinationRoot`. Creates `<DestinationRoot>/executables/` and `<DestinationRoot>/docker/`, then copies the bundle's `executables/` and `docker/` subtrees recursively with relative paths preserved. Calls `New-Item` and `Copy-Item` under `ShouldProcess`.
- [x] [P1-T12] Add `Initialize-DotEnv` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-DestDockerDir`. Computes `$destEnv = Join-Path $DestDockerDir '.env'` and `$srcExample = Join-Path $DestDockerDir '.env.example'`. When `-not (Test-Path -LiteralPath $destEnv)`, calls `Copy-Item -LiteralPath $srcExample -Destination $destEnv` under `ShouldProcess`. When `$destEnv` exists, returns silently without writing or warning.
- [x] [P1-T13] Add `Invoke-MsixInstall` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-MsixPath` (mandatory string), `-AllowUnsigned` (switch). Wraps `Add-AppxPackage -Path $MsixPath` and conditionally appends `-AllowUnsigned` when the switch is supplied. Re-throws `Add-AppxPackage` errors with added context that names `$MsixPath` and the `-AllowUnsigned` flag state.
- [x] [P1-T14] Update `Export-ModuleMember` in `scripts/Install.Helpers.psm1` to also export `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`.
- [x] [P1-T15] Add `Describe 'Copy-BundleContents'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) creates the destination `executables/` and `docker/` subdirectories via `New-Item`; (b) invokes `Copy-Item` with `-Recurse` for each subtree; (c) `-WhatIf` produces no `New-Item` or `Copy-Item` calls. Mock `New-Item` and `Copy-Item` at the module scope.
- [x] [P1-T16] Add `Describe 'Initialize-DotEnv'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) copies `.env.example` to `.env` when `.env` is absent; (b) does not invoke `Copy-Item` when `.env` already exists; (c) `-WhatIf` produces no `Copy-Item` call. Mock `Test-Path` and `Copy-Item`.
- [x] [P1-T17] Add `Describe 'Invoke-MsixInstall'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) calls `Add-AppxPackage -Path <MsixPath>` with no additional flags by default; (b) calls `Add-AppxPackage -Path <MsixPath> -AllowUnsigned` when the switch is supplied; (c) re-throws with a message that references the MSIX path when `Add-AppxPackage` fails. Mock `Add-AppxPackage`.
- [x] [P1-T18] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Install.Helpers.psm1` and `tests/scripts/Install.Helpers.Tests.ps1`. Restart Batch 2 QA from P1-T18 on any file change.
- [x] [P1-T19] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same files. Zero diagnostics required.
- [x] [P1-T20] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on all helpers added in Batches 1 and 2 and zero test regressions.

### Phase 1 — Batch 3 (MSIX capture + MSIX remove + docker readiness)

- [x] [P1-T21] Add `Invoke-MsixCapture` to `scripts/Install.Helpers.psm1`. Signature: `[CmdletBinding()] [OutputType([string])] param()`. Wraps `Get-AppxPackage -Name 'OpenClaw.MailBridge'` and returns the `PackageFullName` property. Throws when no package is found with a message naming the expected identity.
- [x] [P1-T22] Add `Invoke-MsixRemove` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-PackageFullName`. Calls `Get-AppxPackage -Name 'OpenClaw.MailBridge'`; if the result is null/empty, returns silently. Otherwise calls `Remove-AppxPackage -Package $PackageFullName` under `ShouldProcess`.
- [x] [P1-T23] Add `Test-DockerAvailable` to `scripts/Install.Helpers.psm1`. Signature: `[CmdletBinding()] [OutputType([bool])] param()`. Calls `& docker info 2>$null | Out-Null`; returns `$true` on `$LASTEXITCODE -eq 0`; throws on non-zero with the remediation message: "Docker Desktop is not running or not installed. Start Docker Desktop and retry, or pass -SkipDocker to skip the container stage."
- [x] [P1-T24] Update `Export-ModuleMember` in `scripts/Install.Helpers.psm1` to also export `Invoke-MsixCapture`, `Invoke-MsixRemove`, `Test-DockerAvailable`.
- [x] [P1-T25] Add `Describe 'Invoke-MsixCapture'` to `tests/scripts/Install.Helpers.Tests.ps1` with two `It` blocks: (a) returns the `PackageFullName` of the package returned by `Get-AppxPackage`; (b) throws with a descriptive message when `Get-AppxPackage` returns null. Mock `Get-AppxPackage`.
- [x] [P1-T26] Add `Describe 'Invoke-MsixRemove'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) calls `Remove-AppxPackage -Package <name>` when the package is installed; (b) returns silently (no `Remove-AppxPackage` call) when `Get-AppxPackage` returns null; (c) `-WhatIf` produces no `Remove-AppxPackage` call. Mock `Get-AppxPackage` and `Remove-AppxPackage`.
- [x] [P1-T27] Add `Describe 'Test-DockerAvailable'` to `tests/scripts/Install.Helpers.Tests.ps1` with two `It` blocks: (a) returns `$true` when the `docker` shim sets `$LASTEXITCODE = 0`; (b) throws with a remediation message containing "-SkipDocker" when the shim sets `$LASTEXITCODE = 1`. Define `function global:docker { $script:LASTEXITCODE = 0 }` in `BeforeEach`, overridden per-test with the required exit code.
- [x] [P1-T28] Run `mcp__drmCopilotExtension__run_poshqc_format` against both files. Restart Batch 3 QA from P1-T28 on any change.
- [x] [P1-T29] Run `mcp__drmCopilotExtension__run_poshqc_analyze`. Zero diagnostics required.
- [x] [P1-T30] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on helpers added in Batches 1-3 and zero test regressions.

### Phase 1 — Batch 4 (compose up + compose health poll + compose down)

- [x] [P1-T31] Add `Invoke-ComposeUp` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-DestDockerDir`, `-ComposeFilePath`, `-ProjectName` (default `'openclaw'`). Under `ShouldProcess`, invokes `& docker compose --project-name $ProjectName --project-directory $DestDockerDir -f $ComposeFilePath up -d openclaw-core openclaw-agent`. Throws on non-zero `$LASTEXITCODE` with the compose file path in the message.
- [x] [P1-T32] Add `Wait-ComposeHealthy` to `scripts/Install.Helpers.psm1`. Parameters: `-ComposeFilePath`, `-ProjectName` (default `'openclaw'`), `-TimeoutSeconds` (default `90`), `-PollIntervalSeconds` (default `3`). Polls `& docker compose --project-name $ProjectName -f $ComposeFilePath ps --format json`, parses the JSON array, and checks that entries for both `openclaw-core` and `openclaw-agent` report `State -eq 'running'` and `Health -eq 'healthy'` (or empty `Health`). Emits `Write-Information` with elapsed seconds each cycle. Throws on timeout with the failing service name and last-observed `State`/`Health` values. Planner decision Q1 values (90s, 3s) are documented in the function's comment-based help.
- [x] [P1-T33] Add `Invoke-ComposeDown` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-ComposeFilePath`, `-ProjectName` (default `'openclaw'`). Under `ShouldProcess`, invokes `& docker compose --project-name $ProjectName -f $ComposeFilePath down`. Throws on non-zero exit with the compose file path in the message.
- [x] [P1-T34] Update `Export-ModuleMember` in `scripts/Install.Helpers.psm1` to also export `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`.
- [x] [P1-T35] Add `Describe 'Invoke-ComposeUp'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) the `docker` shim receives `compose --project-name openclaw --project-directory <dir> -f <file> up -d openclaw-core openclaw-agent` verbatim; (b) throws on non-zero exit; (c) `-WhatIf` does not invoke the shim. Define `function global:docker { $global:lastArgs = $args; $script:LASTEXITCODE = 0 }` and assert `$global:lastArgs`.
- [x] [P1-T36] Add `Describe 'Wait-ComposeHealthy'` to `tests/scripts/Install.Helpers.Tests.ps1` with four `It` blocks: (a) returns when both services report `running` + `healthy` on the first poll; (b) retries until the configured timeout and throws with the failing service name when a service never reports `healthy`; (c) accepts `Health` as null/empty when no healthcheck is defined; (d) default values `-TimeoutSeconds 90` and `-PollIntervalSeconds 3` are applied when not supplied (assert via parameter default introspection). Shim `docker` to return controlled JSON arrays per call.
- [x] [P1-T37] Add `Describe 'Invoke-ComposeDown'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) the `docker` shim receives `compose --project-name <name> -f <file> down` verbatim; (b) throws on non-zero exit; (c) `-WhatIf` does not invoke the shim.
- [x] [P1-T38] Run `mcp__drmCopilotExtension__run_poshqc_format` against both files. Restart Batch 4 QA on any change.
- [x] [P1-T39] Run `mcp__drmCopilotExtension__run_poshqc_analyze`. Zero diagnostics required.
- [x] [P1-T40] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert `>= 90%` coverage on all helpers added through Batch 4 and zero test regressions.

### Phase 1 — Batch 5 (install record I/O + export surface + line-count guard)

- [x] [P1-T41] Add `Write-InstallRecord` to `scripts/Install.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-Record` (pscustomobject, mandatory), `-RecordPath` (string, mandatory). Serializes `$Record` via `ConvertTo-Json -Depth 5` and writes to `$RecordPath` via `Set-Content -LiteralPath $RecordPath -Encoding utf8` under `ShouldProcess`. Ensures parent directory exists via `New-Item -ItemType Directory -Force` on `Split-Path $RecordPath`.
- [x] [P1-T42] Add `Read-InstallRecord` to `scripts/Install.Helpers.psm1`. Signature: `[CmdletBinding()] [OutputType([pscustomobject])] param([Parameter(Mandatory=$true)][string]$RecordPath)`. Throws a terminating error with a clear "no prior install recorded" message when `-not (Test-Path -LiteralPath $RecordPath)`. Otherwise reads the file and returns the result of `Get-Content -LiteralPath $RecordPath -Raw | ConvertFrom-Json`.
- [x] [P1-T43] Update `Export-ModuleMember` in `scripts/Install.Helpers.psm1` to export the complete list of 13 functions. Acceptance: `Import-Module scripts/Install.Helpers.psm1 -Force; Get-Command -Module Install.Helpers` lists exactly those 13 names.
- [x] [P1-T44] Update the export-surface `Describe` block in `tests/scripts/Install.Helpers.Tests.ps1` (introduced at P1-T5) so its `It` assertion now asserts the full 13-function set.
- [x] [P1-T45] Add `Describe 'Write-InstallRecord'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) calls `Set-Content` with the serialized JSON at the supplied path; (b) ensures the parent directory is created; (c) `-WhatIf` produces no `Set-Content` call. Mock `Set-Content` and `New-Item`.
- [x] [P1-T46] Add `Describe 'Read-InstallRecord'` to `tests/scripts/Install.Helpers.Tests.ps1` with three `It` blocks: (a) returns a parsed pscustomobject when the file exists; (b) throws a specific "no prior install recorded" message when the file is absent; (c) returned object exposes the documented fields `version`, `destinationPath`, `packageFullName`, `composeProjectName`, `composeFilePath`, `skipDocker`, `allowUnsigned`. Mock `Test-Path` and `Get-Content`.
- [x] [P1-T47] Run `mcp__drmCopilotExtension__run_poshqc_format` against both files. Restart Batch 5 QA on any change.
- [x] [P1-T48] Run `mcp__drmCopilotExtension__run_poshqc_analyze`. Zero diagnostics required.
- [x] [P1-T49] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on `scripts/Install.Helpers.psm1` overall and zero test regressions.
- [x] [P1-T50] Verify `scripts/Install.Helpers.psm1` line count via `Get-Content -Path scripts/Install.Helpers.psm1 | Measure-Object -Line`. Acceptance: `<= 500` lines. Record the count at the end of Phase 1 as a note in `evidence/baseline/phase1-line-count.2026-04-18T00-00.md` with all four schema fields.

---

## Phase 2 — Install.ps1 Orchestrator and Tests

Introduce `scripts/Install.ps1` as a thin orchestrator (target `<= 250` lines). Apply Planner Decision Q2 (administrator precheck on `-AllowUnsigned`) and Q1 (health-poll defaults) in the script.

- [x] [P2-T1] Create `scripts/Install.ps1` with `#Requires -Version 7.0`, a file header comment block (professional tone) restating the three planner decisions (Q1 90s/3s health-poll defaults, Q2 administrator precheck on `-AllowUnsigned`, Q3 runbook Path D reference), and `[CmdletBinding(SupportsShouldProcess=$true)]`.
- [x] [P2-T2] Add the `param()` block to `scripts/Install.ps1`: `-SourcePath` (string, default `''`), `-Version` (string, default `''`, `[ValidatePattern('^(\d+\.\d+\.\d+\.\d+)?$')]`), `-AllowUnsigned` (switch), `-SkipDocker` (switch), `-Force` (switch).
- [x] [P2-T3] Add `Import-Module (Join-Path $PSScriptRoot 'Install.Helpers.psm1') -Force` and `Set-StrictMode -Version Latest; $ErrorActionPreference = 'Stop'` at the top of `scripts/Install.ps1`.
- [x] [P2-T4] Add the administrator precheck stage in `scripts/Install.ps1` gated on `$AllowUnsigned`. Uses `[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)`; throws the Q2 remediation message when not elevated. No other stage runs before this check. Emits `[install:precheck]` progress.
- [x] [P2-T5] Add the bundle-selection stage in `scripts/Install.ps1`. Resolves `$BundleRoot` in priority order: (1) `-SourcePath` if non-empty; (2) `artifacts/publish/<Version>/` if `-Version` is non-empty; (3) `(Find-NewestPublishVersion -PublishRoot 'artifacts/publish').Path`. Throws when the resolved path does not exist with the path in the message. Emits `[install:select]` progress.
- [x] [P2-T6] Add the manifest-integrity stage in `scripts/Install.ps1`: calls `Test-ManifestIntegrity -BundleRoot $BundleRoot`. Emits `[install:verify]` progress. Throws propagate the helper's terminating error unchanged. Acceptance: no destination folder creation occurs before this stage completes.
- [x] [P2-T7] Add the prior-install detection stage in `scripts/Install.ps1`. Reads `$InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'` and `$DestinationPath = Join-Path $env:LOCALAPPDATA "OpenClaw/$ResolvedVersion"`. When either exists: if `-not $Force`, throw the "pass -Force or run Uninstall.ps1 first" remediation; if `-Force`, invoke the uninstall sequence (`Read-InstallRecord` -> optional `Invoke-ComposeDown` -> `Invoke-MsixRemove` -> `Remove-Item -Recurse -Force $DestinationPath` -> `Remove-Item $InstallRecordPath`) against the prior record, tolerating per-step failures per the uninstall contract. Emits `[install:force-uninstall]` progress.
- [x] [P2-T8] Add the Docker readiness stage in `scripts/Install.ps1` gated on `-not $SkipDocker`: calls `Test-DockerAvailable`. Emits `[install:docker-check]` progress. When `$SkipDocker` is set, the stage is skipped silently.
- [x] [P2-T9] Add the bundle-copy stage in `scripts/Install.ps1`: calls `New-Item -ItemType Directory -Force $DestinationPath`, then `Copy-BundleContents -SourceBundleRoot $BundleRoot -DestinationRoot $DestinationPath`. Emits `[install:copy]` progress.
- [x] [P2-T10] Add the `.env` guard stage in `scripts/Install.ps1`: computes `$DestDockerDir = Join-Path $DestinationPath 'docker'` and calls `Initialize-DotEnv -DestDockerDir $DestDockerDir`. Emits `[install:env]` progress.
- [x] [P2-T11] Add the MSIX-install stage in `scripts/Install.ps1`: resolves `$MsixPath = Join-Path $BundleRoot "msix/OpenClaw.MailBridge_${ResolvedVersion}_x64.msix"`. Throws with the expected path when `-not (Test-Path -LiteralPath $MsixPath)`. Calls `Invoke-MsixInstall -MsixPath $MsixPath -AllowUnsigned:$AllowUnsigned`. Then captures `$PackageFullName = Invoke-MsixCapture`. Emits `[install:msix]` progress.
- [x] [P2-T12] Add the docker-up stage in `scripts/Install.ps1` gated on `-not $SkipDocker`: calls `Invoke-ComposeUp -DestDockerDir $DestDockerDir -ComposeFilePath (Join-Path $DestDockerDir 'docker-compose.yml')` then `Wait-ComposeHealthy -ComposeFilePath (Join-Path $DestDockerDir 'docker-compose.yml')` (using default Q1 values 90s/3s). Emits `[install:docker]` progress.
- [x] [P2-T13] Add the install-record stage in `scripts/Install.ps1`: builds a pscustomobject with `installedAt` (UTC ISO-8601 via `(Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')`), `version`, `sourcePath`, `destinationPath`, `packageFullName`, `composeProjectName = 'openclaw'`, `composeFilePath`, `skipDocker`, `allowUnsigned`. Calls `Write-InstallRecord -Record $record -RecordPath $InstallRecordPath`. Emits `[install:record]` progress and a final success line.
- [x] [P2-T14] Add the main-body dot-source guard at the end of `scripts/Install.ps1`: wrap the stage orchestration in `if ($MyInvocation.InvocationName -ne '.') { ... }` so tests can dot-source without triggering execution.
- [x] [P2-T15] Create `tests/scripts/Install.Tests.ps1` with a Pester v5 `Describe 'scripts/Install.ps1'` block. `BeforeAll` defines a global `docker` shim, imports `Install.Helpers.psm1`, sets `$env:LOCALAPPDATA = 'TestDrive:\AppData\Local'` equivalent (via `TestDrive` + environment override without creating real files), and dot-sources the script with `-WhatIf` as appropriate.
- [x] [P2-T16] Add `Context 'parameter binding'` in `tests/scripts/Install.Tests.ps1` with three `It` blocks: (a) accepts all five parameters with defaults; (b) `-SourcePath` overrides `-Version`; (c) an invalid 3-part `-Version` is rejected by ValidatePattern.
- [x] [P2-T17] Add `Context 'administrator precheck on -AllowUnsigned'` in `tests/scripts/Install.Tests.ps1` with two `It` blocks: (a) when `-AllowUnsigned` is supplied and the principal lookup returns `$false`, the script throws with a message containing the remediation text before any helper is invoked; (b) when `-AllowUnsigned` is NOT supplied, the principal lookup is not performed. Mock the `WindowsPrincipal.IsInRole` probe via a wrapper function or `Mock` on the enclosing helper.
- [x] [P2-T18] Add `Context 'stage ordering (happy path)'` in `tests/scripts/Install.Tests.ps1`: with every helper in `Install.Helpers.psm1` mocked with a call-log, assert the invocation order is: `Test-ManifestIntegrity` -> `Test-DockerAvailable` -> `New-Item` (destination) -> `Copy-BundleContents` -> `Initialize-DotEnv` -> `Invoke-MsixInstall` -> `Invoke-MsixCapture` -> `Invoke-ComposeUp` -> `Wait-ComposeHealthy` -> `Write-InstallRecord`.
- [x] [P2-T19] Add `Context '-SkipDocker path'` in `tests/scripts/Install.Tests.ps1` with three `It` blocks: (a) `Test-DockerAvailable` is NOT invoked; (b) `Invoke-ComposeUp` and `Wait-ComposeHealthy` are NOT invoked; (c) the written install record captures `skipDocker = $true`.
- [x] [P2-T20] Add `Context '-Force over existing install'` in `tests/scripts/Install.Tests.ps1`: when the destination or install-record file exists and `-Force` is supplied, assert the uninstall sequence (`Invoke-ComposeDown` -> `Invoke-MsixRemove` -> `Remove-Item` -> delete record) is invoked before the install sequence starts. When `-Force` is NOT supplied and a prior install exists, assert the script throws with a remediation message that names `-Force` and `Uninstall.ps1`.
- [x] [P2-T21] Add `Context 'manifest integrity failure'` in `tests/scripts/Install.Tests.ps1`: when `Test-ManifestIntegrity` throws, assert `New-Item` for the destination is never invoked and no helper after `Test-ManifestIntegrity` is invoked.
- [x] [P2-T22] Add `Context 'docker not running'` in `tests/scripts/Install.Tests.ps1`: when `Test-DockerAvailable` throws (non-zero `docker info`), assert `Copy-BundleContents` is never invoked and the thrown message contains "-SkipDocker".
- [x] [P2-T23] Add `Context 'MSIX missing'` in `tests/scripts/Install.Tests.ps1`: when the MSIX file path does not exist, the script throws with the expected path; `Invoke-MsixInstall` is never invoked.
- [x] [P2-T24] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Install.ps1` and `tests/scripts/Install.Tests.ps1`. Restart Phase 2 QA from P2-T24 on any change.
- [x] [P2-T25] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same files. Zero diagnostics required.
- [x] [P2-T26] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on new lines in `scripts/Install.ps1` and zero test regressions.
- [x] [P2-T27] Verify `scripts/Install.ps1` is `<= 500` lines via line-count measurement. Record in `evidence/baseline/phase2-line-count.2026-04-18T00-00.md` with all four schema fields.

---

## Phase 3 — Uninstall.ps1 Orchestrator and Tests

Introduce `scripts/Uninstall.ps1` as a thin orchestrator (target `<= 150` lines). Uninstall accepts no parameters and reads everything from `install-record.json`.

- [x] [P3-T1] Create `scripts/Uninstall.ps1` with `#Requires -Version 7.0`, a file header comment block (professional tone) stating that the uninstall consumes `%LOCALAPPDATA%\OpenClaw\install-record.json` and that all steps run regardless of per-step failure, and `[CmdletBinding(SupportsShouldProcess=$true)]` with no `param()` block.
- [x] [P3-T2] Add `Import-Module (Join-Path $PSScriptRoot 'Install.Helpers.psm1') -Force`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'` at the top of `scripts/Uninstall.ps1`.
- [x] [P3-T3] Add the record-load stage in `scripts/Uninstall.ps1`: resolves `$InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'`; calls `Read-InstallRecord -RecordPath $InstallRecordPath` (which throws when absent). Emits `[uninstall:load]` progress.
- [x] [P3-T4] Add the failure-collection loop in `scripts/Uninstall.ps1`: initializes `$failures = @()`, then runs the four uninstall steps in order, catching per-step exceptions and appending `{ Step = <name>; Error = $_.Exception.Message }` to `$failures` before continuing.
- [x] [P3-T5] Add the compose-down step inside the failure-collection loop in `scripts/Uninstall.ps1`: when `$record.skipDocker -ne $true`, call `Invoke-ComposeDown -ComposeFilePath $record.composeFilePath -ProjectName $record.composeProjectName`. Emits `[uninstall:docker]` progress.
- [x] [P3-T6] Add the MSIX-remove step in `scripts/Uninstall.ps1`: calls `Invoke-MsixRemove -PackageFullName $record.packageFullName`. Emits `[uninstall:msix]` progress.
- [x] [P3-T7] Add the destination-remove step in `scripts/Uninstall.ps1`: calls `Remove-Item -LiteralPath $record.destinationPath -Recurse -Force` under `ShouldProcess`. Tolerates the missing-target case silently. Emits `[uninstall:folder]` progress.
- [x] [P3-T8] Add the install-record-remove step in `scripts/Uninstall.ps1`: calls `Remove-Item -LiteralPath $InstallRecordPath -Force` under `ShouldProcess`. Emits `[uninstall:record]` progress.
- [x] [P3-T9] Add the terminal failure-report stage in `scripts/Uninstall.ps1`: after all steps complete, when `$failures.Count -gt 0`, throw a single terminating error listing every failed step and a remediation suggestion per step. On zero failures, emit a `[uninstall:done]` success line.
- [x] [P3-T10] Add the main-body dot-source guard at the end of `scripts/Uninstall.ps1` with `if ($MyInvocation.InvocationName -ne '.') { ... }`.
- [x] [P3-T11] Create `tests/scripts/Uninstall.Tests.ps1` with a Pester v5 `Describe 'scripts/Uninstall.ps1'` block. `BeforeAll` defines the global `docker` shim and imports `Install.Helpers.psm1`.
- [x] [P3-T12] Add `Context 'missing install record'` in `tests/scripts/Uninstall.Tests.ps1`: when `Read-InstallRecord` throws, the script throws with a message containing "no prior install" and no helper after the record-load stage is invoked.
- [x] [P3-T13] Add `Context 'stage ordering (happy path)'` in `tests/scripts/Uninstall.Tests.ps1`: with every helper mocked, assert invocation order is `Read-InstallRecord` -> `Invoke-ComposeDown` -> `Invoke-MsixRemove` -> `Remove-Item` (destination) -> `Remove-Item` (install record). Assert exit code 0 / no terminating throw.
- [x] [P3-T14] Add `Context 'skipDocker = true'` in `tests/scripts/Uninstall.Tests.ps1`: when the record has `skipDocker = $true`, `Invoke-ComposeDown` is NOT invoked; the MSIX, destination, and record steps still run.
- [x] [P3-T15] Add `Context 'partial state tolerance'` in `tests/scripts/Uninstall.Tests.ps1` with three `It` blocks: (a) when `Invoke-MsixRemove` returns silently (package already absent), subsequent steps still run and no failure is recorded; (b) when `Remove-Item` for the destination targets a missing path, no failure is recorded; (c) all four steps still run when one step fails mid-way.
- [x] [P3-T16] Add `Context 'failure collection'` in `tests/scripts/Uninstall.Tests.ps1`: when two of the four steps throw, assert (a) all four steps are attempted; (b) the script throws one terminating error whose message references both failing step names; (c) the install-record-remove step still runs when the destination-remove step fails.
- [x] [P3-T17] Add `Context 'preserves user config'` in `tests/scripts/Uninstall.Tests.ps1`: assert `Remove-Item` is never invoked against `%LOCALAPPDATA%\OpenClaw\MailBridge\` or any path under that sibling directory. Verified by inspecting all `Remove-Item` invocations' `-LiteralPath`.
- [x] [P3-T18] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Uninstall.ps1` and `tests/scripts/Uninstall.Tests.ps1`. Restart Phase 3 QA from P3-T18 on any change.
- [x] [P3-T19] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same files. Zero diagnostics required.
- [x] [P3-T20] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on new lines in `scripts/Uninstall.ps1` and zero test regressions.
- [x] [P3-T21] Verify `scripts/Uninstall.ps1` is `<= 500` lines via line-count measurement. Record in `evidence/baseline/phase3-line-count.2026-04-18T00-00.md` with all four schema fields.

---

## Phase 4 — Documentation Updates

Update user-facing documents per Planner Decision Q3. No changes to policy documents under `.claude/rules/` or `.github/instructions/`. Path A content in `docs/mailbridge-runbook.md` is preserved verbatim.

- [x] [P4-T1] Edit `README.md` to add a new bullet under "What It Does" naming `Install.ps1` as the scripted bundle install path and to update the "Repository Layout" `scripts/` row so the description includes `Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1`. Acceptance: README shows both the existing scheduled-task path text (unchanged) and the new scripted bundle path entry.
- [x] [P4-T2] Edit `docs/mailbridge-runbook.md` to add a new "Install Path D: Scripted Bundle Install" section between "Install Path C: Additive HostAdapter Plus Docker Core" and "Optional OpenClaw Assistant Service". Section contents: (a) prerequisites (PowerShell 7+, Docker Desktop running when docker stage is enabled, administrator shell when using `-AllowUnsigned`); (b) command invocations (default, `-SourcePath`, `-Version`, `-AllowUnsigned`, `-SkipDocker`, `-Force`); (c) uninstall command; (d) outputs (install record location); (e) operator notes for HostAdapter token and `secrets/.env.anthropic` still being out-of-band. Acceptance: grep for `Install Path A:` and `Install Path B:` in the file still returns the pre-existing sections unchanged; `Install Path D:` is present exactly once.
- [x] [P4-T3] Edit `docs/mailbridge-runbook.md` "Troubleshooting" table to add rows for: (a) "No prior install recorded" (uninstall with absent `install-record.json`); (b) "Docker Desktop not running" (install with docker stage enabled); (c) "Manifest integrity failure" (hash/size mismatch or missing files under bundle root). Each row cites the remediation text emitted by the script.
- [x] [P4-T4] Append a one-line cross-reference to the end of "Install Path B: MSIX Package" section pointing to Path D for the scripted bundle flow. Do not remove or reorder existing Path B content.
- [x] [P4-T5] Append a one-line cross-reference to the end of "Install Path C: Additive HostAdapter Plus Docker Core" section pointing to Path D for the automated compose stage. Do not remove or reorder existing Path C content.
- [x] [P4-T6] Record a trigger-parity-style artifact `evidence/other/runbook-path-preservation.2026-04-18T00-00.md` with `Timestamp:`, `Command:` (the grep invocations used), `EXIT_CODE:`, and `Output Summary:` listing (a) presence of pre-existing `Install Path A:`, `Install Path B:`, `Install Path C:` section headings after the edit; (b) presence of the new `Install Path D:` section; (c) zero removals of pre-existing Path A content.

---

## Phase 5 — Final QA Gate

Run the complete PowerShell QA loop (format -> analyze -> test) against the whole repo and compare against the Phase 0 baseline. Every command step produces its own evidence artifact under `evidence/qa-gates/`. Restart the loop from P5-T1 if any step auto-fixes files or fails.

- [x] [P5-T1] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/` and `tests/`. Persist output to `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail, any files changed). If any file changes, restart the phase from P5-T1.
- [x] [P5-T2] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `scripts/` and `tests/`. Persist output to `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md` with all four schema fields and a diagnostic count. Must equal zero.
- [x] [P5-T3] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled. Persist output to `evidence/qa-gates/final-pester.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that records numeric post-change repo-wide line coverage AND the targeted coverage for `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1`. Assert: repo-wide `>= 80%`, targeted new-code `>= 90%`, zero test failures, zero test regressions vs baseline.
- [x] [P5-T4] Emit a coverage-delta artifact `evidence/qa-gates/coverage-delta.2026-04-18T00-00.md` that records (a) baseline repo-wide coverage (from `evidence/baseline/baseline-pester.2026-04-18T00-00.md`), (b) post-change repo-wide coverage (from P5-T3), (c) new-code coverage for each of the three new production files, and (d) an explicit assertion `post-change >= baseline - 0` (no regression). Include `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P5-T5] Verify end-state of new files: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1` all present. Verify preservation of retained files: `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` present and unchanged (use `git diff --stat HEAD -- scripts/install-mailbridge.ps1 scripts/uninstall-mailbridge.ps1` to confirm zero changed lines). Persist results to `evidence/qa-gates/end-state-file-presence.2026-04-18T00-00.md` with all four schema fields.
- [x] [P5-T6] Verify line-count policy end-state: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1` each `<= 500` lines; `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, and `tests/scripts/Install.Helpers.Tests.ps1` each `<= 500` lines. Persist counts and pass/fail to `evidence/qa-gates/end-state-line-counts.2026-04-18T00-00.md` with all four schema fields.
- [x] [P5-T7] Record a workflow-trigger-parity-style artifact `evidence/other/workflow-trigger-parity.2026-04-18T00-00.md` confirming that no existing workflow under `.github/workflows/` is modified by this feature (the feature does not touch CI). Fields: `Timestamp:`, `Command:` (the `git diff` invocation used), `EXIT_CODE:`, `Output Summary:` stating zero workflow file changes.
- [x] [P5-T8] Reconcile the spec's Definition of Done checklist against the artifacts produced across Phases 0-5. Write `evidence/qa-gates/definition-of-done-reconciliation.2026-04-18T00-00.md` with one row per DoD item, its status (PASS/FAIL), and the evidence artifact path that proves it. Include an additional table covering the spec's "Seeded Test Conditions (from potential)" list. Acceptance: all DoD items marked PASS with a cited artifact.

---

## Preflight Checklist

- **All acceptance criteria map to plan tasks.** Verified: spec.md DoD (22 items including signed-install happy path, `-SkipDocker`, `-AllowUnsigned`, `-Force`, manifest mismatch, Docker-not-running, `.env` guard, uninstall ordering and failure collection, user config preservation, coverage thresholds, PoshQC format/analyze/test, README + runbook updates) plus the 11 items in user-story.md AC are all covered by Phase 0 reads (P0-T6, P0-T7), Phase 1 helpers (P1-T2 through P1-T46), Phase 2 install stages (P2-T4 through P2-T13), Phase 3 uninstall stages (P3-T3 through P3-T9), Phase 4 documentation (P4-T1, P4-T2), and Phase 5 final QA gate (P5-T1 through P5-T8).
- **All three open questions resolved.** Q1 health-poll defaults (90s timeout, 3s interval) recorded in Planner Decisions section and enforced at P1-T32 + P2-T12. Q2 administrator precheck on `-AllowUnsigned` recorded with exact remediation message and enforced at P2-T4 + P2-T17. Q3 runbook Path D placement recorded and enforced at P4-T2 + P4-T6.
- **Per-batch budget compliance.** Phase 1 is split into five batches; each batch contains at most three new production-function tasks (P1-T2/T3 single-function tasks, P1-T11/T12/T13, P1-T21/T22/T23, P1-T31/T32/T33, P1-T41/T42) and at most three new test `Describe` tasks (P1-T6/T7, P1-T15/T16/T17, P1-T25/T26/T27, P1-T35/T36/T37, P1-T45/T46). No batch exceeds the 3-production + 3-test ceiling.
- **Line-count projection.** `scripts/Install.Helpers.psm1` projected at approximately 350-420 lines (13 helpers averaging 25-30 lines each plus header + exports); bounded under 500 by P1-T50. `scripts/Install.ps1` projected at approximately 180-230 lines; bounded under 500 by P2-T27. `scripts/Uninstall.ps1` projected at approximately 90-130 lines; bounded under 500 by P3-T21. Test files (`Install.Helpers.Tests.ps1`, `Install.Tests.ps1`, `Uninstall.Tests.ps1`) projected at approximately 400, 280, and 220 lines respectively; bounded under 500 by P5-T6.
- **Evidence schema and locations.** Every command-bearing task in Phase 0, Phase 4, and Phase 5 specifies `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` fields and writes to canonical `evidence/baseline/`, `evidence/other/`, or `evidence/qa-gates/` paths per `evidence-and-timestamp-conventions`.
- **Atomicity.** Each task describes a single binary outcome (single function addition, single `Describe`/`Context` block, single command run, single deletion or edit); no bucket or umbrella tasks detected.
- **Final QA loop structure.** Phase 5 runs format -> analyze -> test in order with an explicit restart-on-change directive at P5-T1 and no `SKIPPED` branches on any command task.

DIRECTIVE: PREFLIGHT VALIDATION ONLY

PREFLIGHT: ALL CLEAR
