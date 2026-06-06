# P0-T3 — Pre-change source line count

- Timestamp: 2026-04-27T09-30
- Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines`
- EXIT_CODE: 0

## Output Summary

`Measure-Object -Line` value: 455 (this measure counts lines containing non-empty content; PowerShell `Measure-Object -Line` does not count blank lines).

True line count via `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1').Count`: 521.

Bytes on disk: 27642.

The 500-line file-size policy is enforced against total file lines. Per `(Get-Content).Count = 521`, the file is 21 lines over the 500-line cap and the split is required. Confirmed > 500.

## Sanity check (exact PowerShell)

```
(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1').Count
521
```
