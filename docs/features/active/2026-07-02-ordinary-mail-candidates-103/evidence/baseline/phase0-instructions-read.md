# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T13-02

Policy Order:
1. `CLAUDE.md`-loaded standing rules (auto-loaded project instructions)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`

Files read:
- `.claude/rules/general-code-change.md` (auto-loaded, confirmed in context)
- `.claude/rules/general-unit-test.md` (auto-loaded, confirmed in context)
- `.claude/rules/csharp.md` (read in full)
- `.claude/rules/architecture-boundaries.md` (read in full)
- `.claude/rules/quality-tiers.md` (auto-loaded, confirmed in context)
- `.claude/rules/tonality.md` (auto-loaded, confirmed in context)
- `.claude/rules/ci-workflows.md` (auto-loaded, confirmed in context)
- `.claude/rules/benchmark-baselines.md` (auto-loaded, confirmed in context)
- `.claude/rules/orchestrator-state.md` (auto-loaded, confirmed in context)

Notes:
- `.claude/rules/csharp.md` names xUnit/NSubstitute; the plan (Open Questions) documents that `tests/OpenClaw.Core.Tests` is established on MSTest + Moq + FluentAssertions + CsCheck and the spec mandates that stack. This plan follows the repository's established convention.
- CSharpier is invoked as the global tool (`csharpier format .` / `csharpier check .`), version 1.3.0; there is no local dotnet-tools manifest entry.
