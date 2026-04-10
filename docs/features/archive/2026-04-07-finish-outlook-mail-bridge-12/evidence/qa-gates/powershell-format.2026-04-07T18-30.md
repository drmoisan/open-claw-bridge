# PowerShell Format QA Gate

Timestamp: 2026-04-07T18:30
Tool: Invoke-Formatter (fallback — mcp__drmCopilotExtension__run_poshqc_format unavailable)
Command: `Import-Module PSScriptAnalyzer; Get-ChildItem -Path 'scripts','tests/scripts' -Filter '*.ps1' -Recurse | ForEach-Object { $formatted = Invoke-Formatter -ScriptDefinition (Get-Content $_.FullName -Raw); $original = Get-Content $_.FullName -Raw; if ($formatted -ne $original) { "CHANGED: $($_.FullName)" } else { "OK: $($_.FullName)" } }`
EXIT_CODE: 0
Output Summary: All 13 PowerShell files passed formatting check (0 CHANGED, 13 OK). Files: Build.ps1, install-mailbridge.ps1, register-mailbridge-task.ps1, Run-Bridge.ps1, Run-Client.ps1, test-mailbridge.ps1, Test.ps1, uninstall-mailbridge.ps1, install-mailbridge.Tests.ps1, register-mailbridge-task.Tests.ps1, runner-scripts.Tests.ps1, test-mailbridge.Tests.ps1, uninstall-mailbridge.Tests.ps1.
