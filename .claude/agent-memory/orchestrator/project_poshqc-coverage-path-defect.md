---
name: poshqc-coverage-path-defect
description: The bundled mcp__drm-copilot__run_poshqc_test coverage mode fails in this repo (foreign hardcoded CodeCoverage.Path); use the F11 corrected-runsettings workaround to capture real PowerShell coverage.
metadata:
  type: project
---

`mcp__drm-copilot__run_poshqc_test` (coverage mode) fails on EVERY invocation in this repo, for any PowerShell feature. Its bundled `pester.runsettings.psd1` (under the drm-copilot VS Code extension resources, e.g. `C:\Users\<user>\.vscode-insiders\extensions\danmoisan.drm-copilot-<ver>\resources\powershell\PoshQC\settings\pester.runsettings.psd1`) hardcodes `CodeCoverage.Path` to an allowlist of files that exist only in the drm-copilot SOURCE repo (e.g. `.claude/hooks/check-python-test-purity.ps1`, `scripts/dev-tools/Invoke-FullRelease.ps1`), none of which exist here. Pester's `Resolve-CoverageInfo` throws at RunStart ("Could not resolve coverage path") and the tool exits non-zero even though all tests pass. The repo-local override path that `.claude/rules/powershell.md` references (`scripts/powershell/PoshQC/settings/pester.runsettings.psd1`) does NOT exist in this repo.

**Workaround (established F11 precedent — `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/baseline/poshqc-test.2026-07-02T17-25.md`):** import the bundled `PoshQC.psd1` module directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>` where `<corrected>` is a SCRATCHPAD copy of the bundled runsettings with `CodeCoverage.Path` rewritten to this repo's actual production PowerShell files under `scripts/**` (glob the `.ps1`/`.psm1`). This is the same underlying code path the MCP tool wraps, just with a correct coverage scope. Do NOT write the runsettings into the repo tree. F16/#125 captured repo-wide 89.94% command coverage and 100% on a new script this way.

Pester v5 note: emits command-level coverage only (no separate branch metric); the command-coverage % is the branch-sensitive signal (untaken branch arms count as uncovered commands). Record it as the line/branch proxy citing this precedent — do not invent a separate branch number.

Coverage-exclusion caveat: this repo's `general-unit-test.md` forbids excluding any production file from measurement, so the corrected runsettings should cover ALL `scripts/**` production files with no `ExcludedPath` (stricter than F11's/bundled settings which excluded CLI wrappers).

**Why:** the feature-review coverage-evidence contract requires NUMERIC coverage; an executor that only records "remediation-required" because the MCP tool failed has left a real gap the reviewer will (correctly) flag. The workaround is mandatory to close it.

**How to apply:** For any feature adding/changing PowerShell, expect the MCP coverage tool to fail and go straight to the corrected-runsettings workaround for baseline + post-change + new-file coverage. Related: [[checkpoint-validator-contract]], [[ci-powershell-qc-flake]].
