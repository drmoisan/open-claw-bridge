# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T18-51

Policy Order: per `.claude/skills/policy-compliance-order/SKILL.md` — (1) CLAUDE.md / standing instructions, (2) `.claude/rules/general-code-change.md`, (3) `.claude/rules/general-unit-test.md`, (4) language-specific rule `.claude/rules/csharp.md`.

Files read (in order):

1. Standing instructions (CLAUDE.md-equivalent): no `CLAUDE.md` file exists at the repository root (verified by glob for `CLAUDE.md` and `.claude/CLAUDE.md`, both empty). Standing instructions are auto-loaded from `.claude/rules/` project-instruction files, which were present in session context: `benchmark-baselines.md`, `ci-workflows.md`, `general-code-change.md`, `general-unit-test.md`, `orchestrator-state.md`, `quality-tiers.md`, `tonality.md`.
2. `.claude/rules/general-code-change.md` — read (auto-loaded in session context; design principles, toolchain loop, 500-line cap, dependency policy, I/O boundaries).
3. `.claude/rules/general-unit-test.md` — read (auto-loaded in session context; five core properties, coverage >= 85% line / >= 75% branch, no temp files in tests, determinism infrastructure, test location mirror rule).
4. `.claude/rules/csharp.md` — read explicitly via Read tool (CSharpier formatting, analyzer stack via `dotnet build`, nullable analysis, banned APIs incl. `DateTime.UtcNow`/`Task.Delay`/`Thread.Sleep`, `TimeProvider`/`FakeTimeProvider` clock seam, CsCheck property tests for T1/T2, file-scoped namespaces).

Notes:
- Test-framework reality: plan and spec record that `tests/OpenClaw.Core.Tests/` uses MSTest + FluentAssertions + Moq (established repo precedent) rather than the xUnit + NSubstitute prescription in `csharp.md`; new tests follow repository reality per the approved plan.
