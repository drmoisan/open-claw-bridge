# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T14-04

Policy Order:
1. `CLAUDE.md`-loaded rules (repository has no root `CLAUDE.md` file; standing rules are auto-loaded from `.claude/rules/` via path-scoped frontmatter: `benchmark-baselines.md`, `ci-workflows.md`, `general-code-change.md`, `general-unit-test.md`, `orchestrator-state.md`, `quality-tiers.md`, `tonality.md`)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`

Files read (explicit list):
- `.claude/rules/general-code-change.md` (auto-loaded project instructions)
- `.claude/rules/general-unit-test.md` (auto-loaded project instructions)
- `.claude/rules/csharp.md` (read directly)
- `.claude/rules/architecture-boundaries.md` (read directly)
- `.claude/rules/quality-tiers.md` (auto-loaded project instructions)
- `.claude/rules/tonality.md` (auto-loaded project instructions)
- `.claude/rules/ci-workflows.md` (auto-loaded project instructions)
- `.claude/rules/benchmark-baselines.md` (auto-loaded project instructions)
- `.claude/rules/orchestrator-state.md` (auto-loaded project instructions)

Notes:
- `.claude/rules/csharp.md` names xUnit/NSubstitute as defaults; per the approved plan and the repository's established stack, this feature uses MSTest + FluentAssertions + Moq + CsCheck.
- Banned-API policy (no `DateTime.Now`/`DateTime.UtcNow`) is satisfied by design: the store and guard are clock-free with caller-supplied timestamps.
