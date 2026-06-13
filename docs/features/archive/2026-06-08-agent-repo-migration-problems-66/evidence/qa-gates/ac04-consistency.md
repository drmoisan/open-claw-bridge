# AC-04 — Threshold and Tool-Name Agreement (Issue #66)

Timestamp: 2026-06-08T09-48
Command: `rg -n "85%|75%|80%|90%" .claude/rules AGENTS.md`; `rg -n "MSTest|Moq|FluentAssertions" .claude/rules .claude/skills .claude/agents AGENTS.md`; `rg -n "xUnit|NSubstitute" .claude .github AGENTS.md`
EXIT_CODE: 0

Output Summary: AC-04 PASS.

Coverage thresholds:
- All coverage statements across `.claude/rules/*` and `AGENTS.md` read line >= 85% / branch >= 75%
  (AGENTS.md:348; csharp.md:74; general-unit-test.md:23-24; quality-tiers.md:33-34,51; powershell.md:63-64).
- No 80% or 90% coverage gate remains. The only `75%` non-coverage hit is the tier-dependent mutation-score
  row (quality-tiers.md:43, general-unit-test.md:71), which is a separate gate, not a coverage threshold.

Tool names present (MSTest / Moq / FluentAssertions):
- AGENTS.md:600,608,611,612,614,615; csharp.md:10,33,65,67,68; csharp-typed-engineer.md:3,23; etc.

xUnit / NSubstitute — only qualified / not-applicable occurrences remain:
- `AGENTS.md:601` and `.github/instructions/csharp-unit-test.instructions.md:24` and `.claude/rules/csharp.md:65`
  — all "Do not introduce xUnit or NUnit" prohibitions (do not name xUnit as the active framework).
- `.claude/agent-memory/*` — provenance memos quoting the terms in "not xUnit/NSubstitute" form (gitignored).

No document names xUnit or NSubstitute as the active C# test or mocking framework. Thresholds and tool
names agree as specified.
