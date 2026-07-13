---
name: poshqc-corrected-runsettings-workaround-location
description: Location of the bundled PoshQC module and pester.runsettings.psd1 for the corrected-runsettings coverage workaround, and how to invoke Invoke-PoshQCTest directly
metadata:
  type: reference
---

The `mcp__drm-copilot__run_poshqc_*` MCP tools wrap a bundled PoshQC PowerShell module that also exists on disk at (version varies by installed extension, check for the latest):
`C:\Users\DanMoisan\.vscode-insiders\extensions\danmoisan.drm-copilot-<version>\resources\powershell\PoshQC\PoshQC.psd1`

Its bundled `settings/pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files from the `drm-copilot` source repo (not the consuming repo), reproducing a known defect (open-claw-bridge issues #111, #125, #135, #137, #139, #142, #144, #147) where `mcp__drm-copilot__run_poshqc_test` in coverage mode either returns a bare `ok:true`/`ok:false` summary with no numeric coverage, or fails outright with exit code 4294967295 when run over the full repo without `scan_folders`.

**Workaround:** copy `settings/pester.runsettings.psd1` to a scratchpad file, rewrite only the `CodeCoverage.Path` array to the target repo's production PowerShell files (no `ExcludedPath`), then:
```
Import-Module '<extension>\resources\powershell\PoshQC\PoshQC.psd1' -Force
Invoke-PoshQCTest -Root '<repo>' -SettingsPath '<scratchpad>\pester.runsettings.corrected.psd1'
```
This prints a real `Tests Passed/Failed` summary and a coverage line (`Covered X% / 0%. N analyzed Commands in M Files.`), and writes `artifacts/pester/powershell-coverage.xml` (JaCoCo/CoverageGutters XML — has `LINE` and `INSTRUCTION` counters per class/method, but no `BRANCH` counter; use `INSTRUCTION` coverage as the branch-coverage proxy per established repo precedent, e.g. issue #144's baseline artifact). `artifacts/pester/pester-junit.xml` has per-`<testsuite>` `failures="N"` counts useful for diffing against a baseline.

This run can take 2+ minutes over the full suite — use `run_in_background: true` and wait for the notification rather than polling.

**Two gotchas (verified issue #148):**
1. `CodeCoverage.Path` is a STATIC explicit file list (Pester globs like `scripts/**/*.ps1` do NOT expand under CodeCoverage.Path — a glob yields "1 File" / 0% garbage). Enumerate explicit files: `find scripts -type f \( -name '*.ps1' -o -name '*.psm1' \)`. Critically, REGENERATE the list AFTER creating new production scripts — a list built at Phase-0 baseline silently omits new files, so per-new-file coverage reports 0 matched classes and the overall denominator is wrong. Rebuild before the final-QC coverage run.
2. `run_poshqc_analyze` treats `[Information]`-severity findings as failing (exit 1), e.g. `PSAvoidUsingPositionalParameters` on `Join-Path a b c` (3 positional args) — use named `-Path`/`-ChildPath` with a combined child path. Also `PSUseDeclaredVarsMoreThanAssignments` fires on a var assigned INSIDE a `{ ... } | Should -Throw` scriptblock (analyzer sees the scriptblock scope in isolation) — restructure to try/catch so the captured var is genuinely read.
3. Full-suite (`tests/scripts`) contamination is execution-ORDER dependent: issue #148 baseline showed 9 spurious `Invoke-OpenClawContainerPathValidation` failures (pass in isolation) that DISAPPEARED once 3 new alphabetically-ordered test files were added, shifting order. Do not treat baseline full-suite failures as genuine without an isolation cross-check ([[pester-narrow-filelist-false-failures]]).
