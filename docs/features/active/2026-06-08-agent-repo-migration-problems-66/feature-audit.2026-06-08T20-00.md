# Feature Audit: Agent Harness Migration Correction (#66)

**Audit Date:** 2026-06-08
**Feature Folder:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
**Base Branch:** `development` (commit `72d11879918bab20652abf2965eea42f17ab67d1`)
**Head Branch:** `bug/agent-repo-migration-problems-66` (commit `3ed46efaa28c43fefc946413bb3ba64866ca8d29`)
**Work Mode:** `full-bug`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `development` (commit `72d11879918bab20652abf2965eea42f17ab67d1`)
- **Head branch/commit:** `bug/agent-repo-migration-problems-66` (commit `3ed46efaa28c43fefc946413bb3ba64866ca8d29`)
- **Merge base:** `72d11879918bab20652abf2965eea42f17ab67d1`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/**`
  - Additional evidence: `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`
- **Feature folder used:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
- **Requirements source:** `spec.md` (AC-01..AC-15)
- **Work mode resolution note:** `issue.md` carries `- Work Mode: full-bug`. Per the work-mode contract, the authoritative AC source for `full-bug` is `spec.md` only. The spec's `## Acceptance Criteria` section (AC-01..AC-10) plus the `### Additional acceptance criteria` section (AC-11..AC-15, scope extension) together comprise the 15 criteria.
- **Scope note:** Audit scope is the full branch diff against the merge-base (198 changed paths). No caller narrowing was accepted; the caller's "no PowerShell source change" framing was recorded and rejected in `policy-audit.2026-06-08T20-00.md` under `## Rejected Scope Narrowing`. The change is documentation/policy/configuration only; no product/test/build/CI/workflow/benchmark file is in the diff.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `spec.md` — only source (work mode `full-bug`)

### Acceptance criteria (verbatim from `spec.md`)

1. **AC-01** — Zero residual markers remain in `.claude/`, `.github/`, and `AGENTS.md` for the marker set {`No-COM`, `TaskMaster`, `xUnit`, `NSubstitute`, `Office.js`, `taskpane`, `dependency-cruiser`, `Directory.Build.props`, `vstest.console`, `msbuild TaskMaster`}. (Historical `docs/features/**` is excluded.)
2. **AC-02** — Every filesystem path referenced by a retained `.claude/rules/*`, `.github/instructions/*`, or `AGENTS.md` either exists in the repository OR the referencing document explicitly states the path is not yet present. This covers at minimum `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` and `scripts/benchmarks/Test-BaselineProvenance.ps1` (both qualified as not-yet-present) and `mailbridge.runsettings` (present and referenced).
3. **AC-03** — `quality-tiers.yml` exists at repo root and classifies every project in `OpenClaw.MailBridge.sln` (the six production projects and three test projects) with a valid tier in {T1, T2, T3, T4}. No project is listed that is absent from the solution, and no solution project is omitted.
4. **AC-04** — `.claude/rules/*` and `AGENTS.md` agree on coverage thresholds (line >= 85%, branch >= 75%) and on tool names (MSTest, Moq, FluentAssertions, CSharpier, `dotnet build`/`dotnet test`, `OpenClaw.MailBridge.sln`). No document states the prior 80%/90% thresholds and no document names xUnit or NSubstitute as the C# test or mocking framework.
5. **AC-05** — The removed TypeScript and Python harness files are gone (eight `.claude/` files and eleven `.github/agents/*` REMOVE-list personas). No retained harness file references a removed worker.
6. **AC-06** — `AGENTS.md` C# commands use `dotnet build` / `dotnet test` against `OpenClaw.MailBridge.sln`. No occurrence of `msbuild TaskMaster.sln` or `vstest.console.exe` remains in `AGENTS.md` or in `.github/instructions/csharp-*.instructions.md`.
7. **AC-07** — The orchestrator-cited memory file `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` exists and contains the strict-handoff delegation content, so the citation at `.claude/agents/orchestrator.md:155` resolves.
8. **AC-08** — `docs/ci.research.md` exists with a section 1 describing the tier system, and `.claude/rules/quality-tiers.md` cites it as the tier source of truth (no dangling `docs/ci.research.md` reference).
9. **AC-09** — The C# QA-gate and engineer skills and `.github/instructions/csharp-code-change.instructions.md` invoke global `csharpier` and do not invoke `dotnet csharpier check` / `dotnet tool run csharpier`, and do not claim `Directory.Build.props`-centralized analyzer configuration.
10. **AC-10** — A representative `dotnet build OpenClaw.MailBridge.sln` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` are valid commands for this repository — the solution and runsettings paths they name resolve.
11. **AC-11** — `.gitignore` no longer excludes the harness: `.claude/` and `.github/{agents,instructions,prompts,skills}` are tracked, while `artifacts/` remains ignored. `git check-ignore` returns no match for representative harness paths.
12. **AC-12** — The full now-tracked harness contains no genuine (unqualified) residual marker for the AC-01 set. The only permitted matches are explicit prohibitions, qualified not-present statements, generic illustrative framework lists, or agent-memory provenance.
13. **AC-13** — No tracked harness file references a removed Python/TypeScript worker, skill, rule, or hook (agent-memory provenance excepted).
14. **AC-14** — `.claude/settings.json` parses as valid JSON; it no longer lists the removed agents, Python skills, or Python hooks; and every hook command path it still references exists on disk.
15. **AC-15** — The Python/TS ecosystem files are deleted: the three Python skill directories, the two Python/TS prompts, and the two Python hooks are absent.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-01 | Zero residual markers (10-term set) | PASS | All `rg` hits are explicit prohibitions ("Do not introduce xUnit"), qualified not-present statements (`csharp.md:35`, `architecture-boundaries.md:19`), or agent-memory provenance. No genuine unqualified marker. | `rg -n "No-COM\|TaskMaster\|xUnit\|NSubstitute\|Office\.js\|taskpane\|dependency-cruiser\|Directory\.Build\.props\|vstest\.console\|msbuild TaskMaster" .claude .github AGENTS.md` | Reproduced executor's per-hit disposition table. |
| AC-02 | Referenced paths exist or qualified | PASS | `mailbridge.runsettings`, `quality-tiers.yml`, `docs/ci.research.md`, `OpenClaw.MailBridge.sln` all present. `pester.runsettings.psd1` qualified "not yet present" in `powershell.md`, `powershell-unit-test.instructions.md`, `AGENTS.md`; `Test-BaselineProvenance.ps1` qualified in `benchmark-baselines.md`. | `Test-Path` for each; `rg "pester.runsettings.psd1\|Test-BaselineProvenance.ps1"` | All qualifications explicit. |
| AC-03 | `quality-tiers.yml` classifies all 9 projects | PASS | File lists exactly the 6 production + 3 test projects with tiers in {T1,T2,T3,T4}; no extra projects; cross-checked against `OpenClaw.MailBridge.sln`. | `Read quality-tiers.yml`; `rg -o "OpenClaw\.[A-Za-z.]+" OpenClaw.MailBridge.sln` | T1: Core, HostAdapter, Core.Tests, HostAdapter.Tests; T2: MailBridge.Contracts, HostAdapter.Contracts, MailBridge, MailBridge.Tests; T3: MailBridge.Client. |
| AC-04 | Threshold/tool-name agreement | PASS | No stray 80%/90% coverage gate in `.claude/rules` or `AGENTS.md`; MSTest/Moq/FluentAssertions present; xUnit/NSubstitute appear only as prohibitions. | `rg "80%\|90%" .claude/rules AGENTS.md`; `rg "MSTest\|Moq\|FluentAssertions"`; `rg "xUnit\|NSubstitute"` | 85%/75% present in `general-unit-test.md`, `quality-tiers.md`, `AGENTS.md`. |
| AC-05 | Removed files gone; no dangling worker | PASS | All 8 `.claude/` + 11 `.github/agents/*` removal targets absent (`Test-Path` False); no `python-typed-engineer`/`typescript-engineer` delegation in either orchestrator file. | `Test-Path` per file; `rg "python-typed-engineer\|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md` | Confirmed against branch diff (deletions) and live tree. |
| AC-06 | `dotnet` commands; no `msbuild`/`vstest` | PASS | No `msbuild TaskMaster`/`vstest.console` in `AGENTS.md` or `.github/instructions`; `dotnet build`/`dotnet test`/`OpenClaw.MailBridge.sln` present at `AGENTS.md` L442,450,623,624. | `rg "msbuild TaskMaster\|vstest\.console" AGENTS.md .github/instructions`; `rg "dotnet build\|dotnet test\|OpenClaw.MailBridge.sln" AGENTS.md` | Empty match for the prohibited forms. |
| AC-07 | Orchestrator memory file resolves | PASS | `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` present and non-empty; cited at `orchestrator.md:155`. | `Test-Path ...`; `rg "remediation-loop-strict-handoff" .claude/agents/orchestrator.md` | Citation resolves. |
| AC-08 | `docs/ci.research.md` + citation | PASS | File present; `quality-tiers.md:9` cites `docs/ci.research.md section 1` as the tier source of truth. | `Test-Path docs/ci.research.md`; `rg "ci.research.md" .claude/rules/quality-tiers.md` | No dangling reference. |
| AC-09 | Global csharpier; no prohibited forms | PASS | No `dotnet csharpier check`/`dotnet tool run csharpier`/`Directory.Build.props` analyzer claim in the three named files; global `csharpier`/`csharpier check .` form present. | `rg "dotnet csharpier check\|dotnet tool run csharpier\|Directory\.Build\.props" <3 files>`; `rg "csharpier" <3 files>` | Empty match for prohibited forms. |
| AC-10 | Corrected toolchain commands valid | PASS | `evidence/qa-gates/ac10-command-smoke.md`: `dotnet build` and `dotnet test ... --settings mailbridge.runsettings` resolve and run (298 passed, 0 failed, 3 skipped). | `dotnet build OpenClaw.MailBridge.sln`; `dotnet test ... --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | Solution and runsettings paths resolve. |
| AC-11 | `.gitignore` un-ignores harness | PASS | `.gitignore` diff adds `!.github/{agents,instructions,prompts,skills}/` and `!.claude/`, removes bare `.claude`, keeps `artifacts/` ignored, adds `.claude/settings.local.json`. `git check-ignore` no match for harness paths; still matches `artifacts/`. | `git diff ... -- .gitignore`; `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md`; `git check-ignore artifacts/coverage` | Tracking confirmed by 196 harness additions in branch diff. |
| AC-12 | No genuine residual in tracked harness | PASS | Full-tree marker scan: every hit in the permitted-exception set (prohibition, qualified not-present, or agent-memory provenance). `.github/agents/csharp-typed-engineer.agent.md` has no `msbuild TaskMaster`/`vstest.console`. | `rg <AC-01 set> .claude .github AGENTS.md`; `rg "msbuild TaskMaster\|vstest\.console" .github/agents/csharp-typed-engineer.agent.md` | Reproduced executor disposition table. |
| AC-13 | No dangling removed-worker reference | PASS | Dangling-reference scan over `.claude`/`.github` returns no active reference outside agent-memory provenance. | `rg "python-typed-engineer\|typescript-engineer\|python-qa-gate\|invoke-python-engineer\|python-change-budget-router\|check-python-test-purity\|enforce-python-batch-budget\|rules/python\.md\|rules/typescript\.md" .claude .github` (excl. agent-memory) | Empty. |
| AC-14 | `settings.json` valid; no Python residue; hook paths resolve | PASS | `ConvertFrom-Json` succeeds; no Python agent/skill/hook references; all 11 remaining hook command `.ps1` paths resolve (`Test-Path` True). | `pwsh -c "Get-Content .claude/settings.json -Raw \| ConvertFrom-Json"`; `rg <python set> .claude/settings.json`; per-path `Test-Path` | 11/11 hook paths present. |
| AC-15 | Python/TS ecosystem files deleted | PASS | All 7 extension deletion targets absent (`Test-Path` False): 3 Python skill dirs, 2 prompts, 2 hooks. | `Test-Path` per target | Confirmed in branch diff (no additions) and live tree. |

All 15 acceptance criteria evaluate PASS. The coverage gate that prevents overall PASS is a policy-audit gate (PowerShell coverage on the newly-tracked `.claude/hooks/*.ps1` files), not an acceptance-criterion failure: no AC requires harness-hook test coverage.

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION

**Criteria summary:**
- **PASS:** 15 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. PowerShell coverage FAIL on the 15 newly-tracked `.claude/hooks/*.ps1` files in the branch diff (0% line coverage; 10 of 15 unmeasured). This is a policy-audit coverage gate, not an acceptance-criterion failure, but it blocks overall PR readiness per the SKILL coverage-verification mandate. See `policy-audit.2026-06-08T20-00.md` and `remediation-inputs.2026-06-08T20-00.md`.

**Recommended follow-up verification steps:**

1. Resolve the PowerShell coverage gate: add Pester coverage for the harness hooks, or establish a repo-level coverage-scoping policy for runtime-executed harness hooks, then re-run the coverage verification.
2. After the coverage gate is resolved, re-run the AC-01/AC-12/AC-13 marker scans to confirm no regression was introduced by the remediation.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules, all 15 criteria evaluate PASS and are represented as markdown checkboxes in `spec.md`. They were already checked `[x]` by the executor at closeout; this review confirms each check is supported by independently-reproduced evidence. No checkbox state change was required (all 15 already `[x]` and verified PASS). No criterion was downgraded.

### AC Status Summary

- Source: `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md`
- Total AC items: 15
- Checked off (delivered): 15
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 15 | 15 | 0 | Checkbox-backed; all 15 verified PASS on independent re-scan; no state change needed. |

Note: The overall readiness is NEEDS REVISION due to the PowerShell coverage policy gate, which is independent of the acceptance criteria. All acceptance criteria are met.
