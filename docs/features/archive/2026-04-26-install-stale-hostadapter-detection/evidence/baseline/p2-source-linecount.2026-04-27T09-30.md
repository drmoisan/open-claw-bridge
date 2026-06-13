# P2-T7 — Post-prune source line count

- Timestamp: 2026-04-27T09-30
- Command: `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1' | Measure-Object -Line).Lines`
- EXIT_CODE: 0

## Output Summary

`Measure-Object -Line` value: 327 (non-empty lines).

True line count via `(Get-Content -LiteralPath 'tests/scripts/Install.Helpers.Tests.ps1').Count`: 374.

Both measures are well under the 500-line cap. The four Compose `Context` blocks (`Test-DockerAvailable`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`) have been removed from the file; the export-surface assertion (which still names all four helpers in the `$expected` array) and the two AC-14 top-level `Describe` blocks (`Get-ProcessMainModulePath defensive branch`, `Get-ListeningProcessId no-listener path`) remain unchanged.

Acceptance: <= 500. PASS.
