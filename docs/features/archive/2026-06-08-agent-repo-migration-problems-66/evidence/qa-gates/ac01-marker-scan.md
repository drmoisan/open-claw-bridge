# AC-01 — Residual Marker Scan (Issue #66)

Timestamp: 2026-06-08T09-45
Command: `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
EXIT_CODE: 0

Output Summary: AC-01 PASS (canonical scan, default `.gitignore` respected as specified in the spec
Verification section). The only match is a qualified not-applicable prohibition:

- `AGENTS.md:601` — "Do not introduce xUnit or NUnit into existing test projects." This is a
  prohibition that does not name xUnit as the active C# test framework; it qualifies under the
  AC-01 retained-file exception.

All previously-listed live-policy markers (quality-tiers.md, general-unit-test.md, general-code-change.md,
the C# skills, csharp-typed-engineer.md, csharp-code-change/csharp-unit-test instructions, and AGENTS.md
command blocks) are now cleared.

## Supplementary `--no-ignore` scan (diagnostic, not the AC gate)

A diagnostic scan with `--no-ignore` surfaces additional matches in gitignored files. Each is
classified below:

Qualified / not-applicable (PASS):
- `.claude/rules/csharp.md:35` — `Directory.Build.props` inside an explicit "not present" statement.
- `.claude/rules/csharp.md:65` — xUnit inside a "Do not introduce xUnit" prohibition.
- `.claude/rules/architecture-boundaries.md:19` — Office.js / dependency-cruiser inside "There is no ... in this repository".
- `.github/instructions/csharp-unit-test.instructions.md:24` — xUnit inside a prohibition.
- `.claude/agent-memory/task-researcher/project_state_2026_06.md:15` — issue #66 audit note quoting marker terms.
- `.claude/agent-memory/prd-feature/project_harness_canonical_policy.md:12-17` — canonical-policy memo quoting marker terms in "not X" form.

OUT-OF-SCOPE residual (reported for follow-up; NOT in this plan's edit/delete list):
- `.github/agents/csharp-typed-engineer.agent.md:173-175` — unqualified `msbuild TaskMaster.sln`
  (L173, L174) and `vstest.console.exe` (L175). This Copilot agent file carries the same stale
  command block this change corrected in `AGENTS.md` and the `.github/instructions/csharp-*` files,
  but the plan did not enumerate it for edit. It is gitignored, so the canonical AC-01 scan does not
  flag it and AC-01 passes as written. Recommend a follow-up cycle to apply the same
  `dotnet build`/`dotnet test` correction (it mirrors `.github/instructions/csharp-unit-test`).
