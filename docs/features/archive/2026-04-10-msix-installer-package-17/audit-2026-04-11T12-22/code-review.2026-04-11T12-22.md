# Code Review

- Timestamp: 2026-04-11T12-22
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `abd1f73cc8515b81046bfdbb89a5474cd8fdc384`
- Feature folder: `docs/features/active/2026-04-10-msix-installer-package-17`
- Feature folder selection rule: single active feature folder, matched to issue/branch suffix `17` and the materially changed `spec.md` and `user-story.md` recorded in `artifacts/pr_context.summary.txt`.

## Executive Summary

This branch adds the main ingredients needed for MSIX packaging: a startup-task manifest, icon assets, MSIX publish profiles, packaging and certificate scripts, README instructions, and new test coverage in both MSTest and Pester. The implementation direction is correct. The new tests pass directly, the manifest structure is coherent, and the publish profiles capture the required directory-layout settings.

The branch is not ready to open or merge as a PR. The most significant blocker is that the promised CI workflow does not exist even though the plan and spec say it does. The branch also commits generated staging output, leaves the authoritative `issue.md` on an obsolete Windows Service design, and misses a required PowerShell safety guard in `build-msix.ps1`. In addition, the core install/logon/reboot/upgrade/uninstall behaviors are not backed by canonical operator evidence, so the feature’s primary acceptance criteria remain mostly unverified.

**Top 3 risks**

1. **Missing CI implementation** — `.github/workflows/build-msix.yml` is absent, so the branch does not satisfy the “built from CI” acceptance criterion.
2. **Incorrect repository state** — `installer/staging/AppxManifest.xml` is committed despite `.gitignore` excluding that generated staging directory.
3. **Policy/documentation drift** — the PowerShell `ShouldProcess` rule is not honored in `build-msix.ps1`, and `issue.md` still describes a Windows Service instead of the implemented startup-task model.

**Go/No-Go recommendation:** No-Go. The branch needs remediation before PR readiness.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Blocker | `.github/workflows/build-msix.yml` | File missing | The CI workflow claimed by `plan.2026-04-10T19-59.md` Phase 4 and `spec.md` does not exist in the branch. | Add the actual workflow, validate it, and update the plan only after the file and its evidence exist. | Acceptance criterion 6 explicitly requires CI buildability; the current branch cannot demonstrate that requirement. | `artifacts/pr_context.summary.txt` lists `.github/workflows/build-msix.yml` in the implementation strategy, but `list_dir .github` shows no `workflows/` directory and a repo search found no workflow file. |
| Major | `installer/staging/AppxManifest.xml` | Tracked generated file | A generated staging artifact is committed even though `.gitignore` excludes `installer/staging/`. | Remove the tracked staging file from the branch and keep staging output untracked. | Generated packaging output should not be versioned; it also weakens plan credibility because the branch claims ignore rules while still carrying the artifact. | `artifacts/pr_context.appendix.txt` lists `A installer/staging/AppxManifest.xml`; `.gitignore` lines `57-59` exclude `installer/staging/`, `artifacts/msix/`, and `artifacts/publish/`. |
| Major | `docs/features/active/2026-04-10-msix-installer-package-17/issue.md` | `## Proposed Behavior`, `## Acceptance Criteria`, `## Constraints & Risks` | The issue document still specifies a Windows Service model, while the implementation, spec, user story, and tests all use `windows.startupTask`. | Update `issue.md` so the issue narrative, criteria, and constraints align with the startup-task architecture. | The mismatch creates conflicting sources of truth for reviewers, operators, and future planners. | `issue.md` says “Registers the bridge host as a Windows Service”; `spec.md` and `user-story.md` explicitly reject `windows.service`; `installer/Package.appxmanifest` declares `uap5:Extension Category="windows.startupTask"`. |
| Major | `scripts/build-msix.ps1` | line `33` and main body | The script declares `SupportsShouldProcess` but never calls `$PSCmdlet.ShouldProcess(...)` before performing state-changing actions. | Gate staging writes, PRI generation, packing, and signing with `ShouldProcess`, or remove `SupportsShouldProcess` if the script is intentionally not confirmable. | Repo PowerShell policy requires state-changing actions to honor `ShouldProcess`. | `grep` shows `[CmdletBinding(SupportsShouldProcess)]` at line `33`; no `ShouldProcess` call exists anywhere in the file. |
| Major | `tests/scripts/build-msix.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1` | multiple `$TestDrive` usages | The new Pester tests create temporary filesystem content without an explicit exception in the repo’s general unit-test policy. | Rework tests to avoid temporary files, or document and narrow an explicit exception before keeping these tests. | The repo policy expressly prohibits temporary files unless authorized. | `tests/scripts/build-msix.Tests.ps1` uses `$TestDrive` at lines `55`, `84-86`, `98`, `113`, `127`, `144`, `147`; `tests/scripts/New-MsixDevCert.Tests.ps1` uses `$TestDrive` at line `100`. |
| Minor | `scripts/build-msix.ps1` | `Version` parameter / `Invoke-VersionStamp` | The script stamps whatever `-Version` string is supplied into the manifest without validating the required 4-part MSIX format before packaging. | Add early validation for the 4-part `Major.Minor.Build.Revision` format before mutating staging content. | This would fail faster and produce clearer user feedback. | The parameter is `[string]$Version = '1.0.0.0'`; no regex or parse validation is present before `SetAttribute('Version', $Version)`. |
| Minor | Feature evidence set | branch-wide | The branch lacks canonical operator evidence for install, startup on next logon, reboot behavior, upgrade preservation, and uninstall behavior. | Add operator-evidence artifacts or automated integration evidence and keep the acceptance checkboxes in sync with that evidence. | Static manifest/tests are insufficient for installer lifecycle claims. | `artifacts/pr_context.summary.txt` reports `Verification evidence (feature docs + canonical artifacts): No canonical verification evidence parsed`. |

## Typed Python Audit

No Python files changed in this branch. Typed-Python review is not applicable.

## Test Quality Audit

- The new MSTest and Pester additions are targeted and descriptive.
- `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings` passed with `95` total tests, `92` succeeded, `3` skipped, `0` failed.
- Direct `Invoke-Pester` on the new MSIX test files passed `7/7`.
- Direct `Invoke-Pester -Path 'tests/scripts'` passed `26/26`.
- The new test additions do not prove the full installer lifecycle. They validate structure and helper behavior, not actual install/logon/reboot/upgrade/uninstall behavior on a clean machine.
- The new Pester tests conflict with the repository’s temporary-file policy as written.

## Security / Correctness Checks

- No secrets were introduced in code or docs.
- The manifest correctly uses `runFullTrust` and `windows.startupTask`, and the tests verify the absence of `windows.service`.
- The packaging scripts locate Windows SDK tools explicitly and fail when a tool cannot be found.
- The certificate script requires elevation before importing into `Cert:\LocalMachine\Root`, which is an appropriate safety check.
- The main correctness gap is lifecycle verification, not static structure.

## Research Log

No external research was required for this review. The findings are based on repository policy files, PR context artifacts, direct file inspection, and fresh local verification commands.