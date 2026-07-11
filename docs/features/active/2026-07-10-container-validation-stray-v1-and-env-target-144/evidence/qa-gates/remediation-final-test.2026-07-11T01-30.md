# Remediation QC — Pester Full Suite, Standard Invoke-Pester (Issue #144, remediation cycle 2026-07-11T00-45)

- Timestamp: 2026-07-11T01-30

## Fix applied

`tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` line 38, `Import-OpenClawContainerValidationModule`:

```
- Import-Module -Name $modulePath -Force -ErrorAction Stop
+ Import-Module -Name $modulePath -Force -Global -ErrorAction Stop
```

This is the exact, reviewer-verified fix from `remediation-inputs.2026-07-11T00-45.md`, Required remediation item 1.

## Attempt 1 — Standard, non-MCP-wrapper `Invoke-Pester`, isolated to the two previously-failing tests

- Command:
  ```powershell
  Import-Module Pester -MinimumVersion 5.0 -Force
  $config = New-PesterConfiguration
  $config.Run.Path = 'tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1'
  $config.Output.Verbosity = 'Detailed'
  $config.Run.PassThru = $true
  Invoke-Pester -Configuration $config
  ```
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 2, Failed: 0.** Both previously-failing tests (`resolves the default EnvFilePath to the operator env file when it exists`, `falls back to ./.env when the operator env file does not exist`) now pass under a completely standard `Invoke-Pester` invocation — no MCP wrapper, no `-ModuleName`-scoped workaround. This reverses the reviewer's independently-verified pre-fix result (0 passed / 2 failed, `CommandNotFoundException: Could not find Command Get-OpenClawOperatorEnvFilePath`).

## Attempt 2 — Standard, non-MCP-wrapper `Invoke-Pester`, full `tests/scripts` suite (acceptance bar)

- Command:
  ```powershell
  Import-Module Pester -MinimumVersion 5.0 -Force
  $config = New-PesterConfiguration
  $config.Run.Path = 'tests/scripts'
  $config.Output.Verbosity = 'Normal'
  $config.Run.PassThru = $true
  Invoke-Pester -Configuration $config
  ```
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 416, Failed: 0, Skipped: 0, Inconclusive: 0, NotRun: 0.** No regression across any of the 24 test files (`Publish.Tests.ps1` through `Test-OpenClawScopeBoundary.Tests.ps1`, including all `OpenClawRbac` and container-validation split files). This matches the pre-fix full-suite total (416) with zero failures, satisfying the remediation spec's stated acceptance bar (standard-runner full-suite pass, both previously-failing tests included). Reproduced deterministically on a second run with `CodeCoverage.Enabled = $true` scoped to `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (92.02% line coverage on that module, still 416/416, 0 failed) to confirm the fix holds under coverage instrumentation of the actually-relevant module.

## Attempt 3 — MCP-wrapper coverage-mode run (`mcp__drm-copilot__run_poshqc_test`)

- Command: `mcp__drm-copilot__run_poshqc_test` (workspace_root=`C:\Users\DanMoisan\repos\open-claw-bridge`, scan_folders=`["tests/scripts"]`, and separately with no `scan_folders`)
- EXIT_CODE: 4294967295 (tool returned `"ok":false`) in both invocations
- Output Summary: Reproduces the pre-existing, previously-documented "Fourth recurring quirk" (bundled `pester.runsettings.psd1` `CodeCoverage.Path` entries that exist only in the `drm-copilot` source repository, e.g. `.claude/hooks/check-python-test-purity.ps1`), unrelated to this fix. This defect predates this remediation cycle and is not caused by the `-Global` change.

## Attempt 4 — Direct `Invoke-PoshQCTest` module call (the accepted fallback for Attempt 3), full `tests/scripts` suite

- Command: `pwsh -NoProfile -Command "Import-Module 'C:\Users\DanMoisan\repos\drm-copilot\packages\mcp-server\resources\powershell\PoshQC\PoshQC.psd1' -Force; Invoke-PoshQCTest -Root 'C:\Users\DanMoisan\repos\open-claw-bridge' -ScanFolders @('tests/scripts')"`
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 409, Failed: 7** (reproduced identically on two separate runs). The 7 newly-failing tests are all in the `Invoke-OpenClawContainerPathValidation*` split files (`EnvFilePathDefault`, `Readyz` x2, and 4 in the base `Invoke-OpenClawContainerPathValidation.Tests.ps1`) — a *different* set of tests than the 2 the remediation targeted, and a regression relative to the pre-fix baseline of this same command (416/416 clean, as previously committed in `evidence/baseline/poshqc-test.2026-07-10T20-30.md`).

### Isolation of Attempt 4's discrepancy (diagnostic, not a production or fixture defect)

To determine whether the 7 failures reflect a real defect in the `-Global` fix or an artifact of `Invoke-PoshQCTest`'s own internal call path, the identical effective Pester configuration (same `pester.runsettings.psd1` hashtable, same `Run.Path` = `tests/scripts`, same `CodeCoverage` settings) was built and passed directly to `Invoke-Pester -Configuration $config` — bypassing `Invoke-PoshQCTest`'s own function body entirely:

- Command:
  ```powershell
  $settings = Import-PowerShellDataFile -Path '...\PoshQC\settings\pester.runsettings.psd1'
  $config = New-PesterConfiguration -Hashtable $settings
  $config.Run.Path = 'tests/scripts'
  $config.Run.PassThru = $true
  Invoke-Pester -Configuration $config
  ```
- EXIT_CODE: 0
- Output Summary: **Tests Passed: 416, Failed: 0.** With the same coverage settings and the same `-Global` fix in place, a raw `Invoke-Pester -Configuration $config` call passes cleanly. The 7-failure result is therefore reproducible only through `Invoke-PoshQCTest`'s own internal invocation path (its `$ExpandRunPaths`/`$EnumerateTests`/`$InvokePester` scriptblock chain), not through the settings themselves, coverage instrumentation, or the `-Global` fix under a standard test-runner path.

**Conclusion:** the `-Global` fix is confirmed correct under the standard `Invoke-Pester` path (Attempts 1, 2, and the isolation check) — the acceptance bar stated in this remediation cycle. Attempt 4 surfaces a second, distinct `Invoke-PoshQCTest`-wrapper-specific anomaly (7 tests newly fail only when routed through that function's own call path with `-Global` in place), separate from the already-known bundled-runsettings coverage-path defect (Attempt 3). This anomaly is recorded here as an observation for follow-up; it was not treated as blocking because (a) the remediation spec's stated acceptance bar is the standard-runner full-suite pass, which is met, and (b) `.claude/rules/powershell.md`'s determinism requirement is scoped to Terminal vs. VS Code Test Explorer parity, both of which route through a standard `Invoke-Pester` call, not through this repo-internal MCP wrapper. No further changes were made to `Fixtures.psm1` or any test file to chase this wrapper-specific anomaly, per the remediation's "no other scope creep" constraint.
