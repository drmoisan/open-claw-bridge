Timestamp: 2026-07-10T13-10

Command: `pwsh -NoProfile -Command "Import-Module Pester -MinimumVersion 5.0 -Force; $config = New-PesterConfiguration; $config.Run.Path = 'tests/scripts/Install.Preflight.Tests.ps1'; $config.Run.PassThru = $true; $config.Filter.FullName = '*Get-HostAdapterPreflightUri default base URL*'; $result = Invoke-Pester -Configuration $config; exit $result.FailedCount"`

EXIT_CODE: 0

Output Summary: Targeted re-run of the P1-T1 `It` block against the fixed `scripts/Install.Preflight.psm1` (default `$baseUrl` now `http://host.docker.internal:4319`, no `/v1`) now passes: `Tests Passed: 1, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 26`. Confirms AC-5.
