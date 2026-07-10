# Research: Installer fails because docker images are not bundled (issue #142)

- Date: 2026-07-10
- Issue: #142
- Feature folder: `docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/`
- Scope: verify root cause, define insertion points and mechanics for the maintainer-confirmed fix (ship prebuilt images in the publish bundle; `docker load` at install; transformed bundle compose without `build:`).

## 1. Root-Cause Verification (confirmed)

All claims below were verified by reading the current files.

| Evidence | File / line | Finding |
|---|---|---|
| Both services declare `build: { context: . }` | `docker-compose.yml:5-10` (core), `:53-57` (agent) | Build context is the compose project directory. |
| Core Dockerfile requires the repo `src/` tree | `deploy/docker/openclaw-core.Dockerfile:11-12` — `COPY . .` then `RUN dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj` | Restore fails with `MSB1009` when the context lacks `src/`. |
| Bundle `docker/` contains only compose files, `.env.example`, `deploy/docker/**` | `scripts/Publish.Helpers.psm1:64-119` (`Copy-DockerArtifact`) | No `src/` tree is shipped; the bundle context cannot satisfy `COPY . .` + restore. |
| Install runs compose with `--project-directory <DestDockerDir>` | `scripts/Install.Helpers.psm1:302-327` (`Invoke-ComposeUp`), called from `scripts/Install.ps1:421-427` (Stage 9) | `context: .` resolves to `%LOCALAPPDATA%\OpenClaw\<version>\docker`, which has no `src/`. |
| Image tags are local-only names | `docker-compose.yml:11` (`openclaw/core:pre-mvp`), `:58` (`openclaw/agent:pre-mvp`) | Not present in any registry; compose falls back from pull to `build:`, which fails. |
| Agent Dockerfile is registry-dependent only for its base | `deploy/docker/openclaw-agent.Dockerfile:3-4` — `ARG OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest` | Base pull needs network at build (publish) time only. |

Root cause as diagnosed by the orchestrator is confirmed: at install time compose cannot pull (`pull access denied` for nonexistent registry images) and cannot build (bundle context lacks `src/`), so Stage 9 `docker compose up` fails.

## 2. Current State Analysis

### Publish pipeline (`scripts/Publish.ps1`)

Stages (main block, lines 106-249): Stage 00/0a/0b/0c version + signing resolution; Stage 1 bundle-root clean/create; Stage 2 per-project `dotnet publish`; **Stage 3 `Copy-DockerArtifact`** (line 203-206) writes the bundle `docker/` dir; Stage 4 MSIX; Stage 5 `Copy-InstallScriptsIntoBundle`; Stage 6 `Write-PublishManifest` (walks every bundle file, SHA-256 per file). Helper modules: `Publish.Helpers.psm1` (357 lines), `Publish.Msix.psm1`, `Publish.Env.psm1` (`Get-EnvFileMap` is available for resolving `OPENCLAW_AGENT_IMAGE` from the repo `.env`).

### Install pipeline (`scripts/Install.ps1`)

Stages: 0 precheck; 1-2 bundle select + manifest integrity; 3 prior-install/-Force; **4 `Test-DockerAvailable`** (`docker info` probe, `Install.Helpers.psm1:285-300`); 5 `Copy-BundleContents` (copies `executables/` and `docker/` subtrees — a tar inside `docker/` is copied automatically, `Install.Helpers.psm1:126-159`); 6 `.env` guard; 7/7a/8/8b/8.5 HostAdapter + MSIX; **9 `Invoke-ComposeUp` + `Wait-ComposeHealthy`** (`Install.ps1:421-427`); 10 install record.

### Existing docker invocations (no wrapper seam yet)

`scripts/Install.Helpers.psm1` calls the `docker` executable directly at four sites: line 295 (`docker info`), 323 (`compose ... up -d`), 355 (`compose ps --format json`), 424 (`compose down`). Tests cover these via a `function global:docker` shim (`tests/scripts/Install.Helpers.Compose.Tests.ps1:22-77`), not via a wrapper mock.

### File-size headroom (500-line cap)

- `scripts/Install.Helpers.psm1` is already 527 lines — **no new functions can be added there**; new install-side docker helpers require a new module.
- `scripts/Publish.Helpers.psm1` is 357 lines; adding build/save/transform helpers (~150+ lines with comment-based help) would approach the cap. A new publish-side module is the safe choice.
- `scripts/Install.ps1` is 445 lines; a one-call stage insertion fits. `scripts/Publish.ps1` is 250 lines; a stage insertion fits.

### Latent defect found: manifest size field overflows on large files

`New-ManifestEntry` casts file size to `[int]` (`Publish.Helpers.psm1:155`: `size = [int]$fileInfo.Length`). A combined image tar (aspnet 10.0 runtime image plus the node-based agent image) can plausibly exceed 2 GiB, and `[int]` conversion throws on values above 2,147,483,647, failing the publish manifest stage. `Test-ManifestIntegrity` already reads sizes as `[long]` (`Install.Helpers.psm1:97`). **The fix must change the manifest entry to `[long]`** and update the corresponding test (`Publish.Helpers.Tests.ps1` "returns size as non-negative integer"). This is a required enabling change, not optional hardening.

## 3. Candidate Approaches — compose transform mechanics

### Verified: no YAML library is available in-repo

`powershell-yaml` / `ConvertFrom-Yaml` / `ConvertTo-Yaml` appear nowhere in the repository (grep across all files: zero matches). Options evaluated:

1. **Add `powershell-yaml` dependency and round-trip the YAML.** Rejected: adds an external module dependency (violates the approved-dependency policy without strong cause), and YAML round-tripping reorders keys, re-quotes scalars, and can alter flow-style arrays (healthcheck `test:` lists), producing a noisy, hard-to-review transform of a security-sensitive file.
2. **`docker compose config` render-then-edit.** Rejected: `config` interpolates `${VAR:-default}` expressions at publish time, destroying install-time `.env` configurability (ports, polling intervals, `HOSTADAPTER_TOKEN_FILE`).
3. **Targeted line-based structural edit (recommended).** The compose file is repo-controlled, stable, two-space indented, and small (95 lines). A pure function operating on the file's lines can: (a) locate each service's `build:` key at 4-space indentation and drop lines until the next line at indentation <= 4; (b) after each `image:` line, insert `pull_policy: never` at the same indentation; (c) rewrite the `image:` tag. Deterministic, fully unit-testable with string fixtures (no temp files), and preserves every other byte of the file.

**Recommendation: approach 3**, implemented as a pure function (proposed name `Convert-ComposeToBundleCompose -ComposeContent <string[]> -Version <string>` returning `[string[]]`) plus a thin ShouldProcess wrapper that writes the transformed file into the bundle. The function should throw if it does not find exactly the expected `build:`/`image:` keys for `openclaw-core` and `openclaw-agent` (fail fast on compose drift rather than silently emitting a wrong bundle compose).

Rejected alternatives: powershell-yaml round-trip (new dependency, format churn); `docker compose config` (destroys install-time interpolation); maintaining a second hand-written `docker-compose.bundle.yml` in the repo (duplication drift — two files to keep in sync, the exact failure class issue #142 arose from).

### `docker-compose.dev.yml` handling

The dev compose (`docker-compose.dev.yml`) builds `openclaw-dev` from `.devcontainer/Dockerfile`, which is **not shipped in the bundle**, so the file is already unusable at install time; `Install.ps1` never references it. Recommendation: stop copying it into the bundle (remove from `Copy-DockerArtifact`) rather than transforming it. This requires updating the `Publish.Helpers.Tests.ps1` case "copies both compose files when present". Lower-effort fallback (copy it unchanged) is acceptable but ships a known-dead file.

## 4. Candidate Approaches — building the images at publish

1. **`docker compose build` against the repo compose file.** Reuses the tracked build definitions (no arg drift), but interpolates the whole file at publish time: missing `HOSTADAPTER_TOKEN_FILE` / `OPENCLAW_GATEWAY_TOKEN` produce warnings, and behavior around the missing `secrets/.env.anthropic` `env_file` at `build` is version-sensitive. Also tags only `pre-mvp`; version tags need a follow-up `docker tag` anyway.
2. **Direct `docker build` per image (recommended).** Explicit, interpolation-free, and testable through the wrapper seam:
   - Core: `docker build -f deploy/docker/openclaw-core.Dockerfile --target runtime --build-arg BUILD_CONFIGURATION=<Configuration> -t openclaw/core:<version> -t openclaw/core:pre-mvp <RepoRoot>` (mirrors `docker-compose.yml:5-10`).
   - Agent: `docker build -f deploy/docker/openclaw-agent.Dockerfile --build-arg OPENCLAW_AGENT_IMAGE=<resolved> -t openclaw/agent:<version> -t openclaw/agent:pre-mvp <RepoRoot>` (mirrors `docker-compose.yml:53-57`). Resolve `OPENCLAW_AGENT_IMAGE` from the repo-root `.env` via the existing `Get-EnvFileMap` (`Publish.Env.psm1`), defaulting to `ghcr.io/openclaw/openclaw:latest` (the Dockerfile's own `ARG` default, `openclaw-agent.Dockerfile:3`).

**Recommendation: approach 2.** Residual risk: if the compose `build:` args change, the publish build args must change in step; mitigate with a code comment cross-referencing `docker-compose.yml` and a Pester test asserting the exact arg vectors.

## 5. Image Tagging Design (item 4)

- `docker save` writes only the image references named on its command line into the tar's repository metadata. Saving all four refs (`openclaw/core:<version>`, `openclaw/core:pre-mvp`, `openclaw/agent:<version>`, `openclaw/agent:pre-mvp`) in one tar makes `docker load` restore **both** tags per image. This matches the maintainer directive (version tag plus `pre-mvp`).
- **The transformed bundle compose should reference the versioned tag** (`openclaw/core:<version>`), not `pre-mvp`. Rationale: `docker load` is a host-global operation; `pre-mvp` is a floating tag that any subsequent (or prior-version re-)install would re-point. A versioned reference makes each installed version's compose deterministic, makes `-Force` reinstall of an older bundle correct, and costs nothing because the transform function already receives `-Version`. The `pre-mvp` tags are still loaded, preserving dev/manual-compose compatibility on the install host.
- The tracked repo `docker-compose.yml` keeps `build:` + `image: openclaw/*:pre-mvp` unchanged, so `docker compose up --build` in the dev workflow is unaffected.

## 6. `docker save` / `docker load` Semantics (item 6)

- **Multi-image save is supported**: `docker save -o <file.tar> IMAGE [IMAGE...]` accepts multiple references and produces a single tar containing all layers (shared layers stored once) and all named tags. (Docker CLI reference, `docker image save`.)
- **`docker load -i <file.tar>` is idempotent** for a given tar: layers already present are skipped, tags are (re)applied to the same digests, exit code 0. Re-running install with the same bundle is safe. Loading an *older* bundle's tar re-points the shared `pre-mvp` tags to the older image — harmless under the versioned-tag compose recommendation above.
- `docker load` requires no network and no registry auth. The `pull_policy: never` on both services guarantees compose never contacts a registry and fails with an explicit "image not found locally" message if the load step was skipped — a clearer failure than today's `pull access denied` → build fallback → `MSB1009` chain.
- `pull_policy` is a Compose-spec service-level key supported by Docker Compose v2 (which the repo already requires; `Invoke-ComposeUp` uses `docker compose` v2 syntax).

## 7. Requirements Mapping — exact insertion points (item 2)

### Publish side

| Change | Location | Mechanics |
|---|---|---|
| New module `scripts/Publish.Docker.psm1` | new file | Contains: `Invoke-DockerExe -DockerArgs <string[]>` wrapper seam; `Build-OpenClawDockerImage` (or two thin build helpers); `Save-OpenClawDockerImage` (single `docker save -o <bundle docker>/openclaw-images.tar <4 refs>`); pure `Convert-ComposeToBundleCompose`; ShouldProcess writer for the transformed compose. Keeps `Publish.Helpers.psm1` under the 500-line cap. |
| New Stage 3b in `Publish.ps1` | after `Copy-DockerArtifact` (line 206), before Stage 4 MSIX | Resolve `OPENCLAW_AGENT_IMAGE` from `.env` map (already read at Stage 00); build both images; `docker save` into `<BundleRoot>/docker/openclaw-images.tar`; overwrite `<BundleRoot>/docker/docker-compose.yml` with the transformed content. Ordering before Stage 6 means `Write-PublishManifest` records the tar and the transformed compose automatically (no manifest code change needed for inclusion). |
| `Copy-DockerArtifact` | `Publish.Helpers.psm1:104-105` | Stop copying `docker-compose.dev.yml` (recommended; see §3). The plain `docker-compose.yml` copy can remain (Stage 3b overwrites it) or be skipped. |
| `New-ManifestEntry` | `Publish.Helpers.psm1:155` | `[int]` → `[long]` for `size` (required; see §2). |

### Install side

| Change | Location | Mechanics |
|---|---|---|
| New module `scripts/Install.Docker.psm1` | new file | `Invoke-DockerExe -DockerArgs <string[]>` wrapper plus `Invoke-DockerImageLoad -ImageTarPath <path>` (throws with remediation text when the tar is missing or `docker load` exits non-zero). Required because `Install.Helpers.psm1` is already 527 lines. |
| Stage the new module into the bundle | `Copy-InstallScriptsIntoBundle`, `Publish.Helpers.psm1:188` | Add `'Install.Docker.psm1'` to `$names`. Update the staging Write-Information text in `Publish.ps1:239` and the `Publish.Helpers.Tests.ps1` staging-order test. |
| Import + new load step in `Install.ps1` | import near lines 91-106; load step at Stage 9, immediately before `Invoke-ComposeUp` (line 425), gated by `-SkipDocker` | `Invoke-DockerImageLoad -ImageTarPath (Join-Path $DestDockerDir 'openclaw-images.tar')`. Placing it inside Stage 9 (after MSIX rollback-guarded stages) keeps the no-orphan invariant logic untouched; the tar was already copied to the destination by Stage 5 `Copy-BundleContents`. |
| `Uninstall.ps1` (optional follow-up) | — | `docker compose down` does not remove loaded images; an optional `docker rmi openclaw/core:<version> openclaw/agent:<version>` at uninstall is a scoping decision for the planner, not required to fix #142. |

### Change-budget note

The change set spans more than 2 production PowerShell files (`Publish.ps1`, `Publish.Helpers.psm1`, new `Publish.Docker.psm1`, `Install.ps1`, new `Install.Docker.psm1`), so per `.claude/rules/powershell.md` it exceeds the direct-mode budget and must be routed through `powershell-orchestrator`, batched at <= 3 production files per batch (natural split: publish-side batch, install-side batch).

## 8. Wrapper Seam (item 5)

- New code must use the wrapper seam `Invoke-DockerExe -DockerArgs <string[]>` (splat `docker @DockerArgs 2>&1`), per `.claude/rules/powershell.md` (parameter must not be named `Args`).
- Existing direct `docker` invocations in `Install.Helpers.psm1` (lines 295, 323, 355, 424) are tested through a `function global:docker` shim (`tests/scripts/Install.Helpers.Compose.Tests.ps1`). Refactoring them onto the wrapper would require rewriting those tests and touches a file already over the line cap. Recommendation: **do not refactor the four existing call sites in this feature**; scope the wrapper to the new modules and record the retrofit as a follow-up. This keeps the change budget bounded and avoids regression risk in Stage 9 health polling.
- Each new module should define its own `Invoke-DockerExe` (module-scoped) rather than sharing one across publish/install, because install-side modules must be self-contained inside the bundle (Install.ps1 imports only bundled files).

## 9. Behavior Semantics

- **Publish success**: bundle contains `docker/openclaw-images.tar` (single tar, 4 refs), a transformed `docker/docker-compose.yml` with no `build:` keys, `pull_policy: never` on both services, `image:` pinned to `openclaw/*:<version>`, and a `manifest.json` listing both (with `[long]` sizes). Tracked repo compose is byte-identical to before.
- **Publish failure modes (fail fast, specific messages)**: docker daemon unavailable at build; base-image pull failure (network); `docker save` non-zero exit; transform function unable to locate expected `build:`/`image:` keys (compose drift).
- **Install success**: Stage 9 loads the tar (idempotent), then `docker compose up -d` starts both services from local images without any registry or build attempt; `Wait-ComposeHealthy` semantics unchanged (image load does not alter container start_period; the existing 90 s timeout stands — arguably healthier now since no build time precedes `up`).
- **Install failure modes**: tar missing from destination (`throw` naming the expected path and re-publish remediation); `docker load` non-zero exit; compose "image not found locally" if load was bypassed.
- **Edge cases**: re-install same version with `-Force` (load is idempotent — safe); install older version after newer (versioned compose tag keeps it correct even though `pre-mvp` re-points); `-SkipDocker` (load step skipped, mirrors existing Stage 9 gating).

## 10. Testing Implications (item 7)

New tests (Pester v5, under `tests/scripts/`, mirroring production layout; no temp files; mock `Invoke-DockerExe`, never the `docker` executable, per `.claude/rules/powershell.md`):

- `tests/scripts/Publish.Docker.Tests.ps1` — transform pure function (build-block removal, `pull_policy` insertion, versioned image rewrite, everything-else preservation on string fixtures; throw-on-drift); exact `docker build`/`docker save` arg vectors via `Invoke-DockerExe` mock; `-WhatIf` produces zero invocations.
- `tests/scripts/Install.Docker.Tests.ps1` — `Invoke-DockerImageLoad` happy path, missing-tar throw, non-zero-exit throw, `-WhatIf`.

Existing tests that must not regress (and known required updates):

| Test file | Impact |
|---|---|
| `tests/scripts/Publish.Tests.ps1` | "calls helpers in the correct order with -SkipSign" (line 181) and the Copy-InstallScriptsIntoBundle ordering test (line 194) must incorporate the new Stage 3b helpers and staged file. |
| `tests/scripts/Publish.Helpers.Tests.ps1` | "exports the expected 7 helper functions" (line 46) if exports change; "copies both compose files when present" (line 104) if dev compose is dropped; "returns size as non-negative integer" (line 158) for the `[int]`→`[long]` change; "copies Install.ps1, Uninstall.ps1, ... in order" (line 253) for the added `Install.Docker.psm1`. |
| `tests/scripts/Install.Tests.ps1`, `Install.Force.Tests.ps1` | Stage-sequence assertions may need the new load step registered in their mock sets. |
| `tests/scripts/Install.Helpers.Compose.Tests.ps1`, `Install.Helpers.Tests.ps1` | Untouched under the recommended scope (no refactor of existing docker call sites). |

Coverage: uniform gates apply (line >= 85%, branch >= 75%); the transform pure function and arg-composition helpers are fully coverable without docker.

## Automation Feasibility

**Fully automatable; no human-interaction blocker identified.**

- Publish side: `docker build` (both images), `docker tag`-equivalent multi `-t` flags, and `docker save` are non-interactive CLI operations with deterministic exit codes. Base images (`mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `ghcr.io/openclaw/openclaw:latest`) are public — no `docker login`, no credential prompt. Prerequisites are exactly the already-documented set: Docker Desktop running on the publish host plus publish-time network access for base-image pulls.
- Install side: `docker load -i` is non-interactive, offline, idempotent, and requires no registry access; the only prerequisite is the already-required "Docker Desktop running" (enforced today by Stage 4 `Test-DockerAvailable`).
- Compose transform is a pure in-process text operation; no tooling or prompts.
- The one publish-time failure class that cannot be self-remediated by tooling is a network outage or upstream base-image removal; this fails fast with a specific docker error and is a retry condition, not a human-interaction requirement.
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events; no evidence artifacts were produced by this research task.

## Sources

- Repository files read in full: `docker-compose.yml`, `docker-compose.dev.yml`, `deploy/docker/openclaw-core.Dockerfile`, `deploy/docker/openclaw-agent.Dockerfile`, `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`; test inventories under `tests/scripts/`.
- Docker CLI reference semantics for `docker image save` (multiple `IMAGE` operands, tag preservation) and `docker image load` (offline, idempotent layer/tag restore); Compose specification service-level `pull_policy` (`never` supported by Docker Compose v2). These are stable, long-documented CLI behaviors consistent with the assistant knowledge cutoff (2026-01).
