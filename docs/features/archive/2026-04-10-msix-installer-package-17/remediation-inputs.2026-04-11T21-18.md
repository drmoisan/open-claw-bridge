# Remediation Inputs

- Timestamp: 2026-04-11T21-18
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Source audits:
  - `policy-audit.2026-04-11T21-18.md`
  - `code-review.2026-04-11T21-18.md`
  - `feature-audit.2026-04-11T21-18.md`

## Required Fixes

1. **Close the CI acceptance gap in `.github/workflows/build-msix.yml`.**
   - Files: `.github/workflows/build-msix.yml`, `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`, `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`
   - Expected behavior: the workflow must prove the feature can be built from CI on `windows-latest` using the MSIX publish pipeline, and it must align with the story-level expectation of producing a signed MSIX artifact from a tag push.
   - Minimum change: stop relying on `New-MsixDevCert.ps1 -WhatIf` and `build-msix.ps1 -SkipSign` for the authoritative CI path, or explicitly narrow the authored requirement if unsigned CI packaging is the intended outcome.
   - Verification commands/tasks:
     - `./scripts/dev-tools/run-actionlint.ps1`
     - Successful GitHub Actions run of `.github/workflows/build-msix.yml` on `windows-latest`
     - Canonical evidence mirror under `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/`
   - Acceptance criteria: `user-story.md` line `64`; `spec.md` lines `231` and `235`

2. **Exercise the publish-output test branch that is currently skipped.**
   - Files: `.github/workflows/build-msix.yml`, `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`, `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`
   - Expected behavior: the workflow or an equivalent deterministic verification path must set `MSIX_PUBLISH_DIR` and run the publish-output assertions so the layout proof is no longer conditional-only.
   - Minimum change: add a targeted test step or equivalent proof after publish outputs are produced.
   - Verification commands/tasks:
     - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` with `MSIX_PUBLISH_DIR` set to the publish root
     - Evidence mirror showing the publish-output assertions executed without `Assert.Inconclusive`
   - Acceptance criteria: `spec.md` line `246`

3. **Reconcile the spec’s remaining upgrade evidence wording.**
   - Files: `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`, `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/build-msix-v2.*.md`, `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/upgrade-v2.*.md`
   - Expected behavior: either capture evidence for the exact `1.1.0.0` wording currently in `spec.md`, or update the spec text to the intended version sequence if `1.0.1.0` is the correct upgrade scenario.
   - Minimum change: align evidence and scoping text so the checkbox can be checked without ambiguity.
   - Verification commands/tasks:
     - If keeping `1.1.0.0`: rebuild and rerun the upgrade scenario with that exact version
     - If changing the spec: update the scoping text and revalidate acceptance alignment
   - Acceptance criteria: `spec.md` line `248`

4. **Capture explicit uninstall directory-removal proof.**
   - Files: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/other/uninstall.*.md`, `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`
   - Expected behavior: the uninstall evidence must explicitly record that the package `bridge/` and `client/` directories are gone, in addition to the already-recorded package absence and preserved settings.
   - Minimum change: extend the uninstall verification step and evidence artifact with directory-level checks.
   - Verification commands/tasks:
     - `Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage`
     - Directory/path verification against the former install location
   - Acceptance criteria: `spec.md` line `249`

5. **Reconcile acceptance status after the new evidence exists.**
   - Files: `docs/features/active/2026-04-10-msix-installer-package-17/user-story.md`, `docs/features/active/2026-04-10-msix-installer-package-17/spec.md`, `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/acceptance-status.*.md`
   - Expected behavior: source checkboxes must match the new evidence exactly; only PASS items are checked.
   - Minimum change: update the checkboxes and acceptance-status summary only after fixes 1-4 are completed and verified.
   - Verification commands/tasks:
     - Re-run the feature-review acceptance reconciliation step after all evidence is on disk
   - Acceptance criteria: all remaining unchecked items listed in `feature-audit.2026-04-11T21-18.md`

## Do Not Do

- Do not widen scope beyond the CI/evidence alignment needed to close the remaining acceptance criteria.
- Do not weaken repository policy or mark checkboxes complete without matching evidence on disk.
- Do not rely on prose-only claims in place of canonical evidence artifacts.
- Do not silently keep `-WhatIf` / `-SkipSign` in the authoritative CI path if the requirement continues to promise a signed package.

## Acceptance Criteria Not Yet Met

1. `user-story.md` :: Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
   - Minimum change to meet it: prove a successful `windows-latest` CI build path and align the workflow with the signed-package story requirement.

2. `spec.md` :: `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner.
   - Minimum change to meet it: capture a successful Windows-run build artifact for the workflow path.

3. `spec.md` :: Acceptance criteria `1–9` verified (see `user-story.md`).
   - Minimum change to meet it: close the CI criterion above so the aggregate checklist can be checked.

4. `spec.md` :: `MsixPackageTests.cs` publish-output assertion when `MSIX_PUBLISH_DIR` is set.
   - Minimum change to meet it: execute the conditional publish-output branch with `MSIX_PUBLISH_DIR` defined and save the proof.

5. `spec.md` :: Upgrade scenario exact `v1.0.0.0 -> v1.1.0.0` wording.
   - Minimum change to meet it: either produce matching evidence or revise the spec wording to the intended version sequence.

6. `spec.md` :: Uninstall scenario explicit `bridge/` and `client/` directory removal.
   - Minimum change to meet it: extend the uninstall evidence with explicit directory-removal checks.

7. Aggregate acceptance closure.
   - Minimum change to meet it: after items `1-6` are complete, update the source checkboxes and the acceptance-status summary so they match the new evidence set exactly.