# Phase 0 — Policy Instructions Read (Issue #70)

Timestamp: 2026-06-09T12-31

Policy Order: CLAUDE.md (standing instructions) → .claude/rules/general-code-change.md → .claude/rules/general-unit-test.md → language/domain rules (.claude/rules/csharp.md) → tier and boundary rules (.claude/rules/quality-tiers.md, .claude/rules/architecture-boundaries.md) → .claude/rules/tonality.md

Files read (in required order):

1. `CLAUDE.md` (standing project instructions auto-loaded into context)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`
6. `.claude/rules/architecture-boundaries.md`
7. `.claude/rules/tonality.md`

Output Summary: All seven policy files were read in the required order. Key constraints recorded for this feature: (1) `OpenClaw.Core` may depend only on `OpenClaw.HostAdapter.Contracts` (architecture-boundaries rule 6) — no new project and no new ProjectReference; (2) 500-line per-file cap; fail-fast error handling; (3) uniform coverage gates line >= 85%, branch >= 75% with no regression on changed lines; (4) T1 obligations for `OpenClaw.Core` including >= 1 property-based test per pure function; (5) determinism via injected `TimeProvider`/`FakeTimeProvider`, no `Thread.Sleep`/`Task.Delay`/temp files in tests; (6) MSTest + Moq + FluentAssertions; (7) professional tone for all authored content.
