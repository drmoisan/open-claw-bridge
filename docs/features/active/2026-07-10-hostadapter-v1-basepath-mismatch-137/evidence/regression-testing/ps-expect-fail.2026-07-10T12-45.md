Timestamp: 2026-07-10T12-45

Command: `pwsh -NoProfile -Command "Import-Module Pester -MinimumVersion 5.0 -Force; $config = New-PesterConfiguration; $config.Run.Path = 'tests/scripts/Install.Preflight.Tests.ps1'; $config.Run.PassThru = $true; $config.Filter.FullName = '*Get-HostAdapterPreflightUri default base URL*'; $result = Invoke-Pester -Configuration $config; exit $result.FailedCount"`

EXIT_CODE: 1

Output Summary: [expect-fail] Targeted run of the new `It` block ("resolves the default (no OpenClaw__HostAdapter__BaseUrl key in EnvMap) to a URI with no /v1 segment (issue #137)") against the current (pre-fix) `scripts/Install.Preflight.psm1` fails as expected: `Tests Passed: 0, Failed: 1, Skipped: 0, Inconclusive: 0, NotRun: 26`. Assertion failure message: `Expected regular expression '/v1' to not match '/v1/status', but it did match.` This confirms the current default (`$baseUrl = 'http://host.docker.internal:4319/v1'` in `Get-HostAdapterPreflightUri`) resolves to a path containing `/v1`.
