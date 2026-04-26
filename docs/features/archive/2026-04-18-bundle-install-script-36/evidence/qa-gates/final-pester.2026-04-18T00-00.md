# Final QA Gate — PoshQC Test (Pester with coverage)

Timestamp: 2026-04-18T00-00
Command: `Invoke-PoshQCTest -Root <repo> -ScanFolders @('scripts','tests')` plus three targeted `Invoke-Pester -Configuration` runs (one per new production file) to compute the new-code coverage headline.
EXIT_CODE: 0
Output Summary: PASS. Tests discovered: 143 total across 11 test files. Results: 143 passed, 0 failed, 0 skipped, 0 inconclusive, 0 not-run. Duration 11.31s. Repo-wide line coverage: 86.39% (904 analyzed commands in 15 files). Targeted new-code coverage: `scripts/Install.Helpers.psm1` 96.32%, `scripts/Install.ps1` 90.29%, `scripts/Uninstall.ps1` 93.75%. All four thresholds met: repo-wide >= 80%, each targeted file >= 90%, zero test failures, zero test regressions vs baseline.

## Per-file results (test files)

| Test file | Count | Result |
|---|---|---|
| `tests/scripts/Install.Helpers.Tests.ps1` | 43 | pass |
| `tests/scripts/Install.Tests.ps1` | 18 | pass |
| `tests/scripts/Uninstall.Tests.ps1` | 9 | pass |
| `tests/scripts/Publish.Helpers.Tests.ps1` | legacy | pass |
| `tests/scripts/Publish.Tests.ps1` | legacy | pass |
| `tests/scripts/install-mailbridge.Tests.ps1` | legacy | pass |
| `tests/scripts/uninstall-mailbridge.Tests.ps1` | legacy | pass |
| `tests/scripts/register-mailbridge-task.Tests.ps1` | legacy | pass |
| `tests/scripts/runner-scripts.Tests.ps1` | legacy | pass |
| `tests/scripts/test-mailbridge.Tests.ps1` | legacy | pass |
| `tests/scripts/New-MsixDevCert.Tests.ps1` | legacy | pass |

Total: 143 passed.

## Coverage headline

| Scope | Value |
|---|---|
| Repo-wide line coverage (scripts + tests scope) | **86.39%** (post-change) |
| Repo-wide baseline (Phase 0) | 81.71% |
| Repo-wide delta | +4.68pp (no regression) |
| `scripts/Install.Helpers.psm1` | **96.32%** (183/190 commands) |
| `scripts/Install.ps1` | **90.29%** (93/103 commands) |
| `scripts/Uninstall.ps1` | **93.75%** (45/48 commands) |

## Regression check

- Baseline tests: 73 passed (Phase 0; all pre-existing scripts tests).
- Post-change tests: 143 passed.
- Delta: +70 tests (43 Install.Helpers + 18 Install + 9 Uninstall = 70 new tests). Every baseline test still passes; zero regressions across the full suite.
