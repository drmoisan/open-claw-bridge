# deploy-wrapper-entry-point - Plan

- **Issue:** #139
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-10
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata: `- Work Mode: full-feature`)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- PowerShell Standards: `.claude/rules/powershell.md`
- Evidence Conventions: `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`
- AC source (authoritative): `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/issue.md`, `## Acceptance Criteria (early draft)` section (6 items, AC-1 through AC-6 below, numbered here for traceability). `user-story.md`'s `## Acceptance Criteria` section contains the identical 6 items and is kept in sync at Phase 3. `spec.md` mirrors the same items under `## Seeded Test Conditions (from potential)` and has its own `## Definition of Done` checklist, both also synced at Phase 3.
- Research: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/research/2026-07-10T11-30-deploy-wrapper-entry-point-research.md`

**All work must comply with these policies; do not duplicate their content here.**

## Acceptance Criteria (verbatim from `issue.md`, numbered for traceability)

- AC-1: `Publish.ps1` emits exactly one pipeline object (the bundle root) when its output is captured; helper return behavior is unchanged.
- AC-2: New `scripts/Deploy.ps1` runs `Publish.ps1`, captures the bundle root, then invokes `<bundleRoot>\Install.ps1` (the staged copy) without changing the caller's working directory.
- AC-3: `Deploy.ps1` forwards publish parameters (`-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`) and install parameters (`-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`); `-SkipSign` maps to `-AllowUnsigned` on `Install.ps1`.
- AC-4: `Deploy.ps1` uses `CmdletBinding(SupportsShouldProcess = $true)` and propagates `-WhatIf` to both child invocations.
- AC-5: `Deploy.ps1` fails fast: if publish throws or returns no bundle root, it does not attempt the install; on success it returns the bundle root.
- AC-6: Pester tests cover both scripts; child-script invocations are mocked via a wrapper-function seam; no temp files; coverage floors met.

## Explicit Scope and Non-Goals

- Exactly 2 production files: `scripts/Publish.ps1` (modified, 3-line output-suppression fix only) and `scripts/Deploy.ps1` (new). No third production module (no `Deploy.Helpers.psm1`) is introduced — the wrapper functions `Invoke-PublishScript` / `Invoke-InstallScript` are defined at **script scope inside `scripts/Deploy.ps1` itself**, each guarded with the `if (-not (Get-Command -Name '<FunctionName>' -ErrorAction SilentlyContinue))` pattern already used for `Test-IsElevatedAdmin` in `scripts/Install.ps1` (lines 111-121), so Pester tests can pre-register a `global:`-scoped override before the script is invoked via `&`.
- Exactly 2 test files: `tests/scripts/Deploy.Tests.ps1` (new) and `tests/scripts/Publish.Tests.ps1` (extended with one new regression test).
- Out of scope: `scripts/Install.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Publish.Msix.psm1` (return behavior of `Invoke-VersionStamp`, `Invoke-MakeAppx`, `Write-PublishManifest` unchanged), bundle layout, manifest schema.
- `Deploy.ps1` must never call `Set-Location`/`Push-Location`; both child invocations use fully-resolved absolute paths.
- `Deploy.ps1` must not stage or read the files referenced by `-AnthropicEnvFilePath` / `-DockerEnvFilePath`; it forwards path strings only.

## Conventions Used by This Plan

- `FEATURE` = `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139`
- `<ts>` = ISO-8601 timestamp `yyyy-MM-ddTHH-mm` captured at artifact-creation time.
- All evidence artifacts live under `FEATURE/evidence/<kind>/` (canonical kinds used by this plan: `baseline/`, `regression-testing/`, `qa-gates/`, `other/`, `issue-updates/`). No evidence may be written under any `artifacts/` sub-path. Every command-step evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- **PowerShell toolchain loop**: run `mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test` in that order, repo-wide. If any step fails or changes files, restart the loop from format.
- **PowerShell coverage tooling note (established workaround):** `mcp__drm-copilot__run_poshqc_test` in coverage mode fails on every invocation in this repository because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files that exist only in the `drm-copilot` source repository (reproduced defect F11 `#111`, F16 `#125`, `#135`, `#137`). The numeric-coverage source for both baseline and final-QC PowerShell tasks is: import the bundled `PoshQC.psd1` module directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>`, where `<corrected>` is a SCRATCHPAD-only copy of the bundled runsettings with `CodeCoverage.Path` rewritten to this repository's actual production PowerShell files under `scripts/**` (full glob, no `ExcludedPath` entry per the Coverage Exclusion Policy). Record both the failing MCP invocation and the corrected-runsettings invocation in the evidence artifact.
- **Change budget:** this fix touches 1 modified production file (`scripts/Publish.ps1`, 3-line fix) and adds 1 new production file (`scripts/Deploy.ps1`), plus 1 extended test file (`tests/scripts/Publish.Tests.ps1`) and 1 new test file (`tests/scripts/Deploy.Tests.ps1`) — within the direct-mode 2-production-file budget in `.claude/rules/powershell.md`.
- **Test file line-count check (performed during planning):** `tests/scripts/Publish.Tests.ps1` is 462 lines (room under the 500-line cap for the one new regression test added in Phase 1). `scripts/Install.ps1` (293 lines) and `scripts/Publish.ps1` (249 lines) are both well under cap and used only as pattern references, not modified beyond the Phase 1 fix.
- **Design-seam naming (confirmed by research):** `Invoke-PublishScript -PublishScriptPath <string> -PublishParams <hashtable>` and `Invoke-InstallScript -InstallScriptPath <string> -InstallParams <hashtable>`, both `[CmdletBinding(SupportsShouldProcess = $true)]`. Neither parameter is named `Args` (automatic-variable collision rule in `.claude/rules/powershell.md`).

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read `CLAUDE.md` at the repository root.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` is created (or appended to) recording that `CLAUDE.md` was read, with a `Timestamp:` field.
- [x] [P0-T2] Read `.claude/rules/general-code-change.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-code-change.md` as read.
- [x] [P0-T3] Read `.claude/rules/general-unit-test.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-unit-test.md` as read.
- [x] [P0-T4] Read `.claude/rules/powershell.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/powershell.md` as read.
- [x] [P0-T5] Finalize the Phase 0 policy-read evidence artifact covering P0-T1 through P0-T4.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` exists containing `Timestamp:`, `Policy Order:` (listing the four files in the exact order read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`), and an explicit list of files read.
- [x] [P0-T6] Capture the PowerShell format baseline: run `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) against the current pre-change repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail status and count of files changed by the run).
- [x] [P0-T7] Capture the PowerShell analyze baseline: run `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) against the current pre-change repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (diagnostic counts by severity, expected 0 errors).
- [x] [P0-T8] Capture the PowerShell test-and-coverage baseline: run `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround and record the numeric result.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, and `Output Summary:` containing pass/fail test counts and the numeric repo-wide baseline command/line-coverage percentage (no placeholders).

### Phase 1 — `Publish.ps1` Output-Leak Fix (AC-1)

- [x] [P1-T1] [expect-fail] Add one new `It` block to `tests/scripts/Publish.Tests.ps1` (new `Context 'output contract'`) that captures a non-`Out-Null` invocation (`$result = & $script:ScriptPath -Version '1.2.3.0' -SkipSign`) and asserts `@($result).Count -eq 1` and `$result -eq $script:ExpectedBundleRoot` (the bundle root the test's own `Invoke-MakeAppx` mock and stage inputs resolve to for that fixture).
  - Acceptance: AC-1 (test-authored half) — the new `It` block exists in `tests/scripts/Publish.Tests.ps1`, uses the file's existing mock/call-log scaffolding, and asserts the captured result is a single scalar equal to the expected bundle root.
- [x] [P1-T2] [expect-fail] Run the new `It` block from P1-T1 in targeted mode against the current (pre-fix) `scripts/Publish.ps1` and confirm it fails.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:` (targeted Pester invocation scoped to the new `It` block), `EXIT_CODE:` (non-zero / test-failure exit), `Output Summary:` recording the failure and quoting the assertion failure showing the captured result currently has more than one element.
- [x] [P1-T3] In `scripts/Publish.ps1` line 221, change `Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version` to `$null = Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version`.
  - Acceptance: AC-1 (call site 1 of 3) — `git diff scripts/Publish.ps1` shows exactly this one-line change at the `Invoke-VersionStamp` call site and no other line differs from this edit.
- [x] [P1-T4] In `scripts/Publish.ps1` line 227, change `Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath` to `$null = Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath`.
  - Acceptance: AC-1 (call site 2 of 3) — `git diff scripts/Publish.ps1` shows both P1-T3 and this line changed, and no other line differs.
- [x] [P1-T5] In `scripts/Publish.ps1` line 245, change `Write-PublishManifest -BundleRoot $BundleRoot -Version $Version` to `$null = Write-PublishManifest -BundleRoot $BundleRoot -Version $Version`.
  - Acceptance: AC-1 (call site 3 of 3) — `git diff scripts/Publish.ps1` shows exactly the three `$null =` edits from P1-T3, P1-T4, and this task, and no other line in the file differs.
- [x] [P1-T6] Confirm `scripts/Publish.Msix.psm1` and `scripts/Publish.Helpers.psm1` remain byte-identical to their pre-fix state (helper return behavior unchanged, non-goal verification).
  - Acceptance: AC-1 (helper-unchanged half) — `git diff scripts/Publish.Msix.psm1 scripts/Publish.Helpers.psm1` returns no output.
- [x] [P1-T7] Run the mandatory PowerShell toolchain loop (`mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test`) scoped to `scripts/Publish.ps1` and `tests/scripts/Publish.Tests.ps1`; restart from format if any step fails or changes files.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-post-fix-pass.<ts>.md` exists with `Timestamp:`, `Command:` for each of the three steps, `EXIT_CODE: 0` for each, and `Output Summary:` confirming the P1-T1 regression test now passes and the full `Publish.Tests.ps1` suite passes with no formatting/lint changes required on the recorded clean pass.

### Phase 2 — `Deploy.ps1` Wrapper Entry Point (AC-2, AC-3, AC-4, AC-5, AC-6)

- [x] [P2-T1] Create `scripts/Deploy.ps1` with `#Requires -Version 7.0`, comment-based help documenting the publish-then-install behavior, and `[CmdletBinding(SupportsShouldProcess = $true)]` with a `param()` block declaring `-Version [string]`, `-Configuration [string]` (`ValidateSet('Debug','Release')`, default `'Release'`), `-CertThumbprint [string]`, `-SkipSign [switch]`, `-SkipDocker [switch]`, `-DockerEnvFilePath [string]`, `-AnthropicEnvFilePath [string]`, `-Force [switch]`.
  - Acceptance: AC-3 (parameter surface) — Grep of `scripts/Deploy.ps1` matches `CmdletBinding(SupportsShouldProcess = \$true)` and each of the 8 parameter names.
- [x] [P2-T2] Add the guarded wrapper function `Invoke-PublishScript` at script scope in `scripts/Deploy.ps1`, using the `if (-not (Get-Command -Name 'Invoke-PublishScript' -ErrorAction SilentlyContinue))` guard pattern from `scripts/Install.ps1` lines 111-121, `[CmdletBinding(SupportsShouldProcess = $true)]`, parameters `-PublishScriptPath [string]` (mandatory) and `-PublishParams [hashtable]` (mandatory), gated by `$PSCmdlet.ShouldProcess($PublishScriptPath, 'Invoke Publish.ps1')`, returning `& $PublishScriptPath @PublishParams`.
  - Acceptance: AC-6 (wrapper-seam half 1 of 2) — Grep of `scripts/Deploy.ps1` matches the guard line and the `function Invoke-PublishScript` definition with both named parameters.
- [x] [P2-T3] Add the guarded wrapper function `Invoke-InstallScript` at script scope in `scripts/Deploy.ps1`, using the same guard pattern, parameters `-InstallScriptPath [string]` (mandatory) and `-InstallParams [hashtable]` (mandatory), gated by `$PSCmdlet.ShouldProcess($InstallScriptPath, 'Invoke Install.ps1')`, invoking `& $InstallScriptPath @InstallParams`.
  - Acceptance: AC-6 (wrapper-seam half 2 of 2) — Grep of `scripts/Deploy.ps1` matches the guard line and the `function Invoke-InstallScript` definition with both named parameters.
- [x] [P2-T4] Implement the publish-parameter hashtable in `scripts/Deploy.ps1`'s main block, built from `-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign` (only including bound parameters), and call `$bundleRoot = Invoke-PublishScript -PublishScriptPath (Join-Path $PSScriptRoot 'Publish.ps1') -PublishParams $publishParams`.
  - Acceptance: AC-2 (publish-capture half), AC-3 (publish forwarding half) — Grep confirms a `$publishParams` hashtable literal containing the four keys and the `$bundleRoot = Invoke-PublishScript ...` assignment.
- [x] [P2-T5] Implement the fail-fast guard immediately after the publish call: `if ([string]::IsNullOrWhiteSpace($bundleRoot)) { throw '<diagnostic message>' }`, executed before any reference to `Invoke-InstallScript`.
  - Acceptance: AC-5 (empty-bundle-root half) — Grep confirms the `IsNullOrWhiteSpace($bundleRoot)` guard with an explicit `throw` appears before the first `Invoke-InstallScript` call in the file.
- [x] [P2-T6] Implement the install-parameter hashtable in `scripts/Deploy.ps1`'s main block, built from `-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`, plus the conditional mapping `if ($SkipSign) { $installParams['AllowUnsigned'] = $true }`.
  - Acceptance: AC-3 (install forwarding half, `-SkipSign` -> `-AllowUnsigned` mapping) — Grep confirms an `$installParams` hashtable containing the four forwarded keys and the conditional `AllowUnsigned` assignment gated on `$SkipSign`.
- [x] [P2-T7] Implement the staged-install invocation: `Invoke-InstallScript -InstallScriptPath (Join-Path $bundleRoot 'Install.ps1') -InstallParams $installParams`, with no `Set-Location`/`Push-Location` call anywhere in the file.
  - Acceptance: AC-2 (staged-install half, no-CWD-change half) — Grep confirms `Join-Path $bundleRoot 'Install.ps1'` passed to `Invoke-InstallScript`; a second Grep for `Set-Location|Push-Location` over `scripts/Deploy.ps1` returns zero matches.
- [x] [P2-T8] Implement `return $bundleRoot` as the final statement of `scripts/Deploy.ps1`'s main block.
  - Acceptance: AC-5 (success-return half) — Grep confirms `return $bundleRoot` is present after the `Invoke-InstallScript` call.
- [x] [P2-T9] Verify `scripts/Deploy.ps1` introduces no companion `Deploy.Helpers.psm1` module and stays under the 500-line cap.
  - Acceptance: `Test-Path scripts/Deploy.Helpers.psm1` returns `$false`; `(Get-Content scripts/Deploy.ps1).Count` is less than or equal to 500.
- [x] [P2-T10] Create `tests/scripts/Deploy.Tests.ps1` with a header comment, `[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidGlobalVars', ...)]` matching `tests/scripts/Publish.Tests.ps1`'s justification convention, a `BeforeAll` defining `$script:DeployScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Deploy.ps1'`, and a top-level `Describe 'scripts/Deploy.ps1'` block.
  - Acceptance: the file exists at `tests/scripts/Deploy.Tests.ps1` and contains `Describe 'scripts/Deploy.ps1'`.
- [x] [P2-T11] In `BeforeEach`, define `function global:Invoke-PublishScript` and `function global:Invoke-InstallScript` stub implementations matching the production named parameters exactly (`PublishScriptPath`/`PublishParams`, `InstallScriptPath`/`InstallParams`), each appending a call record to a `$global:DeployTestCalls` `ArrayList` (cleared at the start of each test), with `global:Invoke-PublishScript` returning a configurable `$global:DeployTestBundleRoot` value (reset to a default non-empty fixture path each test).
  - Acceptance: AC-6 (mocking-seam setup) — Grep of `tests/scripts/Deploy.Tests.ps1` confirms both `function global:Invoke-PublishScript` and `function global:Invoke-InstallScript` are defined inside a `BeforeEach` block with matching parameter names.
- [x] [P2-T12] Add `Context 'parameter forwarding'` with an `It` asserting the `-PublishParams` hashtable recorded in `$global:DeployTestCalls` for a `& $script:DeployScriptPath -Version '1.2.3.0' -Configuration 'Debug' -CertThumbprint 'ABC123'` invocation contains `Version = '1.2.3.0'`, `Configuration = 'Debug'`, `CertThumbprint = 'ABC123'`.
  - Acceptance: AC-3 (publish-forwarding test) — test passes in targeted mode.
- [x] [P2-T13] Add an `It` in the same Context asserting the `-InstallParams` hashtable recorded for a `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign -SkipDocker -DockerEnvFilePath 'C:\fake\docker.env' -AnthropicEnvFilePath 'C:\fake\anthropic.env' -Force` invocation contains `SkipDocker = $true`, `DockerEnvFilePath = 'C:\fake\docker.env'`, `AnthropicEnvFilePath = 'C:\fake\anthropic.env'`, `Force = $true`.
  - Acceptance: AC-3 (install-forwarding test) — test passes in targeted mode.
- [x] [P2-T14] Add `Context '-SkipSign to -AllowUnsigned mapping'` with an `It` asserting `-SkipSign` on `Deploy.ps1` results in `AllowUnsigned = $true` in the recorded `-InstallParams` hashtable.
  - Acceptance: AC-3 (mapping test, present case) — test passes in targeted mode.
- [x] [P2-T15] Add an `It` in the same Context asserting the recorded `-InstallParams` hashtable does not contain a truthy `AllowUnsigned` key when `-SkipSign` is not supplied.
  - Acceptance: AC-3 (mapping test, absent case) — test passes in targeted mode.
- [x] [P2-T16] Add `Context 'publish-failure short-circuit'` with an `It` that overrides `global:Invoke-PublishScript` to throw, invokes `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign`, asserts the call `Should -Throw`, and asserts `$global:DeployTestCalls` contains zero entries for `Invoke-InstallScript`.
  - Acceptance: AC-5 (publish-throw half, test) — test passes in targeted mode.
- [x] [P2-T17] Add an `It` in the same Context that overrides `global:Invoke-PublishScript` to return `$null`, invokes `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign`, asserts an explicit `Should -Throw`, and asserts zero recorded `Invoke-InstallScript` calls.
  - Acceptance: AC-5 (empty-bundle-root half, test) — test passes in targeted mode.
- [x] [P2-T18] Add `Context '-WhatIf propagation'` with an `It` asserting `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign -WhatIf` does not throw and produces no recorded `Invoke-InstallScript` call whose underlying `ShouldProcess` gate was bypassed (assert the wrapper's `-WhatIf`-aware no-op path, per the `$PSCmdlet.ShouldProcess` gate inside `global:Invoke-InstallScript`).
  - Acceptance: AC-4 (`-WhatIf` propagation test) — test passes in targeted mode.
- [x] [P2-T19] Add `Context 'return value'` with an `It` asserting a successful `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign` invocation returns exactly `$global:DeployTestBundleRoot`.
  - Acceptance: AC-5 (success-return test) — test passes in targeted mode.
- [x] [P2-T20] Add an `It` in the same Context asserting `Get-Location` immediately before and immediately after a successful `& $script:DeployScriptPath -Version '1.2.3.0' -SkipSign` invocation are equal (no working-directory change).
  - Acceptance: AC-2 (no-CWD-change test) — test passes in targeted mode.
- [x] [P2-T21] Run the mandatory PowerShell toolchain loop (`mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test`) scoped to `scripts/Deploy.ps1` and `tests/scripts/Deploy.Tests.ps1`; restart from format if any step fails or changes files.
  - Acceptance: `FEATURE/evidence/qa-gates/phase2-poshqc-loop.<ts>.md` exists with `Timestamp:`, `Command:` for each of the three steps, `EXIT_CODE: 0` for each, and `Output Summary:` confirming all new `Deploy.Tests.ps1` tests (P2-T12 through P2-T20) pass with no formatting/lint changes required on the recorded clean pass.
- [x] [P2-T22] Confirm `scripts/Deploy.ps1` does not read, copy, or otherwise open the files referenced by `-AnthropicEnvFilePath` / `-DockerEnvFilePath` (non-goal verification: forwards path strings only).
  - Acceptance: Grep of `scripts/Deploy.ps1` for `Get-Content|Copy-Item|Set-Content` referencing `DockerEnvFilePath|AnthropicEnvFilePath` returns zero matches.

### Phase 3 — Final QA Loop, Coverage Verification, and Acceptance Sign-Off

- [x] [P3-T1] Run the final PowerShell format check: `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) over the full repository, including `scripts/Publish.ps1`, `scripts/Deploy.ps1`, `tests/scripts/Publish.Tests.ps1`, and `tests/scripts/Deploy.Tests.ps1`. If the run fails or modifies any file, apply the needed fix and restart this task before proceeding to P3-T2.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (pass status, 0 files changed on the recorded clean pass).
- [x] [P3-T2] Run the final PowerShell analyzer check: `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) over the full repository. If any error-severity finding is reported, apply the needed fix and restart from P3-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (diagnostic counts by severity, 0 errors).
- [x] [P3-T3] Run the final PowerShell test-and-coverage check: `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround over the full repository, including all new/changed tests from Phases 1 and 2. If any test fails, apply the needed fix and restart from P3-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, `Output Summary:` containing full pass/fail test counts (all tests passing, including every new test from P1-T1 and P2-T12 through P2-T20) and the numeric repo-wide post-change command/line-coverage percentage (no placeholders).
- [x] [P3-T4] Produce the PowerShell coverage delta/threshold verification comparing the P0-T8 baseline against the P3-T3 post-change values.
  - Acceptance: `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md` reports baseline coverage, post-change coverage, and coverage for the changed file (`scripts/Publish.ps1`) and the new file (`scripts/Deploy.ps1`) as numeric values with an explicit PASS/FAIL against: no repo-wide regression versus baseline, line coverage >= 85%, command-coverage branch proxy >= 75%, and no production PowerShell file excluded from measurement. Any below-threshold, regressed, or unavailable value makes the outcome remediation-required, not PASS.
- [x] [P3-T5] Confirm AC-6's "no temp files" clause holds across both new/extended test files.
  - Acceptance: Grep of `tests/scripts/Deploy.Tests.ps1` and the new `It` block in `tests/scripts/Publish.Tests.ps1` for `New-TemporaryFile|\[System\.IO\.Path\]::GetTempPath|\$env:TEMP` returns zero matches.
- [x] [P3-T6] Confirm AC-1 through AC-6 all hold simultaneously by cross-referencing their supporting evidence artifacts (P1-T2/P1-T7 for AC-1; P2-T7/P2-T20 for AC-2; P2-T4/P2-T6/P2-T12/P2-T13/P2-T14/P2-T15 for AC-3; P2-T18 for AC-4; P2-T5/P2-T8/P2-T16/P2-T17/P2-T19 for AC-5; P2-T11 through P2-T20 and P3-T5 for AC-6), then check off all 6 boxes in `FEATURE/issue.md`'s `## Acceptance Criteria (early draft)` section and the mirrored 6 boxes in `FEATURE/user-story.md`'s `## Acceptance Criteria` section.
  - Acceptance: `FEATURE/evidence/qa-gates/ac-summary.<ts>.md` lists each of AC-1 through AC-6 with a PASS status and a pointer to its supporting evidence artifact; all 6 checkboxes in `issue.md` and all 6 checkboxes in `user-story.md` are changed from `- [ ]` to `- [x]` with criterion text unchanged.
- [x] [P3-T7] Check off the 6 mirrored items in `FEATURE/spec.md`'s `## Seeded Test Conditions (from potential)` section to keep the three documents in sync.
  - Acceptance: all 6 checkboxes in `spec.md`'s `## Seeded Test Conditions (from potential)` section are changed from `- [ ]` to `- [x]` with criterion text unchanged.
- [x] [P3-T8] Mirror an issue-update comment/body change for Issue #139 summarizing the fix (3-call-site `$null =` suppression, new `Deploy.ps1` wrapper, wrapper-function test seam, full toolchain pass) per the issue-update mirroring convention.
  - Acceptance: `FEATURE/evidence/issue-updates/issue-139.<ts>.md` exists with `Timestamp:`, the exact text intended/posted, `PostedAs: body` or `PostedAs: comment` (or `POSTING BLOCKED` with reason if not posted), and the GitHub URL when posted.
- [x] [P3-T9] Prepare PR notes (summary, risks, validation performed, links to evidence and tests) referencing this plan's Phase 3 QA evidence.
  - Acceptance: A PR description draft exists (recorded in `FEATURE/evidence/other/pr-notes.<ts>.md`) that names the 2 changed/added production files, the 2 new/extended test files, and links to the Phase 3 QA-gate evidence paths.

## Test Plan

- Unit (Pester v5, regression): `tests/scripts/Publish.Tests.ps1` — new `Context 'output contract'` `It` (P1-T1) asserting a captured invocation returns exactly one pipeline object; fails before the fix (P1-T2), passes after (P1-T7).
- Unit (Pester v5, new): `tests/scripts/Deploy.Tests.ps1` — parameter forwarding (P2-T12/P2-T13), `-SkipSign` -> `-AllowUnsigned` mapping (P2-T14/P2-T15), publish-failure/empty-bundle-root short-circuit (P2-T16/P2-T17), `-WhatIf` propagation (P2-T18), returned bundle root (P2-T19), no-CWD-change (P2-T20).
- Coverage evidence (PowerShell): baseline `FEATURE/evidence/baseline/poshqc-test.<ts>.md` (P0-T8); post-change `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` (P3-T3); comparison `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md` (P3-T4).
- Manual/CLI: none required this pass; `Deploy.ps1`'s end-to-end behavior against a real bundle is exercised indirectly via the same wrapper-mocking discipline already validated for `Publish.ps1`/`Install.ps1` in prior features (`#34`, `#36`).

## Open Questions / Notes

- `issue.md`'s Acceptance Criteria heading reads `## Acceptance Criteria (early draft)`; this plan treats that section as the authoritative AC source per the delegation instructions despite the "(early draft)" suffix, and keeps `user-story.md`'s exact-title `## Acceptance Criteria` section in sync at Phase 3.
- The research doc (`research/2026-07-10T11-30-...md`) recommended a dedicated `Deploy.Helpers.psm1` module for the wrapper-function seam; this plan follows the delegation prompt's explicit override (script-scope guarded functions inside `Deploy.ps1`, no third production module) rather than the research recommendation, consistent with the 2-production-file scope estimate in `spec.md`/`issue.md`.
