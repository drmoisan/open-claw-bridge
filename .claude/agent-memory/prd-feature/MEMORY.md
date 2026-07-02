# prd-feature agent memory

- [Harness canonical policy](project_harness_canonical_policy.md) — `.claude/rules/*` is the single source of truth; `.github/instructions/*` and `AGENTS.md` reconcile to it (Issue #66, 2026-06-08).
- [Harness deferred follow-ups](project_harness_deferred_followups.md) — generator script, benchmark validator, and dotnet-tools manifest are deferred out of Issue #66 scope.
- [Test framework discrepancy](project_test_framework_discrepancy.md) — repo tests use MSTest + FluentAssertions + Moq despite csharp.md saying xUnit + NSubstitute; specs must cite the real stack.
- [Issue-body staleness](project_issue_body_staleness.md) — April 2026 issue bodies predate #71/#72/#73 DTO fields; re-derive field lists from BridgeContracts.cs and record the delta.
