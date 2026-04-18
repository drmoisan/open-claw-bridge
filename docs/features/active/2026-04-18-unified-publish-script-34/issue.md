# unified-publish-script (Issue #34)

- Date captured: 2026-04-18
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-18-unified-publish-script-34/ (Issue #34)

- Issue: #34
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/34
- Last Updated: 2026-04-18
- Work Mode: full-feature

## Problem / Why

The repository currently produces its shippable outputs through multiple disjoint steps. `scripts/build-msix.ps1` packages the two MailBridge-side executables (`OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.Client.exe`) into a signed MSIX, but the newer server-side components (`OpenClaw.Core`, `OpenClaw.HostAdapter`, their `.Contracts` assemblies) and the Docker deployment artifacts (`docker-compose.yml`, `docker-compose.dev.yml`, the `deploy/docker/` tree, `.env.example`) are not part of a single packaged release. An operator preparing a release has to run multiple commands, remember which docker files matter, and manually assemble a bundle. A unified publish script eliminates that manual assembly and gives every release a deterministic, versioned local artifact directory.

## Proposed Behavior

Introduce `scripts/Publish.ps1` that emits a single versioned local artifact bundle at `artifacts/publish/<version>/` containing three subdirectories — `executables/`, `docker/`, and `msix/` — plus a top-level `manifest.json` listing every file in the bundle with its relative path, size, and SHA-256 hash.

The script runs `dotnet publish` once per runnable `src/` project, copies the docker artifact set (root compose files, the entire `deploy/docker/` tree, and `.env.example` when present) into `docker/`, builds (and optionally signs) the MSIX package into `msix/`, and writes the manifest last. The existing `scripts/build-msix.ps1` is retired; its logic is folded into a shared helper module so MSIX assembly remains a single, testable code path.

The script supports parameters for the version, output root, per-project build configuration, signing thumbprint, and a `-SkipSign` switch. Remote upload to a release server, GitHub Release creation, and release-server integration are explicitly out of scope; only local artifact emission is supported in this iteration.

## Acceptance Criteria (early draft)

- [ ] A new PowerShell script (`scripts/Publish.ps1`) accepts `-Version`, `-OutputDir`, `-Configuration`, `-CertThumbprint`, and `-SkipSign` parameters with sensible defaults.
- [ ] The script publishes every runnable `src/` project (`OpenClaw.Core`, `OpenClaw.HostAdapter`, `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`). `.Contracts` library assemblies are bundled alongside their consumers when required for runtime execution.
- [ ] Published executables and their runtime dependencies land under `artifacts/publish/<version>/executables/<project-name>/`.
- [ ] Docker artifacts are copied to `artifacts/publish/<version>/docker/`, preserving relative paths for: `docker-compose.yml`, `docker-compose.dev.yml`, every file under `deploy/docker/` (recursive), and `.env.example` when present. `secrets/` and any secret-bearing files are explicitly excluded.
- [ ] The MSIX package is built and placed at `artifacts/publish/<version>/msix/OpenClaw.MailBridge_<version>_x64.msix`.
- [ ] `artifacts/publish/<version>/manifest.json` is written last and lists every file in the bundle with relative path, size in bytes, and SHA-256 hash.
- [ ] `scripts/build-msix.ps1` is removed; all in-repo callers are updated (script file, `tests/scripts/build-msix.Tests.ps1`, `README.md`, `docs/mailbridge-runbook.md`, `.github/workflows/build-msix.yml`).
- [ ] Running the script without `-SkipSign` and without a certificate thumbprint fails fast with a clear error.
- [ ] Pester coverage >= 90% for new lines; repo-wide coverage remains >= 80%.
- [ ] Documentation updated: `README.md` release-build section and `docs/mailbridge-runbook.md` Section 3.

## Constraints & Risks

- Retiring `scripts/build-msix.ps1` is a breaking change for any CI workflow, documentation, or developer habit that references it. All in-repo callers are updated in-scope; external callers cannot be updated.
- MSIX signing requires a valid certificate in `Cert:\CurrentUser\My`; the script must tolerate the `-SkipSign` dev path and fail clearly when signing is requested but the thumbprint is missing or invalid.
- `dotnet publish` across four projects plus MSIX assembly is slow; the script must print progress per stage and support clean interruption.
- The `deploy/docker/openclaw-assistant/` tree contains the agent workspace from feature #30 and is bind-mounted into the agent container at runtime. The publish script must copy it verbatim.
- The 500-line-per-file policy requires helper logic to be factored into a shared `.psm1` module.
- No remote upload, no release-server integration, and no GitHub Release creation are included in this iteration.

## Test Conditions to Consider

- [ ] Running `Publish.ps1 -Version '1.2.3.0' -SkipSign` against a clean workspace produces a populated `artifacts/publish/1.2.3.0/` tree and a valid `manifest.json`.
- [ ] Each published executable under `executables/<project>/` runs successfully in isolation when runtime prerequisites are met.
- [ ] The MSIX under `msix/` installs, runs the bridge startup task, and uninstalls cleanly (regression of feature #17 behavior).
- [ ] The docker artifacts under `docker/` reproduce a working compose stack on a clean host with Docker Desktop and a valid `secrets/.env.anthropic` + HostAdapter token file supplied out-of-band.
- [ ] `manifest.json` hashes match re-computed hashes of the files on disk.
- [ ] Every in-repo reference to `build-msix.ps1` is updated; no stale references remain.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create `docs/features/active/2026-04-18-unified-publish-script-34/` folder
- [ ] Invoke task-planner to produce an atomic plan consuming this issue, the spec, and the user story.
