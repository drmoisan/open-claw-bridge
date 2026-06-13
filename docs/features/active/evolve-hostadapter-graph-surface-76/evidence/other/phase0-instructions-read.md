# Phase 0 — Policy Instructions Read

Timestamp: 2026-06-12T22-30

Policy Order:
1. `.claude/skills/policy-compliance-order/SKILL.md` (policy reading order)
2. `CLAUDE.md` / project standing instructions (loaded into context)
3. `.claude/rules/general-code-change.md` (cross-language code change policy)
4. `.claude/rules/general-unit-test.md` (cross-language unit test policy)
5. `.claude/rules/csharp.md` (C#-specific toolchain and coding standards)
6. `.claude/rules/quality-tiers.md` (T1–T4 module rigor tiers and gate matrix)
7. `.claude/rules/architecture-boundaries.md` (project dependency and COM confinement rules)

Files Read:
- `.claude/skills/policy-compliance-order/SKILL.md`
- `CLAUDE.md` (and the `.claude/rules/*` content surfaced in standing instructions: benchmark-baselines.md, ci-workflows.md, general-code-change.md, general-unit-test.md, quality-tiers.md, tonality.md)
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/csharp.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/architecture-boundaries.md`

Key constraints carried into execution:
- Mandatory C# toolchain loop: format (CSharpier) -> build/analyzers -> nullable type-check -> architecture -> test+coverage. Restart from step 1 on any failure or auto-fix.
- File size limit: no production/test file may exceed 500 lines.
- Coverage gates (uniform T1–T4): line >= 85%, branch >= 75%; no regression on changed lines.
- Architecture boundaries: no new ProjectReference edges; Core -> HostAdapter.Contracts only; HostAdapter -> HostAdapter.Contracts + MailBridge.Contracts; no COM crossing.
- Evidence written only under `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/<kind>/`.
- Tonality: professional, factual, evidence-first; no humor/hyperbole.
