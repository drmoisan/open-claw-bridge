# `2026-04-18-unified-publish-script` — User Story

- Issue: #34
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-18T00:00:00Z

## Story Statement

- As a **developer or release operator preparing a local OpenClaw release bundle**, I want a single PowerShell command that publishes every runnable project, copies the docker deployment artifacts, and builds the MSIX into one versioned folder with a manifest, so that I do not have to remember a multi-step build recipe or manually assemble the output.
- As a **CI/CD pipeline maintainer**, I want the publish step to be a single script invocation with deterministic outputs and a manifest listing every file with its SHA-256 hash, so that release integrity is auditable and the workflow YAML stays simple.
- As a **future-me debugging a release**, I want the MSIX logic to live in a shared, tested helper module (not duplicated between a dedicated MSIX script and a unified script), so that there is exactly one code path to inspect when MSIX behavior regresses.

## Problem / Why

Today, producing a release candidate requires running `dotnet publish` individually per project, copying the compose files and `deploy/docker/` tree by hand, invoking `scripts/build-msix.ps1` for the MSIX, and then assembling those outputs into something distributable. The steps are not written down in a single place. There is no manifest, so there is no way to verify that a copy of a release matches what was built. Server-side components (`OpenClaw.Core`, `OpenClaw.HostAdapter`) and the docker stack are not part of any packaged release at all.

Retaining `scripts/build-msix.ps1` alongside a new `Publish.ps1` would leave two places where MSIX logic could drift apart. Folding it into a shared helper module and deleting the old script keeps MSIX assembly in exactly one code path.

## Personas & Scenarios

- **Persona: Release Operator (primary)**
  - Responsible for producing a versioned release bundle on a Windows workstation with the .NET 10 SDK and Windows 10 SDK installed.
  - Cares about: reproducibility, a single command, clear failure messages, a manifest that can be checked into release notes or uploaded alongside the bundle.
  - Constraint: no access to a release server in this iteration; outputs stay on the local machine until manually uploaded elsewhere.
  - Goal: run `Publish.ps1 -Version '<v>'` and receive a populated `artifacts/publish/<v>/` folder with a valid `manifest.json`.
  - Frustration: the current multi-step recipe is error-prone; there is no way to diff two release bundles.

- **Persona: CI Workflow Maintainer (secondary)**
  - Maintains `.github/workflows/build-msix.yml` and any future publish workflow.
  - Cares about: a single script call in YAML, a stable exit-code contract, progress output that surfaces in the run log.
  - Constraint: the workflow must still produce the MSIX artifact today's consumers expect (feature #17 regression surface).
  - Goal: replace the separate `dotnet publish` steps and the `build-msix.ps1` call with one `Publish.ps1` invocation; upload the bundle as a workflow artifact.

- **Persona: Repo Contributor Reading the Code (tertiary)**
  - Wants to understand the release pipeline by reading one script and one helper module.
  - Cares about: small files, clear function names, unit tests that do not require temp files or external processes.
  - Goal: open `scripts/Publish.ps1`, follow the stage calls into `scripts/Publish.Helpers.psm1`, and have confidence that every stage is covered by Pester tests.

- **Scenario: Dev build on a clean workspace**
  - **Trigger**: the operator finishes a feature and wants a local bundle to hand to a tester.
  - **Steps**:
    1. Operator runs `.\scripts\Publish.ps1 -Version '1.2.3.0' -SkipSign`.
    2. The script removes any prior `artifacts/publish/1.2.3.0/` directory and recreates it.
    3. Four `dotnet publish` invocations run in sequence, each emitting progress like `[publish] OpenClaw.Core -> executables/OpenClaw.Core/`.
    4. The docker artifact set is copied to `artifacts/publish/1.2.3.0/docker/`, preserving relative paths.
    5. The MSIX pipeline runs; the unsigned `.msix` lands at `artifacts/publish/1.2.3.0/msix/OpenClaw.MailBridge_1.2.3.0_x64.msix`.
    6. The manifest stage writes `artifacts/publish/1.2.3.0/manifest.json` listing every file under the version root with `path`, `size`, and `sha256`.
    7. The script exits 0.
  - **Expected outcome**: the operator has a single versioned folder containing everything needed to deploy the release locally, plus a manifest proving the bundle contents.

- **Scenario: Signed CI build**
  - **Trigger**: `.github/workflows/build-msix.yml` runs on a `v*` tag push.
  - **Steps**:
    1. The workflow sets up .NET 10, installs the signing certificate into `Cert:\CurrentUser\My`, and runs `.\scripts\Publish.ps1 -Version '${{ inputs.version }}' -CertThumbprint '${{ secrets.MSIX_CERT_THUMBPRINT }}'`.
    2. The script publishes, copies, builds, and signs exactly as in the dev scenario but with `signtool.exe` invoked against the packed MSIX.
    3. The workflow uploads `artifacts/publish/<version>/` as a GitHub Actions artifact.
  - **Expected outcome**: a single workflow step replaces the current separate `dotnet publish` steps and the `build-msix.ps1` call.

- **Scenario: Missing certificate when signing is requested**
  - **Trigger**: the operator runs `.\scripts\Publish.ps1 -Version '1.2.3.0'` with neither `-SkipSign` nor `-CertThumbprint`.
  - **Steps**:
    1. The script's parameter-validation stage throws a terminating error naming the missing parameter.
    2. No `dotnet publish` runs; no files are written under `artifacts/publish/`.
  - **Expected outcome**: the operator sees a clear error and corrects the invocation.

## Acceptance Criteria

- [x] Running `.\scripts\Publish.ps1 -Version '<v>' -SkipSign` on a clean workspace produces `artifacts/publish/<v>/` containing `executables/`, `docker/`, `msix/`, and `manifest.json`.
- [x] `executables/` contains one subdirectory per runnable `src/` project (`OpenClaw.Core`, `OpenClaw.HostAdapter`, `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`). Each subdirectory contains a runnable binary and its runtime dependencies.
- [x] `docker/` contains `docker-compose.yml`, `docker-compose.dev.yml`, the full `deploy/docker/` tree (including the `openclaw-assistant/` agent workspace, copied verbatim), and `.env.example` when it exists in the repo root.
- [x] `docker/` never contains `secrets/` or any file under a `secrets/` path; the script logs a warning if such a directory is detected under a source root.
- [x] `msix/OpenClaw.MailBridge_<v>_x64.msix` is produced. With `-CertThumbprint`, it is signed. With `-SkipSign`, it is unsigned.
- [x] `manifest.json` is written last and contains one entry per file under the version root (excluding `manifest.json` itself) with `path` (forward-slash relative), `size` (bytes), and `sha256` (lowercase hex). Entries are sorted by `path`.
- [x] Running `.\scripts\Publish.ps1 -Version '<v>'` with neither `-SkipSign` nor `-CertThumbprint` fails fast with a clear error before any stage writes files.
- [x] `scripts/build-msix.ps1` is deleted. `tests/scripts/build-msix.Tests.ps1` is deleted. `README.md`, `docs/mailbridge-runbook.md`, and `.github/workflows/build-msix.yml` no longer reference `build-msix.ps1`.
- [x] MSIX packaging logic lives in `scripts/Publish.Helpers.psm1` and is covered by `tests/scripts/Publish.Helpers.Tests.ps1`. Pester coverage >= 90% on new lines; repo-wide coverage remains >= 80%.
- [ ] An MSIX produced by `Publish.ps1` installs, launches the bridge startup task on next logon, and uninstalls cleanly (regression of feature #17 behavior).
- [x] PoshQC suite (format -> analyze -> test) passes on `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, and the new Pester test files.

## Non-Goals

- **Remote upload**: The script does not call `gh release create`, `az storage blob upload`, or any remote publish command. Operators move the bundle elsewhere manually.
- **GitHub Release creation**: Creating or updating a GitHub Release is deferred to a follow-on feature.
- **Release-server integration**: No push to an internal release server in this iteration.
- **Docker image export**: The script does not `docker build` or `docker save`. Only build inputs and compose files are copied.
- **Repo-layout refactor**: Moving compose files or restructuring `deploy/docker/` is handled by a separate feature later.
- **Agent workspace changes**: `deploy/docker/openclaw-assistant/` is copied verbatim. No sanitization, no redaction, no schema migration.
- **Cross-platform support**: MSIX packaging requires Windows. A Linux or macOS end-to-end publish path is not in scope.
- **Standalone Contracts bundles**: `.Contracts` libraries appear inside their consumers' `executables/` subdirectories. No dedicated `executables/OpenClaw.HostAdapter.Contracts/` subdirectory is produced.
