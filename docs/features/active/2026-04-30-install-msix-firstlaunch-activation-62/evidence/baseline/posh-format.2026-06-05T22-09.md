# PowerShell Formatter Baseline (P0-T6)

Timestamp: 2026-06-05T22-09

Command: `Invoke-Formatter` (PSScriptAnalyzer 1.24.0) applied as a check over every `*.ps1`/`*.psm1`/`*.psd1` under `scripts/` and `tests/scripts/`; a file is counted as "needs format" when `Invoke-Formatter -ScriptDefinition <content>` differs from the on-disk content.

TOOLING NOTE: The plan named `mcp__drmCopilotExtension__run_poshqc_format`. The PoshQC MCP server and `scripts/powershell/PoshQC/` are absent from this working tree, and `.github/workflows/ci.yml` runs no formatter gate. The repository's formatter of record per `.claude/rules/powershell.md` step 1 is `Invoke-Formatter`. This baseline uses `Invoke-Formatter` directly as the mechanically-equivalent substitute.

EXIT_CODE: 1

Output Summary:
- Files scanned: 41
- Files differing from Invoke-Formatter canonical output (baseline drift, pre-existing): 4
  - `scripts/Install.Helpers.psm1` — 1 line (L370 hanging-indent on a pipeline continuation inside `Wait-ComposeHealthy`; unrelated to this feature).
  - `scripts/Invoke-OpenClawContainerPathValidation.ps1` — out of feature scope.
  - `scripts/Publish.Helpers.psm1` — out of feature scope.
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` — out of feature scope.
- The baseline is NOT a clean zero. The drift is pre-existing and is not gated by repository CI. Execution rule for this feature: new or modified regions in files this feature touches must match `Invoke-Formatter` output; pre-existing unrelated drift (e.g., Install.Helpers.psm1 L370) is left untouched to avoid out-of-scope reformatting.
