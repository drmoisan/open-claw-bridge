# `2026-04-18-bundle-install-script` — User Story

- Issue: #36
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-18T00:00:00Z

## Story Statement

- As an **operator deploying an OpenClaw release bundle on a Windows host**, I want a single PowerShell command that unpacks the newest bundle under `artifacts/publish/`, installs the MSIX, starts the Docker compose stack, and records what was installed, so that I do not have to hand-copy files, remember the correct `Add-AppxPackage` invocation, or assemble a compose command from the runbook.
- As a **CI/CD workflow maintainer**, I want install and uninstall to be separate scripts with deterministic inputs and a shared install-record file, so that automated end-to-end tests can install a bundle, exercise it, and clean up without manual intervention.
- As a **future-me debugging a failed install**, I want the install to abort before any filesystem side effects when the bundle manifest does not match its contents, so that a partial or tampered bundle cannot leave the host in an inconsistent state.

## Problem / Why

Feature #34 produces a versioned local bundle at `artifacts/publish/<version>/` with `executables/`, `docker/`, `msix/`, and `manifest.json`. Consuming that bundle on a target host today requires:

1. Copying the bundle contents to a chosen install location by hand.
2. Invoking `Add-AppxPackage` against the MSIX manually, remembering the `-AllowUnsigned` flag for dev bundles.
3. Copying the docker artifact set to a working directory, creating `.env` from `.env.example`, and running `docker compose up -d` manually, remembering the correct `--project-name`, `--project-directory`, and `-f` flags.
4. Reversing all of the above manually when rolling back.

The multi-step recipe is not written down in one place, there is no record of what got installed where, and there is no guard against installing a tampered or incomplete bundle. A scripted install and uninstall close the loop between `Publish.ps1` and a running deployment on the target host and give every install a single source of truth (the install record).

## Personas & Scenarios

- **Persona: Operator (primary)**
  - Responsible for installing a release bundle on a Windows host with Docker Desktop and MSIX sideloading enabled.
  - Cares about: a single command, manifest-verified integrity, clear failure messages with remediation guidance, a reversible install.
  - Constraint: no access to a release server; bundles arrive on disk under `artifacts/publish/`. Docker Desktop must be running for the docker stage. The HostAdapter token and `secrets/.env.anthropic` are provisioned out-of-band per the runbook.
  - Goal: run `.\scripts\Install.ps1` and have a fully stood-up MailBridge installation plus a running compose stack. Run `.\scripts\Uninstall.ps1` later to reverse it without residual files (except intentionally preserved user config).
  - Frustration: the current multi-step recipe is error-prone and leaves the operator unsure whether the install was complete.

- **Persona: CI/CD Workflow Maintainer (secondary)**
  - Maintains workflows that need to install a bundle, exercise the product end-to-end, and clean up.
  - Cares about: deterministic exit codes, no interactive prompts, a single script call, a recorded artifact for post-run teardown.
  - Constraint: the workflow runs on Windows runners with Docker Desktop or a compatible Docker runtime. The MSIX may be signed or unsigned depending on the workflow.
  - Goal: `Install.ps1` + `Uninstall.ps1` provide a closed loop. The install record makes teardown deterministic even when the workflow retries.

- **Persona: Future-Me Debugging a Failed Install (tertiary)**
  - Inspects helper modules and install records when an install goes wrong.
  - Cares about: small, readable files, individually testable helpers, deterministic failure messages that name the failing step and the remediation.
  - Constraint: debugging sessions happen under time pressure; diagnostic quality drives mean time to recovery.
  - Goal: read `scripts/Install.Helpers.psm1` and the install record, identify the failing stage (manifest verification, MSIX install, compose up, compose health), and know which `Uninstall.ps1` run or manual step unblocks the next attempt.

- **Scenario: Install newest bundle on a clean host**
  - **Trigger**: Operator has just run `Publish.ps1 -Version '1.2.3.0'` (signed) and wants to install the result locally.
  - **Steps**:
    1. Operator runs `.\scripts\Install.ps1` with no arguments.
    2. The script locates `artifacts/publish/1.2.3.0/` as the newest bundle.
    3. The script verifies `manifest.json` against every file in the bundle.
    4. The script creates `%LOCALAPPDATA%\OpenClaw\1.2.3.0\` and copies `executables/` and `docker/` into it.
    5. The script copies `.env.example` to `.env` under the destination `docker/` directory (since `.env` is absent).
    6. The script installs the MSIX via `Add-AppxPackage` and captures the `PackageFullName`.
    7. The script checks Docker Desktop readiness (`docker info`), runs `docker compose up -d openclaw-core openclaw-agent` with explicit project-name, project-directory, and file flags, and polls `docker compose ps --format json` until both services are healthy.
    8. The script writes `%LOCALAPPDATA%\OpenClaw\install-record.json` and exits 0.
  - **Expected outcome**: MailBridge is installed, the compose stack is running, and the install record captures everything needed for uninstall.

- **Scenario: Install an unsigned dev bundle**
  - **Trigger**: Operator wants to install a bundle produced with `Publish.ps1 -SkipSign`.
  - **Steps**:
    1. Operator runs `.\scripts\Install.ps1 -AllowUnsigned` from an elevated PowerShell 7 session.
    2. The script proceeds through the same stages as the main path, passing `-AllowUnsigned` to `Add-AppxPackage`.
    3. The install record captures `allowUnsigned = true`.
  - **Expected outcome**: Unsigned MSIX is installed. The operator understands that the package manifest must carry the `OID.2.25.311729368913984317654407730594956997722=1` OID and that the session must be elevated.

- **Scenario: Install with docker stage skipped**
  - **Trigger**: Operator wants to install only the MSIX for testing.
  - **Steps**:
    1. Operator runs `.\scripts\Install.ps1 -SkipDocker`.
    2. The script verifies the manifest, copies the bundle, installs the MSIX, captures the `PackageFullName`, and writes the install record with `skipDocker = true`.
    3. No docker readiness check, compose-up, or health poll runs.
  - **Expected outcome**: MSIX-only install. Subsequent `.\scripts\Uninstall.ps1` reads `skipDocker = true` and skips the compose-down step.

- **Scenario: Force reinstall over an existing install**
  - **Trigger**: Operator wants to reinstall the same version over an existing install.
  - **Steps**:
    1. Operator runs `.\scripts\Install.ps1 -Force`.
    2. The script reads the prior install record and runs the full uninstall sequence (compose down, remove MSIX, remove destination folder, delete install record).
    3. The script then proceeds through the full install sequence for the selected bundle.
    4. The new install record reflects the post-reinstall state.
  - **Expected outcome**: Clean same-version reinstall without MSIX identity conflicts.

- **Scenario: Manifest mismatch**
  - **Trigger**: A file under `artifacts/publish/1.2.3.0/` was modified after `manifest.json` was written.
  - **Steps**:
    1. Operator runs `.\scripts\Install.ps1`.
    2. The script reads `manifest.json` and computes SHA-256 for every file.
    3. The script detects the hash mismatch, accumulates the discrepancy (along with any others), and throws a single terminating error listing every discrepancy.
    4. No destination folder is created. No MSIX is installed. No compose command is run.
  - **Expected outcome**: The operator is informed exactly which files are corrupt and can re-publish or fix the bundle before retrying.

- **Scenario: Docker Desktop not running**
  - **Trigger**: Operator runs `.\scripts\Install.ps1` without `-SkipDocker` while Docker Desktop is stopped.
  - **Steps**:
    1. The script verifies the manifest.
    2. Before copying the bundle, the script runs `docker info` and sees a non-zero exit code.
    3. The script throws a terminating error instructing the operator to start Docker Desktop or pass `-SkipDocker`.
  - **Expected outcome**: Early, specific failure with actionable remediation; no partial install state.

- **Scenario: Uninstall**
  - **Trigger**: Operator runs `.\scripts\Uninstall.ps1` on a host with a recorded install.
  - **Steps**:
    1. The script reads `%LOCALAPPDATA%\OpenClaw\install-record.json`.
    2. The script runs `docker compose down` (unless `skipDocker` is true), `Remove-AppxPackage`, `Remove-Item` on the destination folder, and deletes the install record. Each step runs regardless of individual failures.
    3. If any step failed, the script throws a single terminating error listing the failures and suggested corrective actions. If all steps succeeded, the script exits 0.
  - **Expected outcome**: Host is returned to its pre-install state except for user config under `%LOCALAPPDATA%\OpenClaw\MailBridge\`, which is preserved by design.

## Acceptance Criteria

- [x] Running `.\scripts\Install.ps1` with no arguments on a clean host with a signed bundle under `artifacts/publish/` installs the MSIX and brings the docker stack up without further input.
- [x] Running `.\scripts\Install.ps1 -SourcePath <path>` or `.\scripts\Install.ps1 -Version <v>` overrides the newest-bundle auto-detection.
- [x] Running `.\scripts\Install.ps1 -AllowUnsigned` with a bundle produced by `Publish.ps1 -SkipSign` installs the MSIX.
- [x] Running `.\scripts\Install.ps1 -SkipDocker` installs the MSIX only and records `skipDocker = true` in the install record.
- [x] Running `.\scripts\Install.ps1 -Force` over an existing install performs a complete uninstall of the prior version before installing.
- [x] `manifest.json` hash or size mismatch aborts install before any destination folder is created, with a terminating error listing every discrepancy.
- [x] Docker Desktop not running with the docker stage enabled aborts install with a remediation message.
- [x] `.env.example` at the destination `docker/` directory is copied to `.env` only when `.env` is absent; an existing `.env` is never overwritten.
- [x] `%LOCALAPPDATA%\OpenClaw\install-record.json` is written on successful install with the schema documented in spec.md.
- [x] `.\scripts\Uninstall.ps1` reads the install record and runs, in order, `docker compose down` (when `skipDocker` is false), `Remove-AppxPackage`, `Remove-Item` destination, and deletion of the install record. All steps run regardless of individual failures; failures are collected and reported as a single terminating error.
- [x] User config at `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` is preserved by `Uninstall.ps1`.
- [x] Pester coverage >= 90% on new lines in `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1`. Repo-wide coverage remains >= 80%.
- [x] PoshQC suite (format -> analyze -> test) passes on `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, and their Pester test files.
- [x] `README.md` and `docs/mailbridge-runbook.md` document the new install and uninstall flow without displacing the scheduled-task install path.

## Non-Goals

- **Remote bundle download**: The installer consumes a local bundle already present under `artifacts/publish/` or at a path supplied via `-SourcePath`.
- **Multi-version install history**: The install record is a single-record JSON file overwritten on each successful install.
- **Auto-rollback across MSIX and Docker**: Partial failures after the MSIX is installed leave the MSIX in place; the operator runs `Uninstall.ps1` or retries manually.
- **HostAdapter token provisioning**: Out-of-band per runbook.
- **`secrets/.env.anthropic` provisioning**: Out-of-band per runbook.
- **Replacement of the scheduled-task install path**: `scripts/install-mailbridge.ps1` and `scripts/uninstall-mailbridge.ps1` remain unchanged.
- **Docker volume removal on uninstall**: `docker compose down` runs without `--volumes`; operators remove volumes manually if needed.
- **Cross-platform installers**: macOS and Linux installers are not in scope.
- **In-place overwrite reinstall**: `-Force` performs uninstall-then-install, not overwrite.
