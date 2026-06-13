# Remediation Inputs: Issue #66 — Agent Harness Migration Correction

**Entry Timestamp:** 2026-06-08T20-00
**Feature Folder:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
**Base Branch:** `development` (merge-base `72d11879918bab20652abf2965eea42f17ab67d1`)
**Head Branch:** `bug/agent-repo-migration-problems-66` @ `3ed46efaa28c43fefc946413bb3ba64866ca8d29`
**Work Mode:** `full-bug`

## Source Audit Artifacts (findings origin)

- `docs/features/active/2026-06-08-agent-repo-migration-problems-66/policy-audit.2026-06-08T20-00.md`
- `docs/features/active/2026-06-08-agent-repo-migration-problems-66/code-review.2026-06-08T20-00.md`
- `docs/features/active/2026-06-08-agent-repo-migration-problems-66/feature-audit.2026-06-08T20-00.md`

## Blocking Findings Summary

- **Blocking count: 1** (one FAIL finding).
- All 15 acceptance criteria (AC-01..AC-15) verify PASS; no acceptance criterion failed.
- The single blocking finding is a coverage policy gate, not an acceptance-criterion failure.

## Finding 1 (Blocker) — PowerShell coverage FAIL on newly-tracked harness hooks

**What is wrong:**
The branch diff contains 15 PowerShell files under `.claude/hooks/` as additions. They became tracked because `.gitignore` was edited to un-ignore `.claude/` (Scope Extension, Option 1A). Per the SKILL coverage-verification mandate and the Scope Invariant, a language with changed files in the branch diff must carry an explicit PASS or FAIL coverage verdict; it cannot be excused as out-of-scope. The only PowerShell coverage artifact present, `artifacts/pester/powershell-coverage.xml`, reports:

- Report-level total: LINE missed=284, covered=0 (0% line coverage).
- It measures only 5 of the 15 changed hooks; the other 10 changed hooks have no coverage entry at all.

This fails the uniform tier rule (line >= 85%, branch >= 75%) and the repo-wide < 80% FAIL trigger.

**Affected files (15 changed `.ps1`):**
- `.claude/hooks/check-powershell-test-purity.ps1`
- `.claude/hooks/enforce-checkpoint-monotonic.ps1`
- `.claude/hooks/enforce-evidence-locations.ps1`
- `.claude/hooks/enforce-feature-folder-order.ps1`
- `.claude/hooks/enforce-powershell-batch-budget.ps1`
- `.claude/hooks/enforce-pr-author-skill.ps1`
- `.claude/hooks/enforce-prd-feature-before-planner.ps1`
- `.claude/hooks/enforce-promotion-mcp-only.ps1`
- `.claude/hooks/validate-bash.ps1`
- `.claude/hooks/validate-executor-output.ps1`
- `.claude/hooks/validate-feature-review-coverage.ps1`
- `.claude/hooks/validate-orchestrator-output.ps1`
- `.claude/hooks/validate-planner-output.ps1`
- `.claude/hooks/validate-required-artifact-output.ps1`
- `.claude/hooks/validate-task-researcher-output.ps1`

**Expected behavior after remediation (either option satisfies the gate):**

- **Option A — Add coverage.** Author Pester tests for the harness hooks under the canonical test location and produce a PowerShell coverage artifact at `artifacts/pester/powershell-coverage.xml` showing line coverage >= 85% and branch coverage >= 75% for the changed hook files (new-code threshold for files new to version control). Each hook's testable decision logic (the `Get-*Decision` / `Test-*` helper functions already present in several hooks) should be unit-tested with deterministic inputs, no temporary files, and no real wall-clock or network dependency, per `general-unit-test.md`.
- **Option B — Establish a repo-level coverage-scoping policy.** If the harness hooks are intentionally runtime-executed by Claude Code and not subject to in-repo unit coverage, add an explicit, documented coverage-scope exclusion at the repository policy level (for example a coverage-exclusion entry consumed by the coverage tooling, or a `quality-tiers.yml`/`docs/ci.research.md` clause that classifies `.claude/hooks/**` as excluded from the coverage gate). The exclusion must be authored as policy, not asserted at review time, and must not weaken the coverage threshold for product code.

**Verification commands after remediation:**

```bash
# Confirm a PowerShell coverage artifact exists and reports >= 85% line / >= 75% branch
# for the changed .claude/hooks/*.ps1 files (Option A):
Test-Path artifacts/pester/powershell-coverage.xml
# Parse report-level LINE/BRANCH counters and confirm covered/(covered+missed) >= 0.85 line, >= 0.75 branch.

# OR, for Option B, confirm the documented exclusion exists and is consumed by coverage tooling:
rg -n "\.claude/hooks" quality-tiers.yml docs/ci.research.md
# and confirm the coverage tooling honors the exclusion.

# Re-confirm no acceptance-criterion regression after remediation:
rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md
```

## Do Not Do (constraints for the remediation cycle)

- Do not modify policy documents under `.claude/rules/` or `.github/instructions/` to lower or remove the line >= 85% / branch >= 75% coverage thresholds for product code. The threshold is canonical.
- Do not modify the agent-harness hook content to make it appear covered without genuine tests. Coverage must reflect real assertions.
- Do not create temporary files in tests (prohibited by `general-unit-test.md`).
- Do not narrow the audit scope or mark PowerShell as out-of-scope at review time. Any coverage exclusion for `.claude/hooks/**` must be authored as repository policy (Option B), not asserted in an audit artifact.
- Do not touch product source (`src/`), test source (`tests/`), build config, CI workflow, benchmark, or action files. This feature is documentation/policy/configuration only and must remain so.
- Do not re-introduce any removed Python/TypeScript ecosystem file or any residual cross-repo marker. Re-run the AC-01/AC-12/AC-13 scans after remediation to confirm no regression.
- Do not write evidence to non-canonical paths. All evidence must go under `docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/<kind>/`.

## Handoff

Per `remediation-handoff-atomic-planner`, the orchestrator delegates plan authoring to `atomic-planner`, which writes `remediation-plan.2026-06-08T20-00.md` conforming to `atomic-plan-contract`. The plan's final phase must record numeric PowerShell coverage values for the changed hook files. `atomic-executor` preflights and executes; `feature-review` reaudits at the exit timestamp.
