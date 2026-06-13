# File-Size Cap — Phase 6 Test Files (P6-T16)

Timestamp: 2026-06-05T22-09

Command: `(Get-Content <path> | Measure-Object -Line).Lines` for each touched test file.

EXIT_CODE: 0

Output Summary:
- `tests/scripts/Install.Tests.ps1` = 441 lines (<= 499: True)
- `tests/scripts/Install.Preflight.Tests.ps1` = 350 lines (<= 499: True)
- `tests/scripts/Install.Helpers.Tests.ps1` = 341 lines (<= 499: True)
- All three test files remain under the 500-line cap after the Phase 6 additions.
