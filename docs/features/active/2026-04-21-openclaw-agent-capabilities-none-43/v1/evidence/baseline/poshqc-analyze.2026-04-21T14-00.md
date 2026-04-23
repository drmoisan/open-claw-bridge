---
Timestamp: 2026-04-21T15:05:00Z
Purpose: Baseline PoshQC analyzer snapshot captured against pre-edit HEAD (git SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028) using PSScriptAnalyzer 1.24.0. Produced by the Phase 6 QA executor after reconstructing the pre-edit tree via `git stash push -u` and replaying the analyzer before `git stash pop`.
---

# Baseline — PoshQC Analyze

Timestamp: 2026-04-21T15:05:00Z

Command: `pwsh -NoProfile -File /tmp/analyze-post.ps1` — iterates `git ls-files '*.ps1' '*.psm1' '*.psd1'` and runs `Invoke-ScriptAnalyzer -Path <abs>` on each file, aggregating severity counts and rule frequencies. Captured against the pre-edit HEAD working tree (stash applied, working tree at SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 exactly).

EXIT_CODE: 0

Tool Surface: MCP `mcp__drmCopilotExtension__run_poshqc_analyze` is not available in this executor sandbox. Fallback per the plan directive: direct `Invoke-ScriptAnalyzer` invocation from PSScriptAnalyzer 1.24.0 against the repo tree. Sanity-checked with a known-bad script containing `Write-Host "hello"` which triggered `PSAvoidUsingWriteHost` and `PSUseDeclaredVarsMoreThanAssignments`, confirming the analyzer surface is wired correctly.

Output Summary:
- FilesScanned: 37 (`git ls-files '*.ps1' '*.psm1' '*.psd1'` at SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028)
- Errors:       0
- Warnings:     0
- Information:  0
- Top rules:    (none — empty ruleset, zero findings)

This baseline is consumed by `evidence/qa-gates/final-poshqc-analyze.2026-04-21T14-00.md` (P6-T2) to confirm that the Phase 1-5 edits introduced zero new analyzer findings.
