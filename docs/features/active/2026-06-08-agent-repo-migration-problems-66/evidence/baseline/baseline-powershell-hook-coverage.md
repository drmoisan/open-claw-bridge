# Baseline — PowerShell Hook Coverage (Issue #66, P0-T2)

Timestamp: 2026-06-08T20-00

Command: `Select-String -Path artifacts/pester/powershell-coverage.xml -Pattern 'counter type="LINE"'` (artifact inspection, not a re-run)

EXIT_CODE: 0

Output Summary:

- The Pester JaCoCo coverage artifact `artifacts/pester/powershell-coverage.xml` records 30 `counter type="LINE"` entries; every LINE counter has `covered="0"` (line coverage = 0% for all recorded hook-related sourcefiles).
- The artifact records only 5 distinct hook source files by name: `check-powershell-test-purity.ps1`, `check-python-test-purity.ps1`, `enforce-powershell-batch-budget.ps1`, `enforce-python-batch-budget.ps1`, `validate-bash.ps1`.
- The branch carries 15 newly-tracked `.claude/hooks/*.ps1` files. The coverage artifact has no coverage entry for 10 of those 15 changed hooks (including `validate-feature-review-coverage.ps1`, `enforce-pr-author-skill.ps1`, `validate-executor-output.ps1`, `validate-planner-output.ps1`, `enforce-checkpoint-monotonic.ps1`, `validate-orchestrator-output.ps1`, `enforce-prd-feature-before-planner.ps1`, `enforce-promotion-mcp-only.ps1`, `validate-required-artifact-output.ps1`, plus `enforce-evidence-locations.ps1`/`enforce-feature-folder-order.ps1`/`validate-task-researcher-output.ps1`).
- Baseline numeric line coverage for the changed hooks: 0% (covered=0 where recorded; no entry where unrecorded). 10 of 15 changed hooks have no coverage entry at all.

Conclusion: the PowerShell coverage gate fails on the changed hooks because their measured/recorded line coverage is 0% and most are unmeasured. This is the blocking finding that Option B resolves via a documented `.claude/hooks/**` coverage-scope exclusion (not by authoring hook tests).
