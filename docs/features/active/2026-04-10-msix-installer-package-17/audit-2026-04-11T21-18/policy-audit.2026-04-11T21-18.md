# Policy Audit

- Timestamp: 2026-04-11T21-18
- Feature: `docs/features/active/2026-04-10-msix-installer-package-17`
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/msix-installer-package-17`
- Head commit: `281d21cad251522e231dc7a425cee74bcd06fcc3`
- Merge base: `dcb71b791e1ba6f5775d09ab5dee644aec999246`
- Range: `dcb71b791e1ba6f5775d09ab5dee644aec999246..281d21cad251522e231dc7a425cee74bcd06fcc3`
- Feature folder selection rule: selected `docs/features/active/2026-04-10-msix-installer-package-17` because it matches the requested active feature folder, the branch suffix / issue number `17`, and the PR-context scoping docs.
- Provenance: refreshed `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`, direct file inspection, live check-only commands, and canonical feature evidence under `docs/features/active/2026-04-10-msix-installer-package-17/evidence/`.
- Template source: minimal fallback artifact generated because `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md` is not present in this repository.

## Executive Summary

This branch now delivers the intended MSIX packaging structure: manifest, required assets, publish profiles, packaging scripts, certificate tooling, workflow, README guidance, new Pester coverage, and new MSTest coverage. The live quality-gate pass in this review was clean for formatter, analyzer, nullable, solution tests, and workflow lint. The branch-owned QA evidence also shows successful targeted PoshQC runs on the new PowerShell files and successful manual lifecycle evidence for install, next-logon startup, reboot-logon startup, upgrade preservation, uninstall, and local signed package generation.

The branch is still not fully ready for PR/merge. The authoritative `user-story.md` acceptance criterion for CI buildability remains unchecked, and the current workflow evidence is only static. The workflow currently runs `New-MsixDevCert.ps1` with `-WhatIf` and `build-msix.ps1` with `-SkipSign`, which means the CI path does not execute the same signed-package flow that the story statement calls for. In addition, three spec-level checklist items remain unresolved: the conditional publish-output MSTest assertions are still skipped without `MSIX_PUBLISH_DIR`, the upgrade evidence does not match the exact `1.1.0.0` wording in `spec.md`, and the uninstall evidence does not explicitly record removal of the `bridge/` and `client/` directories.

## 1. General Unit Test Policy Compliance

- [✅] [PASS] The new and changed tests are deterministic, isolated, and descriptive. `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` passed with `95` total tests, `92` succeeded, `3` skipped, and `0` failed. Canonical feature evidence `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md` reports the two targeted MSIX Pester files as `PASS`.
- [✅] [PASS] The PowerShell tests no longer use temporary files. A direct search for `$TestDrive` in `tests/scripts/*.Tests.ps1` returned no matches, and the branch-owned artifact `evidence/other/pester-temp-file-check.2026-04-11T19-37.md` records the same outcome.
- [⚠️] [PARTIAL] Scenario completeness is still incomplete for the publish-output test path. `MsixPackageTests` keeps the two publish-output assertions conditional on `MSIX_PUBLISH_DIR`, so the live solution test pass still had `3` skipped tests.
- [✅] [PASS] Coverage evidence is policy-grade. `evidence/qa-gates/coverage-thresholds.2026-04-11T20-44.md` records `BaselineOverallLineCoverage=85.95`, `FinalOverallLineCoverage=85.95`, `BaselineChangedOrNewLineCoverage=100.00`, and `FinalChangedOrNewLineCoverage=100.00`, with no regression and the repository minimum satisfied.

## 2. General Code Change Policy Compliance

- [✅] [PASS] The implementation is cohesive and additive. Packaging concerns stay isolated in `installer/`, `scripts/`, publish profiles, workflow, and tests, and the changed code files remain under the repository size limit.
- [✅] [PASS] Error handling and contracts are explicit. `build-msix.ps1` and `New-MsixDevCert.ps1` fail fast when prerequisites are missing, and `build-msix.ps1` now honors `ShouldProcess` at each state-changing boundary.
- [⚠️] [PARTIAL] Acceptance closure is incomplete. The authoritative `user-story.md` line `64` remains unchecked, and several spec-level checklist items remain unresolved even though the core implementation is in place.
- [✅] [PASS] The final live toolchain pass for this review was clean: `csharpier check .`, analyzer build, nullable build, `dotnet test`, and `./scripts/dev-tools/run-actionlint.ps1` all succeeded.
- [⚠️] [PARTIAL] Fresh PowerShell wrapper reruns were inconclusive in this session because the PoshQC wrapper rejects multi-value scan folders and repo-scope reruns produced non-actionable failures. The branch’s canonical targeted QA artifacts show PASS for the four changed PowerShell files, and editor diagnostics report no current errors in those files.

## 3. Language-Specific Code Change Policy Compliance

- [✅] [PASS] C# policy compliance is strong. `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` is typed, analyzer-clean, nullable-clean, and uses MSTest plus FluentAssertions as required.
- [✅] [PASS] PowerShell policy compliance is strong for the new files. `scripts/build-msix.ps1` and `scripts/New-MsixDevCert.ps1` are advanced functions, use explicit parameters, and the targeted PoshQC evidence for those files is clean.
- [⚠️] [PARTIAL] GitHub Actions compliance is incomplete at the acceptance level. `.github/workflows/build-msix.yml` is syntactically valid and actionlint-clean, but the build step intentionally avoids actual signing and has not been verified by a successful Windows runner artifact.

## 4. Language-Specific Unit Test Policy Compliance

- [⚠️] [PARTIAL] C# unit-test compliance is mostly clean, but the publish-output branch is not exercised in the live solution test pass because `MSIX_PUBLISH_DIR` was unset.
- [✅] [PASS] PowerShell unit-test compliance is clean for the changed files. The two targeted Pester files are Pester v5 tests under `tests/scripts/`, are readable, and the canonical targeted PoshQC test artifact reports PASS.

## 5. Test Coverage Detail

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Baseline overall line coverage | `85.95%` | `>= 80%` | PASS |
| Final overall line coverage | `85.95%` | `>= 80%` | PASS |
| Baseline changed/new line coverage | `100.00%` | No regression required | PASS |
| Final changed/new line coverage | `100.00%` | `>= 90%` for new lines | PASS |
| Publish-output MSTest branch | `Skipped without MSIX_PUBLISH_DIR` | Must be evidenced before final AC closeout | PARTIAL |

Coverage source: `docs/features/active/2026-04-10-msix-installer-package-17/evidence/qa-gates/coverage-thresholds.2026-04-11T20-44.md`.

## 6. Test Execution Metrics

### C# Tests

- Total: `95`
- Succeeded: `92`
- Skipped: `3`
- Failed: `0`
- Duration: `11.8s`

### PowerShell Tests

- Targeted MSIX Pester files: `PASS` per `evidence/qa-gates/poshqc-test.2026-04-11T20-41.md`
- Targeted changed PowerShell analysis: `PASS` per `evidence/qa-gates/poshqc-analyze.2026-04-11T20-41.md`
- Targeted changed PowerShell formatting: `PASS` per `evidence/qa-gates/poshqc-format.2026-04-11T20-41.md`

## 7. Code Quality Checks

| Check | Command | Result | Notes |
|-------|---------|--------|-------|
| C# formatting | `csharpier check .` | PASS | Live run succeeded using the global `csharpier` installation. |
| C# analyzers | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | PASS | Used the SDK-hosted MSBuild entry point available in this shell. |
| C# nullable/type safety | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` | PASS | Live run succeeded. |
| C# tests | `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` | PASS | `95` total, `0` failed. |
| Workflow lint | `./scripts/dev-tools/run-actionlint.ps1` | PASS | Live run returned `EXIT_CODE=0`. |
| PowerShell format | `mcp_drmcopilotext_run_poshqc_format` | PASS | Relied on canonical targeted feature evidence because the wrapper is mutation-oriented and this review stayed check-only. |
| PowerShell analyze | `mcp_drmcopilotext_run_poshqc_analyze` | PASS (canonical evidence) / PARTIAL (live rerun wrapper) | Canonical targeted artifact reports PASS for the four changed files; the live wrapper rerun was inconclusive at repo scope. |
| PowerShell tests | `mcp_drmcopilotext_run_poshqc_test` | PASS (canonical evidence) / PARTIAL (live rerun wrapper) | Canonical targeted artifact reports PASS for the two changed test files; the live wrapper rerun was inconclusive at repo scope. |
| Editor diagnostics | `get_errors` on changed PowerShell files | PASS | No current diagnostics in the changed PowerShell files. |

## 8. Gaps and Exceptions

1. **CI acceptance remains unresolved.** `user-story.md` line `64` is still unchecked, and there is no canonical artifact proving a successful `windows-latest` workflow run.
2. **The workflow’s build step is narrower than the story statement.** `.github/workflows/build-msix.yml` lines `42-43` call `New-MsixDevCert.ps1 -WhatIf` and `build-msix.ps1 -SkipSign`, so the CI path does not execute the signed-package flow described in the story statement.
3. **Three spec checklist items remain open.** `spec.md` lines `231`, `246`, `248`, and `249` remain unresolved because the current evidence set does not prove the exact `windows-latest` build path, the `MSIX_PUBLISH_DIR` publish-output branch, the exact `1.1.0.0` upgrade wording, or explicit removal of the `bridge/` and `client/` directories.
4. **PR-context metadata is partly inconsistent.** The refreshed summary artifact still reports the older head SHA `abd1f73...`, while local git and the refreshed appendix agree on `281d21c...`. This did not change the acceptance inventory, but exact branch-state citations in this audit use local git plus the appendix.

## 9. Summary of Changes

Relative to `development`, the branch adds:

- `.github/workflows/build-msix.yml`
- `installer/Package.appxmanifest` and required MSIX assets
- `scripts/build-msix.ps1` and `scripts/New-MsixDevCert.ps1`
- MSIX publish profiles for bridge and client projects
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`
- `tests/scripts/build-msix.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1`
- active feature docs, evidence, and README guidance for MSIX installation and lifecycle validation

## 10. Compliance Verdict

**Needs revision.**

The branch is structurally strong and passes the live quality gate sequence, but it is not yet ready to open or merge as a PR because the CI acceptance path is not fully verified and the remaining spec/user-story checklist items are not yet evidence-backed.

## Appendix A: Test Inventory

| File | Type | Status | Notes |
|------|------|--------|-------|
| `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` | MSTest | PASS with conditional skips | Manifest, startup-task, icon, publish-profile checks; publish-output branch still conditional on `MSIX_PUBLISH_DIR`. |
| `tests/scripts/build-msix.Tests.ps1` | Pester | PASS | Covered by canonical targeted PoshQC test evidence. |
| `tests/scripts/New-MsixDevCert.Tests.ps1` | Pester | PASS | Covered by canonical targeted PoshQC test evidence. |
| Full solution tests | MSTest | PASS | `95` total, `0` failed. |

## Appendix B: Toolchain Commands Reference

1. `csharpier check .`
2. `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
3. `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
4. `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`
5. `./scripts/dev-tools/run-actionlint.ps1`
6. `mcp_drmcopilotext_run_poshqc_format`
7. `mcp_drmcopilotext_run_poshqc_analyze`
8. `mcp_drmcopilotext_run_poshqc_test`
9. `get_errors` on the changed PowerShell files