Timestamp: 2026-07-10T15-02

Command: `pwsh -NoProfile -File <scratchpad>\run-pester-targeted-139-p1t2.ps1` — imports the bundled `PoshQC.psd1` module (for Pester engine parity with the MCP toolchain), then runs a targeted `Invoke-Pester` scoped to `tests/scripts/Publish.Env.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1`, `tests/scripts/Publish.Msix.Tests.ps1`, and `tests/scripts/Publish.Tests.ps1` (the sibling `Publish.*.Tests.ps1` files are included, unfiltered, because `Publish.Tests.ps1` relies on `Publish.Msix.psm1`'s exported functions already being imported into the shared PowerShell session by an earlier-run sibling test file's `BeforeAll` — an existing test-file ordering dependency, not something introduced by this change; a `-Filter` scoped to only the new `It` skips that sibling `BeforeAll` and produces an unrelated `CommandNotFoundException: Could not find Command Invoke-VersionStamp` setup artifact instead of exercising the assertion under test).
EXIT_CODE: 1 (Tests Passed: 106, Failed: 1)

Output Summary: The new `It` block from P1-T1 (`scripts/Publish.ps1.output contract.emits exactly one pipeline object (the bundle root) when captured (regression: issue #139 AC-1)`) fails against the pre-fix `scripts/Publish.ps1`, exactly as predicted:

```
[-] scripts/Publish.ps1.output contract.emits exactly one pipeline object (the bundle root) when captured (regression: issue #139 AC-1) 127ms (126ms|1ms)
 at @($result).Count | Should -Be 1, ...\tests\scripts\Publish.Tests.ps1:415
 Expected 1, but got 2.
```

This confirms the captured `& $script:ScriptPath -Version '1.2.3.0' -SkipSign` invocation currently emits 2 pipeline objects (the leaked `Invoke-MakeAppx` mock return value plus the script's own `return $BundleRoot`), not 1. All other 106 selected tests across the five sibling files pass; only the new expect-fail test fails, isolating the regression to the output-contract behavior under test.
