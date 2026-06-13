# Baseline Residual-Marker Scan (Issue #66)

## Scope Extension cycle (Option 1A) — pre-change full-tree scan

Timestamp: 2026-06-08T11-08
Command:
`rg -n --no-ignore "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
EXIT_CODE: 0

Output Summary (per-hit disposition, post-prior-plan state):
- `AGENTS.md:601` "Do not introduce xUnit or NUnit" — PERMITTED (explicit prohibition).
- `.claude/rules/csharp.md:35` "no ... Directory.Build.props ... currently present" — PERMITTED (qualified not-present).
- `.claude/rules/csharp.md:65` "Do not introduce xUnit or NUnit" — PERMITTED (explicit prohibition).
- `.claude/rules/architecture-boundaries.md:19` "no ... Office.js layer, or dependency-cruiser configuration" — PERMITTED (qualified not-present).
- `.claude/agent-memory/task-researcher/project_state_2026_06.md:15` — PERMITTED (agent-memory provenance).
- `.claude/agent-memory/prd-feature/project_harness_canonical_policy.md:12,13,14,17` — PERMITTED (agent-memory provenance).
- `.github/instructions/csharp-unit-test.instructions.md:24` "Do not introduce xUnit or NUnit" — PERMITTED (explicit prohibition).
- `.github/agents/csharp-typed-engineer.agent.md:173,174` `msbuild TaskMaster.sln` — GENUINE RESIDUAL (Phase 5 fix target, P5-T1).
- `.github/agents/csharp-typed-engineer.agent.md:175` `vstest.console.exe` — GENUINE RESIDUAL (Phase 5 fix target, P5-T1).

The only genuine residual remaining at extension-cycle start is
`.github/agents/csharp-typed-engineer.agent.md:173-175`; corrected in Phase 5. The generic
illustrative framework list at
`.github/instructions/github-actions-ci-cd-best-practices.instructions.md` L322 and `AGENTS.md` uses
the casing "XUnit/RSpec" and is a permitted exception in AC-12 / AC-01. Baseline for AC-12 and the
AC-01 no-regression note.

---

## Original (prior plan) scan — retained for provenance

Timestamp: 2026-06-08T09-20
Command: `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
EXIT_CODE: 0

Output Summary: Pre-change match set is non-empty as expected. Matches per file (live policy targeted by plan):

- `AGENTS.md` — L445,446,454,455,628,629 (`msbuild TaskMaster.sln`), L630 (`vstest.console`), L606 (xUnit prose).
- `.claude/skills/csharp-qa-gate/SKILL.md` — L3,22,57,70,73 (xUnit), L30,34 (Directory.Build.props).
- `.claude/skills/invoke-csharp-engineer/SKILL.md` — L3,16,40,43 (xUnit).
- `.claude/agents/csharp-typed-engineer.md` — L3,23,35,36 (xUnit), L35 (NSubstitute).
- `.claude/rules/typescript.md` — L28 (Office.js), L56 (No-COM, dependency-cruiser). (file to be deleted)
- `.claude/rules/quality-tiers.md` — L13 (No-COM, SpamBayes, Triage), L14 (TaskMaster), L15 (Office.js, taskpane).
- `.claude/rules/general-unit-test.md` — L70 (Office.js).
- `.claude/rules/general-code-change.md` — L38 (dependency-cruiser).
- `.github/instructions/csharp-unit-test.instructions.md` — L24 (xUnit prose), L46,47 (msbuild TaskMaster), L48 (vstest.console).
- `.github/instructions/csharp-code-change.instructions.md` — L41,42,50,51 (msbuild TaskMaster).

Markers that are already qualified/correct (retained files; NOT failures):
- `.claude/rules/csharp.md` L35 (Directory.Build.props in explicit "not present" qualification), L65 (xUnit in a "Do not introduce xUnit" prohibition).
- `.claude/rules/architecture-boundaries.md` L19 (Office.js, dependency-cruiser in an explicit "There is no ... in this repository" statement).
- `.github/instructions/csharp-unit-test.instructions.md` L24 (xUnit in a prohibition "Do not introduce xUnit").
- `AGENTS.md` L606 (xUnit in a prohibition "Do not introduce xUnit or NUnit").

Out-of-scope markers observed (NOT in plan delete/edit list; reported for orchestrator follow-up, not touched):
- `.claude/agent-memory/task-researcher/project_state_2026_06.md` L15 — narrative audit note quoting marker terms.
- `.claude/agent-memory/prd-feature/project_harness_canonical_policy.md` L12-17 — canonical-policy memo quoting marker terms in "not X" form.
- `.github/agents/csharp-typed-engineer.agent.md` L173,174 (msbuild TaskMaster), L175 (vstest.console).
- `.github/agents/tdd-red.agent.md` L44 (xUnit) — this file is on the Phase 1 REMOVE list (deleted in P1-T16).
