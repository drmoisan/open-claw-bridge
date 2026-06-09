# AC-14 — settings.json Validity, Removals, Hook-path Existence

Timestamp: 2026-06-08T11-28

Command: `pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"`
EXIT_CODE: 0
Result: JSON parses cleanly. PASS.

Command:
`rg -n "Agent\(python-typed-engineer\)|Agent\(typescript-engineer\)|python-change-budget-router|python-qa-gate|invoke-python-engineer|check-python-test-purity|enforce-python-batch-budget" .claude/settings.json`
EXIT_CODE: 1
Result: no matches — removed agents, Python skills, and Python hook wirings are gone. PASS.

Command: `rg -n "python-typed-engineer|typescript-engineer" .claude/settings.json`
EXIT_CODE: 1
Result: no matches — SubagentStop matcher no longer names the removed workers. PASS.

Command: Test-Path for each remaining hook `command` `.ps1` path (enumerated from settings.json)
EXIT_CODE: 0
Result: 11 distinct remaining hook paths, 0 missing. Every remaining hook command resolves on disk:
- check-powershell-test-purity.ps1, enforce-powershell-batch-budget.ps1,
  enforce-evidence-locations.ps1, enforce-feature-folder-order.ps1, enforce-checkpoint-monotonic.ps1,
  validate-bash.ps1, enforce-promotion-mcp-only.ps1, enforce-pr-author-skill.ps1,
  enforce-prd-feature-before-planner.ps1, validate-feature-review-coverage.ps1,
  validate-planner-output.ps1 — all True.

Output Summary: settings.json is valid JSON; all extension removals applied; every remaining hook
path resolves; retained C#/PowerShell agents and PowerShell skills preserved. AC-14 PASS.
