# P1-T7 — New sibling file line count

- Timestamp: 2026-04-27T09-30
- Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1' | Measure-Object -Line).Lines`
- EXIT_CODE: 0

## Output Summary

`Measure-Object -Line` value: 156 (non-empty lines).

True line count via `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Compose.Tests.ps1').Count`: 179.

Both measures are well under the 500-line cap. The new file `tests/scripts/Install.Helpers.Compose.Tests.ps1` is a single outer `Describe 'Install.Helpers.psm1 (Compose helpers)'` containing the four extracted Compose `Context` blocks plus the same `BeforeAll` (Import-Module Install.Helpers.psm1 -Force) / `AfterAll` (Remove-Module) preamble used by the source.

Acceptance: <= 500. PASS.
