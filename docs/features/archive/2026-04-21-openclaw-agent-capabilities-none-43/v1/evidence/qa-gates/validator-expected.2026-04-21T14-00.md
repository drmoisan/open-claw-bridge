---
Timestamp: 2026-04-21T15:11:30Z
Purpose: Phase 6 P6-T8 — validator end-to-end sanity against a healthy mock (AC-2)
---

# Final — Validator End-to-End Sanity (P6-T8)

Timestamp: 2026-04-21T15:11:30Z

Command: `pwsh -NoProfile -File /tmp/pester-p6t8.ps1` — runs Pester 5.6.1 against `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` with `Filter.FullName = '*returns expected when all container endpoints match their validation contracts*'` so only the first `It` block of the `Describe 'Invoke-OpenClawContainerPathValidation.ps1'` suite executes.

EXIT_CODE: 0

Output Summary:
- Pester discovery found 6 tests; filter selected 1.
- Test `returns expected when all container endpoints match their validation contracts` PASSED in 369 ms.
- All assertions in the test body evaluated truthily:
  - `$result.OverallResult | Should -Be 'Expected'` — PASS
  - `$result.IsExpected | Should -BeTrue` — PASS
  - `$result.DockerEngine.IsExpected | Should -BeTrue` — PASS
  - `@($result.ContainerDiagnostics).Count | Should -Be 6` — PASS
  - `@($result.EndpointDiagnostics).Count | Should -Be 6` — PASS (confirms the DashboardAuth entry is removed from the endpoint array — previously 7)
  - `$result.Live.IsExpected | Should -BeTrue` — PASS
  - `$result.Ready.IsExpected | Should -BeTrue` — PASS
  - `$result.CoreStatus.IsExpected | Should -BeTrue` — PASS
  - `$result.AgentDashboard.IsExpected | Should -BeTrue` — PASS
  - `$result.AgentReadyz.IsExpected | Should -BeTrue` — PASS
  - `$result.HostAdapterInContainer.IsExpected | Should -BeTrue` — PASS
  - `$result.GatewayTokenPresence.IsExpected | Should -BeTrue` — PASS
  - `@($result.SupportingDiagnostics).Count | Should -Be 14` — PASS (previously 15; the extra diagnostic row from DashboardAuth is gone)
  - `@($script:DockerRequests) | Should -Contain 'container inspect openclaw-core'` — PASS
  - `@($script:DockerRequests) | Should -Contain 'container inspect openclaw-agent'` — PASS

The test body never writes a `DashboardAuth` property to `$result` because the production script (`scripts/Invoke-OpenClawContainerPathValidation.ps1`) no longer emits one (see P2-T3). Independent confirmation:

- `Get-Command -Type ExternalScript scripts/Invoke-OpenClawContainerPathValidation.ps1` lists these parameters only: `AgentBaseUrl, AgentContainerName, AsJson, CoreBaseUrl, CoreContainerName, DockerPath, EnvFilePath, PassThru, TimeoutSeconds` (plus PowerShell common parameters). `DashboardAuthPath` is absent.
- A source-level substring search (`$source -match 'DashboardAuth'`) on the script file returns no match.

Result: PASS. AC-2 is met — the validator returns `OverallResult = Expected` on a healthy mock, with six endpoint diagnostics and no `DashboardAuth` property on the result object.
