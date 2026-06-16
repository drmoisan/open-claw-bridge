# Phase 0 — Policy Instructions Read

Timestamp: 2026-06-16T06-40

Policy Order:
1. CLAUDE.md (standing instructions) — NOTE: there is no top-level `CLAUDE.md` in this repository; `.claude/rules/` is authoritative and is auto-loaded via path-scoped frontmatter.
2. `.claude/rules/general-code-change.md` (cross-language code change policy)
3. `.claude/rules/general-unit-test.md` (cross-language unit test policy)
4. `.claude/rules/csharp.md` (C#-specific toolchain and coding standards)
5. `.claude/rules/architecture-boundaries.md` (architecture boundary + COM-confinement rules)
6. `.claude/rules/quality-tiers.md` (module rigor tiers and gate matrix)

Files Read (explicit list):
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/csharp.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/quality-tiers.md`

Output Summary:
All five policy files were read in the required order. Key constraints captured for this feature:
- Mandatory C# toolchain loop: format (CSharpier) -> lint/analyzers -> nullable type-check -> architecture-boundary tests -> test+coverage; restart on any failure or auto-fix.
- Uniform coverage gates: line >= 85%, branch >= 75%; no regression on changed lines.
- No file > 500 lines (production, test, or reusable script).
- COM interop confined to `OpenClaw.MailBridge` only; web projects must not pull COM into their closure.
- Project dependency rules 1-7: HostAdapter depends only on HostAdapter.Contracts + MailBridge.Contracts and must not reference MailBridge or perform COM; Core depends only on HostAdapter.Contracts.
- MSTest + Moq + FluentAssertions; no temporary files in tests; no Thread.Sleep/Task.Delay; determinism via TimeProvider.
- Suppressions disallowed except narrow, documented cases; `[ExcludeFromCodeCoverage]` only on live-COM-only members, each covered by the Phase 9 integration test.
- Tier map: OpenClaw.Core T1, OpenClaw.HostAdapter T1, MailBridge.Contracts/HostAdapter.Contracts/MailBridge T2, MailBridge.Client T3.
