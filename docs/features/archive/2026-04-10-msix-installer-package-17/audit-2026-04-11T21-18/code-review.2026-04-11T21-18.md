# Code Review

- Timestamp: 2026-04-11T21-18
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `281d21cad251522e231dc7a425cee74bcd06fcc3`
- Feature folder: `docs/features/active/2026-04-10-msix-installer-package-17`
- Feature folder selection rule: used the requested active feature folder, which also matches the branch suffix / issue number `17` and the scoping docs referenced by PR context.

## Executive Summary

This branch adds the MSIX installer implementation surface for OpenClaw MailBridge: manifest, asset pack, publish profiles, packaging scripts, certificate tooling, workflow automation, new MSTest coverage, new Pester coverage, lifecycle evidence, and README guidance. The implementation direction is coherent. The live review pass in this session confirmed clean C# formatting, analyzer compliance, nullable safety, solution tests, and workflow linting.

The branch is not yet ready for PR/merge. The main blocker is the CI acceptance path: the workflow is present and lint-clean, but the current build step only exercises an unsigned packaging path and there is no canonical evidence of a successful `windows-latest` build run. The remaining open spec items are downstream consequences of that gap: the conditional publish-output MSTest branch still skips without `MSIX_PUBLISH_DIR`, the upgrade evidence does not match the exact `1.1.0.0` wording in `spec.md`, and the uninstall evidence does not explicitly record removal of the `bridge/` and `client/` directories.

Top 3 risks:

1. The CI workflow does not currently execute the signed-package path described by the story statement.
2. The authoritative CI acceptance criterion in `user-story.md` remains unchecked.
3. The publish-output test path is still conditional and therefore does not yet prove the workflow’s produced layout.

Go/No-Go recommendation: **No-Go** until the CI packaging criterion and the remaining spec evidence gaps are closed.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Blocker | `.github/workflows/build-msix.yml` | lines `42-43` | The CI build step intentionally bypasses the signed-package path by running `New-MsixDevCert.ps1 -WhatIf` and `build-msix.ps1 -SkipSign`. | Remove `-WhatIf` / `-SkipSign` and make the workflow execute the same packaging-and-signing path that the feature’s story statement promises, or narrow the requirement text if unsigned CI output is actually intended. | The story statement says the pipeline should produce a signed MSIX from a tag push. The current workflow does not do that. | `.github/workflows/build-msix.yml` lines `42-43`; `user-story.md` story statement; `user-story.md` line `64` remains unchecked. |
| Major | `.github/workflows/build-msix.yml`, `user-story.md` | workflow lines `1-45`; `user-story.md` line `64` | The branch lacks canonical evidence of a successful `windows-latest` workflow run, so the CI acceptance criterion is still unresolved even though the workflow file exists. | Execute the workflow successfully on a Windows runner, mirror the result into a canonical evidence artifact under the feature folder, and only then check off the CI acceptance criterion. | Static linting proves syntax, not successful end-to-end behavior. | `evidence/qa-gates/actionlint-build-msix.2026-04-11T20-44.md` shows syntax PASS; `user-story.md` line `64` is still `- [ ]`; `evidence/qa-gates/acceptance-status.2026-04-11T20-44.md` still lists the CI criterion as remaining. |
| Major | `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` | lines `216-219`, `234-237` | The publish-output assertions remain conditional on `MSIX_PUBLISH_DIR`, so the current test pass does not prove the workflow-produced publish layout. | Add a targeted verification step that sets `MSIX_PUBLISH_DIR` and runs the publish-output assertions after the publish stage, or capture equivalent deterministic evidence and update `spec.md` accordingly. | The workflow/layout proof gap will remain until the conditional branch is actually exercised. | `MsixPackageTests.cs` lines `216-219` and `234-237`; live `dotnet test` reported `3` skipped tests; `spec.md` line `246` remains unchecked. |
| Minor | `docs/features/active/2026-04-10-msix-installer-package-17/spec.md` | lines `248-249` | The spec’s remaining upgrade and uninstall checklist text is narrower than the recorded evidence: upgrade evidence uses `1.0.1.0`, and uninstall evidence does not explicitly log `bridge/` and `client/` directory disappearance. | Either collect exact evidence that matches the current spec wording, or narrow the wording to the intended verification level. | The feature is close, but the remaining open checklist items are now mostly evidence-to-text mismatches. | `evidence/other/build-msix-v2.2026-04-11T19-44.md`, `evidence/other/upgrade-v2.2026-04-11T19-44.md`, `evidence/other/uninstall.2026-04-11T19-44.md`, `spec.md` lines `248-249`. |

## Typed Python Audit

No Python files changed in this branch. Typed-Python review is not applicable.

## Test Quality Audit

- The new MSTest coverage is well-structured and deterministic.
- The new Pester files are focused, readable, and no longer depend on `$TestDrive` temporary files.
- Live solution testing passed: `95` total tests, `92` succeeded, `3` skipped, `0` failed.
- Canonical branch evidence reports PASS for the targeted MSIX Pester files and targeted PowerShell analyzer/formatter runs.
- The remaining test-quality gap is not instability. It is missing exercise of the publish-output branch under `MSIX_PUBLISH_DIR`.

## Security / Correctness Checks

- No secrets were introduced into source-controlled runtime code.
- The manifest correctly uses `windows.startupTask` and `runFullTrust`, and the tests verify the absence of `windows.service`.
- `build-msix.ps1` now uses `ShouldProcess` around state-changing operations.
- `New-MsixDevCert.ps1` still requires elevation before importing into `Cert:\LocalMachine\Root`, which is the correct guard.
- The main correctness risk is proof of CI behavior, not proof of the local installer path.

## Research Log

No external research was required for this review. Findings are based on repository policy files, refreshed PR-context artifacts, canonical feature evidence, direct file inspection, and the live verification commands executed in this session.