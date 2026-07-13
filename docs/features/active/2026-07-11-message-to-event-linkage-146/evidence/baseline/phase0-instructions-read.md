# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-12T21-38

Policy Order:
1. CLAUDE.md (standing instructions, always loaded)
2. .claude/rules/general-code-change.md (cross-language code change policy)
3. .claude/rules/general-unit-test.md (cross-language unit test policy)
4. .claude/rules/quality-tiers.md (module rigor tiers T1-T4 and gate matrix)
5. .claude/rules/csharp.md (C#-specific toolchain and coding standards)

Files Read (in order):
- C:/Users/DanMoisan/repos/open-claw-bridge/.claude/worktrees/agent-a12f48ac10d64aceb/CLAUDE.md and .claude/rules loaded via context (general-code-change.md, general-unit-test.md, quality-tiers.md, tonality.md, ci-workflows.md, benchmark-baselines.md, orchestrator-state.md)
- .claude/rules/general-code-change.md
- .claude/rules/general-unit-test.md
- .claude/rules/quality-tiers.md
- .claude/rules/csharp.md

Notes:
- csharp.md text nominally references xUnit + NSubstitute; the authoritative repository reality and the approved plan use MSTest + Moq + FluentAssertions. Execution follows the plan and repo reality (MSTest + Moq + FluentAssertions), consistent with recorded prior findings.
- Coverage policy (uniform, all tiers): line >= 85%, branch >= 75%; no regression on changed lines; no production file excluded from coverage.
- File-size cap: 500 lines for production/test/reusable-script files.
- Determinism: no temp files in tests, no wall-clock reads/sleeps, use FakeTimeProvider.

Output Summary: All required policy files read in the required order. No policy files were modified. Executor will follow the approved plan for message-to-event-linkage (Issue #146) using MSTest + Moq + FluentAssertions.
