# Phase 0 — Instructions Read

Timestamp: 2026-07-06T22-45

Policy Order (per `.claude/skills/policy-compliance-order/SKILL.md` and plan Required References):

1. `CLAUDE.md` / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`

Files read (explicit list):

- `.claude/rules/general-code-change.md` (cross-language code change policy; toolchain loop; 500-line cap; error handling; I/O boundaries)
- `.claude/rules/general-unit-test.md` (unit test five properties; coverage >= 85% line / >= 75% branch; no temp files; determinism infrastructure; test file location)
- `.claude/rules/csharp.md` (CSharpier; analyzers; nullable; CsCheck property tests on T1/T2; banned APIs; file-scoped namespaces; `internal` preference)
- `.claude/rules/quality-tiers.md` (T1–T4 tiers; uniform coverage; property-test density >= 1 per pure function for T1; mutation >= 75% pre-merge/nightly)
- `.claude/rules/tonality.md` (professional tone for authored content, e.g. runbook and evidence)
- `.claude/rules/architecture-boundaries.md` (No-COM; NetArchTest .NET boundaries; namespace-prefix rules covering new CloudGraph type)
- `.claude/rules/orchestrator-state.md` (human_interaction `exception` requires non-empty `runbook_path`; executor records evidence only, orchestrator records checkpoint)
- Plan: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/plan.2026-07-06T22-16.md`
- Requirements: `spec.md` (10 AC), `user-story.md` (7 AC), `issue.md` (mode marker)
- Research: `research/2026-07-06-send-on-behalf-allowlist-research.md`

Repository-actual deviations acknowledged (recorded in plan Open Questions / Notes):

- CSharpier is the global `csharpier` 1.3.0 executable (`csharpier format .` / `csharpier check .`), not `dotnet csharpier`.
- Test stack is MSTest + FluentAssertions + Moq (`MockBehavior.Strict`) + CsCheck + `FakeTimeProvider` + `FakeHttpHandler`, not the xUnit/NSubstitute wording in `.claude/rules/csharp.md`.

## Mode Verification

Files present in the feature folder: `issue.md`, `spec.md`, `user-story.md` (all three confirmed present).

- `issue.md` mode marker value: `- Work Mode: full-feature` (verified at line 10 of `issue.md`).
- `spec.md` contains a `## Acceptance Criteria` section (verified; 10 checkbox items S1–S10).
- `user-story.md` contains a `## Acceptance Criteria` section (verified; 7 checkbox items U1–U7).

Verdict: PASS. Full-feature mode confirmed; both authoritative AC sources (`spec.md` and `user-story.md`) exist with populated `## Acceptance Criteria` sections. No mismatch; execution proceeds.
