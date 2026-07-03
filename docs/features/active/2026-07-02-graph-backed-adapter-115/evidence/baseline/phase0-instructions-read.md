# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T20-04

Policy Order: per `.claude/skills/policy-compliance-order/SKILL.md` and the plan Required References list (items 1-7):
1. CLAUDE.md / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`
7. `.github/instructions/csharp-code-change.instructions.md` and `.github/instructions/csharp-unit-test.instructions.md`

Files read:
- Repo-root `CLAUDE.md`: does not exist as a standalone file; the standing instructions are the auto-loaded `.claude/rules/` set (benchmark-baselines.md, ci-workflows.md, general-code-change.md, general-unit-test.md, orchestrator-state.md, quality-tiers.md, tonality.md), all loaded into context.
- `.claude/rules/general-code-change.md` (read)
- `.claude/rules/general-unit-test.md` (read)
- `.claude/rules/csharp.md` (read)
- `.claude/rules/architecture-boundaries.md` (read)
- `.claude/rules/quality-tiers.md` (read)
- `.claude/rules/tonality.md` (read)
- `.github/instructions/csharp-code-change.instructions.md` (read)
- `.github/instructions/csharp-unit-test.instructions.md` (read)

Mode Verification:
- `docs/features/active/2026-07-02-graph-backed-adapter-115/issue.md` contains `- Work Mode: full-feature` (line 10) and an explicit `## Acceptance Criteria` section (line 25).
- `docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md` exists.
- `docs/features/active/2026-07-02-graph-backed-adapter-115/user-story.md` exists.
- Verdict: PASS (full-feature preconditions satisfied).

Notes:
- CSharpier command form for this repo: global `csharpier format .` / `csharpier check .` (per plan note; local dotnet-tools manifest is not used).
- Test stack per repo reality and `.github/instructions/csharp-unit-test.instructions.md`: MSTest + Moq + FluentAssertions (+ CsCheck for property tests), notwithstanding the xUnit/NSubstitute wording in `.claude/rules/csharp.md`.
