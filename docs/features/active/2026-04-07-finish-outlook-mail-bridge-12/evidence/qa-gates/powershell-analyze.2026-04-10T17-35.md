# PowerShell Analyze — QA Gate Evidence

Timestamp: 2026-04-10T17-35
Tool: mcp_drmcopilotext_run_poshqc_analyze
EXIT_CODE: 0
Output Summary: PSScriptAnalyzer analysis passed for workspace scripts (`scripts/` and `tests/scripts/`) with no findings. An initial unscoped run reported 7 issues, all located in `.claude/worktrees/crazy-nash/scripts/` (a Codex worktree directory outside the main workspace scope). Scoped runs targeting `scripts/` and `tests/scripts/` each returned exit code 0 with no findings.

## Run Details

### Run 1 — Unscoped (full workspace)
- Tool: mcp_drmcopilotext_run_poshqc_analyze
- Result: EXIT_CODE 1 — 7 PSScriptAnalyzer issues in `.claude/worktrees/crazy-nash/scripts/`
- Root cause: Codex worktree files with `Write-Host` and `$Args` assignment

### Run 2 — Scoped to `scripts/`
- Tool: mcp_drmcopilotext_run_poshqc_analyze (scan_folders: scripts)
- Result: EXIT_CODE 0 — no findings

### Run 3 — Scoped to `tests/scripts/`
- Tool: mcp_drmcopilotext_run_poshqc_analyze (scan_folders: tests/scripts)
- Result: EXIT_CODE 0 — no findings
