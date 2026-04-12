# Remediation Inputs

- Timestamp: 2026-04-11T12-22
- Feature folder: `docs/features/active/2026-04-10-msix-installer-package-17`
- Base branch: `development`
- Source review artifacts:
  - `policy-audit.2026-04-11T12-22.md`
  - `code-review.2026-04-11T12-22.md`
  - `feature-audit.2026-04-11T12-22.md`

## Required Fixes

1. **Add the missing CI workflow and validate it**
   - Files: create `.github/workflows/build-msix.yml`; update `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md` only after the workflow and evidence exist.
   - Expected behavior: the repository contains a workflow that publishes both projects with `/p:PublishProfile=msix`, creates the package using the branch’s MSIX scripts, and uploads the `.msix` artifact.
   - Acceptance criteria addressed: feature-audit criteria `6` and `16`.
   - Verification commands/tasks:
     - `actionlint <workflow-file>` or repo-equivalent workflow lint
     - Trigger path verification for tag and `workflow_dispatch` syntax
     - Static grep confirming `workflow_dispatch`, `dotnet publish`, and `upload-artifact`

2. **Remove generated staging output from version control**
   - Files: remove `installer/staging/AppxManifest.xml` from the branch; verify `.gitignore` remains correct.
   - Expected behavior: no generated files under `installer/staging/` are tracked in git.
   - Acceptance criteria addressed: repository hygiene prerequisite for feature readiness.
   - Verification commands/tasks:
     - `git diff --name-status development...HEAD`
     - `git check-ignore -v installer/staging/`

3. **Synchronize `issue.md` with the implemented startup-task architecture**
   - Files: `docs/features/active/2026-04-10-msix-installer-package-17/issue.md`
   - Expected behavior: `issue.md` no longer describes a Windows Service; its behavior, acceptance criteria, and constraints match the implemented `windows.startupTask` design.
   - Acceptance criteria addressed: documentation consistency supporting all primary criteria.
   - Verification commands/tasks:
     - Inspect `issue.md`, `spec.md`, and `user-story.md` for consistent terminology (`startup task`, not `Windows Service`)

4. **Implement PowerShell safety gating in `build-msix.ps1`**
   - Files: `scripts/build-msix.ps1`; corresponding tests in `tests/scripts/build-msix.Tests.ps1`
   - Expected behavior: state-changing work (staging writes, PRI generation, packing, signing) is gated by `$PSCmdlet.ShouldProcess(...)` or the script removes the unsupported `SupportsShouldProcess` declaration.
   - Acceptance criteria addressed: PowerShell policy compliance for the packaging script.
   - Verification commands/tasks:
     - `mcp_drmcopilotext_run_poshqc_analyze`
     - targeted `Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -PassThru`

5. **Resolve the temporary-file policy violation in the new Pester tests**
   - Files: `tests/scripts/build-msix.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`, and if needed the relevant policy exception source with explicit justification.
   - Expected behavior: either the tests stop creating temporary filesystem content, or a narrow approved exception is documented before the tests remain in place.
   - Acceptance criteria addressed: general unit-test policy compliance for the new test files.
   - Verification commands/tasks:
     - static inspection for `$TestDrive` usage
     - `mcp_drmcopilotext_run_poshqc_test` and direct `Invoke-Pester` rerun after the chosen fix

6. **Produce canonical evidence for installer lifecycle criteria**
   - Files: add canonical evidence artifacts under the feature folder’s evidence structure and update `user-story.md` / `spec.md` checkboxes only when verified.
   - Expected behavior: install, next-logon startup, reboot, upgrade, and uninstall behavior each have auditable evidence artifacts with commands, timestamps, exit codes, and output summaries.
   - Acceptance criteria addressed: feature-audit criteria `1` through `5`, plus `14` and `15` where applicable.
   - Verification commands/tasks:
     - Build package end-to-end on Windows SDK-equipped machine
     - `Add-AppxPackage -Path <package.msix>` on clean Windows machine
     - reboot/logon validation
     - upgrade re-install validation
     - `Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage`

7. **Capture policy-grade coverage evidence**
   - Files: canonical evidence artifact(s) under the feature folder and any supporting notes in the audit trail
   - Expected behavior: numeric baseline coverage, post-change coverage, and changed/new-code coverage are all available for this feature, or the gaps are explicitly resolved before seeking a PASS-style audit outcome.
   - Acceptance criteria addressed: policy-audit PASS gating.
   - Verification commands/tasks:
     - `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings`
     - fresh parse of generated `coverage.cobertura.xml`
     - diff/new-code coverage command or artifact suitable for this repository

8. **Synchronize plan checklist state with evidence on disk**
   - Files: `docs/features/active/2026-04-10-msix-installer-package-17/plan.2026-04-10T19-59.md`
   - Expected behavior: completed checkboxes reflect files and evidence that actually exist. In particular, the Phase 4 workflow tasks must not remain checked unless the workflow file and validation evidence exist.
   - Acceptance criteria addressed: plan integrity and auditability.
   - Verification commands/tasks:
     - compare plan checkboxes to on-disk files and evidence artifacts
     - re-run review after synchronization

## Do Not Do

- Do not weaken repository policies to make the branch appear compliant.
- Do not keep `installer/staging/` outputs tracked in git.
- Do not mark acceptance criteria or plan tasks complete without matching evidence on disk.
- Do not replace missing runtime/operator verification with prose-only claims.
- Do not widen the scope beyond the MSIX packaging feature and the policy issues listed above.

## Unmet Acceptance Criteria And Minimum Changes Required

1. **Install on clean Windows machine** — minimum change: produce and record real install evidence.
2. **Startup task active and bridge starts on next logon** — minimum change: record post-install/logon evidence.
3. **Restart on user login after reboot** — minimum change: capture reboot/logon evidence.
4. **Upgrade preserves `bridge.settings.json`** — minimum change: execute and record an upgrade scenario.
5. **Uninstall removes startup task and binaries, leaves config** — minimum change: execute and record uninstall evidence.
6. **Built from CI** — minimum change: add and validate `.github/workflows/build-msix.yml`.
7. **End-to-end `.msix` production** — minimum change: run `build-msix.ps1` successfully after publish and store evidence.
8. **End-to-end certificate creation/export** — minimum change: run `New-MsixDevCert.ps1` successfully in an elevated session and store evidence.