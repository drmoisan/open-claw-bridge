# Baseline — PoshQC Test (Pester with coverage)

Timestamp: 2026-04-18T00-00
Command: `Invoke-PoshQCTest -Root <repo> -ScanFolders @('scripts','tests')`
EXIT_CODE: 0
Output Summary: PASS. 73 tests passed, 0 failed, 0 skipped, 0 inconclusive, 0 not-run. Total duration 3.29s. Repo-wide line coverage: 81.71% (563 analyzed commands in 12 files). Baseline is above the 80% policy floor.

## Per-file results (baseline test file coverage)

- `tests/scripts/Publish.Tests.ps1`: pass
- `tests/scripts/Publish.Helpers.Tests.ps1`: pass
- `tests/scripts/install-mailbridge.Tests.ps1`: pass
- `tests/scripts/New-MsixDevCert.Tests.ps1`: pass
- `tests/scripts/register-mailbridge-task.Tests.ps1`: pass
- `tests/scripts/runner-scripts.Tests.ps1`: pass
- `tests/scripts/test-mailbridge.Tests.ps1`: pass
- `tests/scripts/uninstall-mailbridge.Tests.ps1`: pass

## Coverage headline

- Repo-wide line coverage (scripts + tests scope): 81.71%.
- Analyzed Commands: 563 across 12 files.
- Policy floor (80%): satisfied.

## Coverage scope

When `-ScanFolders @('scripts','tests')` is supplied to `Invoke-PoshQCTest`, the Pester `CodeCoverage.Path` is overridden at runtime to the resolved scan folder list. This is the behavior used by `PoshQC.Testing.psm1` (see lines 279-286). The number 81.71% therefore reflects all non-test files discovered under `scripts/` as the coverage target.
