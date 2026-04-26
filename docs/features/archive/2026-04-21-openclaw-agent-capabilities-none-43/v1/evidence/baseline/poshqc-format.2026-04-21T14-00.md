---
Timestamp: 2026-04-21T15:01:26Z
Purpose: Baseline PoshQC formatter snapshot captured against pre-edit HEAD (git SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028) using PSScriptAnalyzer 1.24.0 `Invoke-Formatter` in read-only mode. Produced by the Phase 6 QA executor after the prior session could not reach the MCP PoshQC tool surface.
---

# Baseline — PoshQC Format

Timestamp: 2026-04-21T15:01:26Z

Command: `pwsh -NoProfile -File /tmp/baseline-format.ps1` — iterates `git ls-files '*.ps1' '*.psm1' '*.psd1'`, pulls each file's content via `git show HEAD:<path>`, runs `Invoke-Formatter -ScriptDefinition $content`, and counts files whose formatted output differs from the stored HEAD content.

EXIT_CODE: 0

Tool Surface: MCP `mcp__drmCopilotExtension__run_poshqc_format` is not available in this executor sandbox. Fallback per the plan directive: direct `Invoke-Formatter` invocation from PSScriptAnalyzer 1.24.0 against the repo tree. Recorded here so the Phase 6 delta artifact can be computed.

Output Summary:
- FilesScanned: 37 (`git ls-files '*.ps1' '*.psm1' '*.psd1'` at SHA 2397e6d0c5a81ae5c6fd87c5a897b039771c1028)
- FilesReformatted: 6 — pre-existing formatter disagreements unrelated to this feature's edits
- FilesFailed: 0
- Reformatted files (baseline) — all stem from the `PSPlaceOpenBrace` / `NewLineAfterBrace` style (`} else {` on one line) and are pre-existing repo state, not introduced by the Phase 1-5 edits:
  - `scripts/Install.Helpers.psm1`
  - `scripts/Install.ps1`
  - `scripts/Invoke-OpenClawAgentOnboarding.ps1`
  - `scripts/Invoke-OpenClawContainerPathValidation.ps1`
  - `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`

This baseline is consumed by `evidence/qa-gates/final-poshqc-format.2026-04-21T14-00.md` (P6-T1) to confirm the Phase 1-5 edits introduced zero new formatter-dirty files (delta = 0).
