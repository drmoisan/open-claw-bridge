# 2026-04-18-unified-publish-script — Spec

- **Issue:** #34
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-18T00:00:00Z
- **Status:** Draft
- **Version:** 0.1

## Overview

This feature introduces `scripts/Publish.ps1`, a PowerShell 7+ script that produces a single versioned local artifact bundle at `artifacts/publish/<version>/` for every OpenClaw release candidate. The bundle contains published executables for every runnable `src/` project, the docker deployment artifact set required to stand up the containerized stack on another host, and the signed (or unsigned) MSIX installer. A top-level `manifest.json` enumerates every file in the bundle with its relative path, byte size, and SHA-256 hash.

The existing `scripts/build-msix.ps1` is retired. Its helper logic is extracted into a shared `scripts/Publish.Helpers.psm1` module consumed by the new publish script, so MSIX packaging remains a single testable code path. All in-repo callers of the old script are updated in this feature.

This iteration is strictly local-artifact emission. There is no remote upload, no GitHub Release creation, and no release-server integration. Those are follow-on features.

## Behavior

### Main path

1. Operator runs `.\scripts\Publish.ps1 -Version '1.2.3.0' -SkipSign` (dev) or `.\scripts\Publish.ps1 -Version '1.2.3.0' -CertThumbprint 'ABCDEF...'` (signed) on a Windows machine with the .NET 10 SDK and the Windows 10 SDK installed.
2. The script validates parameters. If `-SkipSign` is not supplied and `-CertThumbprint` is empty, the script throws immediately.
3. The script removes and recreates `artifacts/publish/<version>/` so each run starts clean.
4. For every runnable project in `src/`, the script runs `dotnet publish -c <Configuration> -o artifacts/publish/<version>/executables/<ProjectName>/ /p:Deterministic=true`. Progress is written to the information stream per project.
5. The script copies the docker artifact set into `artifacts/publish/<version>/docker/` (see Inputs / Outputs).
6. The script runs the MSIX pipeline (version stamp, layout assembly, PRI generation, `makeappx` pack, optional `signtool` sign) and places the output at `artifacts/publish/<version>/msix/OpenClaw.MailBridge_<version>_x64.msix`. Layout assembly sources its bridge and client inputs from the just-produced executables subdirectories, not from the legacy `artifacts/publish/bridge/` and `artifacts/publish/client/` paths.
7. The script computes SHA-256 for every file under `artifacts/publish/<version>/` (excluding `manifest.json` itself) and writes `manifest.json` last with one entry per file containing `path` (relative to the version root, forward-slash normalized), `size` (bytes, integer), and `sha256` (lowercase hex).
8. The script reports the artifact root path and exits 0.

### Failure paths

- Missing `.NET 10` SDK or `dotnet publish` non-zero exit: the script aborts with a terminating error naming the failing project and the exit code.
- Missing Windows SDK tools (`makeappx.exe`, `makepri.exe`, `signtool.exe`) when the MSIX stage runs: the script aborts with the tool name it could not locate.
- `-CertThumbprint` supplied but the certificate is not found in `Cert:\CurrentUser\My` or does not have a private key: `signtool.exe` returns non-zero; the script aborts with the raw tool output.
- `-SkipSign` not supplied and `-CertThumbprint` empty or whitespace: the script aborts before running any stage.
- A prior failed run leaves a stale `installer/staging/` directory: the MSIX stage removes and recreates the staging directory before assembly.

### Determinism behavior

- Non-binary artifacts (JSON, XML, shell scripts, markdown, icon PNGs) are byte-identical across runs given identical inputs.
- Binary artifacts (`.dll`, `.exe`) are made reproducible to the extent the toolchain allows via `/p:Deterministic=true`. `PublishReadyToRun=true` (set in the existing MSIX publish profile) retains residual non-determinism for those binaries; the manifest entries for those files still record the hashes of the produced artifacts but are not expected to be byte-identical across runs. This limitation is documented in the script header comment and in the runbook update.

### Notable non-paths

- The script does NOT upload artifacts anywhere. It does not call `gh release create`, `az storage blob upload`, or any remote publish command.
- The script does NOT build docker images or save them to `.tar` files. Only the compose files and the `deploy/docker/` build inputs are copied.
- The script does NOT move compose files or restructure `deploy/docker/`. Any repo-layout refactor is handled by a separate feature later.
- The script does NOT change agent workspace contents. Files under `deploy/docker/openclaw-assistant/` are copied verbatim.

## Inputs / Outputs

### `scripts/Publish.ps1` inputs

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-Version` | `string` | _(mandatory)_ | 4-part version (e.g., `1.2.3.0`) stamped into `AppxManifest.xml` and used in the output filename. |
| `-OutputDir` | `string` | `'artifacts/publish'` | Root directory for the versioned bundle. The script writes to `<OutputDir>/<Version>/`. |
| `-Configuration` | `string` (Debug/Release) | `'Release'` | `dotnet publish` configuration for every runnable project. |
| `-CertThumbprint` | `string` | `''` | SHA-1 thumbprint of the signing certificate in `Cert:\CurrentUser\My`. Required unless `-SkipSign`. |
| `-SkipSign` | `switch` | `$false` | When present, the MSIX is built without running `signtool.exe`. |

### `scripts/Publish.ps1` outputs

| Artifact | Path | Notes |
|---|---|---|
| Published executables | `artifacts/publish/<version>/executables/<ProjectName>/` | One subdirectory per runnable project (`OpenClaw.Core`, `OpenClaw.HostAdapter`, `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`). `OpenClaw.Core` and `OpenClaw.HostAdapter` are published **self-contained for `win-x64`** per owner decision; MailBridge and MailBridge.Client retain their existing self-contained `win-x64` posture. `.Contracts` assemblies appear inside their consumers' subdirectories; no dedicated Contracts subdirectory is produced. |
| Docker artifact set | `artifacts/publish/<version>/docker/` | See docker artifact table below. |
| MSIX package | `artifacts/publish/<version>/msix/OpenClaw.MailBridge_<version>_x64.msix` | Signed if `-CertThumbprint` supplied; unsigned if `-SkipSign`. |
| Manifest | `artifacts/publish/<version>/manifest.json` | Written last; enumerates every file under the version root. |

### Docker artifact set

| Source path | Destination path | Inclusion rule |
|---|---|---|
| `docker-compose.yml` | `artifacts/publish/<version>/docker/docker-compose.yml` | Always copied. |
| `docker-compose.dev.yml` | `artifacts/publish/<version>/docker/docker-compose.dev.yml` | Always copied. |
| `.env.example` (repo root) | `artifacts/publish/<version>/docker/.env.example` | Copied when present on disk; skipped silently when absent. |
| `deploy/docker/**` | `artifacts/publish/<version>/docker/deploy/docker/**` | Recursive copy preserving relative paths. Agent workspace files under `deploy/docker/openclaw-assistant/` are copied verbatim. |
| `secrets/**` | _(excluded)_ | Never copied. If a `secrets/` directory is detected under a source root, the script logs a warning and skips it. |
| `secrets/.env.anthropic` | _(excluded)_ | Never copied. |

### `manifest.json` schema

```json
{
  "version": "1.2.3.0",
  "generatedAt": "2026-04-18T00:00:00Z",
  "files": [
    {
      "path": "executables/OpenClaw.Core/OpenClaw.Core.dll",
      "size": 123456,
      "sha256": "abcdef0123456789..."
    }
  ]
}
```

- `version` matches the `-Version` parameter.
- `generatedAt` is an ISO-8601 UTC timestamp.
- `files` is an array sorted by `path` ascending (invariant culture) for stable diffs.
- `path` uses forward-slash separators relative to the version root.
- `size` is a non-negative integer (bytes).
- `sha256` is a 64-character lowercase hex string.
- `manifest.json` itself is not listed in `files`.

### Versioning constraints

- `-Version` must be a 4-part numeric string matching `^\d+\.\d+\.\d+\.\d+$`. The MSIX `AppxManifest.xml` requires exactly four numeric parts, each in the `uint16` range (0–65535). See open question 1 for enforcement vs normalization.
- The MSIX output filename embeds the version verbatim: `OpenClaw.MailBridge_<Version>_x64.msix`.

## API / CLI Surface

### Script invocations

```powershell
# Dev build (unsigned)
.\scripts\Publish.ps1 -Version '1.2.3.0' -SkipSign

# Signed release build
.\scripts\Publish.ps1 -Version '1.2.3.0' -CertThumbprint 'ABCDEF1234...'

# Alternate output root and debug configuration
.\scripts\Publish.ps1 -Version '1.2.3.0' -SkipSign -OutputDir 'D:\releases' -Configuration 'Debug'
```

### Exported helper module

`scripts/Publish.Helpers.psm1` exports the following functions for test and reuse:

| Function | Purpose |
|---|---|
| `Find-WindowsSdkTool` | Resolve a Windows SDK tool (`makeappx.exe`, `makepri.exe`, `signtool.exe`) by scanning `${env:ProgramFiles(x86)}\Windows Kits\10\bin` and falling back to `Get-Command`. |
| `Get-StampedAppxManifestXml` | Pure helper: given manifest XML and a version string, return the stamped XML. |
| `Invoke-VersionStamp` | Write the stamped manifest to `installer/staging/AppxManifest.xml`. |
| `Invoke-LayoutAssembly` | Copy the per-project bridge and client publish outputs plus `installer/Assets/` into `installer/staging/`. |
| `Invoke-MakePri` | Run `makepri createconfig` then `makepri new` against the staging directory. |
| `Invoke-MakeAppx` | Run `makeappx pack /d /p /nv /o` against the staging directory. |
| `Invoke-SignTool` | Run `signtool sign` with `sha1` thumbprint, `/fd SHA256`, `/tr http://timestamp.digicert.com`, `/td SHA256`. |
| `Invoke-DotnetPublish` | Invoke `dotnet publish` for one project with `-c`, `-o`, `/p:Deterministic=true` and any additional profile flags. |
| `Copy-DockerArtifact` | Copy the docker artifact set into the bundle, applying the `secrets/` exclusion rule. |
| `New-ManifestEntry` | Produce one manifest entry given a file path and bundle root. |
| `Write-PublishManifest` | Walk the version root, compose the manifest object, and write `manifest.json`. |

### Retirement of `scripts/build-msix.ps1`

The following in-repo references must be updated when the script is removed:

| File | Action |
|---|---|
| `scripts/build-msix.ps1` | Delete the file. |
| `tests/scripts/build-msix.Tests.ps1` | Delete; replaced by `tests/scripts/Publish.Helpers.Tests.ps1` and `tests/scripts/Publish.Tests.ps1`. |
| `README.md` (release-build section) | Update to document `.\scripts\Publish.ps1`. |
| `docs/mailbridge-runbook.md` (Section 3 "Publish and build the package") | Update to document `.\scripts\Publish.ps1`. |
| `.github/workflows/build-msix.yml` | **Rename to `.github/workflows/publish.yml`.** Replace the separate `dotnet publish` steps and the `build-msix.ps1` call with a single `Publish.ps1` invocation. Preserve existing triggers (for example `v*` tag triggers) on the renamed workflow. |

## Data & State

### Data flow

1. `dotnet publish` (per project) writes directory-layout binaries to `artifacts/publish/<version>/executables/<ProjectName>/`.
2. The docker copy step walks the source paths in the docker artifact table and writes them under `artifacts/publish/<version>/docker/`.
3. The MSIX stage assembles `installer/staging/` from the MailBridge and MailBridge.Client executables subdirectories (just produced), stamps the version into `AppxManifest.xml`, generates `resources.pri`, packs the MSIX, and optionally signs it. The output is written directly into `artifacts/publish/<version>/msix/`.
4. The manifest stage enumerates every file under the version root, computes SHA-256 for each, and writes `manifest.json` last.

### Persistence

- `artifacts/publish/<version>/` is gitignored (all of `artifacts/` is ignored). Bundles persist only on the operator's machine until explicitly uploaded elsewhere.
- `installer/staging/` remains the working directory for MSIX assembly. It is removed and recreated at the start of the MSIX stage to prevent stale-file corruption.
- No state is written to `%LOCALAPPDATA%` or any user profile location by the publish script.

### Migration / backfill

None. Operators who previously ran `scripts/build-msix.ps1` must switch to `scripts/Publish.ps1`. The new entry point documents the parameter mapping in its header comment.

## Constraints & Risks

- **500-line-per-file policy**: The combined publish script plus helpers would exceed 500 lines in a single file. The module split (`Publish.ps1` + `Publish.Helpers.psm1`) is required to stay within policy. This is the research-recommended Design B.
- **Deterministic builds**: `/p:Deterministic=true` is passed to every `dotnet publish` invocation, but `PublishReadyToRun=true` in the MSIX profile still produces non-byte-identical R2R artifacts across runs. The manifest acceptance criterion is worded to allow this.
- **Windows-only**: The MSIX stage requires the Windows SDK (`makeappx.exe`, `makepri.exe`, `signtool.exe`). The script can be invoked on non-Windows hosts for the executables and docker stages only if the planner decides to support that; in this iteration, the script is expected to run on Windows end-to-end.
- **Signing-cert availability**: The signed path requires a certificate in `Cert:\CurrentUser\My`. Operators must use `scripts/New-MsixDevCert.ps1` (unchanged by this feature) or provide a commercial cert.
- **Staging-directory collisions**: `installer/staging/` is shared with `build-msix.ps1`'s historical behavior. The unified script cleans it at the start of each run.
- **External callers of `build-msix.ps1`**: Any caller outside this repository cannot be inspected or updated. The breaking change is called out in the issue and runbook.
- **Docker-artifact verbatim copy**: Agent workspace files under `deploy/docker/openclaw-assistant/` may contain operator-specific context. The script copies them verbatim with no sanitization; operators are responsible for reviewing the bundle before distribution.
- **Secrets exclusion**: The script must never copy `secrets/` or any secret-bearing file. A defensive check logs a warning if a `secrets/` directory is detected under a source root.

## Implementation Strategy

### Scope — new files

| File | Purpose |
|---|---|
| `scripts/Publish.ps1` | Parameter declaration, stage orchestration, `dotnet publish` calls, docker copy, progress output, main-body guard. |
| `scripts/Publish.Helpers.psm1` | Pure and near-pure helper functions listed in the API / CLI Surface section. |
| `tests/scripts/Publish.Helpers.Tests.ps1` | Pester v5 tests covering the module functions via `Import-Module`. |
| `tests/scripts/Publish.Tests.ps1` | Pester v5 tests covering `Publish.ps1` orchestration and parameter binding (dot-sourced with full mock injection). |

### Scope — modified files

| File | Change |
|---|---|
| `README.md` | Update the release-build section to document `Publish.ps1`. |
| `docs/mailbridge-runbook.md` | Update Section 3 ("Publish and build the package") to document `Publish.ps1`. |
| `.github/workflows/build-msix.yml` | **Rename to `.github/workflows/publish.yml`.** Replace the separate `dotnet publish` steps and the `build-msix.ps1` call with a single `Publish.ps1` invocation. Preserve existing triggers on the renamed workflow. |

### Scope — deleted files

| File | Reason |
|---|---|
| `scripts/build-msix.ps1` | Replaced by `Publish.ps1` + `Publish.Helpers.psm1`. |
| `tests/scripts/build-msix.Tests.ps1` | Replaced by the new Pester test files. |

### Dependency changes

None. The script uses only built-in PowerShell 7+ modules (`Microsoft.PowerShell.Utility` for `Get-FileHash`, `ConvertTo-Json`, etc.), the .NET 10 SDK (already required), and the Windows 10 SDK (already required for the MSIX stage).

### Logging / telemetry

The script writes progress via `Write-Information` (stream 6) with a stage prefix (`[publish]`, `[docker]`, `[msix]`, `[manifest]`) and surfaces tool errors via `Write-Error`. No telemetry is emitted.

### Rollout

The new script is added and the old script is removed in the same feature branch. The CI workflow is updated in the same change so no transitional window exists where both entry points are active.

## Definition of Done

- [x] `scripts/Publish.ps1` exists and accepts `-Version`, `-OutputDir`, `-Configuration`, `-CertThumbprint`, `-SkipSign`.
- [x] `scripts/Publish.Helpers.psm1` exists and exports the functions listed in the API / CLI Surface section.
- [x] Running `Publish.ps1 -Version '1.2.3.0' -SkipSign` on a clean workspace produces `artifacts/publish/1.2.3.0/` with `executables/`, `docker/`, `msix/`, and a valid `manifest.json`.
- [x] `manifest.json` lists every file under the version root with relative path, size, and SHA-256 hash. Entries are sorted by `path`.
- [x] `scripts/build-msix.ps1` is deleted. `tests/scripts/build-msix.Tests.ps1` is deleted. `README.md`, `docs/mailbridge-runbook.md`, and `.github/workflows/build-msix.yml` are updated. No references to `build-msix.ps1` remain in-repo.
- [x] The `secrets/` exclusion is enforced and unit-tested.
- [x] The parameter-validation path fails fast when neither `-SkipSign` nor `-CertThumbprint` is supplied, with a unit test to prove it.
- [x] Pester coverage >= 90% on new lines in `scripts/Publish.ps1` and `scripts/Publish.Helpers.psm1`. Repo-wide line coverage remains >= 80%.
- [x] PoshQC suite (format -> analyze -> test) passes on all new and modified PowerShell files.

## Seeded Test Conditions (from potential)

- [ ] End-to-end `Publish.ps1 -Version '1.2.3.0' -SkipSign` smoke test against a clean workspace.
- [ ] Each published executable runs in isolation given its runtime prerequisites.
- [ ] The MSIX under `msix/` installs, launches the bridge startup task, and uninstalls cleanly (regression of feature #17).
- [ ] The docker artifact set under `docker/` reproduces a working compose stack on a clean host given a valid `secrets/.env.anthropic` supplied out-of-band.
- [ ] `manifest.json` hashes match re-computed hashes of the files on disk.
- [ ] No in-repo reference to `build-msix.ps1` remains after the change.

## Non-Goals

- Remote upload, GitHub Release creation, or release-server integration.
- Moving or restructuring compose files or the `deploy/docker/` tree.
- Changes to compose service definitions, Dockerfile behavior, or agent workspace contents.
- Building docker images to `.tar` files.
- Cross-platform packaging (macOS, Linux installers).
- Publishing `.Contracts` libraries as standalone bundle subdirectories.

## Owner Decisions (resolved)

These items were raised as open questions during PRD drafting and have since been resolved by the feature owner. They are binding inputs for the planner.

- **RID for `OpenClaw.Core` and `OpenClaw.HostAdapter`.** Publish **self-contained, `win-x64`**. The unified script must pass `--self-contained true -r win-x64` (or equivalent MSBuild properties) for these two projects. Output sizes grow accordingly; the manifest is expected to reflect the self-contained payload.
- **CI workflow rename.** `.github/workflows/build-msix.yml` is **renamed to `.github/workflows/publish.yml`**. The workflow body is updated to invoke the new `scripts/Publish.ps1` entry point. All existing triggers (for example `v*` tag triggers) are preserved on the renamed workflow unless the planner identifies a concrete reason to change them.

## Open Questions (for planner)

The following items are delegated to the planner's discretion per owner direction. The planner must record its choice and rationale in `plan.<timestamp>.md`.

1. **Version-string enforcement vs normalization.** Whether `-Version` is strictly validated as a 4-part string via `[ValidatePattern]` (reject `1.2.3`) or a 3-part input is auto-normalized by appending `.0`. The MSIX `AppxManifest.xml` requires exactly four parts; both approaches satisfy that constraint. Planner picks one and documents the rule in the script header.
2. **MSIX publish profile retention.** Whether `build-msix.ps1`'s use of `dotnet publish /p:PublishProfile=msix` is preserved by keeping the `msix.pubxml` files for MailBridge and MailBridge.Client (and publishing those two projects via the profile instead of via direct CLI flags), or whether the profile-driven settings (`PublishSingleFile=false`, `PublishReadyToRun=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`) are inlined as CLI flags and the `.pubxml` files are removed. Keeping the `.pubxml` files is the lower-risk choice.
3. **Hash-stability test expectations.** Whether the acceptance test for `manifest.json` asserts byte-identical binary hashes (which requires dropping `PublishReadyToRun=true` for the MSIX-bound projects) or only structural stability (each binary file is present, has a non-zero size, and has a 64-character lowercase hex `sha256` field). The latter is the lower-risk choice and matches the Determinism Behavior wording already present in this spec.
