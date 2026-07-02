# Phase 0 — Instructions Read

Timestamp: 2026-06-16T11-20

Policy Order:
1. CLAUDE.md (standing instructions; auto-loaded)
2. .claude/rules/general-code-change.md (cross-language code change policy)
3. .claude/rules/general-unit-test.md (cross-language unit test policy)
4. .claude/rules/powershell.md (PowerShell-specific policy; PowerShell files in scope)
5. .claude/rules/quality-tiers.md (T1-T4 tier system; T4 classification for scripts/)

Files Read:
- CLAUDE.md (project-instructions block, auto-loaded into context)
- .claude/rules/general-code-change.md
- .claude/rules/general-unit-test.md
- .claude/rules/powershell.md
- .claude/rules/quality-tiers.md
- .claude/rules/benchmark-baselines.md (auto-loaded; not directly in scope)
- .claude/rules/ci-workflows.md (auto-loaded; not directly in scope)
- .claude/rules/tonality.md (auto-loaded)
- docs/features/active/env-driven-publish-versioning/issue.md (requirements source; AC-1..AC-9, D1-D10)
- docs/features/active/env-driven-publish-versioning/plan.2026-06-16T10-28.md (plan of record)

Key constraints confirmed:
- 500-line file cap for all production/test/reusable script files.
- Coverage >= 85% line / >= 75% branch on changed code (uniform across T1-T4).
- No temp files and no disk writes in tests; pure helpers driven with in-memory content.
- Preserve strict version ValidatePattern and signing fail-fast contract.
- PowerShell toolchain order: PoshQC format -> analyze -> test; restart on change/fail.
- Evidence under docs/features/active/env-driven-publish-versioning/evidence/<kind>/.
