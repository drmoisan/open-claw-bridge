# Phase 0 — Policy and Input Reading Evidence (Remediation Cycle 1, Issue #117)

Timestamp: 2026-07-03T09-06

Policy Order: CLAUDE.md auto-loaded rules first, then `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, per the plan's Required References and `policy-compliance-order`.

Files read (in order):

1. `CLAUDE.md`-loaded rules (auto-loaded into context): `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`
2. `.claude/rules/general-code-change.md` (cross-language code change policy)
3. `.claude/rules/general-unit-test.md` (cross-language unit test policy)
4. `.claude/rules/csharp.md` (C# toolchain and coding standards)
5. `.claude/rules/architecture-boundaries.md` (architecture boundary enforcement)
6. `.claude/rules/quality-tiers.md` (T1-T4 tier system; uniform coverage gates)
7. `docs/features/active/2026-07-03-graph-subscriptions-delta-117/remediation-inputs.2026-07-03T02-34.md` (enumerated fix list 1-5, exit verification commands, Do Not Do list)
8. `docs/features/active/2026-07-03-graph-subscriptions-delta-117/policy-audit.2026-07-03T02-34.md` (full document including Section 5 per-file coverage table, Section 8 finding B-117-01, appendices)

Notes:
- Test stack for this repository is MSTest + FluentAssertions + Moq + CsCheck (repo convention, recorded in the policy audit Section 3-C# note), notwithstanding the xUnit/NSubstitute text in `csharp.md`.
- Coverage gates: uniform >= 85% line / >= 75% branch, per-file and pooled; no regression on changed lines.
- Do Not Do constraints from remediation inputs acknowledged: no scope creep beyond items 1-5 (item 3 executed as option (a)), no policy weakening, no silent skips, no temp files, no wall-clock APIs, no AC text edits.
