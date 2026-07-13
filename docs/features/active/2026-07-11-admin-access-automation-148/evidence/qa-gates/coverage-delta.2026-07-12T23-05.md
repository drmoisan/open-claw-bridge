# Final QC - Coverage Delta and Threshold Verification (AC-16)

Timestamp: 2026-07-12T23-05
Thresholds: LINE >= 85%, BRANCH (INSTRUCTION proxy) >= 75%, no regression on changed lines.

## Repo-wide coverage: baseline vs post-change

| Metric | Baseline (P0-T8, 33 files) | Post-change (P5-T2, 36 files) | Delta |
| --- | --- | --- | --- |
| LINE | 90.83% (1635/1800) | 91.09% (1718/1886) | +0.26 pts |
| INSTRUCTION (branch proxy) | 90.26% (2066/2289) | 90.60% (2178/2404) | +0.34 pts |

Note: the baseline denominator is 33 production files (the three new scripts did not exist at
baseline). Post-change includes the three new scripts (36 files). Repo-wide coverage did not
regress; it improved on both metrics because the three well-covered new scripts raise the mean.

## New/changed-code coverage (the three new scripts)

| Script | LINE | BRANCH (INSTRUCTION proxy) | Line >= 85% | Branch >= 75% |
| --- | --- | --- | --- | --- |
| scripts/Get-OpenClawControlUiTokenUrl.ps1 | 92.31% (12/13) | 93.75% (15/16) | PASS | PASS |
| scripts/Invoke-OpenClawDeviceTokenRotation.ps1 | 96.97% (32/33) | 93.02% (40/43) | PASS | PASS |
| scripts/Set-OpenClawWebSearchProvider.ps1 | 87.50% (35/40) | 85.71% (48/56) | PASS | PASS |

All three new scripts clear both thresholds on new/changed code.

## No regression on changed lines

- The only production files added/changed are the three new scripts (measured above) and the
  JSON seed edit (`deploy/docker/openclaw-assistant/openclaw.json`, not a PowerShell file, not
  in the coverage denominator) and the runbook (Markdown, exempt).
- No pre-existing production file was modified (verified P5-T5: onboarding script and
  OpenClawContainerValidation module unchanged), so no existing line's coverage regressed.
- The uncovered lines in the new scripts are the module-load wiring line
  (`Import-Module` inside `if (-not (Get-Module ...))`, unreachable in tests where the module is
  pre-imported) and a small number of defensive/no-op arms; all three files still exceed the 85%
  line and 75% branch thresholds.

## Outcome

PASS. Numeric baseline, post-change, and new/changed-code coverage are all recorded (no
placeholders). All thresholds are met and there is no coverage regression on changed lines.
