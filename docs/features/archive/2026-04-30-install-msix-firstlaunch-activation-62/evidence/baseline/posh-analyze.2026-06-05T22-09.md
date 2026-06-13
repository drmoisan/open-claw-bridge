# PowerShell Analyzer Baseline (P0-T7)

Timestamp: 2026-06-05T22-09

Command: `Invoke-ScriptAnalyzer -Path scripts, tests/scripts -Recurse -Severity Warning, Error` (PSScriptAnalyzer 1.24.0), matching the `Analyze PowerShell` step in `.github/workflows/ci.yml`.

TOOLING NOTE: Plan named `mcp__drmCopilotExtension__run_poshqc_analyze`; the PoshQC MCP server is absent. `Invoke-ScriptAnalyzer` is the repository analyzer of record (`.claude/rules/powershell.md` step 2 and CI). Used directly.

EXIT_CODE: 0

Output Summary:
- Diagnostics (Warning + Error) over `scripts/` and `tests/scripts/`: 0
- Errors: 0
- Warnings: 0
- Clean baseline.
