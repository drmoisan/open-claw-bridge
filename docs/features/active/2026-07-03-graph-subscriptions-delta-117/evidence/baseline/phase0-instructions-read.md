# Phase 0 — Policy and Requirements Reading Evidence

Timestamp: 2026-07-03T01-25
Policy Order: CLAUDE.md auto-loaded rules -> .claude/rules/general-code-change.md -> .claude/rules/general-unit-test.md -> .claude/rules/csharp.md -> .claude/rules/architecture-boundaries.md -> .claude/rules/quality-tiers.md -> feature spec.md -> feature issue.md -> feature user-story.md

Files read (in order):

1. `CLAUDE.md`-loaded rules (auto-loaded into context): `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/orchestrator-state.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`
2. `.claude/rules/general-code-change.md` (auto-loaded, re-confirmed)
3. `.claude/rules/general-unit-test.md` (auto-loaded, re-confirmed)
4. `.claude/rules/csharp.md` (read explicitly)
5. `.claude/rules/architecture-boundaries.md` (read explicitly)
6. `.claude/rules/quality-tiers.md` (auto-loaded, re-confirmed)
7. `docs/features/active/2026-07-03-graph-subscriptions-delta-117/spec.md` (read explicitly)
8. `docs/features/active/2026-07-03-graph-subscriptions-delta-117/issue.md` (read explicitly)
9. `docs/features/active/2026-07-03-graph-subscriptions-delta-117/user-story.md` (read explicitly)

Notes:
- Work Mode marker in `issue.md`: `full-feature` (AC sources: `spec.md` and `user-story.md`; `issue.md` also carries the identical AC list and will be checked off per closeout tasks).
- Repository reality note: the repository test stack is MSTest + FluentAssertions + Moq + CsCheck (per spec Constraints), which supersedes the xUnit/NSubstitute wording in `.claude/rules/csharp.md` for this codebase. CSharpier is invoked as the global tool `csharpier` (1.3.0), not `dotnet csharpier`.
