---
title: "Remediation Plan: 2026-04-10-msix-installer-package-17 (2026-04-11T12-22)"
issue: "17"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T12-22"
status: "Planned"
status_color: "blue"
version: "1.0"
work_mode: "full-feature"
plan_scope: "Remediate the MSIX feature review findings: add the missing CI workflow, remove tracked staging output, align issue.md to the startup-task architecture, implement real ShouldProcess gating, remove policy-forbidden temporary-file Pester tests, capture lifecycle and coverage evidence, and synchronize checklist state with evidence."
---

# Remediation Plan: 2026-04-10-msix-installer-package-17 (2026-04-11T12-22)

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

- **Issue:** #17
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T12-22
- **Status:** Planned
- **Version:** 1.0
- **Work Mode:** full-feature
- **Plan Scope:** Remediate the authoritative review findings from `remediation-inputs.2026-04-11T12-22.md` without widening scope beyond the MSIX packaging feature and the listed policy gaps.

## Required References

- General Code Change Policy: [`.github/instructions/general-code-change.instructions.md`](../../../../.github/instructions/general-code-change.instructions.md)
- General Unit Test Policy: [`.github/instructions/general-unit-test.instructions.md`](../../../../.github/instructions/general-unit-test.instructions.md)
- C# Coding Standards: [`.github/instructions/csharp-code-change.instructions.md`](../../../../.github/instructions/csharp-code-change.instructions.md)
- C# Unit Test Policy: [`.github/instructions/csharp-unit-test.instructions.md`](../../../../.github/instructions/csharp-unit-test.instructions.md)
- PowerShell Coding Standards: [`.github/instructions/powershell-code-change.instructions.md`](../../../../.github/instructions/powershell-code-change.instructions.md)
- PowerShell Unit Test Policy: [`.github/instructions/powershell-unit-test.instructions.md`](../../../../.github/instructions/powershell-unit-test.instructions.md)
- GitHub Actions CI Policy: [`.github/instructions/github-actions.instructions.md`](../../../../.github/instructions/github-actions.instructions.md)
- Workspace Policy Rollup: [`AGENTS.md`](../../../../AGENTS.md)

## Overview

This remediation plan is driven by `docs/features/active/2026-04-10-msix-installer-package-17/remediation-inputs.2026-04-11T12-22.md`, with `policy-audit.2026-04-11T12-22.md`, `code-review.2026-04-11T12-22.md`, `feature-audit.2026-04-11T12-22.md`, `spec.md`, and `research.md` used only to clarify exact repair targets and verification commands. The plan restores policy compliance, rebuilds missing CI and operator evidence, and reconciles all checklist state to on-disk proof.

## Remediation Scope

### Source Hierarchy

1. **Primary source of truth:** `docs/features/active/2026-04-10-msix-installer-package-17/remediation-inputs.2026-04-11T12-22.md`
2. **Authoritative review artifacts:**
	 - `docs/features/active/2026-04-10-msix-installer-package-17/policy-audit.2026-04-11T12-22.md`
	 - `docs/features/active/2026-04-10-msix-installer-package-17/code-review.2026-04-11T12-22.md`
	 - `docs/features/active/2026-04-10-msix-installer-package-17/feature-audit.2026-04-11T12-22.md`
3. **Acceptance-criteria source files for `full-feature`:**
	 - `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`
	 - `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`
4. **Repair targets that must be synchronized with the evidence:**
	 - `docs/features/active/2026-04-10-msix-installer-package-17/issue.md`
	 - `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md`

### Requirements Traceability

| ID | Requirement | Primary source | Planned phase(s) |
|---|---|---|---|
| REQ-001 | Add `.github/workflows/build-msix.yml` and validate it with workflow-specific evidence. | `remediation-inputs.2026-04-11T12-22.md` item 1 | Phase 0, Phase 1, Phase 5 |
| REQ-002 | Remove `installer/staging/AppxManifest.xml` from version control while preserving ignore coverage for `installer/staging/`. | `remediation-inputs.2026-04-11T12-22.md` item 2 | Phase 1, Phase 5 |
| REQ-003 | Align `issue.md` to the implemented `windows.startupTask` architecture. | `remediation-inputs.2026-04-11T12-22.md` item 3 | Phase 2, Phase 5 |
| REQ-004 | Implement real `ShouldProcess` gating in `scripts/build-msix.ps1` for all state-changing operations. | `remediation-inputs.2026-04-11T12-22.md` item 4 | Phase 3, Phase 5 |
| REQ-005 | Remove the `$TestDrive` temporary-file policy violation from the new Pester tests without weakening repository policy. | `remediation-inputs.2026-04-11T12-22.md` item 5 | Phase 3, Phase 5 |
| REQ-006 | Produce canonical install, next-logon, reboot, upgrade, and uninstall evidence for the MSIX lifecycle. | `remediation-inputs.2026-04-11T12-22.md` item 6 | Phase 4, Phase 5 |
| REQ-007 | Capture policy-grade baseline, post-change, and changed/new-code coverage evidence. | `remediation-inputs.2026-04-11T12-22.md` item 7 | Phase 0, Phase 5 |
| REQ-008 | Synchronize `plan.2026-04-10T19-59.md` immediately after remediation-plan creation. | Additional mandatory constraint in the request | Phase 0 |
| REQ-009 | Synchronize `plan.2026-04-10T19-59.md` again after final remediation verification completes. | Additional mandatory constraint in the request | Phase 5 |
| REQ-010 | Update `spec.md` and `user-story.md` acceptance checkboxes only after matching evidence exists on disk. | `remediation-inputs.2026-04-11T12-22.md` items 6-8 and feature-audit output | Phase 5 |

### Evidence Locations

- Remediation baseline evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/`
- Regression-testing evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/regression-testing/`
- Other remediation evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/`
- Final QA evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/`

### Constraints

- CON-001: Do not weaken repository policies to make the branch appear compliant.
- CON-002: Do not keep generated staging output under `installer/staging/` tracked in git.
- CON-003: Do not check off acceptance criteria or original-plan tasks without matching evidence artifacts on disk.
- CON-004: Do not replace install, logon, reboot, upgrade, uninstall, or coverage evidence with prose-only claims.
- CON-005: Do not widen scope beyond the MSIX packaging feature and the remediation findings enumerated in `remediation-inputs.2026-04-11T12-22.md`.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Context, Policy, Baseline, and Initial Plan Sync

- [x] [P0-T1] Search for `.github/copilot-instructions.md` and save either the file path or an auditable negative-evidence claim in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/copilot-instructions-check.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `SearchScope: c:\Users\DanMoisan\repos\open-claw-bridge`, `SearchPatterns: .github/copilot-instructions.md`, and `SearchResult:` with either the resolved path or `none`.

- [x] [P0-T2] Read the required policy files in order and save `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/phase0-instructions-read.yyyy-MM-ddTHH-mm.md`.
	- Required order: `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`, `.github/instructions/github-actions.instructions.md`, `AGENTS.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Policy Order:`, and the exact file list above in the same order.

- [x] [P0-T3] Reconcile `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md` against the current on-disk evidence before any code or documentation remediation begins. [REQ-008]
	- Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md` leaves every workflow-related task unchecked unless `.github/workflows/build-msix.yml` and matching workflow-validation evidence already exist, and leaves every lifecycle acceptance checkbox unchecked unless a matching artifact already exists under the feature `evidence/` folders.

- [x] [P0-T4] Run `csharpier check .` and save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/csharpier-check.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE: 0`, and `Output Summary:` with the checked-file count.

- [x] [P0-T5] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/msbuild-analyzers.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, the exact `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the analyzer-enabled build passed.

- [x] [P0-T6] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/msbuild-nullable.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, the exact `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the nullable/type-safety build passed.

- [x] [P0-T7] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` and save the baseline test-plus-coverage result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/dotnet-test-coverage.yyyy-MM-ddTHH-mm.md`. [REQ-007]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`, `EXIT_CODE: 0`, and `Output Summary:` with numeric `BaselineOverallLineCoverage:` and test pass counts parsed from the run output and its generated coverage file.

- [x] [P0-T8] Parse the newest baseline `coverage.cobertura.xml` plus `git diff --unified=0 development...HEAD` into `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/coverage-summary.yyyy-MM-ddTHH-mm.md`. [REQ-007]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: baseline-coverage-parse`, `EXIT_CODE: 0`, `Output Summary:`, and numeric lines for `BaselineOverallLineCoverage:`, `BaselineChangedOrNewLineCoverage:`, and `CoverageSource:` pointing to the parsed Cobertura file.

- [x] [P0-T9] Run `mcp_drmcopilotext_run_poshqc_format` for `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/poshqc-format.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE: 0`, and `Output Summary:` identifying the four targeted files.

- [x] [P0-T10] Run `mcp_drmcopilotext_run_poshqc_analyze` for `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/poshqc-analyze.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE:`, and `Output Summary:` that lists every finding for the four targeted files exactly as reported.

- [x] [P0-T11] Run `mcp_drmcopilotext_run_poshqc_test` for `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/poshqc-test.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE:`, and `Output Summary:` with the baseline pass/fail counts for the two targeted test files.

- [x] [P0-T12] Record the missing workflow baseline in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/workflow-missing.yyyy-MM-ddTHH-mm.md`. [REQ-001]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: Test-Path .github/workflows/build-msix.yml`, `EXIT_CODE: 0`, and `Output Summary: WorkflowMissing=True`.

- [x] [P0-T13] Record the tracked staging-artifact baseline in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/staging-artifact-tracked.yyyy-MM-ddTHH-mm.md`. [REQ-002]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: git diff --name-status development...HEAD`, `EXIT_CODE: 0`, and `Output Summary:` listing `installer/staging/AppxManifest.xml` as tracked in the branch baseline.

- [x] [P0-T14] Run `scripts/dev-tools/run-actionlint.ps1` and save the baseline result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/remediation-baseline/actionlint.yyyy-MM-ddTHH-mm.md`. [REQ-001]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: scripts/dev-tools/run-actionlint.ps1`, `EXIT_CODE: 0`, and `Output Summary:` describing the pre-change GitHub Actions lint baseline before `.github/workflows/build-msix.yml` is added.

### Phase 1 — Workflow Creation and Repository Hygiene

- [x] [P1-T1] Create `.github/workflows/build-msix.yml` with a single `build-msix` job on `windows-latest`, triggers `push.tags: ['v*']` and `workflow_dispatch.inputs.version`, two `dotnet publish` steps that pass `/p:PublishProfile=msix`, one `pwsh` step that runs `scripts/New-MsixDevCert.ps1` and `scripts/build-msix.ps1`, and one `actions/upload-artifact@v4` step that uploads `artifacts/msix/*.msix` as `msix-package`. [REQ-001]
	- Acceptance: `Select-String` checks on `.github/workflows/build-msix.yml` find `workflow_dispatch`, `tags: ['v*']`, at least two `dotnet publish` lines, `scripts/build-msix.ps1`, and `actions/upload-artifact@v4` with `name: msix-package`.

- [x] [P1-T2] Run `scripts/dev-tools/run-actionlint.ps1` and save the workflow validation result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/actionlint-build-msix.yyyy-MM-ddTHH-mm.md`. [REQ-001]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: scripts/dev-tools/run-actionlint.ps1`, `EXIT_CODE: 0`, and `Output Summary:` mentioning `.github/workflows/build-msix.yml`.

- [x] [P1-T3] Save a static workflow-content verification artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/workflow-static-check.yyyy-MM-ddTHH-mm.md`. [REQ-001]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: workflow-static-check`, `EXIT_CODE: 0`, and `Output Summary:` with exact booleans for `HasWorkflowDispatch=True`, `HasTagTrigger=True`, `HasPublishProfileMsix=True`, `HasBuildScriptStep=True`, and `HasArtifactUpload=True`.

- [x] [P1-T4] Remove `installer/staging/AppxManifest.xml` from version control without deleting the `installer/staging/` ignore rule. [REQ-002]
	- Acceptance: Running `git diff --name-status development...HEAD` no longer lists `installer/staging/AppxManifest.xml`.

- [x] [P1-T5] Save a repository-hygiene verification artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/staging-ignore-check.yyyy-MM-ddTHH-mm.md`. [REQ-002]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: git check-ignore -v installer/staging/`, `EXIT_CODE: 0`, and `Output Summary:` showing the `.gitignore` rule that excludes `installer/staging/`.

### Phase 2 — `issue.md` Architecture Alignment

- [x] [P2-T1] Update the `## Proposed Behavior` section in `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` so it describes startup-task registration on user logon instead of Windows Service registration. [REQ-003]
	- Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` contains the phrase `startup task` in `## Proposed Behavior`, and that section contains no `Windows Service` text.

- [x] [P2-T2] Update the `## Acceptance Criteria (early draft)` section in `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` so criteria 2, 3, and 5 match the startup-task, reboot-logon, and uninstall behavior already defined in `user-story.md`. [REQ-003]
	- Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` contains `startup task named OpenClaw MailBridge`, contains `restarts automatically on user login`, contains `removes the startup task`, and contains no acceptance-criteria bullet mentioning `Windows Service`.

- [x] [P2-T3] Update the `## Constraints & Risks` section in `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` so it explains the Session 0 / Outlook COM incompatibility and states that `windows.startupTask` is required. [REQ-003]
	- Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` contains `Session 0`, contains `Outlook COM`, contains `windows.startupTask`, and contains no statement that the bridge `must run as the logged-in user or use a session-aware launch model` via a Windows Service.

- [x] [P2-T4] Save a terminology-alignment verification artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/issue-terminology-alignment.yyyy-MM-ddTHH-mm.md`. [REQ-003]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: terminology-alignment-check`, `EXIT_CODE: 0`, and `Output Summary:` with exact booleans for `IssueUsesStartupTask=True`, `SpecUsesStartupTask=True`, `UserStoryUsesStartupTask=True`, and `IssueUsesWindowsService=False`.

### Phase 3 — PowerShell Safety Gating and Temporary-File Test Removal

- [x] [P3-T1] Save a `build-msix.ps1` state-change inventory to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/build-msix-state-changes.yyyy-MM-ddTHH-mm.md`. [REQ-004]
	- Acceptance: The artifact exists and identifies the current staging-manifest write, staging-layout copy, PRI generation, MSIX pack, and signing operations as separate state-changing actions.

- [x] [P3-T2] Save a Pester temp-file inventory to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/pester-temp-file-usage.yyyy-MM-ddTHH-mm.md`. [REQ-005]
	- Acceptance: The artifact exists and lists every `$TestDrive` occurrence in `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` with file path and line number.

- [x] [P3-T3] [expect-fail] Add the Pester scenario `WhatIf leaves installer/staging/AppxManifest.xml absent` to `tests/scripts/build-msix.Tests.ps1`. [REQ-004]
	- Acceptance: Running `Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -FullNameFilter 'WhatIf leaves installer/staging/AppxManifest.xml absent' -PassThru` fails, and `docs/features/active/2026-04-10-msix-installer-package-17/evidence/regression-testing/build-msix-whatif-staging.yyyy-MM-ddTHH-mm.md` exists with `Timestamp:`, the exact `Command:`, and a non-zero `EXIT_CODE:`.

- [x] [P3-T4] [expect-fail] Add the Pester scenario `WhatIf does not invoke MakePri, makeappx, or signtool` to `tests/scripts/build-msix.Tests.ps1`. [REQ-004]
	- Acceptance: Running `Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -FullNameFilter 'WhatIf does not invoke MakePri, makeappx, or signtool' -PassThru` fails, and `docs/features/active/2026-04-10-msix-installer-package-17/evidence/regression-testing/build-msix-whatif-tools.yyyy-MM-ddTHH-mm.md` exists with `Timestamp:`, the exact `Command:`, and a non-zero `EXIT_CODE:`.

- [x] [P3-T5] Add the pure helper function `Get-StampedAppxManifestXml` to `scripts/build-msix.ps1` so version stamping can be tested without filesystem writes. [REQ-004] [REQ-005]
	- Acceptance: `scripts/build-msix.ps1` contains `function Get-StampedAppxManifestXml`, and `Invoke-VersionStamp` uses the helper before any file write occurs.

- [x] [P3-T6] Gate the staging-manifest write plus staging-layout copy in `scripts/build-msix.ps1` behind `$PSCmdlet.ShouldProcess(...)`. [REQ-004]
	- Acceptance: `scripts/build-msix.ps1` contains a `ShouldProcess` call for the staged `AppxManifest.xml` write target and a separate `ShouldProcess` call for the staging-layout assembly target.

- [x] [P3-T7] Gate PRI generation, MSIX pack, and signing in `scripts/build-msix.ps1` behind `$PSCmdlet.ShouldProcess(...)` while preserving `-SkipSign` semantics. [REQ-004]
	- Acceptance: `scripts/build-msix.ps1` contains three additional `ShouldProcess` calls whose targets are `resources.pri`, the output `.msix`, and the signing step, and `-SkipSign` still bypasses the signing path.

- [x] [P3-T8] Replace the `$TestDrive`-based version-stamp and layout assertions in `tests/scripts/build-msix.Tests.ps1` with in-memory XML strings, command mocks, and shim call-count assertions. [REQ-005]
	- Acceptance: `tests/scripts/build-msix.Tests.ps1` contains no `$TestDrive` reference and still contains named scenarios for version stamping, missing publish directory handling, layout assembly, `makeappx` arguments, and `-SkipSign` behavior.

- [x] [P3-T9] Add the pure helper function `Get-CertificateExportPaths` to `scripts/New-MsixDevCert.ps1` so export-path behavior can be tested without creating temporary files. [REQ-005]
	- Acceptance: `scripts/New-MsixDevCert.ps1` contains `function Get-CertificateExportPaths`, and the export logic uses the helper to compute `.pfx` and `.cer` paths.

- [x] [P3-T10] Replace the `$TestDrive`-based output-path assertion in `tests/scripts/New-MsixDevCert.Tests.ps1` with in-memory path assertions and command mocks. [REQ-005]
	- Acceptance: `tests/scripts/New-MsixDevCert.Tests.ps1` contains no `$TestDrive` reference and still contains named scenarios for subject forwarding and output-path export behavior.

- [x] [P3-T11] Make the two new `build-msix.Tests.ps1` `WhatIf` scenarios pass and save the targeted result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/build-msix-targeted-pester.yyyy-MM-ddTHH-mm.md`. [REQ-004]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -PassThru`, `EXIT_CODE: 0`, and `Output Summary:` showing both `WhatIf` scenarios passed.

- [x] [P3-T12] Save a temp-file policy verification artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/pester-temp-file-check.yyyy-MM-ddTHH-mm.md`. [REQ-005]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: Select-String -Path 'tests/scripts/build-msix.Tests.ps1','tests/scripts/New-MsixDevCert.Tests.ps1' -Pattern '\$TestDrive'`, `EXIT_CODE: 1`, and `Output Summary: No $TestDrive usage remains in the targeted Pester files`.

### Phase 4 — End-to-End Build, Install, Upgrade, Reboot, and Uninstall Evidence

Autonomous execution boundary: After Phase 3 completes, atomic execution pauses before the manual lifecycle steps in this phase. Resume Phase 4 only after the externally produced lifecycle evidence artifacts required by `[P4-T7]` exist on disk.

- [x] [P4-T1] Save a manual-bootstrap checkpoint artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/manual-bootstrap-checkpoint.yyyy-MM-ddTHH-mm.md` that records the required elevated PowerShell prerequisites for `scripts/New-MsixDevCert.ps1` and marks the plan as awaiting manual lifecycle execution. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: manual-bootstrap-checkpoint`, `EXIT_CODE: 0`, and `Output Summary:` with `RequiresElevatedPowerShell=True`, `RequiresWindowsSdk=True`, `RequiresTrustedCertificateImport=True`, and `ExecutionStatus=PAUSE_FOR_MANUAL_RESUME`.

- [x] [P4-T2] Publish `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` with `/p:PublishProfile=msix` and save the result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/publish-bridge.yyyy-MM-ddTHH-mm.md`. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: dotnet publish src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj /p:PublishProfile=msix`, `EXIT_CODE: 0`, and `Output Summary:` confirming `artifacts/publish/bridge/OpenClaw.MailBridge.exe` exists.

- [x] [P4-T3] Publish `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj` with `/p:PublishProfile=msix` and save the result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/publish-client.yyyy-MM-ddTHH-mm.md`. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: dotnet publish src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix`, `EXIT_CODE: 0`, and `Output Summary:` confirming `artifacts/publish/client/OpenClaw.MailBridge.Client.exe` exists.

- [x] [P4-T4] Run `scripts/build-msix.ps1` to create a signed `1.0.0.0` package and save the result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/build-msix-v1.yyyy-MM-ddTHH-mm.md`. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, the exact `Command:`, `EXIT_CODE: 0`, and `Output Summary:` with the signed package path `artifacts/msix/OpenClaw.MailBridge_1.0.0.0_x64.msix`.

- [x] [P4-T5] Save a manual lifecycle host-prerequisites artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/manual-lifecycle-host-prereqs.yyyy-MM-ddTHH-mm.md` that records the exact external host requirements for install, next-logon, reboot, upgrade, and uninstall validation. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: manual-lifecycle-host-prereqs`, `EXIT_CODE: 0`, and `Output Summary:` with `RequiresCleanWindowsMachine=True`, `RequiresSignOutSignIn=True`, `RequiresRebootSignIn=True`, and `RequiredArtifacts=dev-cert-create,install-v1,logon-startup,reboot-logon,pre-upgrade-settings,build-msix-v2,upgrade-v2,uninstall`.

- [x] [P4-T6] Record the manual-execution pause in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/manual-lifecycle-execution-boundary.yyyy-MM-ddTHH-mm.md` and stop autonomous execution until the required external lifecycle artifacts exist on disk. [REQ-006]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: manual-lifecycle-execution-boundary`, `EXIT_CODE: 0`, and `Output Summary:` with `AutonomousExecutionPaused=True`, `ResumeCondition=AllRequiredLifecycleArtifactsPresent`, and the exact list of required artifact basenames from `[P4-T5]`.

- [x] [P4-T7] On resume, verify that externally produced lifecycle evidence exists on disk for development certificate creation, install, next-logon, reboot, pre-upgrade settings seed, version `1.0.1.0` package build, upgrade, and uninstall. [REQ-006]
	- Acceptance: The newest matching artifacts exist under `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/` for `dev-cert-create.yyyy-MM-ddTHH-mm.md`, `install-v1.yyyy-MM-ddTHH-mm.md`, `logon-startup.yyyy-MM-ddTHH-mm.md`, `reboot-logon.yyyy-MM-ddTHH-mm.md`, `pre-upgrade-settings.yyyy-MM-ddTHH-mm.md`, `build-msix-v2.yyyy-MM-ddTHH-mm.md`, `upgrade-v2.yyyy-MM-ddTHH-mm.md`, and `uninstall.yyyy-MM-ddTHH-mm.md`, and each verified artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` line.

### Phase 5 — Final QA Loop, Coverage Closure, Acceptance Sync, and Final Plan Sync

- [x] [P5-T1] Run `mcp_drmcopilotext_run_poshqc_format` for `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/poshqc-format.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE: 0`, and `Output Summary:` naming the four targeted files.
	- Restart rule: If formatting changes any file or exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T2] Run `mcp_drmcopilotext_run_poshqc_analyze` for `scripts/build-msix.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/build-msix.Tests.ps1`, and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/poshqc-analyze.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE: 0`, and `Output Summary:` reporting zero PSScriptAnalyzer findings for the four targeted files.
	- Restart rule: If the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T3] Run `mcp_drmcopilotext_run_poshqc_test` for `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1`, then save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/poshqc-test.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: 0`, and `Output Summary:` showing all targeted Pester scenarios passed.
	- Restart rule: If the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T4] Run `csharpier check .` and save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/csharpier-check.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE: 0`, and `Output Summary:` with the checked-file count.
	- Restart rule: If formatting changes are required or the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T5] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/msbuild-analyzers.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, the exact `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the analyzer-enabled build passed.
	- Restart rule: If the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T6] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the final result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/msbuild-nullable.yyyy-MM-ddTHH-mm.md`.
	- Acceptance: The artifact exists and contains `Timestamp:`, the exact `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing the nullable/type-safety build passed.
	- Restart rule: If the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T7] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` and save the final test-plus-coverage result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/dotnet-test-coverage.yyyy-MM-ddTHH-mm.md`. [REQ-007]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`, `EXIT_CODE: 0`, and `Output Summary:` with numeric `PostChangeOverallLineCoverage:` and final test pass counts.
	- Restart rule: If the command exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.

- [x] [P5-T8] Parse the newest final `coverage.cobertura.xml` plus `git diff --unified=0 development...HEAD` into `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/coverage-summary.yyyy-MM-ddTHH-mm.md`. [REQ-007]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: final-coverage-parse`, `EXIT_CODE: 0`, `Output Summary:`, and numeric lines for `PostChangeOverallLineCoverage:`, `PostChangeChangedOrNewLineCoverage:`, and `CoverageSource:`.

- [x] [P5-T9] Compare the Phase 0 and Phase 5 coverage artifacts in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/coverage-thresholds.yyyy-MM-ddTHH-mm.md`. [REQ-007]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: coverage-threshold-compare`, `EXIT_CODE: 0`, `Output Summary:`, numeric lines for `BaselineOverallLineCoverage:`, `PostChangeOverallLineCoverage:`, `BaselineChangedOrNewLineCoverage:`, `PostChangeChangedOrNewLineCoverage:`, and a machine-checkable `ThresholdResult:` equal to `PASS` only when post-change overall coverage is greater than or equal to baseline coverage and changed/new-code coverage is recorded numerically.

- [x] [P5-T10] Run `scripts/dev-tools/run-actionlint.ps1` again and save the final workflow-lint result in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/actionlint-build-msix.yyyy-MM-ddTHH-mm.md`. [REQ-001]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: scripts/dev-tools/run-actionlint.ps1`, `EXIT_CODE: 0`, and `Output Summary:` mentioning `.github/workflows/build-msix.yml`.

- [x] [P5-T11] Reconcile the acceptance checkboxes in `docs/features/active/2026-04-10-msix-installer-package-17/spec.md` against the verified evidence from Phases 1 through 5. [REQ-010]
	- Acceptance: Every checkbox changed to `[x]` in `spec.md` is backed by at least one artifact under `evidence/other/` or `evidence/qa-gates/`, and every unmet acceptance item remains unchecked.

- [x] [P5-T12] Reconcile the acceptance checkboxes in `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md` against the verified evidence from Phases 1 through 5. [REQ-010]
	- Acceptance: Every checkbox changed to `[x]` in `user-story.md` is backed by at least one artifact under `evidence/other/` or `evidence/qa-gates/`, and every unmet acceptance item remains unchecked.

- [x] [P5-T13] Save an acceptance-criteria status summary to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/acceptance-status.yyyy-MM-ddTHH-mm.md`. [REQ-006] [REQ-010]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: acceptance-status-summary`, `EXIT_CODE: 0`, `Output Summary:`, `TotalAcceptanceCriteria:`, `CheckedOffAcceptanceCriteria:`, and `RemainingAcceptanceCriteria:`.

- [x] [P5-T14] Reconcile `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md` against the final evidence set after all QA tasks pass. [REQ-009]
	- Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md` contains only evidence-backed completed tasks, leaves any unverified workflow or lifecycle item unchecked, and matches the artifacts present under `evidence/remediation-baseline/`, `evidence/regression-testing/`, `evidence/other/`, and `evidence/qa-gates/`.

- [x] [P5-T15] Save a final branch-state verification artifact to `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/branch-state.yyyy-MM-ddTHH-mm.md`. [REQ-001] [REQ-002] [REQ-009]
	- Acceptance: The artifact exists and contains `Timestamp:`, `Command: git diff --name-status development...HEAD`, `EXIT_CODE: 0`, and `Output Summary:` showing `.github/workflows/build-msix.yml` is present in the branch diff while `installer/staging/AppxManifest.xml` is absent.

## Verification Strategy

- Test scenarios for `scripts/build-msix.ps1`:
	- `WhatIf leaves installer/staging/AppxManifest.xml absent`
	- `WhatIf does not invoke MakePri, makeappx, or signtool`
	- Existing helper scenarios must remain present after the temp-file refactor.
- Test scenarios for `scripts/New-MsixDevCert.ps1`:
	- Subject forwarding to `New-SelfSignedCertificate`
	- Export-path computation without temporary filesystem use
- Lifecycle evidence requirements:
	- `dev-cert-create` evidence must record elevated certificate creation with the generated thumbprint and export paths.
	- `install-v1` evidence must record package installation on a clean Windows machine.
	- `logon-startup` evidence must record startup registration visibility, bridge-process presence, and client JSON status.
	- `reboot-logon` evidence must record the same signals after a restart.
	- `pre-upgrade-settings` evidence must record the sentinel configuration value before the upgrade.
	- `build-msix-v2` evidence must record creation of the signed `1.0.1.0` package.
	- `upgrade-v2` evidence must record version advancement plus preserved sentinel config.
	- `uninstall` evidence must record package removal, startup-registration disappearance, and preserved config.
- Coverage evidence requirements:
	- Phase 0 must record baseline overall coverage and baseline changed/new-code coverage.
	- Phase 5 must record post-change overall coverage and post-change changed/new-code coverage.
	- The threshold artifact must compare both sets numerically.
- Final QA loop rule:
	- If any Phase 5 formatting, linting, build, type-safety, or test command changes files or exits non-zero, correct the cause and restart Phase 5 from `[P5-T1]`.
- Preflight handoff rule:
	- After this plan is updated, submit the exact file path `c:\Users\DanMoisan\repos\open-claw-bridge\docs\features\active\2026-04-10-msix-installer-package-17\remediation-plan.2026-04-11T12-22.md` to the `atomic_executor` agent with the exact directive `DIRECTIVE: PREFLIGHT VALIDATION ONLY`.
	- Only the exact signals `PREFLIGHT: ALL CLEAR` or `PREFLIGHT: REVISIONS REQUIRED` are valid.
	- If `PREFLIGHT: REVISIONS REQUIRED` is returned, apply the delta to this same file and resubmit the same file path until `PREFLIGHT: ALL CLEAR` is returned.

## Open Questions / Notes

- No architecture questions remain open for this remediation plan. The startup-task model is already the repository-validated direction, and the remediation work is limited to closing evidence, policy, workflow, and checklist gaps.
- Phase 4 requires a Windows machine with the Windows SDK available and sufficient privileges to import the development certificate into a trusted certificate store.
- The original feature plan claimed workflow completion without an on-disk workflow. This remediation plan therefore requires both an initial sync in Phase 0 and a final sync in Phase 5.
