# deploy-wrapper-entry-point (Issue #139)

- Date captured: 2026-07-10
- Author: drmoisan
- Status: Promoted -> docs/features/active/deploy-wrapper-entry-point/ (Issue #139)

- Issue: #139
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/139
- Last Updated: 2026-07-10
- Work Mode: full-feature

## Problem / Why

`scripts/Publish.ps1` produces a versioned, self-installing bundle under
`artifacts/publish/<version>/` and returns the bundle root. `scripts/Install.ps1`
self-locates via `$PSScriptRoot` and installs a bundle. There is no single
first-class entry point that runs publish and then install end-to-end; operators
must run `Publish.ps1`, read the bundle root, then invoke the staged
`Install.ps1` inside the produced bundle by hand.

A required, already-verified defect accompanies this gap: `Publish.ps1` leaks
intermediate pipeline output. `Invoke-VersionStamp`, `Invoke-MakeAppx`, and
`Write-PublishManifest` return paths that are not suppressed at their call sites
(`Publish.ps1` stages 4 and 6), so capturing the script's output yields an array
(AppxManifest.xml path, .msix path, manifest.json path, bundle root) instead of
just the bundle root. This makes the return value unreliable for any caller that
captures it — including the proposed deploy wrapper. Verified from a real publish
log on 2026-07-09.

## Proposed Behavior

Add a first-class `scripts/Deploy.ps1` wrapper that runs the publish stage,
captures the returned bundle root, then invokes the staged `Install.ps1` inside
that bundle, without changing the caller's working directory. Fix the
`Publish.ps1` output contract so a captured invocation emits exactly one pipeline
object: the bundle root path. This is primarily a feature addition; the defect
fix is bundled as required accompanying work and does not warrant a separate
issue.

## Acceptance Criteria (early draft)

- [x] `Publish.ps1` emits exactly one pipeline object (the bundle root) when its
      output is captured; helper return behavior is unchanged.
- [x] New `scripts/Deploy.ps1` runs `Publish.ps1`, captures the bundle root, then
      invokes `<bundleRoot>\Install.ps1` (the staged copy) without changing the
      caller's working directory.
- [x] `Deploy.ps1` forwards publish parameters (`-Version`, `-Configuration`,
      `-CertThumbprint`, `-SkipSign`) and install parameters (`-SkipDocker`,
      `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`); `-SkipSign` maps
      to `-AllowUnsigned` on `Install.ps1`.
- [x] `Deploy.ps1` uses `CmdletBinding(SupportsShouldProcess = $true)` and
      propagates `-WhatIf` to both child invocations.
- [x] `Deploy.ps1` fails fast: if publish throws or returns no bundle root, it
      does not attempt the install; on success it returns the bundle root.
- [x] Pester tests cover both scripts; child-script invocations are mocked via a
      wrapper-function seam; no temp files; coverage floors met.

## Constraints & Risks

- PowerShell 7+, repo standards per `.claude/rules/powershell.md` (advanced
  functions, approved verbs, under 500 lines, no `Invoke-Expression`, no
  plaintext secrets).
- Out of scope: any change to `Install.ps1`, `Publish.Helpers.psm1` return
  behavior, bundle layout, or manifest schema.
- `Deploy.ps1` only forwards `-AnthropicEnvFilePath` / `-DockerEnvFilePath` path
  strings; it must not stage or read operator secret files.
- Scope estimate: 2 production files (`Publish.ps1` modified, `Deploy.ps1` new)
  plus 2 test files — at the direct-mode budget boundary.

## Test Conditions to Consider

- [ ] Parameter forwarding for both children (publish side and install side).
- [ ] `-SkipSign` => `-AllowUnsigned` mapping to `Install.ps1`.
- [ ] Publish-failure short-circuit (no install attempted).
- [ ] `-WhatIf` propagation to both children.
- [ ] Returned bundle root on success.
- [ ] Regression: `Publish.ps1` emits exactly one pipeline object on a fully
      mocked run.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/deploy-wrapper-entry-point/` folder from the template
