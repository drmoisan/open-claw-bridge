# Feature Audit: Issue #66 — Agent Harness Migration Correction (Cycle-1 Remediation Re-Audit)

**Audit Date:** 2026-06-08
**Feature Folder:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
**Base Branch:** `development` (commit `72d11879918bab20652abf2965eea42f17ab67d1`)
**Head Branch:** `bug/agent-repo-migration-problems-66` (commit `613564ce90df9a21faf9038f7597252cfd52304f`)
**Work Mode:** `full-bug`
**Audit Type:** Post-remediation acceptance verification (cycle-1)

---

## Scope and Baseline

- **Base branch:** `development` (commit `72d11879918bab20652abf2965eea42f17ab67d1`)
- **Head branch/commit:** `bug/agent-repo-migration-problems-66` (commit `613564ce90df9a21faf9038f7597252cfd52304f`)
- **Merge base:** `72d11879918bab20652abf2965eea42f17ab67d1`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/**`
  - Additional evidence: `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`
- **Feature folder used:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
- **Requirements source:** `spec.md` (AC-01..AC-15)
- **Work mode resolution note:** `issue.md` records `- Work Mode: full-bug`. Per the work-mode acceptance-criteria contract, `full-bug` resolves the AC source to `spec.md` only. The spec carries AC-01..AC-10 plus the Scope-Extension AC-11..AC-15.
- **Scope note:** This is the cycle-1 remediation re-audit. The branch diff is the full feature-vs-base range (merge-base `72d1187` -> head `613564c`): 211 paths (209 additions, 2 modifications). All 15 ACs are re-verified against head `613564c`. The prior cycle-1 audit's single blocking finding (PowerShell coverage FAIL) was a policy-audit coverage finding, not an AC failure; all 15 ACs verified PASS in cycle-1 and re-verify PASS here.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md` — only source (work mode `full-bug`)

### Acceptance criteria

1. **AC-01** — Zero residual markers remain in `.claude/`, `.github/`, and `AGENTS.md` for the marker set {`No-COM`, `TaskMaster`, `xUnit`, `NSubstitute`, `Office.js`, `taskpane`, `dependency-cruiser`, `Directory.Build.props`, `vstest.console`, `msbuild TaskMaster`}. (Historical `docs/features/**` is excluded.)
2. **AC-02** — Every filesystem path referenced by a retained `.claude/rules/*`, `.github/instructions/*`, or `AGENTS.md` either exists in the repository OR the referencing document explicitly states the path is not yet present. Covers at minimum `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` and `scripts/benchmarks/Test-BaselineProvenance.ps1` (qualified not-yet-present) and `mailbridge.runsettings` (present and referenced).
3. **AC-03** — `quality-tiers.yml` exists at repo root and classifies every project in `OpenClaw.MailBridge.sln` (six production + three test projects) with a valid tier in {T1, T2, T3, T4}. No absent project listed; no solution project omitted.
4. **AC-04** — `.claude/rules/*` and `AGENTS.md` agree on coverage thresholds (line >= 85%, branch >= 75%) and on tool names (MSTest, Moq, FluentAssertions, CSharpier, `dotnet build`/`dotnet test`, `OpenClaw.MailBridge.sln`). No 80%/90% thresholds; no xUnit/NSubstitute as the C# framework.
5. **AC-05** — The removed TypeScript and Python harness files are gone; no retained harness file references a removed worker.
6. **AC-06** — `AGENTS.md` C# commands use `dotnet build` / `dotnet test` against `OpenClaw.MailBridge.sln`; no `msbuild TaskMaster.sln` or `vstest.console.exe` in `AGENTS.md` or `.github/instructions/csharp-*.instructions.md`.
7. **AC-07** — The orchestrator-cited memory file `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` exists with the strict-handoff content; the `orchestrator.md` citation resolves.
8. **AC-08** — `docs/ci.research.md` exists with a section 1 describing the tier system; `.claude/rules/quality-tiers.md` cites it (no dangling reference).
9. **AC-09** — The C# QA-gate and engineer skills and `.github/instructions/csharp-code-change.instructions.md` invoke global `csharpier` and do not invoke `dotnet csharpier check` / `dotnet tool run csharpier`, and do not claim `Directory.Build.props`-centralized analyzer configuration.
10. **AC-10** — `dotnet build OpenClaw.MailBridge.sln` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` are valid commands; the solution and runsettings paths resolve.
11. **AC-11** — `.gitignore` no longer excludes the harness: `.claude/` and `.github/{agents,instructions,prompts,skills}` are tracked; `artifacts/` remains ignored; `git check-ignore` returns no match for representative harness paths.
12. **AC-12** — The full now-tracked harness contains no genuine (unqualified) residual marker for the AC-01 set; permitted matches are explicit prohibitions, qualified not-present statements, generic illustrative lists, or agent-memory provenance. `.github/agents/csharp-typed-engineer.agent.md` contains no `msbuild TaskMaster.sln` or `vstest.console.exe`.
13. **AC-13** — No tracked harness file references a removed Python/TypeScript worker, skill, rule, or hook (agent-memory provenance excepted).
14. **AC-14** — `.claude/settings.json` parses as valid JSON; it no longer lists the removed agents, Python skills, or Python hooks; every remaining hook command path resolves on disk.
15. **AC-15** — The Python/TS ecosystem files are deleted: the three Python skill directories, the two Python/TS prompts, and the two Python hooks are absent.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | AC-01 residual marker scan | PASS | `rg` over `.claude .github AGENTS.md` returns 10 hits, all permitted exceptions (explicit prohibitions at `AGENTS.md:601`, `csharp.md:65`, `csharp-unit-test.instructions.md:24`; qualified not-present at `csharp.md:35`, `architecture-boundaries.md:19`; agent-memory provenance in `task-researcher/` and `prd-feature/`). No genuine residual marker. | `rg -n "No-COM\|TaskMaster\|xUnit\|NSubstitute\|Office\.js\|taskpane\|dependency-cruiser\|Directory\.Build\.props\|vstest\.console\|msbuild TaskMaster" .claude .github AGENTS.md` | Re-verified against head `613564c`. |
| 2 | AC-02 path existence / qualification | PASS | `mailbridge.runsettings`, `quality-tiers.yml`, `docs/ci.research.md`, and the orchestrator memory file all exist; `pester.runsettings.psd1` and `Test-BaselineProvenance.ps1` are qualified not-present in `powershell.md` / `benchmark-baselines.md` (2 qualification hits). | `[ -e <path> ]` for each; `rg -ni "not yet present\|absent" .claude/rules/powershell.md .claude/rules/benchmark-baselines.md` | |
| 3 | AC-03 quality-tiers.yml completeness | PASS | `quality-tiers.yml` lists all 9 solution projects (Core, HostAdapter, MailBridge.Contracts, HostAdapter.Contracts, MailBridge, MailBridge.Client, Core.Tests, HostAdapter.Tests, MailBridge.Tests) with valid tiers T1/T2/T3; no extras. | `cat quality-tiers.yml` | Tiers in {T1,T2,T3}; all valid members of {T1..T4}. |
| 4 | AC-04 threshold/tool-name agreement | PASS | No 80%/90% gate; line >= 85% / branch >= 75% uniform; MSTest/Moq/FluentAssertions present; no xUnit/NSubstitute as the named framework (subsumed by AC-01). | `rg -n "85%\|75%\|80%\|90%" .claude/rules AGENTS.md`; `evidence/qa-gates/regression-rescan.md` | Thresholds unchanged by the remediation. |
| 5 | AC-05 removals + no removed-worker delegation | PASS | Representative removed files absent (`.claude/rules/typescript.md`, `python.md`, `.claude/agents/python-typed-engineer.md`, `.github/agents/typescript-engineer.agent.md`, `python-orchestrator.agent.md`, `tdd-red.agent.md`); dangling-worker scan returns 0 matches. | `[ -e <path> ]`; AC-13 `rg` scan | |
| 6 | AC-06 corrected C# commands | PASS | Zero `msbuild TaskMaster` / `vstest.console` hits in `AGENTS.md` or `.github/instructions`. | `rg -n "msbuild TaskMaster\|vstest\.console" AGENTS.md .github/instructions` | |
| 7 | AC-07 orchestrator memory file | PASS | `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` exists (35 lines, non-empty); cited once in `orchestrator.md`. | `wc -l <file>`; `rg -c "remediation-loop-strict-handoff.md" .claude/agents/orchestrator.md` | |
| 8 | AC-08 ci.research.md citation | PASS | `docs/ci.research.md` exists; `.claude/rules/quality-tiers.md` cites it (1 hit). | `[ -e docs/ci.research.md ]`; `rg -c "ci.research.md" .claude/rules/quality-tiers.md` | |
| 9 | AC-09 csharpier global form | PASS | Zero `dotnet csharpier check` / `dotnet tool run csharpier` / `Directory.Build.props` analyzer-config hits in the three named files. | `rg -n "dotnet csharpier check\|dotnet tool run csharpier\|Directory\.Build\.props" .claude/skills/csharp-qa-gate/SKILL.md .claude/skills/invoke-csharp-engineer/SKILL.md .github/instructions/csharp-code-change.instructions.md` | |
| 10 | AC-10 command validity smoke | PASS | `dotnet test` smoke recorded valid (298 passed, 0 failed, 3 skipped); solution and runsettings paths resolve. | `evidence/qa-gates/ac10-command-smoke.md` | Command-validity, not a coverage gate. |
| 11 | AC-11 gitignore tracking | PASS | `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md` returns no match (tracked); `git check-ignore artifacts/foo` matches (still ignored). | `git check-ignore <paths>` | |
| 12 | AC-12 full-tree residual scan | PASS | Same 10-hit set as AC-01, every hit in the permitted-exception set; `.github/agents/csharp-typed-engineer.agent.md` has zero `msbuild TaskMaster` / `vstest.console`. | AC-01 `rg` scan over tracked tree | The prior cycle-1 OUT-OF-SCOPE residual on this file is resolved on the tracked file. |
| 13 | AC-13 dangling-reference scan | PASS | Scan returns 0 matches across `.claude` and `.github`. | `rg -n "python-typed-engineer\|typescript-engineer\|python-qa-gate\|invoke-python-engineer\|python-change-budget-router\|check-python-test-purity\|enforce-python-batch-budget\|rules/python\.md\|rules/typescript\.md" .claude .github` | |
| 14 | AC-14 settings.json validity | PASS | `.claude/settings.json` parses as valid JSON; remaining hook command paths resolve. | `python -c "import json;json.load(open('.claude/settings.json'))"`; `evidence/qa-gates/ac14-settings-json.md` | |
| 15 | AC-15 ecosystem deletions | PASS | `.claude/skills/python-qa-gate`, `invoke-python-engineer`, `.github/prompts/orchestrate-python-work.prompt.md`, `.claude/hooks/check-python-test-purity.ps1`, `enforce-python-batch-budget.ps1` absent (representative of the 7 targets). | `[ -e <path> ]` for each | |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 15 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None.

**Recommended follow-up verification steps:**

1. The three deferred follow-ups in spec.md "Out of scope / follow-ups" (generator script `scripts/dev-tools/sync-agents-from-instructions.ps1`, benchmark validator `scripts/benchmarks/Test-BaselineProvenance.ps1`, `.config/dotnet-tools.json` CSharpier manifest) remain out of scope for this change and are tracked for a future cycle.
2. The `.github/agents/*` REVIEW-classified personas (beast-mode, `hlbpa`, `mentor`, `commentary-remediation`) remain pending a separate human decision, per spec.md non-goals.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- Criteria evaluated as **PASS** may be checked off in the authoritative source file(s) if represented as markdown checkboxes and not already checked.
- Criteria evaluated as **PARTIAL**, **FAIL**, or **UNVERIFIED** must remain unchecked.

All 15 ACs (AC-01..AC-15) are evaluated PASS and are already checked off `[x]` in `spec.md` from the cycle-1 delivery and review. No checkbox state change was required during this re-audit; the existing `[x]` states are confirmed correct against head `613564c`. No criterion regressed to require unchecking.

### AC Status Summary

- Source: `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md`
- Total AC items: 15
- Checked off (delivered): 15
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 15 | 15 | 0 | Checkbox-backed; all `[x]` confirmed against head `613564c`. No state change required this cycle. |
