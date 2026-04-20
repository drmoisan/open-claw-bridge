# bundle-install-script (Potential)

- Date captured: 2026-04-18
- Author: drmoisan
- Status: Draft

## Problem / Why

The unified publish script (feature #34) produces a versioned local bundle at `artifacts/publish/<version>/` containing `executables/`, `docker/`, `msix/`, and a `manifest.json`. However, there is no scripted installer that consumes that bundle to stand up the complete OpenClaw solution on a target host. An operator receiving a publish bundle must currently:

1. Manually copy the bundle contents to a chosen install location.
2. Install the MSIX by double-clicking it or running `Add-AppxPackage` by hand.
3. Copy the docker artifacts to a working directory, create `.env` from `.env.example`, provision the HostAdapter token, and run `docker compose up -d` manually.
4. Stop containers and remove the package manually when rolling back.

This hand-assembly is error-prone, undocumented in a single place, and makes install outcomes non-deterministic. A unified install script (plus matching uninstall) closes the loop between `Publish.ps1` and a running deployment on the target host.

## Proposed Behavior

Introduce `scripts/Install.ps1` and `scripts/Uninstall.ps1` (working names) plus shared helpers in `scripts/Install.Helpers.psm1`. The install script:

1. Auto-detects the newest bundle under `artifacts/publish/` (highest-version folder) unless `-SourcePath` overrides.
2. Validates `manifest.json` integrity against every file in the bundle before any side effects.
3. Creates `%LOCALAPPDATA%\OpenClaw\<version>\` as the install destination, unpacks `executables/` and `docker/` into subdirectories, and preserves relative paths.
4. Installs the MSIX from `msix/` via `Add-AppxPackage`. Supports `-AllowUnsigned` for dev bundles produced with `Publish.ps1 -SkipSign`.
5. Copies `docker/` to `%LOCALAPPDATA%\OpenClaw\<version>\docker\`, runs `docker compose -f docker-compose.yml up -d` for the `openclaw-core` and `openclaw-agent` services, and verifies container health.
6. Writes an install record at `%LOCALAPPDATA%\OpenClaw\install-record.json` containing the version, destination path, timestamp, MSIX package full name, and compose project name for later uninstall.

The uninstall script reads the install record, runs `docker compose down` against the recorded compose files, removes the MSIX package by `PackageFullName`, and removes the destination folder. It leaves user config (`%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`) in place to match existing MSIX uninstall behavior.

## Acceptance Criteria (early draft)

- [ ] `scripts/Install.ps1` accepts `-SourcePath` (optional), `-Version` (optional), `-AllowUnsigned` (switch), and `-SkipDocker` (switch) parameters. Defaults auto-detect the newest `artifacts/publish/<version>/`.
- [ ] When no `-SourcePath` is supplied, the script selects the highest-version folder under `artifacts/publish/` and fails clearly if none exist.
- [ ] The script validates every file in the bundle against `manifest.json` (path, size, SHA-256) before making any modifications to the host. Validation failure aborts before unpacking.
- [ ] The destination folder `%LOCALAPPDATA%\OpenClaw\<version>\` is created fresh; an existing destination for the same version is refused unless `-Force` is supplied.
- [ ] `executables/` and `docker/` are unpacked to `%LOCALAPPDATA%\OpenClaw\<version>\executables\` and `%LOCALAPPDATA%\OpenClaw\<version>\docker\` preserving relative paths.
- [ ] The MSIX at `msix/OpenClaw.MailBridge_<version>_x64.msix` is installed via `Add-AppxPackage`. Signed bundles install by default; `-AllowUnsigned` is required for bundles produced with `Publish.ps1 -SkipSign`.
- [ ] `docker compose -f docker-compose.yml up -d` runs against the unpacked `docker/` directory unless `-SkipDocker` is supplied. The script verifies that `openclaw-core` and `openclaw-agent` reach a running state before reporting success.
- [ ] The script requires Docker Desktop to be running when the docker stage is enabled and produces a clear error when it is not.
- [ ] An install record is written to `%LOCALAPPDATA%\OpenClaw\install-record.json` on success, capturing the version, destination, timestamp, MSIX `PackageFullName`, and compose project name.
- [ ] `scripts/Uninstall.ps1` reads the install record, runs `docker compose down` against the recorded compose files, removes the MSIX by `PackageFullName`, and removes `%LOCALAPPDATA%\OpenClaw\<version>\`. User config under `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is preserved.
- [ ] Installing over an existing install with `-Force` performs a complete uninstall of the prior version before installing the new one.
- [ ] PoshQC suite (format -> analyze -> test) passes on `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, and their Pester test files. Coverage >= 90% on new lines; repo-wide coverage remains >= 80%.
- [ ] Documentation updated: `README.md` and `docs/mailbridge-runbook.md` describe the new install and uninstall flow.

## Constraints & Risks

- **Windows-only**: MSIX installation via `Add-AppxPackage` requires Windows. Docker Desktop with `host.docker.internal` networking is required for the docker stage.
- **No elevation by default**: `%LOCALAPPDATA%\OpenClaw\` is per-user and does not require admin elevation. Installing the MSIX into the current-user context via `Add-AppxPackage` likewise avoids elevation in most configurations, but signature trust and developer-mode requirements may still surface.
- **Unsigned bundles**: Bundles produced with `Publish.ps1 -SkipSign` cannot be installed without `-AllowUnsigned`, which requires developer mode or a locally trusted signing certificate. The script must surface this clearly rather than silently degrading.
- **Docker dependency**: If Docker Desktop is not installed or not running, the docker stage cannot proceed. The script must detect this and fail with a specific remediation message rather than an opaque CLI error.
- **Environment file**: `.env.example` is shipped in the bundle. The install script must copy it to `.env` if no `.env` already exists at the destination, but it must NEVER overwrite an existing `.env` (may contain secrets on re-install).
- **HostAdapter token**: The agent container requires a HostAdapter token file. This install script does not provision the token automatically; the operator must follow the existing runbook step. This limitation is documented and an acceptance criterion.
- **500-line-per-file policy**: If `Install.ps1` plus helpers exceeds 500 lines combined, the module split (`Install.ps1` + `Install.Helpers.psm1`) must stay within policy, mirroring the `Publish.ps1` + `Publish.Helpers.psm1` pattern.
- **Rollback on partial failure**: If the docker stage fails after the MSIX has been installed, the script should leave the MSIX in place (not auto-rollback) and emit clear guidance to run `Uninstall.ps1` or retry after fixing Docker. Auto-rollback across MSIX + Docker boundaries is explicitly out of scope.

## Test Conditions to Consider

- [ ] Running `.\scripts\Install.ps1` with no arguments on a clean host that has a signed bundle under `artifacts/publish/` installs the MSIX and brings the docker stack up without further input.
- [ ] Running `.\scripts\Install.ps1 -SkipDocker` installs the MSIX only and writes an install record with a docker-skipped flag; `Uninstall.ps1` reads the flag and does not attempt `docker compose down`.
- [ ] Running `.\scripts\Install.ps1 -AllowUnsigned` with a `-SkipSign` bundle installs the MSIX in developer mode.
- [ ] `manifest.json` hash mismatch aborts install before any destination folder is created.
- [ ] Running `.\scripts\Install.ps1` when `artifacts/publish/` is empty fails fast with a clear error.
- [ ] Running `.\scripts\Install.ps1` when Docker Desktop is not running and `-SkipDocker` is not supplied fails fast with a remediation message.
- [ ] `.\scripts\Uninstall.ps1` on a healthy install removes the MSIX, stops the compose stack, and removes `%LOCALAPPDATA%\OpenClaw\<version>\` while preserving user config under `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- [ ] Running `.\scripts\Install.ps1 -Force` over an existing install performs an implicit uninstall of the prior version before unpacking the new one.
- [ ] Pester coverage on new lines >= 90%, repo-wide line coverage >= 80%.
- [ ] PoshQC format and analyze produce zero diagnostics on all new PowerShell files.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/2026-04-18-bundle-install-script-<issue>/` folder
- [ ] Invoke task-researcher for investigation of `Add-AppxPackage` trust and unsigned-install paths, `docker compose` programmatic health verification, and MSIX `PackageFullName` capture for later uninstall
