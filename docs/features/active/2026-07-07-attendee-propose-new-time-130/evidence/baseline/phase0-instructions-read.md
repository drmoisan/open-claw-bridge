# Phase 0 — Instructions Read (F19 attendee-propose-new-time, #130)

Timestamp: 2026-07-07T05-56

Policy Order: per `.claude/skills/policy-compliance-order/SKILL.md` and the plan's Required References (items 1-5):

1. `CLAUDE.md` / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`

Files read (explicit list):

- `CLAUDE.md`-equivalent auto-loaded standing rules: `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/tonality.md`, `.claude/rules/architecture-boundaries.md` (loaded via path-scoped frontmatter)
- `.claude/rules/general-code-change.md` (cross-language code change policy; 500-line file cap, seven-stage toolchain loop, fail-fast error handling)
- `.claude/rules/general-unit-test.md` (unit test policy; line >= 85% / branch >= 75% coverage, determinism infra, no temp files, tests mirror source tree)
- `.claude/rules/csharp.md` (C# standards; CSharpier, analyzers-as-errors, nullable, TimeProvider, CsCheck property tests on T1/T2, banned APIs)
- `.claude/rules/quality-tiers.md` (T1-T4 tiers; uniform coverage thresholds; `OpenClaw.Core` is T1)

Additional authoritative feature documents read before execution:

- `docs/features/active/2026-07-07-attendee-propose-new-time-130/plan.2026-07-07T05-21.md` (approved plan; source of truth)
- `docs/features/active/2026-07-07-attendee-propose-new-time-130/spec.md` (AC-1..AC-9, gate truth table, evaluation order, wire contract)
- `docs/features/active/2026-07-07-attendee-propose-new-time-130/user-story.md` (AC-1..AC-9, scenarios)
- `docs/features/active/2026-07-07-attendee-propose-new-time-130/issue.md` (mode marker)
- F18 precedent production and test files (GraphHostAdapterClient.RescheduleEvent.cs, SchedulingWorker.Reschedule.cs, SchedulingWorker.Pipeline.cs, HostAdapterSchedulingService.cs, and the F18 test suites)

Note on `csharp.md` test-framework wording: `csharp.md` names xUnit/NSubstitute, but the repository-actual test stack is MSTest + FluentAssertions + Moq + CsCheck (verified in F18 test files and documented in the plan's Open Questions). Execution follows the repository-actual stack and the F18 precedent, per the approved plan.

## Mode Verification

- `issue.md` mode marker: `- Work Mode: full-feature` (confirmed present, line 12).
- `spec.md`: exists in the feature folder; contains a `## Acceptance Criteria` section listing AC-1 through AC-9 (confirmed).
- `user-story.md`: exists in the feature folder; contains a `## Acceptance Criteria` section listing AC-1 through AC-9 (confirmed).
- Three files named: `issue.md` (marker `full-feature`), `spec.md` (AC source), `user-story.md` (AC source).
- Verdict: PASS. Full-feature preconditions satisfied; `spec.md` and `user-story.md` are the AC check-off targets (AC-1..AC-9 in both).
