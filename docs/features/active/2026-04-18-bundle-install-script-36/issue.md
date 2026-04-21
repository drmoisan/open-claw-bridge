# bundle-install-script (Issue #36)

- Date captured: 2026-04-18
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-18-bundle-install-script-36/ (Issue #36)

- Issue: #36
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/36
- Last Updated: 2026-04-18
- Work Mode: full-feature

## Problem / Why

Feature #34 introduced `scripts/Publish.ps1`, which emits a versioned local bundle at `artifacts/publish/<version>/` containing `executables/`, `docker/`, `msix/`, and a top-level `manifest.json`. There is no scripted installer that consumes that bundle to stand up the complete OpenClaw solution on a target host. An operator receiving a publish bundle today must:

1. Manually copy the bundle contents to a chosen install location.
2. Install the MSIX by double-clicking it or running `Add-AppxPackage` by hand.
3. Copy the docker artifacts to a working directory, create `.env` from `.env.example`, provision the HostAdapter token, and run `docker compose up -d` manually.
4. Stop containers and remove the package manually when rolling back.

This hand-assembly is error-prone, undocumented in a single place, and makes install outcomes non-deterministic. A unified install script (plus a matching uninstall) closes the loop between `Publish.ps1` and a running deployment on the target host.

## Proposed Behavior

Introduce `scripts/Install.ps1` and `scripts/Uninstall.ps1` plus shared helpers in `scripts/Install.Helpers.psm1`. This mirrors the `Publish.ps1` + `Publish.Helpers.psm1` pattern established by feature #34 and keeps each file within the 500-line policy.

The install script:

1. Auto-detects the newest bundle under `artifacts/publish/` (highest parseable `[System.Version]` subdirectory) unless `-SourcePath` overrides.
2. Validates `manifest.json` integrity against every file in the bundle (path, size, SHA-256) before any side effects.
3. Creates `%LOCALAPPDATA%\OpenClaw\<version>\` as the install destination, unpacks `executables/` and `docker/` into subdirectories, and preserves relative paths.
4. Installs the MSIX from `msix/` via `Add-AppxPackage`. Supports `-AllowUnsigned` for dev bundles produced with `Publish.ps1 -SkipSign`.
5. Runs `docker compose --project-name openclaw --project-directory <dest-docker-dir> -f <dest-docker-dir>\docker-compose.yml up -d openclaw-core openclaw-agent` against the unpacked `docker/` directory, and polls `docker compose ps --format json` until `openclaw-core` and `openclaw-agent` reach `running` / `healthy` within a bounded timeout.
6. Writes a single-record install record at `%LOCALAPPDATA%\OpenClaw\install-record.json` containing the version, destination path, timestamp, MSIX `PackageFullName`, compose project name, compose file path, and the `skipDocker` / `allowUnsigned` flags for later uninstall.

The uninstall script reads the install record, runs `docker compose down` against the recorded compose file, removes the MSIX package by `PackageFullName`, and removes the destination folder. It leaves user config (`%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`) in place to match existing MSIX uninstall behavior. All uninstall steps run regardless of individual failures; failures are collected and reported as a single terminating error.

`scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain in the repo unchanged. They serve the scheduled-task install path (Path A in the runbook), which is distinct from this bundle install path.

## Acceptance Criteria

- [ ] `scripts/Install.ps1` accepts `-SourcePath` (optional), `-Version` (optional), `-AllowUnsigned` (switch), `-SkipDocker` (switch), and `-Force` (switch) with sensible defaults.
- [ ] When no `-SourcePath` is supplied, the script selects the highest-version subdirectory under `artifacts/publish/` using `[System.Version]` ordering and fails clearly if no parseable version directory exists.
- [ ] The script validates every file in the bundle against `manifest.json` (path, size, SHA-256) before making any modifications to the host. Validation failure aborts before the destination folder is created.
- [ ] The destination folder `%LOCALAPPDATA%\OpenClaw\<version>\` is created fresh; an existing destination for the same version is refused unless `-Force` is supplied.
- [ ] `executables/` and `docker/` are unpacked to `%LOCALAPPDATA%\OpenClaw\<version>\executables\` and `%LOCALAPPDATA%\OpenClaw\<version>\docker\` preserving relative paths.
- [ ] The MSIX at `msix/OpenClaw.MailBridge_<version>_x64.msix` is installed via `Add-AppxPackage`. Signed bundles install by default; `-AllowUnsigned` is required for bundles produced with `Publish.ps1 -SkipSign`.
- [ ] `docker compose up -d openclaw-core openclaw-agent` runs against the unpacked `docker/` directory unless `-SkipDocker` is supplied, using explicit `--project-name openclaw`, `--project-directory <dest-docker-dir>`, and `-f docker-compose.yml` flags. The script verifies that both services reach a `running` / `healthy` state within a bounded timeout before reporting success.
- [ ] The script requires Docker Desktop to be running when the docker stage is enabled (detected via `docker info` exit code) and produces a remediation message when it is not.
- [ ] `.env.example` at the destination `docker/` directory is copied to `.env` only when `.env` is absent. An existing `.env` is never overwritten.
- [ ] An install record is written to `%LOCALAPPDATA%\OpenClaw\install-record.json` on success, capturing `installedAt`, `version`, `sourcePath`, `destinationPath`, `packageFullName`, `composeProjectName`, `composeFilePath`, `skipDocker`, and `allowUnsigned`. The record is a single-record JSON file overwritten on each successful install.
- [ ] `scripts/Uninstall.ps1` reads the install record and runs, in order: `docker compose down` against the recorded compose file (skipped when `skipDocker` is true), `Remove-AppxPackage -Package <PackageFullName>`, `Remove-Item` on the destination folder, and deletion of `install-record.json`. User config under `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is preserved. All steps run regardless of individual failures; failures are collected and reported as a single terminating error.
- [ ] Installing over an existing install with `-Force` performs a complete uninstall of the prior version (via the same sequence used by `Uninstall.ps1`) before installing the new one.
- [ ] PoshQC suite (format -> analyze -> test) passes on `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, and their Pester test files. Coverage >= 90% on new lines; repo-wide coverage remains >= 80%.
- [ ] Documentation updated: `README.md` and `docs/mailbridge-runbook.md` describe the new install and uninstall flow without displacing the scheduled-task install path.

## Constraints & Risks

- **Windows-only**: MSIX installation via `Add-AppxPackage` requires Windows. Docker Desktop with `host.docker.internal` networking is required for the docker stage.
- **No elevation by default**: `%LOCALAPPDATA%\OpenClaw\` is per-user and does not require admin elevation. Installing the MSIX into the current-user context via `Add-AppxPackage` likewise avoids elevation in most configurations, but `-AllowUnsigned` with a package that contains executable content requires PowerShell to run as administrator per Microsoft Learn guidance. The script surfaces this precondition clearly rather than silently degrading.
- **Unsigned bundles**: Bundles produced with `Publish.ps1 -SkipSign` cannot be installed without `-AllowUnsigned`. The package manifest must carry the `OID.2.25.311729368913984317654407730594956997722=1` OID in its `Publisher` attribute (produced by the publish pipeline) for `-AllowUnsigned` to succeed.
- **Docker precondition**: If Docker Desktop is not installed or not running, the docker stage cannot proceed. The script detects this via `docker info` exit code and fails with a specific remediation message.
- **Environment file**: `.env.example` is shipped in the bundle docker directory. The install script copies it to `.env` only when `.env` is absent at the destination. It never overwrites an existing `.env` (which may contain secrets on re-install).
- **HostAdapter token**: The agent container requires a HostAdapter token file and the `openclaw-agent` service references `env_file: ./secrets/.env.anthropic`, which is excluded from the bundle. This install script does not provision the token or secrets file automatically; the operator must follow the existing runbook step. This limitation is documented.
- **500-line-per-file policy**: The module split (`Install.ps1` + `Uninstall.ps1` + `Install.Helpers.psm1`) mirrors the `Publish.ps1` + `Publish.Helpers.psm1` pattern and keeps each file within policy.
- **No auto-rollback across MSIX + Docker**: If the docker stage fails after the MSIX has been installed, the script leaves the MSIX in place and emits guidance to run `Uninstall.ps1` or retry after fixing Docker. Auto-rollback across MSIX and Docker boundaries is out of scope.
- **Scheduled-task path preserved**: `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain unchanged and continue to serve the published-binaries + scheduled-task install path.

## Test Conditions to Consider

- [ ] Running `.\scripts\Install.ps1` with no arguments on a clean host that has a signed bundle under `artifacts/publish/` installs the MSIX and brings the docker stack up without further input.
- [ ] Running `.\scripts\Install.ps1 -SkipDocker` installs the MSIX only and writes an install record with `skipDocker = true`; `Uninstall.ps1` reads the flag and skips `docker compose down`.
- [ ] Running `.\scripts\Install.ps1 -AllowUnsigned` with a `-SkipSign` bundle installs the MSIX in developer mode (or with the certificate installed to `Cert:\LocalMachine\TrustedPeople`).
- [ ] `manifest.json` hash mismatch aborts the install before any destination folder is created.
- [ ] Running `.\scripts\Install.ps1` when `artifacts/publish/` contains no parseable version directory fails fast with a clear error.
- [ ] Running `.\scripts\Install.ps1` when Docker Desktop is not running and `-SkipDocker` is not supplied fails fast with a remediation message.
- [ ] `.\scripts\Uninstall.ps1` on a healthy install removes the MSIX, stops the compose stack, removes `%LOCALAPPDATA%\OpenClaw\<version>\`, and deletes `install-record.json` while preserving user config under `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- [ ] Running `.\scripts\Install.ps1 -Force` over an existing install performs an implicit uninstall of the prior version before unpacking the new one.
- [ ] An existing `.env` at the destination `docker/` directory is not overwritten by a re-install.
- [ ] Pester coverage on new lines >= 90%, repo-wide line coverage >= 80%.
- [ ] PoshQC format and analyze produce zero diagnostics on all new PowerShell files.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create `docs/features/active/2026-04-18-bundle-install-script-36/` folder
- [ ] Invoke task-planner to produce an atomic plan consuming this issue, the spec, and the user story.
