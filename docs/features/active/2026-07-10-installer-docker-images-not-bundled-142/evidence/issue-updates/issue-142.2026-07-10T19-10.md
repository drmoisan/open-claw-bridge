# Issue #142 Update Mirror

Timestamp: 2026-07-10T19-10
PostedAs: unknown
POSTING DEFERRED: This execution pass makes no network/GitHub calls and runs no git commit (per coordinator directive). The comment below is prepared for the PR-authoring step to post to https://github.com/drmoisan/open-claw-bridge/issues/142.

## Intended comment text

Fixed the scripted-bundle install failure by moving container-image acquisition from install time to publish time.

Publish side:
- New `scripts/Publish.Docker.psm1` builds `openclaw-core` and `openclaw-agent` (mirroring the tracked `docker-compose.yml` build blocks) and writes a single combined `docker/openclaw-images.tar` naming all four refs (`openclaw/core:<version>`, `openclaw/core:pre-mvp`, `openclaw/agent:<version>`, `openclaw/agent:pre-mvp`).
- A new Stage 3b in `scripts/Publish.ps1` (after `Copy-DockerArtifact`, before MSIX and before the manifest) runs the build/save and writes a transformed bundle `docker/docker-compose.yml`: both `build:` blocks removed, `pull_policy: never` added to both services, and each `image:` rewritten to the versioned tag. The transform throws on compose drift.
- `Copy-DockerArtifact` no longer ships `docker-compose.dev.yml` (dead at install time; its build context is not bundled).
- Enabling fix: `New-ManifestEntry` size cast widened `[int]` -> `[long]` so image tars above 2 GiB no longer overflow.

Install side:
- New `scripts/Install.Docker.psm1` (self-contained, staged into the bundle) exposes `Invoke-DockerImageLoad`, which `docker load`s the bundled tar. `Install.ps1` Stage 9 loads the images before `Invoke-ComposeUp`, gated by `-SkipDocker`. Missing-tar and non-zero-exit failures throw with remediation text.

Both new modules route every docker call through a module-scoped `Invoke-DockerExe -DockerArgs <string[]>` seam; unit tests mock only that seam (no real docker, no temp files). Full Pester suite: 406 passed, 0 failed; PoshQC format + analyze clean; coverage 89.91% (no regression). The tracked `docker-compose.yml` and the four direct docker call sites in `Install.Helpers.psm1` are unchanged.

Follow-ups (out of scope here):
- Retrofit the four direct `docker` call sites in `scripts/Install.Helpers.psm1` (`docker info`, `compose up -d`, `compose ps`, `compose down`) onto a wrapper seam.
- Optional `docker rmi` of the versioned images in `scripts/Uninstall.ps1`.
