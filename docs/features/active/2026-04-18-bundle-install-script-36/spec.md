# 2026-04-18-bundle-install-script — Spec

- **Issue:** #36
- **Parent (optional):** #34 (unified-publish-script)
- **Owner:** drmoisan
- **Last Updated:** 2026-04-18T00:00:00Z
- **Status:** Draft
- **Version:** 0.1

## Overview

This feature introduces a scripted installer and uninstaller that consumes the versioned local bundle produced by `scripts/Publish.ps1` (feature #34). The installer unpacks a selected bundle under `%LOCALAPPDATA%\OpenClaw\<version>\`, installs the MSIX via `Add-AppxPackage`, starts the Docker compose stack (`openclaw-core` and `openclaw-agent`), and writes a single-record install manifest for later rollback. The uninstaller reverses those steps using the recorded manifest.

The feature delivers three new PowerShell files under `scripts/`:

- `scripts/Install.ps1` — thin orchestrator for the install flow.
- `scripts/Uninstall.ps1` — thin orchestrator for the uninstall flow.
- `scripts/Install.Helpers.psm1` — shared helper module for bundle selection, manifest integrity verification, MSIX installation, compose invocation, install-record I/O, and the `.env` guard.

The file split mirrors the `Publish.ps1` + `Publish.Helpers.psm1` pattern established by feature #34 and keeps each file under the 500-line policy. `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain in the repo unchanged and continue to serve the scheduled-task install path (runbook Path A), which is distinct from this bundle install path.

This iteration is strictly local install and uninstall from a bundle already present on disk. Remote bundle download, release-server integration, and multi-version install history are explicitly out of scope.

## Behavior

### Main path (install)

1. Operator runs `.\scripts\Install.ps1` on a Windows host with Docker Desktop running. Optional parameters select a non-default bundle (`-SourcePath`, `-Version`), opt into unsigned installs (`-AllowUnsigned`), skip the docker stage (`-SkipDocker`), or force a full reinstall (`-Force`).
2. The script resolves the bundle root. When `-SourcePath` is supplied, it is used verbatim. Otherwise the script enumerates `artifacts/publish/`, filters directory names parseable as `[System.Version]`, sorts descending, and selects the first entry. When `-Version` is supplied, the script resolves `artifacts/publish/<Version>/` directly.
3. The script verifies that `manifest.json` exists at the bundle root and that every entry in `manifest.json` matches the file on disk by relative path, byte size, and SHA-256. The script also verifies that no files under the bundle root (excluding `manifest.json`) are missing from the manifest. Any mismatch produces a single terminating error enumerating every discrepancy.
4. The script detects whether an install already exists for the selected version by checking the destination folder `%LOCALAPPDATA%\OpenClaw\<Version>\` and the install record at `%LOCALAPPDATA%\OpenClaw\install-record.json`. If either exists:
   - Without `-Force`: the script aborts with a remediation message.
   - With `-Force`: the script runs the full uninstall sequence (compose down, remove MSIX, remove destination folder, delete install record) against the prior install before proceeding.
5. When the docker stage is enabled (`-SkipDocker` not supplied), the script checks Docker Desktop readiness by running `docker info` and inspecting the exit code. A non-zero exit code aborts the install with a remediation message that instructs the operator to start Docker Desktop or pass `-SkipDocker`.
6. The script creates `%LOCALAPPDATA%\OpenClaw\<Version>\`, copies `executables/` to `%LOCALAPPDATA%\OpenClaw\<Version>\executables\`, and copies `docker/` to `%LOCALAPPDATA%\OpenClaw\<Version>\docker\`, preserving relative paths.
7. The script copies `.env.example` from the destination `docker/` directory to `.env` in the same directory only when `.env` is absent. An existing `.env` is never overwritten.
8. The script installs the MSIX at `<bundle>/msix/OpenClaw.MailBridge_<Version>_x64.msix` via `Add-AppxPackage`. When `-AllowUnsigned` is supplied, the `-AllowUnsigned` flag is passed to `Add-AppxPackage`. After a successful install, the script captures the `PackageFullName` via `Get-AppxPackage -Name 'OpenClaw.MailBridge'`.
9. When the docker stage is enabled, the script runs `docker compose --project-name openclaw --project-directory <dest-docker-dir> -f <dest-docker-dir>\docker-compose.yml up -d openclaw-core openclaw-agent` and then polls `docker compose --project-name openclaw -f <dest-docker-dir>\docker-compose.yml ps --format json` until both services report `State == "running"` and `Health == "healthy"` (or `Health` is empty when no healthcheck is defined), within a bounded timeout. Failure to reach a healthy state within the timeout aborts the install with a message that names the unhealthy service.
10. The script writes `%LOCALAPPDATA%\OpenClaw\install-record.json` containing the schema listed in Data & State and exits 0.

### Main path (uninstall)

1. Operator runs `.\scripts\Uninstall.ps1` on the host where the bundle was previously installed.
2. The script reads `%LOCALAPPDATA%\OpenClaw\install-record.json`. If the file is absent, the script aborts with a clear message.
3. The script runs the uninstall sequence in order:
   a. When `skipDocker` is `false` in the record: `docker compose --project-name <composeProjectName> -f <composeFilePath> down`.
   b. `Remove-AppxPackage -Package <packageFullName>`. If `Get-AppxPackage -Name 'OpenClaw.MailBridge'` returns nothing, this step is skipped silently.
   c. `Remove-Item -Recurse -Force` on `<destinationPath>`.
   d. `Remove-Item` on the install record file.
4. Every step runs regardless of individual step failures. Each failure is collected. If any step failed, after all steps complete the script throws a single terminating error summarizing which steps failed and what corrective action the operator should take. User configuration under `%LOCALAPPDATA%\OpenClaw\MailBridge\` is not affected because it lives under a sibling directory.

### Negative / edge paths

1. **Empty publish root**: `artifacts/publish/` contains no directory whose name parses as `[System.Version]`. The script aborts before any side effects with a message identifying the publish root searched.
2. **`-Version` points to missing bundle**: The supplied `-Version` does not correspond to a directory under `artifacts/publish/`. The script aborts with the resolved path it attempted.
3. **`-SourcePath` missing `manifest.json`**: The supplied `-SourcePath` exists but does not contain `manifest.json`. The script aborts with the path searched.
4. **Manifest mismatch**: Any entry in `manifest.json` does not match the on-disk file (missing, wrong size, wrong hash), or any on-disk file under the bundle root is absent from the manifest. The script accumulates all discrepancies and throws a single terminating error listing them. No destination folder is created.
5. **MSIX missing**: The bundle does not contain `msix/OpenClaw.MailBridge_<Version>_x64.msix`. The script aborts with the expected path.
6. **Unsigned MSIX without `-AllowUnsigned`**: `Add-AppxPackage` fails with a trust-validation error. The script surfaces the raw error and suggests passing `-AllowUnsigned` or installing the signing certificate to `Cert:\LocalMachine\TrustedPeople`.
7. **Same-version reinstall without `-Force`**: The destination folder already exists, or `install-record.json` exists for the same version. The script aborts with guidance to pass `-Force` or run `Uninstall.ps1` first.
8. **Docker Desktop not running**: `docker info` exits non-zero. The script aborts with a remediation message that instructs the operator to start Docker Desktop or pass `-SkipDocker`.
9. **Compose start failure**: `docker compose up -d` exits non-zero, or one of the two services fails to reach a healthy state within the bounded timeout. The script leaves the MSIX in place, writes no install record, and throws with the failing service name and the last observed `State` / `Health` values. The operator is instructed to run `Uninstall.ps1` or re-run with a working Docker environment.
10. **`.env` already exists at destination**: The existing `.env` is not overwritten and no warning is emitted; the copy step is a silent no-op.
11. **Uninstall with no record**: `install-record.json` is absent. The script aborts with a message indicating no prior install is known.
12. **Uninstall with partial state**: One or more of the recorded artifacts (compose project, MSIX package, destination folder) is already gone. The missing-target cases are treated as success for that step; only genuine failures are collected and reported.
13. **`-AllowUnsigned` on a host without administrator privileges and with a package containing executable content**: `Add-AppxPackage` fails. The script surfaces the raw error and documents the administrator-privilege requirement per Microsoft Learn guidance.

## Inputs / Outputs

### `scripts/Install.ps1` inputs

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-SourcePath` | `string` | `''` | Absolute or relative path to a specific bundle root. When present, overrides auto-detection. |
| `-Version` | `string` | `''` | 4-part version string. When present and `-SourcePath` is empty, selects `artifacts/publish/<Version>/`. |
| `-AllowUnsigned` | `switch` | `$false` | Passes `-AllowUnsigned` to `Add-AppxPackage`. Required for bundles produced with `Publish.ps1 -SkipSign`. |
| `-SkipDocker` | `switch` | `$false` | Skips the Docker readiness check, compose up, and health polling. The install record captures `skipDocker = true` so uninstall mirrors the skip. |
| `-Force` | `switch` | `$false` | Performs a full uninstall-then-install against any prior install for the same version. |

### `scripts/Uninstall.ps1` inputs

| Parameter | Type | Default | Description |
|---|---|---|---|
| _(none)_ | | | The uninstaller reads all state from `%LOCALAPPDATA%\OpenClaw\install-record.json`. No parameters are exposed in this iteration. |

### Destination layout

| Artifact | Path |
|---|---|
| Bundle destination root | `%LOCALAPPDATA%\OpenClaw\<Version>\` |
| Executables | `%LOCALAPPDATA%\OpenClaw\<Version>\executables\<ProjectName>\` |
| Docker artifacts | `%LOCALAPPDATA%\OpenClaw\<Version>\docker\` |
| Install record | `%LOCALAPPDATA%\OpenClaw\install-record.json` |
| Preserved user config | `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` |

### Install record schema

```json
{
  "installedAt": "2026-04-18T14:32:00Z",
  "version": "1.2.3.0",
  "sourcePath": "C:\\...\\artifacts\\publish\\1.2.3.0",
  "destinationPath": "C:\\Users\\<user>\\AppData\\Local\\OpenClaw\\1.2.3.0",
  "packageFullName": "OpenClaw.MailBridge_1.2.3.0_x64__abc1234",
  "composeProjectName": "openclaw",
  "composeFilePath": "C:\\Users\\<user>\\AppData\\Local\\OpenClaw\\1.2.3.0\\docker\\docker-compose.yml",
  "skipDocker": false,
  "allowUnsigned": false
}
```

- `installedAt` is an ISO-8601 UTC timestamp captured at the moment the record is written (after all install stages succeed).
- `version` matches the installed bundle's version (the `<Version>` segment of the destination path).
- `sourcePath` is the absolute path of the bundle root that was installed.
- `destinationPath` is the absolute path of the per-version destination folder.
- `packageFullName` is the string returned by `(Get-AppxPackage -Name 'OpenClaw.MailBridge').PackageFullName` after `Add-AppxPackage`.
- `composeProjectName` is the literal `openclaw`.
- `composeFilePath` is the absolute path of `docker-compose.yml` under the destination `docker/` directory.
- `skipDocker` and `allowUnsigned` capture the switches that were active during install so uninstall mirrors them.

## API / CLI Surface

### Script invocations

```powershell
# Install the newest bundle under artifacts/publish/ (signed MSIX, docker stage included).
.\scripts\Install.ps1

# Install a specific bundle directory explicitly.
.\scripts\Install.ps1 -SourcePath 'C:\releases\openclaw\1.2.3.0'

# Install a specific version from the default publish root.
.\scripts\Install.ps1 -Version '1.2.3.0'

# Install an unsigned dev bundle produced with Publish.ps1 -SkipSign.
.\scripts\Install.ps1 -AllowUnsigned

# Install without the docker stage.
.\scripts\Install.ps1 -SkipDocker

# Force reinstall over an existing install of the same version.
.\scripts\Install.ps1 -Force

# Uninstall the currently recorded install.
.\scripts\Uninstall.ps1
```

### Exported helper module

`scripts/Install.Helpers.psm1` exports the following functions for test and reuse:

| Function | Purpose |
|---|---|
| `Find-NewestPublishVersion` | Enumerate subdirectories of the publish root, filter those parseable as `[System.Version]`, return the highest. Pure; throws on empty. |
| `Test-ManifestIntegrity` | Read `manifest.json` from a bundle root, verify every entry against the file on disk by relative path, size, and SHA-256, and verify every on-disk file (excluding `manifest.json`) is listed. Throws a single terminating error enumerating all discrepancies. |
| `Copy-BundleContents` | Copy `executables/` and `docker/` subtrees from a bundle root to a destination root, preserving relative paths. |
| `Initialize-DotEnv` | Copy `.env.example` to `.env` under a destination docker directory only when `.env` is absent. |
| `Invoke-MsixInstall` | Wrap `Add-AppxPackage -Path <msix-path>` with optional `-AllowUnsigned`. |
| `Invoke-MsixCapture` | Wrap `Get-AppxPackage -Name 'OpenClaw.MailBridge'` and return the `PackageFullName`. |
| `Invoke-MsixRemove` | Wrap `Remove-AppxPackage -Package <PackageFullName>`. Silent no-op when the package is not installed. |
| `Test-DockerAvailable` | Wrap `docker info` and return `$true` on exit code 0, throw with remediation guidance otherwise. |
| `Invoke-ComposeUp` | Wrap `docker compose --project-name openclaw --project-directory <dest-docker-dir> -f <compose-file> up -d openclaw-core openclaw-agent`. |
| `Wait-ComposeHealthy` | Poll `docker compose ps --format json` until both services report running and healthy (or healthcheck absent) within a bounded timeout. |
| `Invoke-ComposeDown` | Wrap `docker compose --project-name <name> -f <compose-file> down`. |
| `Write-InstallRecord` | Serialize the install-record object to JSON and write it to `%LOCALAPPDATA%\OpenClaw\install-record.json`. |
| `Read-InstallRecord` | Read and parse the install record; throw when absent. |

### Interaction with retained scripts

The scheduled-task install scripts (`scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`) remain unchanged. They serve runbook Path A (published binaries + scheduled task) and are not invoked by the new scripts.

## Data & State

### Data flow (install)

1. The script selects the bundle root and verifies manifest integrity (read-only).
2. The script creates `%LOCALAPPDATA%\OpenClaw\<Version>\` and copies the `executables/` and `docker/` subtrees into it.
3. The script copies `.env.example` to `.env` under the destination `docker/` directory when `.env` is absent.
4. The script invokes `Add-AppxPackage` against the bundle's MSIX, then captures `PackageFullName` via `Get-AppxPackage -Name 'OpenClaw.MailBridge'`.
5. The script invokes `docker compose up -d` against the destination `docker/` directory and polls `docker compose ps --format json` until services are healthy.
6. The script writes the install record to `%LOCALAPPDATA%\OpenClaw\install-record.json`.

### Data flow (uninstall)

1. The script reads `%LOCALAPPDATA%\OpenClaw\install-record.json`.
2. The script runs `docker compose down` (skipped when `skipDocker` is true), `Remove-AppxPackage -Package <PackageFullName>`, and `Remove-Item -Recurse -Force <destinationPath>` in order, collecting failures.
3. The script removes the install record file.
4. If any step failed, the script throws a single terminating error listing the failures.

### Persistence

- `%LOCALAPPDATA%\OpenClaw\<Version>\` persists until uninstall or manual deletion.
- `%LOCALAPPDATA%\OpenClaw\install-record.json` persists until uninstall; single-record JSON overwritten on each successful install.
- `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is never touched by either script.
- `%LOCALAPPDATA%\OpenClaw\<Version>\docker\.env` is written once per destination (when absent) and never overwritten.

### Migration / backfill

None. Operators who previously performed the install steps manually may continue to do so. The new scripts add a scripted path without replacing the scheduled-task path.

## Constraints & Risks

- **500-line-per-file policy**: The module split (`Install.ps1` + `Uninstall.ps1` + `Install.Helpers.psm1`) keeps each file within policy and mirrors feature #34's pattern.
- **Windows-only**: `Add-AppxPackage`, `Get-AppxPackage`, and `Remove-AppxPackage` require Windows. Docker Desktop is required for the docker stage.
- **Docker precondition**: The docker stage requires Docker Desktop to be running. The script detects readiness via `docker info` exit code and fails fast with a remediation message when the daemon is unavailable.
- **Unsigned MSIX requirements**: `-AllowUnsigned` requires the package manifest to include the `OID.2.25.311729368913984317654407730594956997722=1` OID in its `Publisher` attribute. Packages containing executable content require PowerShell to run as administrator per Microsoft Learn guidance. The script surfaces this requirement in error messages rather than silently degrading.
- **`env_file` reference not provisioned**: `docker-compose.yml` references `env_file: ./secrets/.env.anthropic`, which is excluded from the bundle. The operator must provision the secrets file out-of-band. This is documented in the runbook and is not automated by this feature.
- **HostAdapter token not provisioned**: The agent container requires a HostAdapter token. This install script does not provision it. This is documented in the runbook and acknowledged as out-of-scope.
- **MSIX identity constraint on same-version reinstall**: Windows blocks `Add-AppxPackage` with error `0x80073CFB` when the installed package has the same identity and version but different contents. The script avoids this by requiring `-Force` for same-version reinstalls and performing a full uninstall before reinstall rather than an in-place overwrite.
- **No auto-rollback across MSIX and Docker**: If the docker stage fails after the MSIX is installed, the script does not auto-rollback. The MSIX remains installed and the operator runs `Uninstall.ps1` or retries after fixing Docker.
- **Docker volume preservation on uninstall**: `docker compose down` is invoked without `--volumes`. The `openclaw_data` volume is preserved on uninstall. Volume removal is a manual step if the operator requires it.

## Implementation Strategy

### Scope — new files

| File | Purpose |
|---|---|
| `scripts/Install.ps1` | Thin orchestrator. Parameter declaration, stage sequencing, progress output via `Write-Information` with stage prefixes (`[install:select]`, `[install:verify]`, `[install:copy]`, `[install:msix]`, `[install:docker]`, `[install:record]`). |
| `scripts/Uninstall.ps1` | Thin orchestrator. Reads the install record and invokes the uninstall helpers in order, collecting failures. |
| `scripts/Install.Helpers.psm1` | Pure and near-pure helper functions listed in API / CLI Surface. |
| `tests/scripts/Install.Helpers.Tests.ps1` | Pester v5 tests for helper functions via `Import-Module`. |
| `tests/scripts/Install.Tests.ps1` | Pester v5 tests for `Install.ps1` stage ordering and parameter binding via dot-source with full mock injection. |
| `tests/scripts/Uninstall.Tests.ps1` | Pester v5 tests for `Uninstall.ps1` stage ordering, failure collection, and missing-record behavior. |

### Scope — modified files

| File | Change |
|---|---|
| `README.md` | Document the bundle install path (`.\scripts\Install.ps1`) alongside the existing scheduled-task path. Reference the new scripts in the "Repository Layout" `scripts/` description. |
| `docs/mailbridge-runbook.md` | Document the bundle install path (Path B or a new section) with the new scripts. Add prerequisites (Docker Desktop running for the docker stage). Add troubleshooting entries for manifest integrity failure, Docker not running, and missing install record. Preserve Path A (scheduled-task) content. |

### Scope — deleted files

None. `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` are retained.

### Dependency changes

None. The scripts use only built-in PowerShell 7+ modules (`Microsoft.PowerShell.Utility` for `Get-FileHash`, `ConvertTo-Json`, `ConvertFrom-Json`), the Windows Appx module (`Add-AppxPackage`, `Get-AppxPackage`, `Remove-AppxPackage`), and the Docker CLI (`docker`, `docker compose`).

### Logging / telemetry

Progress is written via `Write-Information` (stream 6) with stage-prefixed messages. Errors are raised via `throw` or `Write-Error`. No telemetry is emitted.

### Test seams

Helpers are designed to be individually mockable:

| Helper | Mock target |
|---|---|
| `Find-NewestPublishVersion` | `Mock Get-ChildItem` |
| `Test-ManifestIntegrity` | `Mock Get-FileHash`, `Mock Get-Item` |
| `Copy-BundleContents` | `Mock New-Item`, `Mock Copy-Item` |
| `Initialize-DotEnv` | `Mock Test-Path`, `Mock Copy-Item` |
| `Invoke-MsixInstall` | `Mock Add-AppxPackage` |
| `Invoke-MsixCapture` | `Mock Get-AppxPackage` |
| `Invoke-MsixRemove` | `Mock Remove-AppxPackage`, `Mock Get-AppxPackage` |
| `Test-DockerAvailable` | `function global:docker` shim |
| `Invoke-ComposeUp` | `function global:docker` shim |
| `Wait-ComposeHealthy` | `function global:docker` shim |
| `Invoke-ComposeDown` | `function global:docker` shim |
| `Write-InstallRecord` | `Mock Set-Content` |
| `Read-InstallRecord` | `Mock Get-Content`, `Mock Test-Path` |

The orchestrators are tested via stage-ordering assertions: all helpers are mocked with a call-log, the orchestrator is invoked, and the call sequence is asserted. This mirrors the `Publish.Tests.ps1` pattern established by feature #34.

### Rollout

All new files land in a single feature branch (`feature/bundle-install-script-36`) with accompanying documentation updates. No transitional window is required because the scheduled-task path remains untouched.

## Definition of Done

- [x] `scripts/Install.ps1` exists and accepts `-SourcePath`, `-Version`, `-AllowUnsigned`, `-SkipDocker`, `-Force`.
- [x] `scripts/Uninstall.ps1` exists and consumes `install-record.json` with no parameters.
- [x] `scripts/Install.Helpers.psm1` exists and exports the functions listed in the API / CLI Surface section.
- [x] Running `.\scripts\Install.ps1` on a clean host with a signed bundle under `artifacts/publish/` installs the MSIX, starts the docker stack, and writes `install-record.json`.
- [x] Running `.\scripts\Uninstall.ps1` reverses the install and deletes `install-record.json`, while preserving `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- [x] `-Force` performs a complete uninstall of any prior install of the same version before installing.
- [x] Manifest-integrity failure aborts before any destination folder is created, with a terminating error listing all discrepancies.
- [x] Docker-not-running with the docker stage enabled fails fast with a remediation message.
- [x] `.env` is never overwritten; `.env.example` is copied to `.env` only when `.env` is absent.
- [x] Pester coverage >= 90% on new lines in `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1`. Repo-wide line coverage remains >= 80%.
- [x] PoshQC suite (format -> analyze -> test) passes on all new and modified PowerShell files.
- [x] `README.md` and `docs/mailbridge-runbook.md` document the new install and uninstall flow without removing the scheduled-task path content.

## Seeded Test Conditions (from potential)

- [x] End-to-end `.\scripts\Install.ps1` against a clean host with a signed bundle under `artifacts/publish/` installs the MSIX and brings the docker stack up.
- [x] `.\scripts\Install.ps1 -SkipDocker` installs the MSIX only and records `skipDocker = true`; `Uninstall.ps1` skips the compose-down step.
- [x] `.\scripts\Install.ps1 -AllowUnsigned` installs a bundle produced with `Publish.ps1 -SkipSign`.
- [x] `manifest.json` hash mismatch aborts install before any destination folder is created.
- [x] `artifacts/publish/` empty of parseable version directories fails fast with a clear error.
- [x] Docker Desktop not running with the docker stage enabled fails fast with a remediation message.
- [x] `.\scripts\Uninstall.ps1` on a healthy install removes MSIX, stops compose, removes destination folder, deletes install record, and preserves `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- [x] `.\scripts\Install.ps1 -Force` performs an implicit uninstall before reinstall.
- [x] Existing `.env` at destination is not overwritten on re-install.
- [x] Pester coverage on new lines >= 90%, repo-wide line coverage >= 80%.
- [x] PoshQC format and analyze produce zero diagnostics on new PowerShell files.

## Non-Goals

- **Remote bundle download**: The installer consumes a local bundle. It does not fetch from a release server, GitHub Release, or cloud storage.
- **Multi-version install history**: The install record is single-record and overwritten on each install. A history file is not produced.
- **Auto-rollback across MSIX and Docker**: If the docker stage fails after the MSIX is installed, the script does not undo the MSIX install automatically.
- **HostAdapter token provisioning**: The script does not provision the HostAdapter token file. Operators follow the runbook.
- **Secrets provisioning**: The script does not provision `secrets/.env.anthropic`. Operators provision it out-of-band.
- **Cross-platform installers**: macOS and Linux installers are not in scope.
- **Replacement of the scheduled-task install path**: `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` are retained unchanged.
- **Docker volume removal on uninstall**: `docker compose down` is invoked without `--volumes`. Volume cleanup is a manual step.
- **In-place overwrite reinstall**: `-Force` performs uninstall-then-install, not in-place overwrite.

## Owner Decisions (resolved)

These items were raised during research or scoping and resolved by the feature owner. They are binding inputs for the planner.

- **Newest-bundle detection**: Auto-detect the newest `artifacts/publish/<version>/` by `[System.Version]` ordering. `-SourcePath` overrides.
- **Destination**: `%LOCALAPPDATA%\OpenClaw\<version>\`.
- **Compose invocation**: Copy `docker/` subtree to destination, then run `docker compose --project-name openclaw --project-directory <dest-docker-dir> -f docker-compose.yml up -d openclaw-core openclaw-agent`; verify both services reach running / healthy within a bounded timeout.
- **Install record**: Single-record JSON at `%LOCALAPPDATA%\OpenClaw\install-record.json`, overwritten per install, with the schema listed in Data & State.
- **`-Force` semantics**: Full uninstall-then-install (not in-place overwrite).
- **`.env` guard**: Copy `.env.example` to `.env` only when `.env` is absent.
- **Uninstall order**: `docker compose down` → `Remove-AppxPackage` → `Remove-Item` destination → delete `install-record.json`. All steps run regardless of individual failures; failures are collected and reported as a single terminating error.
- **File split**: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`.
- **Language and platform**: PowerShell 7+, Windows-only. Docker Desktop running is a precondition for the docker stage.
- **Scheduled-task scripts**: `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain in the repo unchanged.

## Open Questions (for planner)

The following items are delegated to the planner's discretion per research. The planner must record its choice and rationale in `plan.<timestamp>.md`.

1. **Compose health-poll timeout and interval**: Research recommends a bounded loop of up to 60 seconds polling every 5 seconds. The planner may retain that baseline or tune it based on observed `start_period` values in `docker-compose.yml` (`openclaw-core` uses `start_period: 20s`, `openclaw-agent` uses `start_period: 30s`). The chosen values must be documented in the script header and be observable to the operator via progress output.
2. **Administrator-privilege precheck for `-AllowUnsigned`**: Whether `Install.ps1` performs a proactive check that the current process is elevated before invoking `Add-AppxPackage -AllowUnsigned`, and the wording of the resulting failure message, is left to the planner. Microsoft Learn requires administrator privileges for `-AllowUnsigned` on packages containing executable content; the planner decides whether to precheck (fail fast) or rely on the `Add-AppxPackage` error surface.
3. **Runbook structure for the new path**: Whether the bundle install path is documented as a new "Path D" in the runbook, or replaces/supersedes one of the existing Path B / Path C sections. The scope decision mandates retention of Path A content; the specific section layout for the new path is a documentation-structure decision for the planner.
