# Phase 0 — Policy Read Evidence (Cycle 2)

- Timestamp: 2026-07-09T19-13
- Feature: docs/features/active/2026-07-07-env-array-wrap-corruption-135 (Issue #135)
- Plan: plan.2026-07-09T09-15.md
- Policy Order:
  1. `CLAUDE.md`
  2. `.claude/rules/general-code-change.md`
  3. `.claude/rules/general-unit-test.md`
  4. `.claude/rules/powershell.md`

## Files Read

1. `CLAUDE.md` — **confirmed absent**. Searched with `Glob **/CLAUDE.md` (no results) and listed the repository root and `.claude/` directory directly; no `CLAUDE.md` file exists anywhere in this repository at any path. This repository's standing instructions are loaded entirely via path-scoped frontmatter under `.claude/rules/*.md` and `.claude/settings.json`, not a physical root `CLAUDE.md` file. This is a pre-existing repository characteristic, not introduced by this work cycle. Recorded here as a plan-precondition gap (P0-T1's stated file does not exist) and escalated in the executor's final completion report per the atomic-executor escalation protocol; it does not block the fix itself since no code or test change in this plan depends on `CLAUDE.md` content.
2. `.claude/rules/general-code-change.md` — read in full (cross-language code change policy: simplicity/reusability/extensibility/separation-of-concerns design principles, mandatory 7-stage toolchain loop, 500-line file limit, fail-fast error handling, naming conventions, public API compatibility, dependency policy, I/O boundary isolation).
3. `.claude/rules/general-unit-test.md` — read in full (five core test properties, >=85% line / >=75% branch coverage requirements applying uniformly across tiers, coverage exclusion policy prohibiting excluding production files, AAA test structure, external-dependency mocking rules, test file location mirroring `tests/`, determinism infrastructure requirements).
4. `.claude/rules/powershell.md` — read in full (PoshQC format -> analyze -> test toolchain order via MCP commands, PowerShell 7+ compatibility, change-budget limits, design-seam guidance, Pester v5 testing standards, deterministic test requirements, mocking rules, prohibited behaviors).

## Additional Context Read (not part of the four-file policy order, informational)

- `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`
- `scripts/Publish.Env.psm1`
- `tests/scripts/Publish.Env.Tests.ps1`
- `tests/scripts/Publish.Tests.ps1`
