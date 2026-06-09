# P3-T4 — Final line counts

- Timestamp: 2026-04-27T09-30
- Commands:
  1. `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines`
  2. `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1' | Measure-Object -Line).Lines`
- EXIT_CODE: 0

## Output Summary

`Measure-Object -Line` (non-empty line count):
- `tests/scripts/Install.Helpers.Tests.ps1`: 327
- `tests/scripts/Install.Helpers.Compose.Tests.ps1`: 156

`(Get-Content).Count` (true file line count, used to enforce the 500-line policy):
- `tests/scripts/Install.Helpers.Tests.ps1`: 374
- `tests/scripts/Install.Helpers.Compose.Tests.ps1`: 179

Both files are well under the 500-line cap. Acceptance: each <= 500. PASS.
