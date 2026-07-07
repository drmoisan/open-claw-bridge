Timestamp: 2026-07-07T01-10

Policy Order:
1. `CLAUDE.md` (auto-loaded standing instructions)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`
6. `.claude/rules/architecture-boundaries.md`

Files read (in order):
- CLAUDE.md (standing instructions, auto-loaded into context)
- .claude/rules/general-code-change.md
- .claude/rules/general-unit-test.md
- .claude/rules/csharp.md
- .claude/rules/quality-tiers.md
- .claude/rules/architecture-boundaries.md

Notes:
- Confirmed `.claude/rules/csharp-suppressions.md` does not exist in this repository (glob check at execution time); it is correctly excluded from this reading order per the plan's own note.
- `.claude/rules/csharp.md` states xUnit/NSubstitute as the aspirational test framework; the actual established convention in this codebase's F9/F14 test files (`SchedulingWorkerAuditTests.cs`, `GraphSubscriptionManagerTests.cs`) is MSTest + Moq + FluentAssertions. Per spec.md's explicit resolution, this plan's new tests use MSTest + Moq + FluentAssertions to match established repo reality, not the aspirational rule-file wording.
