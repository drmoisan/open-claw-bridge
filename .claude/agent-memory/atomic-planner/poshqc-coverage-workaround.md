---
name: poshqc-coverage-workaround
description: PoshQC coverage mode fails repo-wide (bundled runsettings defect); plans must cite the corrected-runsettings Invoke-PoshQCTest workaround for numeric coverage
metadata:
  type: project
---

`mcp__drm-copilot__run_poshqc_test` in coverage mode fails on every invocation in open-claw-bridge because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files that exist only in the drm-copilot source repository.

**Why:** Reproduced defect across features #111, #125, #135, #137, #139 (see the Conventions block of `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/plan.2026-07-10T10-42.md`, which is the established wording). Coverage evidence contract requires numeric values, so plans cannot leave coverage tasks pointing only at the failing MCP call.

**How to apply:** In every PowerShell plan's baseline (Phase 0) and final-QC coverage tasks, require both: (1) the failing MCP invocation recorded, and (2) the corrected-runsettings workaround — import the bundled `PoshQC.psd1` directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>` where `<corrected>` is a SCRATCHPAD-only copy of the runsettings with `CodeCoverage.Path` rewritten to this repo's `scripts/**` production files (no production ExcludedPath, per the Coverage Exclusion Policy). Related: [[oversized-test-files-sibling-add]].
