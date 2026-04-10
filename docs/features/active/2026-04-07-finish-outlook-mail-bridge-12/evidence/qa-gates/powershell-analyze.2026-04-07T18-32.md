# PowerShell Analyze QA Gate

Timestamp: 2026-04-07T18:32
Tool: Invoke-ScriptAnalyzer (fallback — mcp__drmCopilotExtension__run_poshqc_analyze unavailable)
Command: `Invoke-ScriptAnalyzer -Path './scripts' -Recurse -Severity Error,Warning,Information; Invoke-ScriptAnalyzer -Path './tests/scripts' -Recurse -Severity Error,Warning,Information`
EXIT_CODE: 0
Output Summary: No findings in production scripts (`scripts/`). Test files (`tests/scripts/`) report `PSAvoidGlobalVars` warnings only — this is expected and standard for Pester tests that mock external commands (schtasks, dotnet, Set-Content) via global functions/variables for cross-scope capture. No Error or Information findings. Baseline had 16 findings including production script warnings; post-change has 0 production warnings (improvement over baseline).
