# 2026-07-10-installer-docker-images-not-bundled (Spec)

- **Issue:** #142
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-10
- **Status:** Ready for planning
- **Version:** 1.0
- **Work Mode:** full-bug (spec.md is the sole acceptance-criteria source; user-story.md intentionally absent)

## Context
The scripted-bundle install (`Install.ps1`) fails at the `docker compose up` stage because the bundle does not ship the container images or the source needed to build them. The compose service `openclaw-core` builds from `deploy/docker/openclaw-core.Dockerfile`, whose `COPY . .` + `dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj` requires the `src/` tree, but the bundle `docker/` directory (the build context at install time) ships only the compose files, `.env.example`, and `deploy/docker/**`.

Environment:
- OS/version: Windows 11 Pro 10.0.26200
- Python version: n/a (PowerShell 7 / .NET 10 / Docker Desktop)
- Command/flags used: `.\Install.ps1 -DockerEnvFilePath ... -AnthropicEnvFilePath ...` from `artifacts\publish\1.0.2.3`
- Data source or fixture: published bundle `artifacts/publish/1.0.2.3`

Impact / Severity:
- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Install cannot complete via the recommended scripted-bundle path.

## Repro & Evidence
Steps to Reproduce:
1. Run `.\scripts\Publish.ps1` to produce a versioned bundle.
2. Prepare operator config and run `.\Install.ps1` from the bundle root.
3. Install proceeds through HostAdapter start and MSIX install, then runs `docker compose up`.

Expected:
The compose stack (`openclaw-core`, `openclaw-agent`) starts from images delivered by the bundle without requiring the .NET SDK, the `src/` tree, or registry access on the target machine.

Actual:
`docker compose up` falls back to building the images (the `openclaw/core:pre-mvp` / `openclaw/agent:pre-mvp` tags are not in any registry: `pull access denied`), and the `openclaw-core` build fails:

```
#24 [openclaw-core build 4/5] RUN dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj
#24 MSBUILD : error MSB1009: Project file does not exist.
#24 Switch: ./src/OpenClaw.Core/OpenClaw.Core.csproj
target openclaw-core: failed to solve: ... exit code: 1
Exception: docker compose up failed (exit 1) ...
```

Logs / Screenshots:
- [x] Attached minimal logs or screenshot
- Snippet: see Actual Behavior (MSB1009 at build stage; `pull access denied` for both `image:` tags).

## Scope & Non-Goals
- In scope:
  - Publish-side prebuilt-image distribution: build `openclaw-core` and `openclaw-agent` at publish time, tag each with the bundle version and `pre-mvp`, and `docker save` all four refs into a single combined tar at `<BundleRoot>/docker/openclaw-images.tar` (canonical name; supersedes the issue's provisional `docker/images.tar`).
  - Publish-side compose transform: emit a transformed `docker-compose.yml` into the bundle `docker/` directory that removes both `build:` blocks, adds `pull_policy: never` to both services, and rewrites each `image:` reference to the versioned tag (`openclaw/core:<version>`, `openclaw/agent:<version>`).
  - New module `scripts/Publish.Docker.psm1` (build/save/transform helpers behind an `Invoke-DockerExe -DockerArgs` seam) and a new Stage 3b in `scripts/Publish.ps1` after `Copy-DockerArtifact` and before the MSIX stage.
  - New module `scripts/Install.Docker.psm1` (`Invoke-DockerExe` seam plus `Invoke-DockerImageLoad`), staged into the bundle via `Copy-InstallScriptsIntoBundle`, and a `docker load` step in `Install.ps1` Stage 9 immediately before `Invoke-ComposeUp`, gated by `-SkipDocker`.
  - Enabling fix: `New-ManifestEntry` size cast `[int]` -> `[long]` (`scripts/Publish.Helpers.psm1:155`); image tars can exceed 2 GiB and `[int]` conversion throws above 2,147,483,647.
  - Stop copying `docker-compose.dev.yml` into the bundle (`Copy-DockerArtifact`); it builds from `.devcontainer/Dockerfile`, which is not shipped, so it is already dead at install time and `Install.ps1` never references it.
  - Pester test additions (`tests/scripts/Publish.Docker.Tests.ps1`, `tests/scripts/Install.Docker.Tests.ps1`) and updates to existing tests affected by the new stage, staging list, export count, dev-compose removal, and size type.
- Out of scope / non-goals:
  - Do NOT change the tracked repo `docker-compose.yml` build capability: it keeps both `build:` blocks and the `openclaw/*:pre-mvp` `image:` tags so `docker compose up --build` remains the dev workflow.
  - Do NOT retrofit the four pre-existing direct `docker` call sites in `scripts/Install.Helpers.psm1` (lines 295, 323, 355, 424: `docker info`, `compose up -d`, `compose ps`, `compose down`) onto the wrapper seam; the file is already 527 lines and those paths are covered by existing shim-based tests. Record the retrofit as a follow-up.
  - Do NOT introduce a general process-runner framework; the seam is a per-module `Invoke-DockerExe` function scoped to the two new modules.
  - No registry publishing, image signing, or `docker login` flows.
  - No `Uninstall.ps1` image removal (`docker rmi`); optional follow-up, not required to fix #142.
  - No changes to `Wait-ComposeHealthy` timeouts or health-poll semantics.
- Explicitly excluded systems, integrations, or datasets: GitHub Container Registry (no push), MSIX pipeline stages, HostAdapter stages, `.env`/secrets handling logic (read-only reuse of `Get-EnvFileMap`).

## Root Cause Analysis
- `docker-compose.yml` `openclaw-core` and `openclaw-agent` declare `build: { context: . }` (lines 5-10 and 53-57), resolved against the bundle `docker/` directory at install time, which lacks `src/`.
- `Copy-DockerArtifact` (`scripts/Publish.Helpers.psm1:64-119`) copies compose files + `.env.example` + `deploy/docker/**` into the bundle but neither the source tree nor prebuilt images.
- The `image:` tags (`openclaw/core:pre-mvp`, `openclaw/agent:pre-mvp`) exist in no registry, so compose falls back from pull (`pull access denied`) to `build:`, which fails with MSB1009 because the context has no `src/`.
- Latent enabling defect: `New-ManifestEntry` casts file size to `[int]`, which throws for files larger than 2 GiB; shipping a combined image tar makes this reachable. `Test-ManifestIntegrity` already reads sizes as `[long]` (`scripts/Install.Helpers.psm1:97`), so the write side is the only gap.

## Proposed Fix

### Design summary (what changes where):
Move image acquisition from install time (pull/build — both impossible) to publish time (build + `docker save`), and make install a pure offline `docker load` + `compose up` against a bundle compose that can never pull or build.

- `scripts/Publish.Docker.psm1` (new): module-scoped `Invoke-DockerExe -DockerArgs <string[]>` seam; image build helper(s) issuing direct `docker build` per image; `Save-OpenClawDockerImage` issuing one `docker save -o <bundle docker>/openclaw-images.tar` naming all four refs; pure `Convert-ComposeToBundleCompose -ComposeContent <string[]> -Version <string>` returning `[string[]]`; a `ShouldProcess` writer that writes the transformed compose into the bundle.
- `scripts/Publish.ps1`: new Stage 3b after `Copy-DockerArtifact` (currently ends near line 206) and before Stage 4 MSIX — build both images, save the tar, overwrite the bundle `docker/docker-compose.yml` with transformed content. Running before Stage 6 `Write-PublishManifest` means the tar and transformed compose are manifested automatically.
- `scripts/Publish.Helpers.psm1`: `New-ManifestEntry` size cast `[int]` -> `[long]` (line 155); `Copy-DockerArtifact` stops copying `docker-compose.dev.yml`; `Copy-InstallScriptsIntoBundle` stages `Install.Docker.psm1`.
- `scripts/Install.Docker.psm1` (new, self-contained for bundle import): module-scoped `Invoke-DockerExe` seam plus `Invoke-DockerImageLoad -ImageTarPath <path>` (throws with remediation text when the tar is missing or `docker load` exits non-zero).
- `scripts/Install.ps1`: import the new module alongside the existing bundled-module imports; inside Stage 9 (gated by `-SkipDocker`), call `Invoke-DockerImageLoad -ImageTarPath (Join-Path $DestDockerDir 'openclaw-images.tar')` immediately before `Invoke-ComposeUp` (line 425). The tar reaches `$DestDockerDir` via the existing Stage 5 `Copy-BundleContents` (copies the `docker/` subtree unchanged).

### Boundaries and invariants to preserve:
- Tracked repo `docker-compose.yml` remains byte-identical (dev `--build` path unaffected).
- Install stage ordering and the no-orphan/rollback invariants around MSIX stages are untouched; the load step lives inside existing Stage 9 gating.
- The four pre-existing direct `docker` call sites in `Install.Helpers.psm1` are not modified.
- `Install.ps1` imports only files staged inside the bundle; `Install.Docker.psm1` therefore defines its own seam rather than sharing one with the publish module.
- 500-line cap: no new function is added to `Install.Helpers.psm1` (527 lines, already over); `Publish.Helpers.psm1` (357 lines) receives only the one-token size fix and small edits; all new logic lives in the two new modules, each under 500 lines.
- Install-time `.env` interpolation in the bundle compose is preserved (the transform is line-targeted; it does not render `${VAR:-default}` expressions).

### Dependencies or blocked work:
- No new library dependencies. YAML round-trip via `powershell-yaml` and `docker compose config` rendering were evaluated and rejected (dependency policy; publish-time interpolation destroys install-time configurability). The transform is a deterministic line-based structural edit of the repo-controlled 95-line compose file.
- Publish host prerequisites (already documented): Docker Desktop running; network access for base-image pulls (`mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, resolved `OPENCLAW_AGENT_IMAGE` default `ghcr.io/openclaw/openclaw:latest`).
- Change budget: more than 2 production PowerShell files change, so execution routes through `powershell-orchestrator` per `.claude/rules/powershell.md`, batched at <= 3 production files (natural split: publish-side batch, install-side batch).

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
- New: `scripts/Publish.Docker.psm1`, `scripts/Install.Docker.psm1`.
- Modified: `scripts/Publish.ps1` (Stage 3b + staging message near line 239), `scripts/Publish.Helpers.psm1` (`New-ManifestEntry`, `Copy-DockerArtifact`, `Copy-InstallScriptsIntoBundle`), `scripts/Install.ps1` (module import + Stage 9 load call).
- New tests: `tests/scripts/Publish.Docker.Tests.ps1`, `tests/scripts/Install.Docker.Tests.ps1`.
- Updated tests: `tests/scripts/Publish.Tests.ps1` (helper-order and staging-order assertions), `tests/scripts/Publish.Helpers.Tests.ps1` (export count; dev-compose copy case; size-type case; staged-file-order case), `tests/scripts/Install.Tests.ps1` / `tests/scripts/Install.Force.Tests.ps1` (register the load step in stage-sequence mock sets).
- Unchanged: `docker-compose.yml`, `deploy/docker/*.Dockerfile`, `scripts/Install.Helpers.psm1` docker call sites, `tests/scripts/Install.Helpers.Compose.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`.

#### Functions/classes/CLI commands impacted:
- `Invoke-DockerExe -DockerArgs <string[]>` (one per new module; parameter must not be named `Args` per `.claude/rules/powershell.md`; implementation splats `docker @DockerArgs 2>&1`).
- Publish build commands (direct `docker build`, mirroring the tracked compose `build:` blocks; cross-reference comment required):
  - Core: `docker build -f deploy/docker/openclaw-core.Dockerfile --target runtime --build-arg BUILD_CONFIGURATION=<Configuration> -t openclaw/core:<version> -t openclaw/core:pre-mvp <RepoRoot>`
  - Agent: `docker build -f deploy/docker/openclaw-agent.Dockerfile --build-arg OPENCLAW_AGENT_IMAGE=<resolved> -t openclaw/agent:<version> -t openclaw/agent:pre-mvp <RepoRoot>`, with `OPENCLAW_AGENT_IMAGE` resolved from the repo-root `.env` via existing `Get-EnvFileMap` (`Publish.Env.psm1`), defaulting to `ghcr.io/openclaw/openclaw:latest`.
- `docker save -o <BundleRoot>/docker/openclaw-images.tar openclaw/core:<version> openclaw/core:pre-mvp openclaw/agent:<version> openclaw/agent:pre-mvp` (single tar; shared layers stored once; `docker load` restores both tags per image).
- `Convert-ComposeToBundleCompose` (pure): removes each service's `build:` block (4-space-indented key through the last deeper-indented line), inserts `pull_policy: never` at `image:` indentation, rewrites `image:` values to versioned tags; throws if it does not find exactly the expected `build:`/`image:` keys for `openclaw-core` and `openclaw-agent` (fail fast on compose drift).
- `Invoke-DockerImageLoad -ImageTarPath <path>`: `docker load -i <path>`; throws naming the expected path and re-publish remediation when the tar is missing; throws on non-zero exit.
- `New-ManifestEntry`: `size = [long]$fileInfo.Length`.

#### Data flow and validation changes:
- Publish: repo sources -> `docker build` (x2) -> local image store -> `docker save` -> bundle tar; tracked compose lines -> `Convert-ComposeToBundleCompose` -> bundle compose; both new artifacts flow into `Write-PublishManifest` (path + `[long]` size + SHA-256) with no manifest-code change beyond the size cast.
- Install: Stage 2 manifest integrity already verifies the tar hash/size; Stage 5 copies it to `$DestDockerDir`; Stage 9 loads it, then `Invoke-ComposeUp` starts services from local images only (`pull_policy: never` guarantees no registry contact).

#### Error handling and logging updates:
- Publish fail-fast with specific messages: docker daemon unavailable at build; base-image pull failure; non-zero `docker build`/`docker save` exit; transform drift (expected keys not found).
- Install fail-fast: missing tar (message names the expected path and directs to re-publish); non-zero `docker load` exit; if the load is bypassed, compose fails with an explicit "image not found locally" (clearer than today's pull-denied -> build -> MSB1009 chain).
- Stage progress via the existing `Write-Information` pattern in both entry scripts.

#### Rollback/feature-flag considerations (if applicable):
- No feature flag. `-SkipDocker` already gates the entire container path and also gates the new load step.
- Rollback = revert the commits; the tracked compose is untouched, so dev workflows carry no rollback risk.
- Re-install and downgrade safety: `docker load` is idempotent; the bundle compose pins versioned tags, so a `-Force` reinstall of an older bundle remains correct even though the floating `pre-mvp` tags re-point.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:
- Bundle artifact: `docker/openclaw-images.tar` — single `docker save` tar containing both images under four refs.
- Bundle compose: `docker/docker-compose.yml` — no `build:` keys; `pull_policy: never` and `image: openclaw/<name>:<version>` on both services; all other lines preserved from the tracked file.
- `manifest.json` entry shape unchanged (`path`, `size`, `sha256`); `size` now serializes from `[long]`.

#### Required configuration keys and defaults:
- `OPENCLAW_AGENT_IMAGE` read from repo-root `.env` at publish time; default `ghcr.io/openclaw/openclaw:latest` (matches the Dockerfile `ARG` default).
- `BUILD_CONFIGURATION` from the existing publish `-Configuration` value (compose default: `Release`).
- No new install-time configuration keys.

#### Backward-compatibility expectations:
- Dev workflow (`docker compose up --build` against the tracked compose) unchanged.
- `Test-ManifestIntegrity` already parses sizes as `[long]`; older bundles (all sizes < 2 GiB) remain valid.
- `pull_policy` is a Compose-spec v2 service key; the repo already requires Docker Compose v2 (`docker compose` syntax in `Invoke-ComposeUp`).
- `-SkipDocker` behavior is unchanged apart from also skipping the new load step.

#### Performance constraints (latency/throughput/memory):
- Bundle size grows by the combined image tar (plausibly > 2 GiB); publish duration grows by two image builds plus the save. No install-time health-poll changes: the existing 90 s `Wait-ComposeHealthy` timeout stands, and `up` no longer pays any build time.

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access): Docker Desktop running on both publish and install hosts (install side already enforced by Stage 4 `Test-DockerAvailable`); publish host has network access for base-image pulls; install host needs no network or registry auth.
- Constraints (budget, performance, compatibility): 500-line file cap (drives the two new modules); PowerShell change budget routes through `powershell-orchestrator`; no new external module dependencies; no temp files in tests.
- External dependencies (services, libraries, releases): public base images `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `ghcr.io/openclaw/openclaw:latest` (publish-time only).

## Data / API / Config Impact
- User-facing or API changes: none to CLI parameters; bundle contents change (adds `docker/openclaw-images.tar` and `Install.Docker.psm1`; bundle compose is transformed; `docker-compose.dev.yml` no longer shipped).
- Data or migration considerations: none; no schema change beyond the manifest `size` widening, which the reader already accepts.
- Logging/telemetry updates (if any): new stage messages for image build/save (publish) and image load (install), following existing `Write-Information` conventions.
- Compatibility notes (CLI flags, config schemas, versioning): `-SkipDocker` gates the new step; bundle compose pins `openclaw/*:<version>` while loaded images also carry `pre-mvp` for on-host dev/manual-compose compatibility.

## Test Strategy
Confirmed approach (maintainer decision, 2026-07-10): ship prebuilt images. Framework: Pester v5 under `tests/scripts/` mirroring production layout; mock the `Invoke-DockerExe` seam, never the `docker` executable; string fixtures only; no temp files (prohibited by `.claude/rules/general-unit-test.md`).

- Regression tests to add or update:
  - `tests/scripts/Publish.Docker.Tests.ps1` (new): `Convert-ComposeToBundleCompose` on string fixtures — build-block removal for both services, `pull_policy: never` insertion, versioned `image:` rewrite, byte-preservation of all other lines, throw-on-drift (missing/renamed `build:` or `image:` keys); exact `docker build` and `docker save` argument vectors asserted via `Invoke-DockerExe` mock; `-WhatIf` produces zero seam invocations.
  - `tests/scripts/Install.Docker.Tests.ps1` (new): `Invoke-DockerImageLoad` happy path (correct `load -i <path>` argument vector), missing-tar throw (message names the path), non-zero-exit throw, `-WhatIf` produces zero seam invocations.
  - `tests/scripts/Publish.Helpers.Tests.ps1`: size-type case updated for `[long]` including a value > 2,147,483,647 (`[int]::MaxValue`); dev-compose copy case updated for removal; export-count and staged-file-order cases updated for `Install.Docker.psm1`.
  - `tests/scripts/Publish.Tests.ps1`: helper-order assertion incorporates Stage 3b; staging assertion includes the new module.
  - `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Force.Tests.ps1`: stage-sequence mocks register the load step before `Invoke-ComposeUp`.
- Edge cases and negative scenarios: compose drift (transform throws); tar missing at load; `docker load`/`docker build`/`docker save` non-zero exits; `-SkipDocker` skips the load; `-Force` reinstall (idempotent load); file size above `[int]::MaxValue`.
- Error handling and logging verification: throw messages asserted for the missing-tar and drift paths; `ShouldProcess` (`-WhatIf`) verified for all state-changing helpers.
- Coverage impact and targets: uniform gates apply (line >= 85%, branch >= 75%); the transform pure function and argument-composition helpers are fully coverable without docker.
- Toolchain commands to run: PoshQC format -> PoshQC analyze -> full Pester suite; repeat until a single clean pass (per `.claude/rules/general-code-change.md` loop, PowerShell stages).
- Manual validation steps: integration retest of full `Publish.ps1` -> `Install.ps1` on a machine without the `src/` tree in the bundle; confirm `docker compose up` starts both services healthy from loaded images with no build or registry attempt.

## Acceptance Criteria
- [x] AC1 — Image tar produced: running `Publish.ps1` invokes `docker build` for both images and a single `docker save` writing `<BundleRoot>/docker/openclaw-images.tar` that names all four refs (`openclaw/core:<version>`, `openclaw/core:pre-mvp`, `openclaw/agent:<version>`, `openclaw/agent:pre-mvp`). Verified by Pester argument-vector assertions against the mocked `Invoke-DockerExe` seam.
- [x] AC2 — Build args mirror compose: the core build passes `-f deploy/docker/openclaw-core.Dockerfile --target runtime --build-arg BUILD_CONFIGURATION=<Configuration>`; the agent build passes `-f deploy/docker/openclaw-agent.Dockerfile --build-arg OPENCLAW_AGENT_IMAGE=<resolved-from-.env-or-default>`; both use the repo root as context. Verified by exact arg-vector tests plus a code comment cross-referencing `docker-compose.yml`.
- [x] AC3 — Bundle compose transformed: the emitted bundle `docker/docker-compose.yml` contains no `build:` key, contains `pull_policy: never` on both `openclaw-core` and `openclaw-agent`, references `image: openclaw/core:<version>` and `image: openclaw/agent:<version>`, and preserves every other line of the tracked compose. Verified by pure-function tests on string fixtures.
- [x] AC4 — Transform fails fast on drift: `Convert-ComposeToBundleCompose` throws when the expected `build:`/`image:` keys for either service are absent, rather than emitting a partial compose. Verified by negative-path unit tests.
- [x] AC5 — Dev compose path unaffected: the tracked repo `docker-compose.yml` still contains both `build:` blocks and the `openclaw/*:pre-mvp` image tags (no diff to the tracked file in the change set).
- [x] AC6 — Manifest correct at any size: `manifest.json` includes `docker/openclaw-images.tar` with a correct SHA-256, and `New-ManifestEntry` returns `size` as `[long]`, verified by a unit test using a length greater than 2,147,483,647 that would previously throw under the `[int]` cast.
- [x] AC7 — Install loads before compose up: `Install.ps1` Stage 9 calls `Invoke-DockerImageLoad` on `<DestDockerDir>/openclaw-images.tar` before `Invoke-ComposeUp`, and the entire step is skipped under `-SkipDocker`. Verified by stage-sequence tests in `Install.Tests.ps1` / `Install.Force.Tests.ps1`.
- [x] AC8 — Load failure modes are explicit: `Invoke-DockerImageLoad` throws with remediation text naming the expected tar path when the tar is missing, and throws on non-zero `docker load` exit. Verified by unit tests.
- [x] AC9 — Wrapper seam in place: `scripts/Publish.Docker.psm1` and `scripts/Install.Docker.psm1` exist, each under 500 lines, each defining a module-scoped `Invoke-DockerExe -DockerArgs <string[]>` seam; every docker invocation introduced by this fix routes through the seam; the four pre-existing direct `docker` call sites in `Install.Helpers.psm1` are unmodified.
- [x] AC10 — Bundle staging complete: `Copy-InstallScriptsIntoBundle` stages `Install.Docker.psm1` into the bundle, and `Copy-DockerArtifact` no longer copies `docker-compose.dev.yml`. Verified by updated `Publish.Helpers.Tests.ps1` cases.
- [x] AC11 — Tests are hermetic: `tests/scripts/Publish.Docker.Tests.ps1` and `tests/scripts/Install.Docker.Tests.ps1` mock only the `Invoke-DockerExe` seam (no real docker, no `global:docker` shim for new code, no temp files) and cover positive, negative, and `-WhatIf` paths.
- [x] AC12 — No regression, clean toolchain: the full existing Pester suite passes with the documented test updates applied, and PoshQC format + analyze report clean on all new and modified PowerShell files, in a single full pass.

## Risks & Mitigations
- Technical or operational risks:
  - Build-arg drift: if the tracked compose `build:` args change, the publish-side `docker build` vectors silently diverge.
  - Compose-structure drift: future edits to the tracked compose could break the line-based transform.
  - Bundle size/time growth: multi-GiB tar increases publish duration, disk use, and copy time at install.
  - Floating-tag re-pointing: loading an older bundle re-points `pre-mvp` on the host.
  - Publish-time network dependency: base-image pulls can fail transiently.
- Mitigations and rollbacks:
  - Cross-reference comment in the build helper plus exact arg-vector Pester assertions (AC2) make drift a test failure.
  - Transform throws on unexpected structure (AC4) instead of emitting a wrong compose.
  - Size growth documented in publish output; manifest `[long]` fix (AC6) removes the overflow failure mode.
  - Bundle compose pins versioned tags (AC3), so `pre-mvp` re-pointing is harmless to installed stacks.
  - Network failures fail fast with specific docker errors; retry is the remediation. Full rollback is a commit revert with no tracked-compose impact.

## Rollout & Follow-up
- Release/rollout steps: merge; run `Publish.ps1` to produce the next versioned bundle; execute the integration retest (`Publish.ps1` -> `Install.ps1` on a machine without `src/` in the bundle); confirm both services healthy via existing `Wait-ComposeHealthy`.
- Post-fix monitoring or clean-up tasks: watch bundle size and publish duration; follow-ups (explicitly out of scope here): retrofit the four direct `docker` call sites in `Install.Helpers.psm1` onto a wrapper seam; optional `docker rmi` of versioned images in `Uninstall.ps1`.
- Links: issue #142 (https://github.com/drmoisan/open-claw-bridge/issues/142); `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/issue.md`; research: `research/2026-07-10T00-00-installer-docker-images-not-bundled-research.md`; rules: `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`.
