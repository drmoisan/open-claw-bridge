Timestamp: 2026-07-12T11-50

## POSTING BLOCKED

Reason: the delegating instructions for this execution explicitly state "Do not commit or push anything — leave the working tree with your changes uncommitted. I will handle git operations (staging, commit, review) myself after your report." No instruction authorized posting a live comment or body update to GitHub issue #147 in this session, so this artifact is a local mirror only; it has not been posted anywhere.

## Exact text intended for issue #147 (comment)

---

Fix implemented for the installer image-version-alignment gap:

- **`scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`** gains two new pure, exported functions: `ConvertFrom-OpenClawImageReference` (splits a `repo:tag` reference on the last `:`, returning an empty `Tag` rather than throwing when no `:` is present) and `Test-OpenClawImageVersionAligned` (compares the Control UI/`openclaw/core` and gateway/`openclaw/agent` image tags against the resolved bundle version, returning a `Get-OpenClawValidationResult`-shaped object). Both are listed in `OpenClawContainerValidation.psd1`'s `FunctionsToExport`.
- **`scripts/Install.ps1`** Stage 9 gains a small, self-contained guard (`Get-ComposeServiceImageTag` + `Assert-ComposeImageVersionAligned`), invoked immediately before `Invoke-DockerImageLoad` inside the existing `-SkipDocker`-gated block. It reads the staged `docker-compose.yml`, extracts the `openclaw/core` and `openclaw/agent` pinned tags, and aborts with an actionable error (naming both image references, both tags, `$ResolvedVersion`, and `$ComposeFilePath`) when either tag is absent, malformed, or does not match the resolved bundle version — including the same-wrong-version-on-both-sides case.
- Per the ratified design decision in `spec.md`, `Install.ps1` does **not** `Import-Module` `OpenClawContainerValidation.psm1` (that module is not part of the bundle-staged file set); the guard intentionally duplicates a small, bounded comparison instead.
- New unit tests: `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.ImageVersionAlignment.Tests.ps1` (12 tests). Extended: `tests/scripts/Install.DockerStage.Tests.ps1` with a new `Context 'image version alignment guard'` (5 tests covering matched, cross-service mismatch, same-wrong-version, missing-image-line, and `-SkipDocker` scenarios).
- Full PowerShell toolchain (PoshQC format, PoshQC analyze, Pester test with coverage) passes in a single clean pass across the full repository. Post-change coverage: `Install.ps1` 89.36% line / 86.55% instruction; `OpenClawContainerValidation.psm1` 92.90% line / 91.40% instruction — both above baseline and above the 85%/75% thresholds.
- Full regression run of the AC14-named test files (`Install.Tests.ps1`, `Install.Force.Tests.ps1`, `Install.Docker.Tests.ps1`, the six `Invoke-OpenClawContainerPathValidation.*.Tests.ps1` files, `Publish.Docker.Tests.ps1`, `Publish.Tests.ps1`, `Publish.Helpers.Tests.ps1`) shows 424/433 passing, with the 9 failures being the identical pre-existing baseline set in the `Invoke-OpenClawContainerPathValidation.*` files (environment-dependent, unrelated to this fix) — zero new failures.
- #142 and #144 invariants verified green (no changes to `docker-compose.yml`, `Install.Docker.psm1`, `Install.Helpers.psm1`, `Publish.Helpers.psm1`, `Invoke-OpenClawContainerPathValidation.ps1`, or the shared test fixture module).

---

`PostedAs: unknown` (posting was not attempted; see POSTING BLOCKED above).
