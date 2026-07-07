# PowerShell Test-and-Coverage Baseline (P0-T5)

- Timestamp: 2026-07-07T01-30 (original MCP attempt); remediation capture 2026-07-07T04-10
- Command (MCP, still fails): `mcp__drm-copilot__run_poshqc_test` (workspace_root = repo root; also retried with `scan_folders: ["tests/scripts"]`)
- EXIT_CODE (MCP): 4294967295 (unsigned representation of process exit code -1; consistent across two attempts)
- Command (remediation, bundled-pipeline workaround): `pwsh -NoProfile -File <scratchpad>/run-poshqc-test-openclaw.ps1` importing the bundled `PoshQC.psd1` directly and calling `Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad>/pester.openclaw.baseline.runsettings.psd1` (settings identical to the bundled `pester.runsettings.psd1` except `CodeCoverage.Path` corrected to this repository's 29 pre-existing PowerShell production files under `scripts/**`, i.e. the F16 script `scripts/Test-OpenClawBicepParameterSecrets.ps1` and its test excluded/removed to measure the true pre-change baseline).
- EXIT_CODE (remediation): 0

## Root Cause (documented, not fabricated) — MCP tool failure

The MCP tool's bundled Pester settings file (`.../drm-copilot/extensions/drm-copilot/resources/powershell/PoshQC/settings/pester.runsettings.psd1`, resolved relative to the extension's own module root, not this repo) hardcodes `CodeCoverage.Path` to a fixed allowlist of files that exist only in the `drm-copilot` source repository itself (e.g. `.claude/hooks/check-python-test-purity.ps1`, `.claude/hooks/enforce-epic-merge-gate.ps1`, `scripts/dev-tools/Invoke-FullRelease.ps1`). None of those paths exist in `open-claw-bridge`. `Invoke-PoshQCTest`'s `-SettingsPath` parameter defaults to this bundled, extension-relative path (`$script:PesterSettings`) with no repo-local override resolved from `workspace_root`. This is a pre-existing environment/tooling defect independent of this feature (F16), reproduced first by feature #111 (F11) and now by F16.

## Remediation (established F11 precedent, reproduced here)

Per the F11 precedent (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/baseline/poshqc-test.2026-07-02T17-25.md`), the bundled `PoshQC.psd1` module was imported directly (bypassing the MCP wrapper) and `Invoke-PoshQCTest` was called with a corrected `-SettingsPath` — a scratchpad-only copy of the bundled runsettings whose `CodeCoverage.Path` allowlist was replaced with this repository's actual `scripts/**` production files. No runsettings or coverage-tool file was written into the repo tree; the corrected settings file lives only in the session scratchpad. Per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy ("No production file may be excluded from coverage measurement"), the corrected `CodeCoverage.Path` includes every `scripts/**` production `.ps1`/`.psm1` file with no `ExcludedPath` entries (unlike the bundled drm-copilot settings, which exclude several of its own CLI-wrapper scripts — that exclusion pattern was not reproduced here).

To capture a genuine pre-F16 baseline (rather than merely the current repo-wide figure), the feature's two new files (`scripts/Test-OpenClawBicepParameterSecrets.ps1` and `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1`) were temporarily moved out of the repo tree to the session scratchpad, the corrected-runsettings coverage run was executed against the remaining 29 production files, and the two files were then moved back to their original locations. This is the documented method used when a true pre-change baseline was practical to obtain (it was, since the new files are additive and untracked).

## Coverage Values (baseline, F16 files excluded)

- Repo-wide baseline test result: **Tests Passed: 358, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0**
- Repo-wide baseline command/line coverage: **89.66%** (1,963 analyzed Commands in 29 Files; all `scripts/**` production `.ps1`/`.psm1`; `.claude/hooks/**` excluded per the documented Issue #66 T4-scaffolding coverage-scope exclusion — not applicable here since no `.claude/hooks/**` file is part of this repository's `scripts/**` production set).
- Repo-wide baseline branch coverage: Pester v5 emits command-level coverage only and produces no branch-percentage metric for PowerShell (tool reported "89.66% / 0%" where the second value is the unused branch slot). Command coverage counts commands inside every branch arm, so untaken branch arms register as uncovered commands; the 89.66% command figure is the branch-sensitive baseline signal. This matches repository precedent (F11 baseline `poshqc-test.2026-07-02T17-25.md`, and that artifact's own citations to feature #62 and #58).
- Raw tool output (scratchpad-relative, ephemeral, gitignored `artifacts/pester/`): `powershell-coverage.xml` (JaCoCo-style), `powershell-coverage.koverage.xml`, `pester-junit.xml`.

## Disposition

Numeric coverage is now captured using the F11-precedent bundled-pipeline workaround. This baseline artifact is **complete**: repo-wide baseline command coverage is 89.66% across 29 pre-existing production PowerShell files, with 358/358 tests passing. This baseline is compared against the post-change figures in P6-T3/P6-T4.
