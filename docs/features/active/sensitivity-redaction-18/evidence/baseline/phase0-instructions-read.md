# Phase 0 — Policy and Feature Document Reads

Timestamp: 2026-07-02T08-58
Policy Order: per `.claude/skills/policy-compliance-order/SKILL.md` — CLAUDE.md standing instructions, then cross-language code change policy, cross-language unit test policy, then language/domain-specific rules.

## Policy files read (in order)

1. `.claude/rules/general-code-change.md`
2. `.claude/rules/general-unit-test.md`
3. `.claude/rules/csharp.md`
4. `.claude/rules/architecture-boundaries.md`
5. `.claude/rules/quality-tiers.md`
6. `.claude/rules/tonality.md`

Notes: `.claude/rules/csharp.md` names xUnit; the established suite for this repository is MSTest + FluentAssertions, and `spec.md` Constraints directs following the suite. This execution follows MSTest + FluentAssertions.

## Feature documents read (P0-T2)

Timestamp: 2026-07-02T08-58

1. `docs/features/active/sensitivity-redaction-18/spec.md` — **authoritative AC source** (19 AC items: Groups A/B/C plus toolchain/coverage AC)
2. `docs/features/active/sensitivity-redaction-18/issue.md`
3. `docs/features/active/sensitivity-redaction-18/user-story.md`
4. `docs/features/active/sensitivity-redaction-18/github-issue-18.md`
5. `docs/features/active/sensitivity-redaction-18/github-issue-20.md`

Work Mode: full-feature (per `issue.md` metadata); AC tracked in `spec.md` and `user-story.md` per acceptance-criteria-tracking skill.
