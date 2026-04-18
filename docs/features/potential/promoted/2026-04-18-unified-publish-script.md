# unified-publish-script (Potential)

- Date captured: 2026-04-18
- Author: drmoisan
- Status: Draft

## Problem / Why

The repository currently produces its shippable outputs through multiple disjoint steps. `scripts/build-msix.ps1` packages the two MailBridge-side executables (`OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.Client.exe`) into a signed MSIX, but the newer server-side components (`OpenClaw.Core`, `OpenClaw.HostAdapter`, their `.Contracts` assemblies) and the Docker deployment artifacts (`docker-compose.yml`, `docker-compose.dev.yml`, the `deploy/docker/` build and runtime inputs, `.env.example`) are not part of a single packaged release. An operator preparing a release has to run multiple commands, remember which docker files matter, and manually assemble a bundle. A unified publish script eliminates that manual assembly and gives every release a deterministic, versioned local artifact directory.

## Proposed Behavior

Introduce `scripts/Publish.ps1` (working name) that emits a single versioned local artifact bundle at `artifacts/publish/<version>/` containing three subdirectories — `executables/`, `docker/`, and `msix/` — plus a top-level `manifest.json` listing every file in the bundle with its size and SHA-256 hash.

The script runs `dotnet publish` once per src/ project that produces runnable output, copies the docker artifacts (root compose files, the entire `deploy/docker/` tree, and `.env.example`) into `docker/`, builds (and optionally signs) the MSIX package into `msix/`, and writes the manifest last. The existing `scripts/build-msix.ps1` is retired; its logic is folded into the new script or factored into shared helper functions so MSIX assembly remains a single, testable code path.

The script supports parameters for the version, output root, signing thumbprint or `-SkipSign`, and per-project build configuration (`Debug`/`Release`). Remote upload to a release server is explicitly out of scope; only local artifact emission is supported in this iteration.

## Acceptance Criteria (early draft)

- [ ] A new PowerShell script (`scripts/Publish.ps1` or equivalent name) accepts `-Version`, `-OutputDir`, `-Configuration`, `-CertThumbprint`, and `-SkipSign` parameters with sensible defaults.
- [ ] The script enumerates and publishes every src/ project that produces runnable output (at minimum: `OpenClaw.Core`, `OpenClaw.HostAdapter`, `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`). `.Contracts` library assemblies are published alongside their consumers when required for runtime execution.
- [ ] Published executables and their runtime dependencies land under `artifacts/publish/<version>/executables/<project-name>/`.
- [ ] Docker artifacts are copied to `artifacts/publish/<version>/docker/`, preserving relative paths for: `docker-compose.yml`, `docker-compose.dev.yml`, every file under `deploy/docker/` (recursive), and `.env.example`.
- [ ] The MSIX package is built and placed at `artifacts/publish/<version>/msix/OpenClaw.MailBridge_<version>_x64.msix`.
- [ ] `artifacts/publish/<version>/manifest.json` is written last and lists every file in the bundle with relative path, size in bytes, and SHA-256 hash.
- [ ] `scripts/build-msix.ps1` is removed; all of its callers are updated to invoke the new publish script (or a shared helper it exposes).
- [ ] Running the script twice in succession against a clean workspace produces byte-identical `manifest.json` entries for non-binary files and stable structure for binaries (hashes may differ only because of compiler timestamp differences, which is documented).
- [ ] Documentation updated: `README.md` release-build section, and any developer-facing docs that reference `build-msix.ps1`.

## Constraints & Risks

- Retiring `scripts/build-msix.ps1` is a breaking change for any CI workflow, documentation, or developer muscle memory that references it; all in-repo callers must be updated in-scope. External callers (if any exist outside the repo) cannot be updated.
- MSIX signing requires a valid certificate in the current-user store; the script must tolerate the `-SkipSign` dev path and fail clearly when signing is requested but the thumbprint is missing or invalid.
- `dotnet publish` across four or more projects plus MSIX assembly is slow; the script should print progress per stage and support being interrupted cleanly.
- The `deploy/docker/openclaw-assistant/` tree contains the agent workspace from feature #30 and is bind-mounted into the agent container at runtime. The publish script must copy it verbatim without rewriting relative paths inside its contents.
- No remote upload, no release-server integration, and no GitHub Release creation is included in this iteration. Those are follow-on features.
- The `scripts/build-msix.ps1` script was the output of feature #17 and is currently exercised by Pester tests; equivalent coverage must be preserved against the new script.

## Test Conditions to Consider

- [ ] Running `Publish.ps1 -Version '1.2.3.0' -SkipSign` against a clean workspace produces a populated `artifacts/publish/1.2.3.0/` tree and a valid `manifest.json`.
- [ ] Each published executable under `executables/<project>/` runs successfully in isolation when its runtime prerequisites are met.
- [ ] The MSIX under `msix/` installs, runs the bridge startup task, and uninstalls cleanly (regression of feature #17 behavior).
- [ ] The docker artifacts under `docker/` reproduce a working compose stack when copied to a clean host that has Docker Desktop and a valid `secrets/.env.anthropic` + HostAdapter token file.
- [ ] `manifest.json` hashes match re-computed hashes of the files on disk.
- [ ] Pester coverage for the new script is >= 90% for new lines and overall repo-wide line coverage remains >= 80%.
- [ ] Running the script without `-SkipSign` and without a certificate thumbprint fails fast with a clear error.
- [ ] Every in-repo reference to `build-msix.ps1` has been updated to the new entry point; no stale references remain.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/2026-04-18-unified-publish-script-<issue>/` folder
- [ ] Invoke task-researcher for deeper investigation of dotnet publish strategies across multi-project solutions and MSIX payload shaping
