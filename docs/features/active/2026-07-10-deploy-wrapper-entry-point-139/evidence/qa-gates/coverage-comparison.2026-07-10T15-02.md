Timestamp: 2026-07-10T15-02
Command: comparison derived from P0-T8 baseline (`evidence/baseline/poshqc-test.2026-07-10T15-02.md`) and P3-T3 post-change (`evidence/qa-gates/final-poshqc-test.2026-07-10T15-02.md`) corrected-runsettings artifacts, plus direct inspection of `artifacts/pester/powershell-coverage.xml` (JaCoCo/CoverageGutters format emitted by the P3-T3 run) for per-file line counters.
EXIT_CODE: 0 (analysis only; no command executed beyond the already-recorded P0-T8/P3-T3 runs)

Output Summary:

- **Repo-wide baseline (P0-T8):** `Covered 89.93% / 0%. 2,015 analyzed Commands in 30 Files.` (Tests Passed: 370)
- **Repo-wide post-change (P3-T3):** `Covered 89.94% / 0%. 2,057 analyzed Commands in 31 Files.` (Tests Passed: 380)
- **Repo-wide delta:** +0.01 percentage points, +42 analyzed commands, +1 file (`scripts/Deploy.ps1`, new). **No regression.**

- **Changed file `scripts/Publish.ps1`:** LINE counter from `powershell-coverage.xml`: `missed=2, covered=77` -> **97.47%** line coverage (77/79). The two missed lines (160-161) are the pre-existing `if (Test-Path $BundleRoot) { if ($PSCmdlet.ShouldProcess(...)) { Remove-Item ... } }` prior-bundle-cleanup branch, unrelated to and unchanged by this feature's fix (the test suite's `Test-Path` mock always returns `$false`, so this branch was already unexercised before the fix). The 3 lines this feature modified (the `$null = Invoke-VersionStamp ...`, `$null = Invoke-MakeAppx ...`, `$null = Write-PublishManifest ...` call sites) are the same physical statements that were already executed by every existing test before the fix (only the assignment target changed, not reachability), so those specific changed lines are covered both before and after. **No regression on changed lines.**
- **New file `scripts/Deploy.ps1`:** LINE counter: `missed=4, covered=27` -> **87.10%** line coverage (27/31). The 4 missed lines are the `$PSCmdlet.ShouldProcess(...)` / `return & ...` bodies inside the production `Invoke-PublishScript` and `Invoke-InstallScript` wrapper functions, which only execute when no test double is pre-registered; every `Deploy.Tests.ps1` test pre-registers a global override per the wrapper-seam mocking convention (matching the same accepted pattern for `Test-IsElevatedAdmin` in `scripts/Install.ps1`). 87.10% is above the 85% line floor.

**Threshold verification:**
- No repo-wide regression versus baseline: **PASS** (89.94% >= 89.93%).
- Line coverage >= 85%: **PASS** (repo-wide 89.94%; changed file `Publish.ps1` 97.47%; new file `Deploy.ps1` 87.10%).
- Command-coverage branch proxy >= 75%: **PASS** (same command-coverage percentage used as the branch-coverage proxy per the established repo convention, since Pester's coverage engine does not emit a separate branch metric — repo-wide 89.94%, `Publish.ps1` 97.47%, `Deploy.ps1` 87.10%, all >= 75%).
- No production PowerShell file excluded from measurement: **PASS** (`ExcludedPath` is empty in the corrected runsettings; all 28 production files under `scripts/**`, including the new `scripts/Deploy.ps1`, are measured).

**Overall outcome: PASS.** No below-threshold, regressed, or unavailable value was found.
