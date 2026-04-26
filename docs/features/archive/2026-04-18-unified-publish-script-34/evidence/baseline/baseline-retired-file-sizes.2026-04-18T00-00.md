# Baseline Retired File Sizes

- Timestamp: 2026-04-18T00-00
- Command: `Get-Item + Measure-Object -Line` on `scripts/build-msix.ps1` and `tests/scripts/build-msix.Tests.ps1`
- EXIT_CODE: 0
- Output Summary: Both retirement targets exist on disk prior to deletion. `scripts/build-msix.ps1`: 10,930 bytes, 241 lines. `tests/scripts/build-msix.Tests.ps1`: 6,575 bytes, 145 lines. Phase 3 deletion checks will assert `Test-Path` returns `$false` against both paths.

## File table

| Path | Size (bytes) | Line count |
|---|---|---|
| `scripts/build-msix.ps1` | 10930 | 241 |
| `tests/scripts/build-msix.Tests.ps1` | 6575 | 145 |
