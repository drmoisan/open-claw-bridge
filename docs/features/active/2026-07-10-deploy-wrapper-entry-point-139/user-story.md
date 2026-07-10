# `deploy-wrapper-entry-point` — User Story

- Issue: #139
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-10

## Story Statement

- As an operator, I want to run a single command that publishes and installs an
  OpenClaw bundle end-to-end, so that I do not have to manually run
  `Publish.ps1`, read its output, and separately locate and invoke the staged
  `Install.ps1`.
- As an operator, I want `Deploy.ps1` to fail before attempting install when
  publishing fails, so that I do not install a partial or stale bundle.

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

## Personas & Scenarios

- **Persona: Release operator**
  - Who: an engineer or release manager responsible for building and installing
    OpenClaw bundles on a target Windows host.
  - Cares about: a single reliable command, predictable output, and not having
    to remember the staged-vs-source-tree distinction between
    `scripts/Install.ps1` and `<bundleRoot>\Install.ps1`.
  - Constraints: runs PowerShell 7+ locally or on a deployment host; may need
    to run elevated when installing an unsigned bundle; must supply operator
    secret file paths (Docker `.env`, Anthropic `.env`) without those files being
    copied or read by the wrapper itself.
  - Goals and frustrations: wants one command for the common case; is
    currently frustrated by having to manually chain `Publish.ps1` output into
    a second `Install.ps1` invocation, and by `Publish.ps1` printing extra
    lines when its output is captured.
  - Context and motivations: deploying a new OpenClaw version to a workstation
    or lab host as part of a release or upgrade.

- **Scenario: End-to-end deploy of a signed release**
  - Who is acting: the release operator.
  - What triggered the action: a new version is ready to build and install.
  - Steps taken: operator runs
    `& .\scripts\Deploy.ps1 -Version '1.2.3.0' -DockerEnvFilePath <path> -AnthropicEnvFilePath <path>`.
  - Obstacles/decisions: none in the happy path; `Deploy.ps1` runs
    `Publish.ps1`, captures the single returned bundle-root string, then
    invokes the staged `Install.ps1` inside that bundle.
  - Expected outcome: the command returns the bundle root; the bundle is
    installed; the operator's working directory is unchanged throughout.

- **Scenario: Unsigned bundle requiring elevation**
  - Who is acting: the release operator, testing a local unsigned build.
  - What triggered the action: operator runs
    `& .\scripts\Deploy.ps1 -Version '1.2.3.0' -SkipSign` from an elevated
    session.
  - Steps taken: `Deploy.ps1` forwards `-SkipSign` to `Publish.ps1` and maps it
    to `-AllowUnsigned` on the staged `Install.ps1` call.
  - Obstacles/decisions: if the session is not elevated, `Install.ps1`'s Stage 0
    administrator precheck throws before any install side effect; `Deploy.ps1`
    does not duplicate or suppress that check.
  - Expected outcome: in an elevated session, the unsigned bundle installs
    successfully; in a non-elevated session, the operator sees a clear error
    before any install action is attempted.

- **Scenario: Publish failure stops the deploy**
  - Who is acting: the release operator.
  - What triggered the action: a publish-stage failure (for example, an
    invalid `-CertThumbprint` or a build error).
  - Steps taken: operator runs `& .\scripts\Deploy.ps1 -Version 'bad'`.
  - Obstacles/decisions: `Publish.ps1` throws, or returns no bundle root.
  - Expected outcome: `Deploy.ps1` does not attempt to invoke any
    `Install.ps1`; the failure propagates to the operator with a clear error.

- **Scenario: Dry run with `-WhatIf`**
  - Who is acting: the release operator, previewing a deploy before committing
    to it.
  - What triggered the action: operator runs
    `& .\scripts\Deploy.ps1 -Version '1.2.3.0' -WhatIf`.
  - Obstacles/decisions: `-WhatIf` propagates to both the publish invocation and
    the install invocation.
  - Expected outcome: no real install side effects occur; the operator sees
    what would be run for both stages.

## Acceptance Criteria

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

## Non-Goals

- Changing `Install.ps1`'s behavior, parameter surface, or its Stage 0
  administrator precheck.
- Changing `Publish.Helpers.psm1` or `Publish.Msix.psm1` return behavior,
  bundle layout, or manifest schema.
- Staging, reading, or validating the contents of operator secret files (Docker
  `.env`, Anthropic `.env`); `Deploy.ps1` forwards only the path strings.
- Introducing a feature flag, staged rollout, or new persisted state —
  `Deploy.ps1` is a stateless composition of the two existing entry points.
