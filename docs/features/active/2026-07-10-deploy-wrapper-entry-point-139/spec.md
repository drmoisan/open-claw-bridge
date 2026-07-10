# deploy-wrapper-entry-point — Spec

- **Issue:** #139
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-10
- **Status:** Draft
- **Version:** 0.1

## Overview

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

## Behavior

Add a first-class `scripts/Deploy.ps1` wrapper that runs the publish stage,
captures the returned bundle root, then invokes the staged `Install.ps1` inside
that bundle, without changing the caller's working directory. Fix the
`Publish.ps1` output contract so a captured invocation emits exactly one pipeline
object: the bundle root path. This is primarily a feature addition; the defect
fix is bundled as required accompanying work and does not warrant a separate
issue.

`Deploy.ps1` behavior:

- Runs `scripts/Publish.ps1`, capturing its return value as the bundle root.
- Invokes `<bundleRoot>\Install.ps1` — the staged copy of `Install.ps1` placed
  inside the produced bundle by `Copy-InstallScriptsIntoBundle` — never
  `scripts/Install.ps1` directly. The staged copy's `$PSScriptRoot` resolves to
  the bundle root, giving `Install.ps1` correct self-location without
  `Deploy.ps1` needing to forward a `-SourcePath` value.
- Never changes the caller's working directory (no `Set-Location` /
  `Push-Location`); both child invocations use fully-resolved absolute paths.
- Fails fast: if the publish stage throws, the exception propagates uncaught and
  `Install.ps1` is never invoked. If the publish stage returns a null or empty
  bundle root, `Deploy.ps1` throws an explicit, diagnostic error before
  attempting install.
- Returns the bundle root on success, matching `Publish.ps1`'s own return
  contract so `Deploy.ps1` is drop-in composable with any caller that already
  expects a scalar bundle-root string.

`Publish.ps1` fix:

- At the three call sites in `Publish.ps1`'s stage 4 and stage 6 blocks —
  `Invoke-VersionStamp`, `Invoke-MakeAppx`, and `Write-PublishManifest` — assign
  the call's return value to `$null` (`$null = Invoke-VersionStamp ...`) so it is
  not emitted to the success-output stream. No other call site in `Publish.ps1`
  leaks output; this is the only defect.
- The three helper functions themselves are unchanged: `Publish.Msix.psm1` and
  `Publish.Helpers.psm1` continue to return their path values exactly as before.
  Their own dedicated Pester suites (`Publish.Msix.Tests.ps1`,
  `Publish.Helpers.Tests.ps1`) call the functions directly and assert on the
  returned string; those suites are unaffected because this fix touches only the
  `Publish.ps1` call sites, not the functions' `return` statements.

## Inputs / Outputs

- **Inputs (CLI flags):**
  - Publish-side pass-through (forwarded to `scripts/Publish.ps1`):
    `-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`.
  - Install-side pass-through (forwarded to the staged `Install.ps1`):
    `-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`.
  - Common parameter: `-WhatIf` (via `SupportsShouldProcess`), propagated to
    both child invocations.
- **Derived/mapped input:** when `-SkipSign` is set on `Deploy.ps1`, the
  install-side parameter set includes `-AllowUnsigned` (an unsigned bundle
  requires it at install time; `Install.ps1`'s Stage 0 administrator precheck
  applies unchanged — `Deploy.ps1` does not duplicate or bypass that check).
- **Outputs:** the bundle root path (`[string]`), returned on success; console
  logging via `Write-Information`, matching the existing `[publish]` /
  `[install]`-prefixed logging convention already used by `Publish.ps1` and
  `Install.ps1`.
- **Config keys and defaults:** none introduced. `Deploy.ps1` holds no
  persisted state or `.env`/manifest keys of its own; state ownership stays with
  `Publish.ps1` (bundle contents, `.env`) and `Install.ps1` (install record at
  `%LOCALAPPDATA%\OpenClaw\install-record.json`).
- **Versioning / backward-compatibility:** `Publish.ps1`'s return contract
  becomes a single scalar string (previously an unintentional multi-element
  array); any existing caller that already discarded output (piped to
  `Out-Null` or ignored the return value) is unaffected. A caller that happened
  to depend on the leaked array shape is out of scope — the leak is a defect,
  not a documented contract.

## API / CLI Surface

- `scripts/Deploy.ps1 [-Version <string>] [-Configuration <string>] [-CertThumbprint <string>] [-SkipSign] [-SkipDocker] [-DockerEnvFilePath <string>] [-AnthropicEnvFilePath <string>] [-Force] [-WhatIf]`
- Example invocations:
  - `& .\scripts\Deploy.ps1 -Version '1.2.3.0'` — publish and install a signed
    bundle at version `1.2.3.0`; returns the bundle root string.
  - `& .\scripts\Deploy.ps1 -Version '1.2.3.0' -SkipSign` — publish an unsigned
    bundle, then install it with `-AllowUnsigned` forwarded (requires an
    elevated session per `Install.ps1`'s Stage 0 precheck).
  - `& .\scripts\Deploy.ps1 -Version '1.2.3.0' -SkipDocker -Force -WhatIf` —
    simulates both stages; publish's internal state-changing sub-steps no-op
    under `-WhatIf`, and the install stage is skipped under
    `$PSCmdlet.ShouldProcess`.
- **Contracts and validation rules:**
  - `Deploy.ps1` does not attempt install when publish throws or returns an
    empty/null bundle root (explicit `throw` with a diagnostic message).
  - `Deploy.ps1` never invokes `scripts/Install.ps1` directly — only the staged
    `<bundleRoot>\Install.ps1`.
  - `Deploy.ps1` does not stage, read, or otherwise open the files referenced by
    `-AnthropicEnvFilePath` / `-DockerEnvFilePath`; these are forwarded to
    `Install.ps1` as path strings only.

## Data & State

- **Data transformations and invariants:** `Deploy.ps1` performs no data
  transformation of its own; it captures the bundle-root string returned by the
  publish stage and passes it, unmodified, as the resolved base path for the
  install-stage invocation (`Join-Path $bundleRoot 'Install.ps1'`).
- **Caching or persistence details:** none. No new files, caches, or records
  are introduced by `Deploy.ps1`.
- **Migration or backfill requirements:** none.

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
- **Exactly 2 production files change.** `scripts/Publish.ps1` (modified,
  3-line output-suppression fix) and `scripts/Deploy.ps1` (new). No third
  production module (for example a `Deploy.Helpers.psm1`) is introduced. The
  child-invocation design seam is implemented as wrapper functions defined at
  script scope inside `Deploy.ps1` itself (for example `Invoke-PublishScript`
  and `Invoke-InstallScript`), each guarded with the established
  `if (-not (Get-Command -Name '<FunctionName>' -ErrorAction SilentlyContinue))`
  pattern already used in `scripts/Install.ps1` for `Test-IsElevatedAdmin` and
  `Invoke-HostAdapterStart`. This guard lets Pester tests override the wrapper
  with a `global:`-scoped function before invoking `Deploy.ps1` via `&`, without
  requiring a separate module for `Mock` to target.
- `Deploy.ps1` must never call `Set-Location` / `Push-Location` toward the
  bundle directory; both child invocations use fully-resolved absolute paths, so
  the caller's `$PWD` is unaffected regardless of success or failure.

## Implementation Strategy

- **Implementation scope:**
  - `scripts/Publish.ps1`: assign `$null =` at the three leaking call sites
    (`Invoke-VersionStamp`, `Invoke-MakeAppx`, `Write-PublishManifest`) in
    stages 4 and 6. No other line changes.
  - `scripts/Deploy.ps1` (new): `[CmdletBinding(SupportsShouldProcess = $true)]`
    top-level script with a parameter block covering all pass-through
    parameters listed above; two script-scope wrapper functions
    (`Invoke-PublishScript`, `Invoke-InstallScript`), each guarded by a
    `Get-Command`-existence check so tests can substitute a `global:`-scoped
    override; a main block that builds the publish-side and install-side
    parameter hashtables (including the `-SkipSign` -> `-AllowUnsigned`
    mapping), calls `Invoke-PublishScript`, guards against an empty/null
    return, calls `Invoke-InstallScript` against
    `Join-Path $bundleRoot 'Install.ps1'`, and returns the bundle root.
- **New classes/functions/commands to add or update:**
  - `Invoke-PublishScript` (script-scope wrapper in `Deploy.ps1`, guarded,
    `SupportsShouldProcess`, returns the bundle root from the underlying
    `Publish.ps1` call).
  - `Invoke-InstallScript` (script-scope wrapper in `Deploy.ps1`, guarded,
    `SupportsShouldProcess`, invokes the staged `Install.ps1`).
  - `Deploy.ps1` main block (parameter forwarding, fail-fast guard, return).
- **Dependency changes:** none. No new packages.
- **Logging/telemetry additions:** `Write-Information` calls at the start of
  the publish stage and the install stage (`[deploy]`-prefixed), matching the
  existing convention in `Publish.ps1` / `Install.ps1`; no new telemetry sinks.
- **Rollout plan:** no feature flag. `Deploy.ps1` is an additive, opt-in entry
  point; existing direct use of `Publish.ps1` and `Install.ps1` is unaffected
  and remains supported. No staged deploy is required because the change is
  local tooling, not a runtime/service change.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)
- [x] Parameter forwarding for both children (publish side and install side).
- [x] `-SkipSign` => `-AllowUnsigned` mapping to `Install.ps1`.
- [x] Publish-failure short-circuit (no install attempted).
- [x] `-WhatIf` propagation to both children.
- [x] Returned bundle root on success.
- [x] Regression: `Publish.ps1` emits exactly one pipeline object on a fully
      mocked run.
