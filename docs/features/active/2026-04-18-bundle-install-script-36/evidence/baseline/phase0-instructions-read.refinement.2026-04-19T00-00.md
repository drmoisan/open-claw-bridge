# Phase 0 Instructions Read (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Policy Order: CLAUDE.md (standing) -> general-code-change -> general-unit-test -> powershell -> tonality

## Files Read

- `.claude/rules/general-code-change.md` — cross-language code change policy (design, toolchain loop, 500-line guard, fail-fast error handling, dependency rules, I/O boundaries).
- `.claude/rules/general-unit-test.md` — cross-language unit test policy (independence, isolation, determinism, repo-wide coverage >= 80%, new-code coverage >= 90%, AAA structure, no temp files).
- `.claude/rules/powershell.md` — PowerShell-specific toolchain (PoshQC format/analyze/test via MCP), PowerShell 7+ compatibility, advanced-function/CmdletBinding, 500-line cohesion, Pester v5 conventions, >= 80% repo / >= 90% new-code coverage.
- `.claude/rules/tonality.md` — required professional tone (no humor, no hyperbole, metaphors restricted, evidence-first wording).
- `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` (refreshed post-Phase A) — Definition of Done list contains 15 items (11 [x], 4 [ ]); Seeded Test Conditions list contains 12 items (10 [x], 2 [ ]); Version bumped to 0.2; Last Updated 2026-04-19T00:00:00Z.
- `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` (refreshed post-Phase A) — Acceptance Criteria list contains 16 items (12 [x], 4 [ ]); Last Updated 2026-04-19T00:00:00Z.
