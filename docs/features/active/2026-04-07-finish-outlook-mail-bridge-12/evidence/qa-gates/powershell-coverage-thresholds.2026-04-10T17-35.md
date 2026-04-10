# PowerShell Coverage Thresholds — QA Gate Evidence

Timestamp: 2026-04-10T17-35
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass (PowerShell coverage threshold comparison — baseline vs post-change)
EXIT_CODE: 0
Output Summary: ThresholdResult: FAIL. BaselineOverallLineCoverage: 100.0, PostChangeOverallLineCoverage: 78.7, ChangedOrNewLineCoverage: 77.67, NewProductionCoverage: 100.0.

## Parsed Results

- BaselineOverallLineCoverage: 100
- PostChangeOverallLineCoverage: 78.7
- ChangedOrNewLineCoverage: 77.67
- NewProductionCoverage: 100
- ThresholdResult: FAIL

## Threshold Evaluation

| Criterion | Required | Actual | Status |
|-----------|----------|--------|--------|
| PostChangeOverallLineCoverage >= 80.0 | >= 80.0 | 78.7 | FAIL |
| PostChangeOverallLineCoverage >= BaselineOverallLineCoverage | >= 100.0 | 78.7 | FAIL |
| ChangedOrNewLineCoverage >= 80.0 | >= 80.0 | 77.67 | FAIL |
| NewProductionCoverage >= 90.0 | >= 90.0 | 100.0 | PASS |

## Measurement Context

The baseline `OverallLineCoverage: 100.0` was produced by the `mcp_drmcopilotext_run_poshqc_test` MCP tool during Phase 0, which generated a `coverage.json` with an empty `Files` array and defaulted to 100%. This is a known measurement artifact documented in the plan's execution notes: "repair the drmCopilotExtension PowerShell coverage path behind mcp_drmcopilotext_run_poshqc_test summary translation." The post-change 78.7% reflects actual measured coverage using Pester's CodeCoverage configuration against 277 commands across 8 script files.

The baseline comparison is not meaningful because baseline coverage was not actually measured. The post-change coverage of 78.7% with 77.67% changed-line coverage represents the first real measurement of PowerShell script coverage in this feature.
