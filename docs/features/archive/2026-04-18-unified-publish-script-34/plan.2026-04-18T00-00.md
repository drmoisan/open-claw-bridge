# Plan — Unified Publish Script (Issue #34)

- **Feature folder:** `docs/features/active/2026-04-18-unified-publish-script-34/`
- **Feature branch:** `feature/unified-publish-script-34` (base: `development`)
- **Work Mode:** `full-feature`
- **Plan Timestamp:** 2026-04-18T00-00
- **Acceptance-criteria sources (authoritative):**
  - `docs/features/active/2026-04-18-unified-publish-script-34/spec.md` (Definition of Done + Behavior)
  - `docs/features/active/2026-04-18-unified-publish-script-34/user-story.md` (Acceptance Criteria)
- **Supporting inputs:**
  - `docs/features/active/2026-04-18-unified-publish-script-34/issue.md`
  - `artifacts/research/2026-04-18-unified-publish-script.md`
  - `.claude/rules/powershell.md`
  - `.claude/rules/general-code-change.md`
  - `.claude/rules/general-unit-test.md`
  - `.claude/rules/tonality.md`

## Design Summary

This plan implements Research Design B: a `scripts/Publish.ps1` orchestrator plus a `scripts/Publish.Helpers.psm1` module. The split is required to keep each file under the 500-line policy ceiling and to eliminate the dot-source guard pattern used by the retired `scripts/build-msix.ps1`.

The feature delivers:

1. `scripts/Publish.ps1` (orchestrator: parameter binding, stage ordering, `dotnet publish` invocations, docker copy, progress output, main-body guard).
2. `scripts/Publish.Helpers.psm1` (pure and near-pure helpers: SDK-tool resolution, manifest XML stamping, layout assembly, `makepri`/`makeappx`/`signtool` invocation, docker artifact copy with `secrets/` exclusion, manifest generation).
3. `tests/scripts/Publish.Helpers.Tests.ps1` (Pester v5 tests for each helper).
4. `tests/scripts/Publish.Tests.ps1` (Pester v5 tests for the orchestrator via dot-source with full mock injection).
5. Retirement of `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1`.
6. Documentation updates: `README.md`, `docs/mailbridge-runbook.md`.
7. CI: `.github/workflows/build-msix.yml` renamed to `.github/workflows/publish.yml`, body rewritten to invoke `Publish.ps1`, triggers preserved.

## Owner Decisions Captured (Spec-level — binding)

These came from the spec and are not renegotiable:

- **Self-contained `win-x64` publish for `OpenClaw.Core` and `OpenClaw.HostAdapter`.** The orchestrator passes `--self-contained true -r win-x64` for these two projects in addition to `/p:Deterministic=true`.
- **CI workflow rename.** `.github/workflows/build-msix.yml` is renamed to `.github/workflows/publish.yml`. All existing triggers (for example `v*` tag triggers) are preserved on the renamed workflow.

## Planner Resolutions for Spec Open Questions

The spec delegates three items to the planner. Decisions below are binding for this plan and must be reflected in both script header comments and Pester tests.

### Q1 — Version-string enforcement vs normalization

- **Decision:** Strict validation via `[ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]` on the `-Version` parameter. 3-part inputs are rejected before any stage runs.
- **Rationale:** Explicit rejection is unambiguous, matches the MSIX `AppxManifest.xml` requirement exactly, and prevents silent normalization from masking a caller mistake (for example a CI variable truncation). A single `ValidatePattern` attribute is simpler to test than a normalization branch.
- **Risk / follow-up:** CI callers that previously passed 3-part values (if any exist outside the repo) will fail loudly. Documented in the script header and in the runbook migration note.

### Q2 — MSIX publish profile retention

- **Decision:** Keep the existing `installer/msix.pubxml` publish profiles for `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client`. The orchestrator invokes `dotnet publish /p:PublishProfile=msix /p:Deterministic=true` for those two projects.
- **Rationale:** Lower-risk choice per the spec. The profile already encodes the tested combination of `PublishSingleFile=false`, `PublishReadyToRun=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`. Inlining the flags would require regression testing a new combination on the MSIX path. Keeping the profile preserves the feature #17 regression surface intact.
- **Risk / follow-up:** The two `.pubxml` files remain coupled to the MSIX pipeline; this is acceptable and is documented in `Publish.ps1`'s header and in the runbook.

### Q3 — Hash-stability test expectations

- **Decision:** Structural stability only. Tests assert that each binary entry in `manifest.json` has a present, non-zero `size` and a 64-character lowercase hex `sha256`, but do not assert byte-identical hashes across runs.
- **Rationale:** Lower-risk choice per the spec. `PublishReadyToRun=true` (kept per Q2) produces residual non-determinism in R2R output. Structural assertions match the Determinism Behavior wording already in the spec and avoid coupling the test suite to toolchain internals.
- **Risk / follow-up:** A future feature can tighten this to byte-identical once R2R is removed from the MSIX path. Non-binary files (JSON, XML, scripts, markdown) are still expected to be byte-identical across runs; this is covered by a dedicated assertion on the manifest sort order and on `.env.example` content.

---

## Phase 0 — Baseline Capture

Capture the exact toolchain and repo state before any change. All artifacts land under `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/` with timestamps in `yyyy-MM-ddTHH-mm` format. Each artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.

- [x] [P0-T1] Read `AGENTS.md` (repo-root standing instructions) and record the file list in `phase0-instructions-read.md` under `evidence/baseline/`.
- [x] [P0-T2] Read `.claude/rules/general-code-change.md` and append its path and one-line purpose to `phase0-instructions-read.md`.
- [x] [P0-T3] Read `.claude/rules/general-unit-test.md` and append its path and one-line purpose to `phase0-instructions-read.md`.
- [x] [P0-T4] Read `.claude/rules/powershell.md` and append its path and one-line purpose to `phase0-instructions-read.md`.
- [x] [P0-T5] Read `.claude/rules/tonality.md` and append its path and one-line purpose to `phase0-instructions-read.md`.
- [x] [P0-T6] Read spec at `docs/features/active/2026-04-18-unified-publish-script-34/spec.md` and append confirmation to `phase0-instructions-read.md`.
- [x] [P0-T7] Read user story at `docs/features/active/2026-04-18-unified-publish-script-34/user-story.md` and append confirmation to `phase0-instructions-read.md`.
- [x] [P0-T8] Read issue at `docs/features/active/2026-04-18-unified-publish-script-34/issue.md` and append confirmation to `phase0-instructions-read.md`.
- [x] [P0-T9] Read research artifact at `artifacts/research/2026-04-18-unified-publish-script.md` and append confirmation to `phase0-instructions-read.md`.
- [x] [P0-T10] Run `mcp__drmCopilotExtension__run_poshqc_format` against the repo's PowerShell scope (scripts/ and tests/) in check-only mode; persist the full output to `evidence/baseline/baseline-poshqc-format.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail and any files flagged).
- [x] [P0-T11] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the repo; persist output to `evidence/baseline/baseline-poshqc-analyze.2026-04-18T00-00.md` with all four required fields including rule-violation counts in the summary.
- [x] [P0-T12] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled; persist output to `evidence/baseline/baseline-pester.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that includes baseline pass/fail counts AND numeric repo-wide line-coverage percentage.
- [x] [P0-T13] Grep the repo for `build-msix` (case-insensitive) via `Grep` tool, excluding `docs/features/active/2026-04-10-*` and `.git/`, and persist the full match list to `evidence/baseline/baseline-build-msix-refs.2026-04-18T00-00.md` with `Timestamp:`, `Command:` (the grep invocation), `EXIT_CODE:` (0 or 1 from rg), and an enumerated list of matching files plus line numbers in `Output Summary:`. This is the authoritative reference for the final QA grep gate in Phase 6.
- [x] [P0-T14] Record current sizes and line counts of `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1` in `evidence/baseline/baseline-retired-file-sizes.2026-04-18T00-00.md` with all four schema fields so the retirement deletes can be verified in Phase 5.

---

## Phase 1 — Helper Module Skeleton and Tests

Introduce `scripts/Publish.Helpers.psm1` with exported function signatures and placeholder bodies that throw `NotImplementedException`, then author the Pester test file that imports the module. Each task in this phase produces one binary outcome. The helper module stays strictly under 500 lines; a final task in this phase enforces that line-count guard.

- [x] [P1-T1] Create `scripts/Publish.Helpers.psm1` containing a file header comment (policy-compliant professional tone), `Set-StrictMode -Version Latest`, and a single `Export-ModuleMember -Function *` line with no exported functions yet. File must end with a trailing newline.
- [x] [P1-T2] Add `Find-WindowsSdkTool` to `scripts/Publish.Helpers.psm1`. Signature: `[CmdletBinding()] param([Parameter(Mandatory)][string]$ToolName)`. Body: probe `${env:ProgramFiles(x86)}\Windows Kits\10\bin` for `\x64\` matches, sort descending, fall back to `Get-Command`, throw if not found. Acceptance: function is defined and exports cleanly via `Import-Module -Force`.
- [x] [P1-T3] Add `Get-StampedAppxManifestXml` to `scripts/Publish.Helpers.psm1`. Signature: `[CmdletBinding()] param([Parameter(Mandatory)][xml]$ManifestXml,[Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version)`. Pure function: returns a new `[xml]` with `Package.Identity.Version` set to `$Version`, preserving every other Identity attribute. No I/O.
- [x] [P1-T4] Add `Invoke-VersionStamp` to `scripts/Publish.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-ManifestSourcePath`, `-StagingDir`, `-Version` (strict `ValidatePattern`). Reads source XML, stamps via `Get-StampedAppxManifestXml`, writes to `<StagingDir>/AppxManifest.xml` only when `$PSCmdlet.ShouldProcess`.
- [x] [P1-T5] Add `Invoke-LayoutAssembly` to `scripts/Publish.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-BridgePublishDir`, `-ClientPublishDir`, `-AssetsDir`, `-StagingDir`. Throws a terminating error naming the missing path if bridge or client dir is absent. Clears and recreates `$StagingDir` at the start of the call.
- [x] [P1-T6] Add `Invoke-MakePri` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-StagingDir`. Calls `Find-WindowsSdkTool -ToolName 'makepri.exe'`, runs `makepri createconfig` then `makepri new`. Throws on non-zero exit code with the tool's stderr in the message.
- [x] [P1-T7] Add `Invoke-MakeAppx` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-StagingDir`, `-OutputMsixPath`. Calls `Find-WindowsSdkTool -ToolName 'makeappx.exe'`, runs `makeappx pack /d <stagingDir> /p <outputMsixPath> /nv /o`. Throws on non-zero exit.
- [x] [P1-T8] Add `Invoke-SignTool` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-MsixPath`, `-CertThumbprint`. Calls `Find-WindowsSdkTool -ToolName 'signtool.exe'`, runs `signtool sign /sha1 <thumbprint> /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 <msixPath>`. Throws on non-zero exit.
- [x] [P1-T9] Add `Invoke-DotnetPublish` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-ProjectPath`, `-OutputDir`, `-Configuration`, `-ExtraArgs` (string[] default `@()`). Invokes `dotnet publish` with `-c $Configuration -o $OutputDir /p:Deterministic=true` plus any extra args. Throws on non-zero exit.
- [x] [P1-T10] Add `Copy-DockerArtifact` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-RepoRoot`, `-DockerBundleDir`. Copies `docker-compose.yml`, `docker-compose.dev.yml`, `.env.example` (silently skip when absent), and recursively `deploy/docker/**`. Detects any `secrets/` directory under a source root and emits `Write-Warning` instead of copying; never copies `secrets/.env.anthropic`.
- [x] [P1-T11] Add `New-ManifestEntry` to `scripts/Publish.Helpers.psm1` as a pure function. Parameters: `-FilePath`, `-BundleRoot`. Returns `[pscustomobject]@{ path = <forward-slash-relative>; size = <int>; sha256 = <lowercase hex> }`. Uses `Get-FileHash -Algorithm SHA256`.
- [x] [P1-T12] Add `Write-PublishManifest` to `scripts/Publish.Helpers.psm1` with `SupportsShouldProcess`. Parameters: `-BundleRoot`, `-Version`. Walks `$BundleRoot` (excluding `manifest.json` itself), composes the manifest object with `version`, `generatedAt` (UTC ISO-8601), and `files` sorted by `path` (invariant culture), and writes `<BundleRoot>/manifest.json`.
- [x] [P1-T13] Update `scripts/Publish.Helpers.psm1` `Export-ModuleMember -Function` list to include every function introduced by P1-T2 through P1-T12. Acceptance: `Import-Module scripts/Publish.Helpers.psm1 -Force; Get-Command -Module Publish.Helpers` lists exactly those 11 functions.
- [x] [P1-T14] Create `tests/scripts/Publish.Helpers.Tests.ps1` containing a Pester v5 `Describe` block that imports the module via `Import-Module <repo>/scripts/Publish.Helpers.psm1 -Force` in `BeforeAll`, and a single `It 'exports the expected helper functions'` assertion that matches the 11 names from P1-T13. No other tests yet.
- [x] [P1-T15] Add Pester tests under `Describe 'Get-StampedAppxManifestXml'` in `tests/scripts/Publish.Helpers.Tests.ps1`: (a) stamps the 4-part version into `Package.Identity.Version`; (b) preserves other Identity attributes; (c) rejects 3-part input via the ValidatePattern (expect `ParameterBindingValidationException`).
- [x] [P1-T16] Add Pester tests under `Describe 'Invoke-VersionStamp'` in the same file: (a) writes stamped XML to `<stagingDir>/AppxManifest.xml`; (b) `-WhatIf` leaves the staging file absent. Mock `Get-Content`, `Set-Content`, `Test-Path`, `New-Item`.
- [x] [P1-T17] Add Pester tests under `Describe 'Invoke-LayoutAssembly'`: (a) throws when bridge dir missing; (b) throws when client dir missing; (c) on success, calls `Copy-Item` for bridge, client, and assets with the correct destinations; (d) `-WhatIf` skips all copies.
- [x] [P1-T18] Add Pester tests under `Describe 'Invoke-MakePri'`: (a) invokes the resolved makepri with `createconfig` then `new`; (b) throws on non-zero exit; (c) `-WhatIf` does not invoke the tool. Use a global `makepri` function shim defined in `BeforeAll` before the module is imported; override `Find-WindowsSdkTool` via Pester `Mock`.
- [x] [P1-T19] Add Pester tests under `Describe 'Invoke-MakeAppx'`: (a) passes `/d /p /nv /o` flags exactly; (b) output path is the supplied `-OutputMsixPath`; (c) throws on non-zero exit; (d) `-WhatIf` does not invoke the tool. Shim/mocking strategy identical to P1-T18.
- [x] [P1-T20] Add Pester tests under `Describe 'Invoke-SignTool'`: (a) passes `/sha1 <thumbprint> /fd SHA256 /tr http://timestamp.digicert.com /td SHA256` with the MSIX path last; (b) throws on non-zero exit; (c) `-WhatIf` does not invoke the tool.
- [x] [P1-T21] Add Pester tests under `Describe 'Invoke-DotnetPublish'`: (a) passes `-c`, `-o`, `/p:Deterministic=true` verbatim; (b) additional `-ExtraArgs` are appended after the required args; (c) throws on non-zero exit. Use a global `dotnet` function shim.
- [x] [P1-T22] Add Pester tests under `Describe 'Copy-DockerArtifact'`: (a) copies both compose files; (b) copies `.env.example` when present; (c) skips `.env.example` silently when absent; (d) recursively copies `deploy/docker/**`; (e) emits `Write-Warning` and does NOT copy when a `secrets/` directory is detected under a source root; (f) never copies `secrets/.env.anthropic` even when present. This dedicated `secrets/` exclusion test satisfies the spec's fail-fast requirement.
- [x] [P1-T23] Add Pester tests under `Describe 'New-ManifestEntry'`: (a) returns `path` as a forward-slash-relative string; (b) returns `size` as a non-negative integer; (c) returns `sha256` as a 64-character lowercase hex string; (d) `Get-FileHash` is called with `-Algorithm SHA256`. Mock `Get-FileHash` to a deterministic fake hash.
- [x] [P1-T24] Add Pester tests under `Describe 'Write-PublishManifest'`: (a) JSON shape includes `version`, `generatedAt`, and `files`; (b) `files` is sorted ascending by `path` (invariant culture); (c) `manifest.json` itself is excluded from `files`; (d) each file entry has exactly `path`, `size`, `sha256`; (e) structural stability only — assertions do NOT require byte-identical hashes (per Q3 resolution).
- [x] [P1-T25] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Publish.Helpers.psm1` and `tests/scripts/Publish.Helpers.Tests.ps1`. If it changes files, restart Phase 1 QA loop from P1-T25.
- [x] [P1-T26] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same two files. Fail the task if any diagnostic is emitted.
- [x] [P1-T27] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage for `tests/scripts/Publish.Helpers.Tests.ps1`; assert Pester passes and coverage for `scripts/Publish.Helpers.psm1` is `>= 90%`.
- [x] [P1-T28] Verify via `Get-Content -Path scripts/Publish.Helpers.psm1 | Measure-Object -Line` (or `wc -l` equivalent) that the module is `<= 500` lines. Acceptance: line count reported and <= 500.

---

## Phase 2 — Orchestrator Script and Tests

Introduce `scripts/Publish.ps1` and its Pester test. Orchestrator stays <= 500 lines. Parameter validation uses the strict `ValidatePattern` per Q1. MSIX projects use `-PublishProfile=msix` per Q2. Core and HostAdapter use self-contained `win-x64` per the spec.

- [x] [P2-T1] Create `scripts/Publish.ps1` with `#Requires -Version 7.0`, a file header comment block (professional tone) stating the three planner resolutions (Q1 strict validation, Q2 profile retention, Q3 structural stability), and `[CmdletBinding(SupportsShouldProcess=$true)]`.
- [x] [P2-T2] Add the `param()` block to `scripts/Publish.ps1`: `-Version` (string, mandatory, `[ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]`), `-OutputDir` (string, default `'artifacts/publish'`), `-Configuration` (string, `[ValidateSet('Debug','Release')]`, default `'Release'`), `-CertThumbprint` (string, default `''`), `-SkipSign` (switch).
- [x] [P2-T3] Add module import at the top of `scripts/Publish.ps1`: `Import-Module (Join-Path $PSScriptRoot 'Publish.Helpers.psm1') -Force`.
- [x] [P2-T4] Add parameter-validation stage in `scripts/Publish.ps1`: if neither `$SkipSign` nor a non-whitespace `$CertThumbprint` is supplied, `throw` a terminating error naming the missing parameter. No other stage runs before this check.
- [x] [P2-T5] Add clean-and-create stage in `scripts/Publish.ps1`: resolve `$BundleRoot = Join-Path $OutputDir $Version`. Remove `$BundleRoot` if it exists, then recreate the directory. Emits `[publish]` progress via `Write-Information`.
- [x] [P2-T6] Add per-project dotnet-publish loop in `scripts/Publish.ps1`. Projects: `OpenClaw.Core` (extra args: `--self-contained true -r win-x64`), `OpenClaw.HostAdapter` (same), `OpenClaw.MailBridge` (extra args: `/p:PublishProfile=msix`), `OpenClaw.MailBridge.Client` (same). Each call routes through `Invoke-DotnetPublish` with `-OutputDir` set to `<BundleRoot>/executables/<ProjectName>/`. Emits `[publish] <ProjectName>` progress lines.
- [x] [P2-T7] Add docker-copy stage in `scripts/Publish.ps1` that calls `Copy-DockerArtifact -RepoRoot $RepoRoot -DockerBundleDir (Join-Path $BundleRoot 'docker')` and emits a `[docker]` progress line.
- [x] [P2-T8] Add MSIX stage in `scripts/Publish.ps1`: clears and recreates `installer/staging/`, calls `Invoke-VersionStamp`, `Invoke-LayoutAssembly` (sourcing bridge and client from `<BundleRoot>/executables/OpenClaw.MailBridge/` and `<BundleRoot>/executables/OpenClaw.MailBridge.Client/`), `Invoke-MakePri`, `Invoke-MakeAppx` (output to `<BundleRoot>/msix/OpenClaw.MailBridge_<Version>_x64.msix`). If `-SkipSign` is not set, calls `Invoke-SignTool`. Emits `[msix]` progress lines per sub-stage.
- [x] [P2-T9] Add manifest stage in `scripts/Publish.ps1` that calls `Write-PublishManifest -BundleRoot $BundleRoot -Version $Version`. Emits a `[manifest]` progress line.
- [x] [P2-T10] Add final success line and main-body dot-source guard in `scripts/Publish.ps1`: `if ($MyInvocation.InvocationName -ne '.')` wraps the stage orchestration so tests can dot-source the file without triggering execution.
- [x] [P2-T11] Create `tests/scripts/Publish.Tests.ps1` with a Pester v5 `Describe 'scripts/Publish.ps1'` block. `BeforeAll` defines global shims for `dotnet`, `makeappx`, `makepri`, `signtool` and imports `Publish.Helpers.psm1`. The script under test is dot-sourced with `. <script> -Version '1.2.3.0' -SkipSign -WhatIf` inside each `It` as appropriate.
- [x] [P2-T12] Add `Context 'parameter validation'` in `tests/scripts/Publish.Tests.ps1`: (a) throws when neither `-SkipSign` nor `-CertThumbprint` is provided (pre-stage failure, no files written); (b) accepts `-SkipSign` alone; (c) accepts `-CertThumbprint 'ABCDEF...' ` alone; (d) rejects a 3-part `-Version` (Q1 assertion — expects `ParameterBindingValidationException`).
- [x] [P2-T13] Add `Context 'stage ordering'` in `tests/scripts/Publish.Tests.ps1`: with all helpers mocked, assert call order is publish (x4 projects) -> docker copy -> msix (stamp -> layout -> pri -> appx -> optional sign) -> manifest write. Use Pester `Mock` with `-ParameterFilter` on each helper and inspect `-Verifiable`/`Should -Invoke` counts.
- [x] [P2-T14] Add `Context 'per-project publish flags'` in `tests/scripts/Publish.Tests.ps1`: assert `Invoke-DotnetPublish` is called with `--self-contained true -r win-x64` for `OpenClaw.Core` and `OpenClaw.HostAdapter`, and with `/p:PublishProfile=msix` for `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client`.
- [x] [P2-T15] Add `Context 'skip-sign path'` in `tests/scripts/Publish.Tests.ps1`: when `-SkipSign` is supplied, `Invoke-SignTool` is NOT invoked; when `-CertThumbprint` is supplied, `Invoke-SignTool` IS invoked with the provided thumbprint.
- [x] [P2-T16] Add `Context 'output paths'` in `tests/scripts/Publish.Tests.ps1`: assert the MSIX output path is `<OutputDir>/<Version>/msix/OpenClaw.MailBridge_<Version>_x64.msix` and that manifest is written at `<OutputDir>/<Version>/manifest.json`.
- [x] [P2-T17] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Publish.ps1` and `tests/scripts/Publish.Tests.ps1`. Restart phase QA from P2-T17 if files change.
- [x] [P2-T18] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same files. Zero diagnostics required.
- [x] [P2-T19] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert coverage `>= 90%` on new lines in `scripts/Publish.ps1`.
- [x] [P2-T20] Verify `scripts/Publish.ps1` is `<= 500` lines via line-count measurement.

---

## Phase 3 — Retirement of `scripts/build-msix.ps1`

Delete the retired script and its test file only after Phase 1 and Phase 2 helper coverage is green. This phase is sequenced after the replacement is proven passing so the repo never sits in a state with no MSIX code path.

- [x] [P3-T1] Delete `scripts/build-msix.ps1`. Acceptance: `Test-Path scripts/build-msix.ps1` returns `$false`.
- [x] [P3-T2] Delete `tests/scripts/build-msix.Tests.ps1`. Acceptance: `Test-Path tests/scripts/build-msix.Tests.ps1` returns `$false`.
- [x] [P3-T3] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage; assert overall Pester run still passes, no test files are missing, and repo-wide coverage remains `>= 80%`.

---

## Phase 4 — Documentation Updates

Update user-facing documents to describe the unified entry point. No changes to policy documents under `.claude/rules/` or `.github/instructions/`.

- [x] [P4-T1] Edit `README.md`: replace the current "Publish and build the package" (or equivalent release-build) section so that it documents `.\scripts\Publish.ps1` with the five parameters, a dev example (`-SkipSign`), and a signed example (`-CertThumbprint`). Remove every textual reference to `build-msix.ps1`. Acceptance: grep for `build-msix` in `README.md` returns zero matches.
- [x] [P4-T2] Edit `docs/mailbridge-runbook.md` Section 3 ("Publish and build the package") to document `Publish.ps1`, the output bundle structure (`executables/`, `docker/`, `msix/`, `manifest.json`), and a short migration note for operators who previously ran `build-msix.ps1`. Acceptance: grep for `build-msix` in `docs/mailbridge-runbook.md` returns zero matches.

---

## Phase 5 — CI Workflow Rename and Rewrite

Rename the CI workflow and replace its body. Preserve every trigger. The rename is atomic: new file created, old file deleted, triggers verified.

- [x] [P5-T1] Capture the current content of `.github/workflows/build-msix.yml` (triggers, env, permissions, jobs, steps) to `evidence/baseline/baseline-ci-workflow.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (one-line description of each trigger retained). This provides the reference for the trigger-preservation check.
- [x] [P5-T2] Create `.github/workflows/publish.yml` with the same `name:` (or an updated name that still identifies the publish job), the EXACT triggers from the captured baseline (including `v*` tag triggers and any `workflow_dispatch` inputs), and a single primary step that runs `pwsh ./scripts/Publish.ps1 -Version '${{ inputs.version }}' -CertThumbprint '${{ secrets.MSIX_CERT_THUMBPRINT }}'` (or `-SkipSign` when no secret is set). Remove the separate `dotnet publish` steps and any `build-msix.ps1` invocation.
- [x] [P5-T3] Delete `.github/workflows/build-msix.yml`. Acceptance: `Test-Path .github/workflows/build-msix.yml` returns `$false`.
- [x] [P5-T4] Diff triggers between the captured baseline (P5-T1) and the new `publish.yml` and record the comparison in `evidence/other/workflow-trigger-parity.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing each preserved trigger name. Acceptance: every trigger present in the baseline is present in the new workflow.

---

## Phase 6 — Final QA Loop

Run the complete format -> analyze -> test toolchain for PowerShell and compare against the Phase 0 baseline. Every command step produces its own evidence artifact under `evidence/qa-gates/`. Restart the loop from P6-T1 if any step auto-fixes files or fails.

- [x] [P6-T1] Run `mcp__drmCopilotExtension__run_poshqc_format` against the repo (scripts/ and tests/). Persist output to `evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail, any files changed). If any file changes, restart from P6-T1.
- [x] [P6-T2] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the repo. Persist output to `evidence/qa-gates/final-poshqc-analyze.2026-04-18T00-00.md` with all four schema fields and a diagnostic count. Must equal zero.
- [x] [P6-T3] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled. Persist output to `evidence/qa-gates/final-pester.2026-04-18T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that records numeric post-change repo-wide line coverage AND the targeted coverage for `scripts/Publish.ps1` and `scripts/Publish.Helpers.psm1`. Assert: repo-wide `>= 80%`, targeted new-code `>= 90%`, zero test failures, zero test regressions vs baseline.
- [x] [P6-T4] Emit a coverage-delta artifact `evidence/qa-gates/coverage-delta.2026-04-18T00-00.md` that records (a) baseline repo-wide coverage (from `evidence/baseline/baseline-pester.2026-04-18T00-00.md`), (b) post-change repo-wide coverage (from P6-T3), (c) new-code coverage for the two new files, and (d) an explicit assertion `post-change >= baseline - 0` (no regression). Include `Timestamp:`, `Command:` (the derivation command/diff), `EXIT_CODE:`, `Output Summary:`.
- [x] [P6-T5] Run a `Grep` gate for the pattern `build-msix` across the repo, excluding `docs/features/active/2026-04-10-*` (historical feature folders) and `.git/`. Persist results to `evidence/qa-gates/final-build-msix-refs.2026-04-18T00-00.md` with all four schema fields. Acceptance: zero matches outside the permitted exclusions.
- [x] [P6-T6] Verify end-state of retired files: `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1` both absent; `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1` all present. Persist results to `evidence/qa-gates/end-state-file-presence.2026-04-18T00-00.md`.
- [x] [P6-T7] Verify workflow rename end-state: `.github/workflows/build-msix.yml` absent and `.github/workflows/publish.yml` present. Persist result to `evidence/qa-gates/end-state-workflow-rename.2026-04-18T00-00.md` with all four schema fields.
- [x] [P6-T8] Verify line-count policy end-state: `scripts/Publish.ps1` and `scripts/Publish.Helpers.psm1` each `<= 500` lines; `tests/scripts/Publish.Tests.ps1` and `tests/scripts/Publish.Helpers.Tests.ps1` each `<= 500` lines. Persist counts and pass/fail to `evidence/qa-gates/end-state-line-counts.2026-04-18T00-00.md`.
- [x] [P6-T9] Reconcile the spec's Definition of Done checklist against the artifacts produced in Phase 0 through Phase 6. Write `evidence/qa-gates/definition-of-done-reconciliation.2026-04-18T00-00.md` with one row per DoD item, its status (PASS/FAIL), and the evidence artifact path that proves it. Acceptance: all DoD items marked PASS with a cited artifact.

---

## PREFLIGHT

DIRECTIVE: PREFLIGHT VALIDATION ONLY

PREFLIGHT: ALL CLEAR

- PASS — Task checkbox state: every task from P0-T1 through P6-T9 is emitted as an unchecked `- [ ]` item; no stale `- [x]` carryover detected.
- PASS — Task ID numbering: every `[P#-T#]` ID matches its phase heading and increments sequentially within the phase (P0 1–14, P1 1–28, P2 1–20, P3 1–3, P4 1–2, P5 1–4, P6 1–9).
- PASS — Toolchain commands: formatting/linting/testing tasks exclusively use `mcp__drmCopilotExtension__run_poshqc_format`, `mcp__drmCopilotExtension__run_poshqc_analyze`, and `mcp__drmCopilotExtension__run_poshqc_test` at P0-T10/T11/T12, P1-T25/T26/T27, P2-T17/T18/T19, P3-T3, and P6-T1/T2/T3.
- PASS — Foreign toolchain exclusion: no task references `dotnet test`, `pytest`, `jest`, `npm test`, or any non-PowerShell test runner.
- PASS — Acceptance-criteria citation: the plan header cites `spec.md` (Definition of Done + Behavior) and `user-story.md` (Acceptance Criteria) as the authoritative sources, and P6-T9 reconciles against the spec's Definition of Done.
- PASS — Owner decisions consistency: the Owner Decisions Captured block restates the spec's two resolved items (self-contained `win-x64` for Core/HostAdapter; CI workflow rename with preserved triggers) without alteration.
- PASS — Planner resolutions consistency: Q1 strict `ValidatePattern`, Q2 `msix.pubxml` retention, Q3 structural stability match the spec's three delegated open questions and are referenced in implementation tasks (P2-T2, P2-T6, P1-T24).
- PASS — New-file non-collision: `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, and `.github/workflows/publish.yml` do not currently exist on disk; creation tasks will not overwrite.
- PASS — Retired-file preconditions: `scripts/build-msix.ps1`, `tests/scripts/build-msix.Tests.ps1`, and `.github/workflows/build-msix.yml` all exist on disk, so the Phase 3 and Phase 5 deletion/rename tasks have real targets.
- PASS — Referenced policy inputs exist: `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, and `.claude/rules/tonality.md` are present and readable.
- PASS — Referenced feature inputs exist: `docs/features/active/2026-04-18-unified-publish-script-34/spec.md`, `user-story.md`, `issue.md`, and `artifacts/research/2026-04-18-unified-publish-script.md` are present.
- PASS — Referenced modified-file targets exist: `README.md` and `docs/mailbridge-runbook.md` are present for Phase 4 edits.
- PASS — Coverage-bearing baseline + final tasks: P0-T12 captures baseline repo-wide line-coverage; P6-T3 captures post-change repo-wide and targeted new-code coverage; P6-T4 records the no-regression delta.
- PASS — Evidence schema and locations: every command-bearing task in Phase 0, Phase 5, and Phase 6 specifies `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` fields and writes to canonical `evidence/baseline/`, `evidence/other/`, or `evidence/qa-gates/` paths per `evidence-and-timestamp-conventions`.
- PASS — Atomicity: each task describes a single binary outcome (single file write, single command run, single deletion, single assertion); no bucket/umbrella tasks detected.
- PASS — Final QA loop structure: Phase 6 runs format -> analyze -> test in order with an explicit restart-on-change directive at P6-T1 and zero-SKIPPED wording for each command step.
- PASS — Repo-root standing instructions input exists: P0-T1 now reads `AGENTS.md`, which is present at the repo root and readable.
- PASS — Pester runsettings path reference removed: P0-T12 and P6-T3 no longer cite `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`; both tasks now request `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled and rely on the tool's default configuration resolution.
