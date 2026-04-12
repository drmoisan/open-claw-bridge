---
title: "Remediation Plan: 2026-04-10-msix-installer-package-17 (2026-04-11T21-18)"
issue: "17"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T21-18"
status: "Planned"
status_color: "blue"
version: "1.0"
work_mode: "full-feature"
plan_scope: "Close the remaining CI and evidence-alignment gaps identified by the 2026-04-11T21-18 review artifacts without widening scope beyond the unchecked MSIX acceptance criteria."
---

# Remediation Plan: 2026-04-10-msix-installer-package-17 (2026-04-11T21-18)

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

- **Issue:** #17
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-11T21-18
- **Status:** Planned
- **Version:** 1.0
- **Work Mode:** full-feature
- **Plan Scope:** close the remaining CI and evidence-alignment gaps identified by `remediation-inputs.2026-04-11T21-18.md`.
- **Planning provenance:** local fallback implementing the `atomic-planner` contract because a direct atomic-planner execution surface was not available in this Codex session.

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

This plan is driven by `docs/features/active/2026-04-10-msix-installer-package-17/remediation-inputs.2026-04-11T21-18.md`. The feature implementation is already in place; the remaining work is concentrated in CI execution fidelity, proof of workflow-produced publish output, and final evidence/text reconciliation for the unchecked acceptance items.

## Remediation Scope

### Source Hierarchy

1. **Primary source of truth:** `docs/features/active/2026-04-10-msix-installer-package-17/remediation-inputs.2026-04-11T21-18.md`
2. **Authoritative review artifacts:**
   - `docs/features/active/2026-04-10-msix-installer-package-17/policy-audit.2026-04-11T21-18.md`
   - `docs/features/active/2026-04-10-msix-installer-package-17/code-review.2026-04-11T21-18.md`
   - `docs/features/active/2026-04-10-msix-installer-package-17/feature-audit.2026-04-11T21-18.md`
3. **Acceptance-criteria source files for `full-feature`:**
   - `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`
   - `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`

### Requirements Traceability

| ID | Requirement | Primary source | Planned phase(s) |
|---|---|---|---|
| REQ-001 | Prove the CI build path on `windows-latest` and align it with the signed-package story requirement. | `remediation-inputs.2026-04-11T21-18.md` item 1 | Phase 1, Phase 2 |
| REQ-002 | Exercise the `MSIX_PUBLISH_DIR` publish-output assertions or replace them with equivalent deterministic proof. | `remediation-inputs.2026-04-11T21-18.md` item 2 | Phase 1, Phase 2 |
| REQ-003 | Reconcile the exact upgrade-version wording between `spec.md` and the recorded evidence. | `remediation-inputs.2026-04-11T21-18.md` item 3 | Phase 1, Phase 2 |
| REQ-004 | Extend uninstall evidence so it explicitly records `bridge/` and `client/` directory removal. | `remediation-inputs.2026-04-11T21-18.md` item 4 | Phase 1, Phase 2 |
| REQ-005 | Reconcile `spec.md`, `user-story.md`, and `acceptance-status` only after the new evidence exists. | `remediation-inputs.2026-04-11T21-18.md` item 5 | Phase 2 |

### Evidence Locations

- Baseline evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/`
- Other evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/`
- QA-gate evidence: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/`

### Constraints

- CON-001: Do not widen scope beyond the remaining CI and evidence-alignment gaps.
- CON-002: Do not weaken repository policy or acceptance wording without explicit source-file reconciliation.
- CON-003: Do not check off any remaining acceptance item until matching evidence exists on disk.
- CON-004: Keep the final QA loop intact: format -> lint -> type-check -> test -> workflow lint.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Policy Refresh and Current-State Baseline

- [x] [P0-T1] Record the Phase 0 policy-read artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/phase0-instructions-read.md` after re-reading these files in this exact order: `.github/copilot-instructions.md`, `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`, `.github/instructions/github-actions.instructions.md`, and `AGENTS.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/phase0-instructions-read.md` exists and contains `Timestamp:`, `Policy Order:`, and the exact ordered file list above; if `.github/copilot-instructions.md` is absent, the artifact contains the explicit line `.github/copilot-instructions.md: ABSENT` and continues with the remaining files.

- [x] [P0-T2] Capture the baseline unchecked-item artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/unchecked-items-vs-development.md` by comparing `spec.md` and `user-story.md` against `development`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/unchecked-items-vs-development.md` exists and contains `Timestamp:`, `Command: git diff --unified=0 development -- docs/features/active/2026-04-10-msix-installer-package-17/spec.md docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`, `EXIT_CODE:`, and `Output Summary:` listing exactly these unchecked items and no additional acceptance items: `spec.md` — `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner; `Acceptance criteria 1–9 verified (see user-story.md)`; `MSTest MsixPackageTests.cs: manifest parses as valid XML; startupTask extension present with correct Executable; Version attribute is a valid 4-part version; OpenClaw.MailBridge.exe and OpenClaw.MailBridge.Client.exe present in publish output when MSIX_PUBLISH_DIR env var is set`; `Upgrade scenario: install v1.0.0.0 -> install v1.1.0.0 -> startup task still registered -> bridge.settings.json unchanged`; `Uninstall scenario: Remove-AppxPackage -> startup task absent from Task Manager -> bridge\ and client\ directories gone -> bridge.settings.json still present in %LOCALAPPDATA%\OpenClaw\MailBridge\`; `user-story.md` — `Package can be built from CI using dotnet publish + makeappx.exe (no Visual Studio required)`.

- [x] [P0-T3] Run `pwsh -File scripts/dev-tools/run-actionlint.ps1` and save the workflow-lint baseline artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/workflow-lint.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/workflow-lint.md` exists and contains `Timestamp:`, `Command: pwsh -File scripts/dev-tools/run-actionlint.ps1`, `EXIT_CODE:`, and `Output Summary:` summarizing the pre-change workflow lint status for `.github/workflows/build-msix.yml`.

- [ ] [P0-T4] Run `csharpier .` and save the baseline C# formatting artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/csharp-format.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/csharp-format.md` exists and contains `Timestamp:`, `Command: csharpier .`, `EXIT_CODE:`, and `Output Summary:` stating whether formatting changes were required before remediation work starts.

- [ ] [P0-T5] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the analyzer-build baseline artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/analyzer-build.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/analyzer-build.md` exists and contains `Timestamp:`, `Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`, `EXIT_CODE:`, and `Output Summary:` summarizing analyzer warnings or the clean-pass baseline.

- [ ] [P0-T6] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the nullable/type-safety baseline artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/nullable-build.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/nullable-build.md` exists and contains `Timestamp:`, `Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true`, `EXIT_CODE:`, and `Output Summary:` summarizing the nullable/type-safety baseline.

- [ ] [P0-T7] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` and save the baseline coverage artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/csharp-test-coverage.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/baseline/csharp-test-coverage.md` exists and contains `Timestamp:`, `Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`, `EXIT_CODE:`, and `Output Summary:` with numeric `BaselineOverallLineCoverage:`, `BaselineCoveredLines:`, and `BaselineTotalLines:` values parsed from the generated coverage output.

### Phase 1 — CI Path Closure and Evidence Alignment

- [ ] [P1-T1] Update `.github/workflows/build-msix.yml` so the authoritative CI path no longer relies on `New-MsixDevCert.ps1 -WhatIf` and `build-msix.ps1 -SkipSign` if the requirement continues to promise a signed MSIX artifact.
  - Acceptance: The workflow text aligns with the signed-package requirement, and the resulting YAML still passes `actionlint`.

- [ ] [P1-T2] Add a deterministic verification step that exercises the `MSIX_PUBLISH_DIR` publish-output assertions after the publish outputs are created.
  - Acceptance: The verification path runs without `Assert.Inconclusive`, and evidence is captured for the executed assertion branch.

- [ ] [P1-T3] Resolve the remaining upgrade-version gap by choosing one deterministic path: executed `1.1.0.0` proof or `spec.md` wording reconciliation to the exact version proven by the executed scenario.
  - Acceptance: The upgrade scenario in `spec.md` and the verification path refer to one exact version target with no conflicting version strings remaining.

- [ ] [P1-T4] Update the uninstall verification path so it emits explicit success signals for both `bridge/` and `client/` directory removal.
  - Acceptance: The uninstall verification command or assertion text contains distinct checks for `bridge/` removal and `client/` removal before the final evidence artifact is captured.

- [ ] [P1-T5] Save the canonical CI-path success artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/ci-path-success.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/ci-path-success.md` exists and contains `Timestamp:`, `Command:` naming the executed CI-path proof command, `EXIT_CODE: 0`, and `Output Summary:` confirming the successful `windows-latest` CI path that satisfies the signed-package requirement or its reconciled wording.

- [ ] [P1-T6] Save the canonical `MSIX_PUBLISH_DIR` assertion-path artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/msix-publish-dir-assertion.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/msix-publish-dir-assertion.md` exists and contains `Timestamp:`, `Command:` naming the executed `MSIX_PUBLISH_DIR` assertion command, `EXIT_CODE: 0`, and `Output Summary:` confirming that the `MSIX_PUBLISH_DIR` assertion path executed successfully without `Assert.Inconclusive`.

- [ ] [P1-T7] Save the canonical upgrade-version proof or spec-reconciliation artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/upgrade-version-reconciliation.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/upgrade-version-reconciliation.md` exists and contains `Timestamp:`, `Command:` naming the executed upgrade-proof or spec-reconciliation command, `EXIT_CODE: 0`, and `Output Summary:` stating either that `1.1.0.0` was proven by executed evidence or that `spec.md` was reconciled to the exact version proven by the executed scenario.

- [ ] [P1-T8] Save the canonical uninstall directory-removal artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/uninstall-directory-removal.md`.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/uninstall-directory-removal.md` exists and contains `Timestamp:`, `Command:` naming the executed uninstall verification command, `EXIT_CODE: 0`, and `Output Summary:` explicitly stating that `bridge/` was removed, `client/` was removed, and `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` remained in place.

### Phase 2 — Final QA Loop and Acceptance Reconciliation

- [ ] [P2-T1] Run `csharpier .` and save the final C# formatting artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/csharp-format.md`; if formatting changes files, restart Phase 2 from [P2-T1].
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/csharp-format.md` exists and contains `Timestamp:`, `Command: csharpier .`, `EXIT_CODE: 0`, and `Output Summary:` confirming that no formatting changes remain at the end of the clean pass.

- [ ] [P2-T2] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the final analyzer-build artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/analyzer-build.md`; if the build fails, restart Phase 2 from [P2-T1].
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/analyzer-build.md` exists and contains `Timestamp:`, `Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`, `EXIT_CODE: 0`, and `Output Summary:` confirming a clean analyzer pass.

- [ ] [P2-T3] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the final nullable/type-safety artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/nullable-build.md`; if the build fails, restart Phase 2 from [P2-T1].
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/nullable-build.md` exists and contains `Timestamp:`, `Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true`, `EXIT_CODE: 0`, and `Output Summary:` confirming a clean nullable/type-safety pass.

- [ ] [P2-T4] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` and save the final coverage-enabled C# test artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/csharp-test-coverage.md`; if the test run fails, restart Phase 2 from [P2-T1].
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/csharp-test-coverage.md` exists and contains `Timestamp:`, `Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`, `EXIT_CODE: 0`, and `Output Summary:` with numeric `PostChangeOverallLineCoverage:`, `PostChangeCoveredLines:`, and `PostChangeTotalLines:` values parsed from the generated coverage output.

- [ ] [P2-T5] Save the coverage-reconciliation artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/coverage-reconciliation.md` by comparing the baseline coverage from [P0-T7] with the post-change coverage from [P2-T4] and the changed/new-code coverage for the touched implementation.
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/coverage-reconciliation.md` exists and contains `BaselineOverallLineCoverage:`, `PostChangeOverallLineCoverage:`, `ChangedOrNewCodeCoverage:`, and `RepositoryCoverageExpectation: PASS` or `RepositoryCoverageExpectation: FAIL`, with the expectation result aligned to repository coverage rules.

- [ ] [P2-T6] Run `pwsh -File scripts/dev-tools/run-actionlint.ps1` and save the final workflow-lint artifact in `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/workflow-lint.md`; if the lint run fails, restart Phase 2 from [P2-T1].
  - Acceptance: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/workflow-lint.md` exists and contains `Timestamp:`, `Command: pwsh -File scripts/dev-tools/run-actionlint.ps1`, `EXIT_CODE: 0`, and `Output Summary:` confirming the final workflow lint pass for `.github/workflows/build-msix.yml`.

- [ ] [P2-T7] Reconcile `user-story.md` and `spec.md` acceptance checkboxes against the new evidence, checking off only the items that are fully proven.
  - Acceptance: `user-story.md` line `64` and the remaining `spec.md` items are checked only if their evidence exists; otherwise they remain unchecked.

- [ ] [P2-T8] Update the acceptance-status summary artifact so it matches the final checkbox state exactly.
  - Acceptance: The summary records updated totals, checked counts, remaining counts, and the exact remaining items list.

- [ ] [P2-T9] Run the repository orchestration artifact validator equivalent to `validate_orchestration_artifacts` for artifact type `plan` on `c:\Users\DanMoisan\repos\open-claw-bridge\docs\features\active\2026-04-10-msix-installer-package-17\remediation-plan.2026-04-11T21-18.md` and keep this file at the same path.
  - Acceptance: The repository orchestration artifact validator exits `0` for artifact type `plan` on this exact plan file path.

## Verification Strategy

- The CI path must be proven by executed evidence, not static YAML inspection alone.
- The publish-output branch must be exercised with `MSIX_PUBLISH_DIR` defined.
- The upgrade and uninstall evidence must either match the current spec wording exactly or the spec wording must be reconciled to the intended scenario.
- After implementation, the plan path must remain `c:\Users\DanMoisan\repos\open-claw-bridge\docs\features\active\2026-04-10-msix-installer-package-17\remediation-plan.2026-04-11T21-18.md`.

## Open Questions / Notes

- This plan intentionally stays narrow because the branch implementation and most lifecycle evidence are already present.
- A direct `atomic-planner -> atomic-executor` preflight delegation was not available in this Codex session; this file is a local fallback artifact and should still be treated as needing external execution against the repository’s normal orchestration surface if one becomes available.