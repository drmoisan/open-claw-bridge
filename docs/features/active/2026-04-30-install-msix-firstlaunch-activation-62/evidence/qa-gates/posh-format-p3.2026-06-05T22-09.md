# PowerShell Formatter — Phase 3 (P3-T4)

Timestamp: 2026-06-05T22-09

Command: `Invoke-Formatter -ScriptDefinition <content>` over `scripts/Install.Helpers.psm1`, comparing against on-disk content (PSScriptAnalyzer 1.24.0).

TOOLING NOTE: PoshQC MCP absent; `Invoke-Formatter` used directly (formatter of record per `.claude/rules/powershell.md`).

EXIT_CODE: 0 (no new drift introduced by this feature's edits)

Output Summary:
- Total lines differing from Invoke-Formatter canonical output: 1.
- That single line is the pre-existing baseline drift at the `Wait-ComposeHealthy` pipeline-continuation hanging indent (was L370 at baseline, now L397 after inserting `Invoke-MsixAppActivate` above it). It is unchanged by this feature and is left untouched to avoid out-of-scope reformatting.
- The newly added `Invoke-MsixAppActivate` function region introduces ZERO new formatter drift. Net new drift = 0 relative to baseline.
