# installer-image-version-alignment (Issue #147)

- Date captured: 2026-07-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/installer-image-version-alignment/ (Issue #147)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #147
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/147
- Last Updated: 2026-07-11
- Work Mode: full-bug

## Summary

The installer can stage a Control UI (openclaw/core) container image whose version does not match the gateway (openclaw/agent) container image, producing a broken runtime where the two containers disagree on their contract version.

## Environment

- OS/version: Windows 11 (installer host); Docker Desktop compose runtime
- Python version: n/a (PowerShell installer surface)
- Command/flags used: `.\Install.ps1` against a bundle produced by `scripts/Publish.ps1`
- Data source or fixture: bundled `docker/docker-compose.yml` (version-pinned `image:` lines) and `docker/openclaw-images.tar` (combined `docker save` of the two images at versioned and `pre-mvp` tags)

## Steps to Reproduce

1. Produce or hand-assemble an install bundle in which the staged `docker-compose.yml` `image:` tag for one service (for example `openclaw/core`) resolves to a different version than the other service (`openclaw/agent`), or in which the loaded image tar contains a version that does not match the compose `image:` tags.
2. Run `.\Install.ps1` from the bundle.
3. Observe that the installer loads the images and starts compose without detecting the cross-image version mismatch.

## Expected Behavior

The installer staging/validation surface should detect a version mismatch between the Control UI (openclaw/core) image and the gateway (openclaw/agent) image (and between the compose-pinned tags and the images actually available to load), fail fast with an explicit remediation message, and never start a compose stack in which the two container images disagree on version.

## Actual Behavior

The installer stages and starts a mismatched image pair without a version-alignment guard. There is no validation function in `OpenClawContainerValidation` that asserts image-version alignment, and `Install.ps1` performs no such check before `Invoke-DockerImageLoad` / `Invoke-ComposeUp`. The result is a runtime in which the Control UI and gateway containers run at different versions.

## Logs / Screenshots

- [ ] Attached minimal logs or screenshot
- Snippet: (to be captured during research/repro)

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

- Image build/save/compose-rewrite occurs in `scripts/Publish.ps1` and `scripts/Publish.Docker.psm1` (versioned + `pre-mvp` tags for both `openclaw/core` and `openclaw/agent`; compose `image:` lines rewritten to the versioned tag).
- Image load and compose start occur in `scripts/Install.ps1` (Stage 9) via `scripts/Install.Docker.psm1` (`Invoke-DockerImageLoad`).
- There is no image-version-alignment guard on the staging/validation surface.
- Fix must stay consistent with prior container-validation fixes #142 (installer-docker-images-not-bundled) and #144 (container-validation-stray-v1-and-env-target) and must not regress them.
- Fix scope is confined to: `scripts/Install.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, and tests under `tests/scripts`.

## Proposed Fix / Validation Ideas

- [ ] Unit coverage areas: a new image-version-alignment validation function in `OpenClawContainerValidation` (version parsing/comparison as pure logic), plus its wiring in `Install.ps1`.
- [ ] Integration scenario to retest: installer aborts on a mismatched image/compose pair; installer proceeds on a matched pair; #142/#144 behaviors remain green.
- [ ] Manual verification notes: confirm fail-fast message names both image versions and the remediation step.

## Next Step

- [ ] Promote to GitHub issue (bug-report template)
- [ ] Move to active fix folder / branch
