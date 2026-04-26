---
Timestamp: 2026-04-21T15:06:00Z
Purpose: Phase 6 P6-T2 — PSScriptAnalyzer post-edit verification (zero errors, zero warnings, zero regression vs. baseline)
---

# Final — PoshQC Analyze (P6-T2)

Timestamp: 2026-04-21T15:06:00Z

Command: `pwsh -NoProfile -File /tmp/analyze-post.ps1` — iterates `git ls-files '*.ps1' '*.psm1' '*.psd1'` and runs `Invoke-ScriptAnalyzer -Path <abs>` against each working-tree file. Repeats the identical command used for the baseline artifact at `evidence/baseline/poshqc-analyze.2026-04-21T14-00.md`.

EXIT_CODE: 0

Tool Surface: MCP `mcp__drmCopilotExtension__run_poshqc_analyze` is not available in this executor sandbox. Fallback per the plan directive: direct `Invoke-ScriptAnalyzer` invocation from PSScriptAnalyzer 1.24.0.

Output Summary:
- FilesScanned: 37
- Post-edit Errors:       0
- Post-edit Warnings:     0
- Post-edit Information:  0
- Baseline Errors:        0
- Baseline Warnings:      0
- Baseline Information:   0
- Delta (post - baseline): Errors 0, Warnings 0, Information 0

Per-file per-severity post-edit breakdown (non-zero only): none.
Per-file per-severity baseline breakdown (non-zero only): none.

Analyzer did not autofix any files during this run (the script invokes `Invoke-ScriptAnalyzer` in reporting mode, not `-Fix`). The P6-T1 loop-restart condition "analyzer autofixes any files" was not triggered.

Result: PASS. Zero analyzer errors, zero warnings, zero delta vs. baseline. The Phase 1-5 edits introduced no new PSScriptAnalyzer findings.
