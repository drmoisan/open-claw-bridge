# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T11-58

Policy Order:
1. `CLAUDE.md`-loaded standing rules (repository has no root `CLAUDE.md` file; standing rules are auto-loaded from `.claude/rules/` per path-scoped frontmatter)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`

Files read (explicit list):
- `.claude/rules/general-code-change.md` (loaded in session context; cross-language code change policy: design principles, seven-stage toolchain loop, 500-line file cap, error handling, naming, I/O boundaries)
- `.claude/rules/general-unit-test.md` (loaded in session context; unit test policy: independence/isolation/determinism, coverage >= 85% line / >= 75% branch, no temp files in tests, tests/ tree mirroring, determinism infrastructure)
- `.claude/rules/csharp.md` (read directly; CSharpier formatting, analyzer stack with TreatWarningsAsErrors, nullable analysis, banned APIs including DateTime.UtcNow/Task.Delay, TimeProvider clock seam, CsCheck property tests for T1/T2)
- `.claude/rules/architecture-boundaries.md` (read directly; No-COM rules, NetArchTest.Rules enforcement for .NET, layer boundary assertions)
- `.claude/rules/quality-tiers.md` (loaded in session context; T1-T4 tier system, uniform coverage thresholds, tier-dependent gate matrix)
- Additional standing rules loaded in session context: `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/tonality.md`

Note: The plan's test-stack convention (MSTest + FluentAssertions + Moq + CsCheck) follows the established in-repo stack per the spec, superseding the xUnit/NSubstitute defaults named in `csharp.md` (documented in plan Open Questions).
