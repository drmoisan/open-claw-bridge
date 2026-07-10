# installer-docker-images-not-bundled (Issue #142)

- Date captured: 2026-07-10
- Author: drmoisan
- Status: Promoted -> docs/features/active/installer-docker-images-not-bundled/ (Issue #142)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #142
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/142
- Last Updated: 2026-07-10
- Work Mode: full-bug

## Summary

The scripted-bundle install (`Install.ps1`) fails at the `docker compose up` stage because the bundle does not ship the container images or the source needed to build them. The compose service `openclaw-core` builds from `deploy/docker/openclaw-core.Dockerfile`, whose `COPY . .` + `dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj` requires the `src/` tree, but the bundle `docker/` directory (the build context at install time) ships only the compose files, `.env.example`, and `deploy/docker/**`.

## Environment

- OS/version: Windows 11 Pro 10.0.26200
- Python version: n/a (PowerShell 7 / .NET 10 / Docker Desktop)
- Command/flags used: `.\Install.ps1 -DockerEnvFilePath ... -AnthropicEnvFilePath ...` from `artifacts\publish\1.0.2.3`
- Data source or fixture: published bundle `artifacts/publish/1.0.2.3`

## Steps to Reproduce

1. Run `.\scripts\Publish.ps1` to produce a versioned bundle.
2. Prepare operator config and run `.\Install.ps1` from the bundle root.
3. Install proceeds through HostAdapter start and MSIX install, then runs `docker compose up`.

## Expected Behavior

The compose stack (`openclaw-core`, `openclaw-agent`) starts from images delivered by the bundle without requiring the .NET SDK, the `src/` tree, or registry access on the target machine.

## Actual Behavior

`docker compose up` falls back to building the images (the `openclaw/core:pre-mvp` / `openclaw/agent:pre-mvp` tags are not in any registry: `pull access denied`), and the `openclaw-core` build fails:

```
#24 [openclaw-core build 4/5] RUN dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj
#24 MSBUILD : error MSB1009: Project file does not exist.
#24 Switch: ./src/OpenClaw.Core/OpenClaw.Core.csproj
target openclaw-core: failed to solve: ... exit code: 1
Exception: docker compose up failed (exit 1) ...
```

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet: see Actual Behavior (MSB1009 at build stage; `pull access denied` for both `image:` tags).

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Install cannot complete via the recommended scripted-bundle path.

## Suspected Cause / Notes

- `docker-compose.yml` `openclaw-core` and `openclaw-agent` declare `build: { context: . }`, resolved against the bundle `docker/` directory at install time, which lacks `src/`.
- `Copy-DockerArtifact` (`scripts/Publish.Helpers.psm1`) copies compose + `.env.example` + `deploy/docker/**` into the bundle but neither the source nor prebuilt images.
- Files to inspect: `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `docker-compose.yml`, `deploy/docker/*.Dockerfile`.

## Proposed Fix / Validation Ideas

Confirmed approach (maintainer decision, 2026-07-10): ship prebuilt images.

- Publish builds `openclaw-core` and `openclaw-agent`, `docker save`s them into the bundle (`docker/images.tar`), version-tags the images, and records the tar in `manifest.json`.
- Publish emits a transformed compose into the bundle that removes the `build:` blocks and sets `pull_policy: never`; the tracked repo compose keeps `build:` for dev.
- Install `docker load`s the tar before `docker compose up`.
- [x] Unit coverage areas: Pester tests for the new Publish save/transform helpers and the Install load helper (via a `docker` wrapper seam, mocked).
- [x] Integration scenario to retest: full `Publish.ps1` → `Install.ps1` on a machine without the `src/` tree present in the bundle.
- [x] Manual verification notes: `docker compose up` from the installed bundle starts both services healthy.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch
