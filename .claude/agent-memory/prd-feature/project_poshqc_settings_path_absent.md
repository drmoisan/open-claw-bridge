---
name: poshqc-settings-path-absent
description: powershell.md cites scripts/powershell/PoshQC/settings/pester.runsettings.psd1 but no PoshQC dir exists in the repo; MCP server supplies settings.
metadata:
  type: project
---

`.claude/rules/powershell.md` instructs using repo config at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, but no `scripts/powershell/PoshQC/` directory exists in the repository (verified 2026-07-02 during Issue #111 spec work). The PoshQC toolchain runs via MCP commands (`run_poshqc_format` / `run_poshqc_analyze` / `run_poshqc_test`) with settings supplied by the drm-copilot MCP server.

**Why:** Specs and plans that cite the repo-local settings path would reference a nonexistent file; auditors could flag it as a broken reference. Similar rule-vs-repo drift as [[test-framework-discrepancy]].

**How to apply:** When authoring specs/plans touching PowerShell, cite the MCP toolchain as the settings source and note the absent repo-local path rather than asserting the file exists. Re-verify with a glob before each use — the directory may be added later.
