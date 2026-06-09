# 2026-06-08-agent-repo-migration-problems (Plan)

- **Issue:** #66
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-08T09-15
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-bug (spec.md required and present; user-story.md not required)

## Authoritative Inputs

- Spec (contract): `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md` (Approved v1.0).
- Research (file:line evidence + per-file inventory): `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md` (Sections 1–8).
- Issue: `docs/features/active/2026-06-08-agent-repo-migration-problems-66/issue.md`.

## Change Class and Gate Definition (read before executing)

This change is documentation / policy / config only. It edits Markdown harness files, creates one
YAML file (`quality-tiers.yml`), and creates two Markdown files (`docs/ci.research.md`,
`.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`). There is NO C# or
PowerShell production-source or test-source change.

Therefore the per-task quality gate is NOT the language toolchain. The executor MUST NOT run
Black, Ruff, Pyright, Prettier, ESLint, CSharpier, PSScriptAnalyzer, `dotnet build`, or
`dotnet test` against the Markdown/YAML edits as a per-task gate. The per-task gate is **content
verification**:

1. Residual-marker greps (`rg`) across `.claude/`, `.github/`, and `AGENTS.md`, excluding
   `docs/features/**` (provenance archive, not live policy).
2. Path-existence checks (`Test-Path`) for created/referenced files.
3. Internal-consistency checks (coverage thresholds and tool names agree across `.claude/rules/*`
   and `AGENTS.md`; no retained file references a deleted worker).

The single representative exception is AC-10 in the Final Consistency phase: one `dotnet build`
and one `dotnet test` smoke against `OpenClaw.MailBridge.sln` to confirm the corrected command
strings name a solution and runsettings path that resolve in this repository. That smoke validates
the *commands documented in the harness*, not a code change. Its success criterion is that the
named paths resolve; a full green test run is a supporting signal, not a gate for this change.

This per-task gate definition is stated explicitly so the executor does not attempt language
toolchains against Markdown.

## Coverage Policy Note

Repository coverage policy (line >= 85%, branch >= 75%) applies to C#/PowerShell **code/test**
changes. This change introduces no such code/test change, so no baseline-vs-post-change coverage
delta is produced or gated. The `dotnet test` smoke in the Final Consistency phase is a
command-validity check, not a coverage gate. Where coverage thresholds appear in this plan they are
treated as **document content** to be reconciled (85%/75% must appear; 80%/90% must not), not as a
numeric coverage measurement of this change.

## Evidence Locations (canonical, non-overridable)

All evidence for this feature is written under
`docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/<kind>/` per
`.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Writing to `artifacts/baselines/`,
`artifacts/qa/`, `artifacts/coverage/`, or any other non-canonical path is a policy violation.
Sub-kinds used by this plan:

- `evidence/baseline/` — Phase 0 baseline marker scans and path-existence captures.
- `evidence/qa-gates/` — Final Consistency phase AC verification captures.
- `evidence/other/` — Phase 0 policy-read evidence and intermediate per-phase scans.
- `evidence/issue-updates/` — issue mirror at closeout.

Each command-bearing evidence artifact MUST record `Timestamp:`, `Command:`, `EXIT_CODE:`, and
`Output Summary:`.

## Fail-closed Evidence Rule

If any required Phase 0 baseline artifact or Final Consistency QA-gate artifact is missing or has
incomplete required fields, the verdict is BLOCKED or INCOMPLETE, never PASS. Do not mark an
evidence-backed task complete without its artifact on disk.

## Ground-truth Reference Data (confirmed in repo state at plan time)

- Solution `OpenClaw.MailBridge.sln` at repo root contains exactly these nine projects:
  - Production (`src/`): `OpenClaw.MailBridge` (`net10.0-windows`, the only Outlook-COM project),
    `OpenClaw.MailBridge.Contracts`, `OpenClaw.MailBridge.Client`, `OpenClaw.HostAdapter.Contracts`,
    `OpenClaw.HostAdapter`, `OpenClaw.Core`.
  - Tests (`tests/`): `OpenClaw.MailBridge.Tests` (`net10.0-windows`), `OpenClaw.HostAdapter.Tests`,
    `OpenClaw.Core.Tests`.
- `mailbridge.runsettings` is present at repo root. `quality-tiers.yml` and `docs/ci.research.md`
  are absent (to be created). `.claude/agent-memory/orchestrator/` is absent (to be created).
- `.claude/agents/orchestrator.md` references the worker set in frontmatter (L5) and in prohibited-
  delegation prose (L120), and cites the memory file at L155.
- Tier mapping to finalize in Phase 6 against the `.sln` (operator-proposed; executor confirms exact
  names from the `.sln` before authoring): T1 = `OpenClaw.HostAdapter`, `OpenClaw.Core`;
  T2 = `OpenClaw.MailBridge.Contracts`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge`;
  T3 = `OpenClaw.MailBridge.Client` and the Outlook-COM-confined surface within `OpenClaw.MailBridge`;
  T4 = DI/bootstrap/build/scripts. Each test project is classified with its production peer. The
  encoded file MUST list every one of the nine solution projects and MUST NOT list any project
  absent from the `.sln`.

---

### Phase 0 — Policy Read & Baseline Capture

- [x] [P0-T1] Read the required policy files in order and write the policy-read evidence artifact to `evidence/other/phase0-instructions-read.md` with `Timestamp:`, `Policy Order:`, and the explicit list of files read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/powershell.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`. Acceptance: artifact exists with all three required fields populated. (Supports all ACs; establishes tonality + file-size constraints.)
- [x] [P0-T2] Capture the baseline residual-marker scan. Run `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md` and write the full output to `evidence/baseline/baseline-marker-scan.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (count of matches per file). Acceptance: artifact records the pre-change match set (expected non-empty). (Baseline for AC-01.)
- [x] [P0-T3] Capture the baseline coverage-threshold scan. Run `rg -n "85%|75%|80%|90%" .claude/rules AGENTS.md` and write output to `evidence/baseline/baseline-coverage-threshold-scan.md` with the four required fields; `Output Summary:` notes which documents currently state 80%/90%. Acceptance: artifact records the pre-change threshold divergence. (Baseline for AC-04.)
- [x] [P0-T4] Capture the baseline path-existence state. Run `Test-Path` for each of `quality-tiers.yml`, `docs/ci.research.md`, `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`, `mailbridge.runsettings`, `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, `scripts/benchmarks/Test-BaselineProvenance.ps1`, and write results to `evidence/baseline/baseline-path-existence.md` with the four required fields; `Output Summary:` lists each path with True/False. Acceptance: artifact confirms `mailbridge.runsettings` True and the three to-be-created files False. (Baseline for AC-02, AC-03, AC-07, AC-08.)
- [x] [P0-T5] Capture the baseline deleted-file inventory. Run `Test-Path` for each of the four `.claude/rules` residue files, the two `.claude/agents` residue files, and the eleven `.github/agents/*` REMOVE-list files, writing results to `evidence/baseline/baseline-delete-inventory.md` with the four required fields. Acceptance: artifact confirms all seventeen target files are present (True) before deletion. (Baseline for AC-05.)
- [x] [P0-T6] Record branch and commit baseline (`git rev-parse HEAD`, current branch) into `evidence/baseline/baseline-git.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Acceptance: artifact records the starting commit for rollback traceability.

### Phase 1 — Delete TypeScript and Python Residue Files

- [x] [P1-T1] Delete `.claude/rules/typescript.md`. Acceptance: `Test-Path .claude/rules/typescript.md` returns False. (AC-05.)
- [x] [P1-T2] Delete `.claude/rules/typescript-suppressions.md`. Acceptance: `Test-Path .claude/rules/typescript-suppressions.md` returns False. (AC-05.)
- [x] [P1-T3] Delete `.claude/rules/python.md`. Acceptance: `Test-Path .claude/rules/python.md` returns False. (AC-05.)
- [x] [P1-T4] Delete `.claude/rules/python-suppressions.md`. Acceptance: `Test-Path .claude/rules/python-suppressions.md` returns False. (AC-05.)
- [x] [P1-T5] Delete `.claude/agents/typescript-engineer.md`. Acceptance: `Test-Path .claude/agents/typescript-engineer.md` returns False. (AC-05.)
- [x] [P1-T6] Delete `.claude/agents/python-typed-engineer.md`. Acceptance: `Test-Path .claude/agents/python-typed-engineer.md` returns False. (AC-05.)
- [x] [P1-T7] Delete `.github/agents/expert-nextjs-developer.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T8] Delete `.github/agents/expert-react-frontend-engineer.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T9] Delete `.github/agents/pytest-unit-test-coding.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T10] Delete `.github/agents/python-atomic-executor.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T11] Delete `.github/agents/python-atomic-planning.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T12] Delete `.github/agents/python-execution-only-typed.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T13] Delete `.github/agents/python-orchestrator.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T14] Delete `.github/agents/python-typed-engineer.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T15] Delete `.github/agents/typescript-engineer.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T16] Delete `.github/agents/tdd-red.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T17] Delete `.github/agents/tdd-green.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T18] Delete `.github/agents/tdd-refactor.agent.md`. Acceptance: `Test-Path` returns False. (AC-05.)
- [x] [P1-T19] Confirm the REVIEW-classified `.github/agents/*` files are NOT deleted (`5.1-Beast-adjusted`, `5.1-Thinking-Beast-Mode-adjusted`, `gpt-5-beast-mode`, `voidbeast-gpt41enhanced`, `hlbpa`, `mentor`, `commentary-remediation`, and `typescript-engineer.agent.md` is the only TS file deleted; the beast/mentor set is out of scope). Acceptance: `Test-Path` returns True for each of the seven REVIEW files; this task documents the scope boundary and deletes nothing. (Spec Out-of-scope.)

### Phase 2 — Create Supporting Files (must precede rule edits that cite them)

- [x] [P2-T1] Create `docs/ci.research.md` with a concise `## 1.` (section 1) describing the T1–T4 module rigor tier system for this repository (criticality definitions and representative OpenClaw examples per tier), authored in professional tone, under 500 lines. Acceptance: `Test-Path docs/ci.research.md` returns True and `rg -n "^## 1" docs/ci.research.md` (or equivalent section-1 heading) matches. (AC-08.)
- [x] [P2-T2] Create `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` populated from the orchestrator's inline strict delegation-chain content (drawn from `.claude/agents/orchestrator.md` Prohibited Delegations During a Remediation Cycle and the Citations section: workers are invoked by `atomic-executor` only; orchestrator must not bypass `atomic-planner`/`atomic-executor`; the chain is orchestrator -> atomic-planner -> atomic-executor (preflight) -> atomic-planner (revise) -> atomic-executor (execute) -> feature-review). The memory must use the standard memory frontmatter (`name`, `description`, `metadata.type: feedback`) and must not reference removed `python-typed-engineer`/`typescript-engineer` workers. Acceptance: `Test-Path` returns True, file is non-empty, and `rg -n "python-typed-engineer|typescript-engineer" <file>` returns no matches. (AC-07.)
- [x] [P2-T3] Read `OpenClaw.MailBridge.sln` and confirm the exact nine project names before authoring `quality-tiers.yml`. Acceptance: a confirmation note is recorded in the task evidence listing the nine names exactly as they appear in the `.sln`. (Precondition for AC-03.)
- [x] [P2-T4] Create `quality-tiers.yml` at repo root classifying every one of the nine confirmed projects with a tier in {T1, T2, T3, T4} using the mapping in Ground-truth Reference Data (test projects classified with their production peers). The file MUST NOT list any project absent from the `.sln` and MUST omit no solution project. Author in YAML with a brief header comment citing `docs/ci.research.md` section 1. Acceptance: `Test-Path quality-tiers.yml` returns True; parsing the YAML yields exactly the nine solution projects, each with a valid tier. (AC-03.)

### Phase 3 — Edit `.claude/rules/*`

- [x] [P3-T1] Edit `.claude/rules/quality-tiers.md`: replace the T1–T4 examples (No-COM/SpamBayes/Triage at L13, TaskMaster.Domain/Application at L14, Office.js/taskpane at L15) with real OpenClaw projects; point the tier source-of-truth reference (L9) at `docs/ci.research.md` section 1 (now present); retain the `quality-tiers.yml` requirement (L9, L20) now that the file exists. Acceptance: `rg -n "No-COM|TaskMaster|SpamBayes|Triage|Office\.js|taskpane" .claude/rules/quality-tiers.md` returns no matches; `rg -n "ci.research.md|quality-tiers.yml" .claude/rules/quality-tiers.md` still present and resolves to existing files. (AC-01, AC-08.)
- [x] [P3-T2] Edit `.claude/rules/general-code-change.md`: scope the toolchain examples (L35–41) to C#/PowerShell; remove the `dependency-cruiser` example (L38) and any Python/TS-only tool names (Black/Ruff/Pyright/Prettier/ESLint/Pytest/Vitest/oasdiff/fast-check/hypothesis) where they misstate this repo; keep the `quality-tiers.yml` reference (L29) now that the file exists. Acceptance: `rg -n "dependency-cruiser|Black|Ruff|Pyright|Prettier|ESLint|fast-check|hypothesis|oasdiff" .claude/rules/general-code-change.md` returns no matches that assert these as this repo's tools; `rg -n "quality-tiers.yml" .claude/rules/general-code-change.md` present. (AC-01, AC-04.)
- [x] [P3-T3] Edit `.claude/rules/general-unit-test.md`: replace the `Office.js` host-boundary example (L70) with a repo-appropriate boundary (Outlook COM / local HTTP); scope property-based and framework examples (fast-check/hypothesis/Vitest) to C#/PowerShell (MSTest/Pester). Acceptance: `rg -n "Office\.js|fast-check|hypothesis|Vitest" .claude/rules/general-unit-test.md` returns no matches. (AC-01.)
- [x] [P3-T4] Edit `.claude/rules/powershell.md`: replace the `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` reference (L18) with the actual Pester invocation against `tests/scripts` (`Invoke-Pester -Path tests/scripts -Output Detailed -CI`, as used in `ci.yml`), OR qualify the PoshQC path explicitly as not yet present. Acceptance: any retained `pester.runsettings.psd1` mention is accompanied by an explicit not-present qualification, and the real `tests/scripts`/`Invoke-Pester` invocation is documented. (AC-02.)
- [x] [P3-T5] Edit `.claude/rules/benchmark-baselines.md`: qualify the text so it states `scripts/benchmarks/Test-BaselineProvenance.ps1` is not yet present (do NOT author the script). Acceptance: `rg -n "Test-BaselineProvenance.ps1" .claude/rules/benchmark-baselines.md` matches are each accompanied by an explicit not-yet-present qualification. (AC-02.)
- [x] [P3-T6] Verify-only: confirm `.claude/rules/csharp.md` and `.claude/rules/architecture-boundaries.md` contain no residual markers from the AC-01 set; edit only if a residual marker remains. Acceptance: `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser" .claude/rules/csharp.md .claude/rules/architecture-boundaries.md` returns no matches (qualifying notes about absent `Directory.Build.props`/Stryker are permitted only as explicit not-present statements). (AC-01.)
- [x] [P3-T7] Verify-only: confirm `.claude/rules/ci-workflows.md` requires no change (issue/PR #26/#30 provenance verified to this repo). Acceptance: file unchanged; task records the no-change decision. (Spec checklist.)

### Phase 4 — Edit `.claude/agents/*` and `.claude/skills/*`

- [x] [P4-T1] Edit `.claude/agents/csharp-typed-engineer.md`: replace `xUnit` (L3, 23, 35, 36) with MSTest, `NSubstitute` (L35) with Moq, and `Directory.Build.props` analyzer-config claims with SDK/analyzer defaults; set commands to `dotnet build`/`dotnet test` and global `csharpier`. Acceptance: `rg -n "xUnit|NSubstitute|Directory\.Build\.props" .claude/agents/csharp-typed-engineer.md` returns no matches; `rg -n "MSTest|Moq|csharpier" .claude/agents/csharp-typed-engineer.md` present. (AC-01, AC-04, AC-09.)
- [x] [P4-T2] Edit `.claude/agents/orchestrator.md`: remove `python-typed-engineer` and `typescript-engineer` from the frontmatter worker/tools list (L5) and from the prohibited-delegation prose (L120), leaving `csharp-typed-engineer` and `powershell-typed-engineer`; remove any other Python/TS delegation prose. Do NOT remove the L155 memory-file citation (the file now exists from P2-T2). Acceptance: `rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md` returns no matches; `rg -n "remediation-loop-strict-handoff.md" .claude/agents/orchestrator.md` still present and resolves. (AC-05, AC-07.)
- [x] [P4-T3] Edit `.claude/skills/csharp-qa-gate/SKILL.md`: replace all `xUnit` occurrences (L3, 22, 57, 70, 73) with MSTest; replace `Directory.Build.props` analyzer-authority claims (L30, 34) with SDK/analyzer defaults; invoke global `csharpier` (drop `dotnet csharpier check`). Acceptance: `rg -n "xUnit|Directory\.Build\.props|dotnet csharpier check|dotnet tool run csharpier" .claude/skills/csharp-qa-gate/SKILL.md` returns no matches; `rg -n "MSTest|csharpier" <file>` present. (AC-01, AC-09.)
- [x] [P4-T4] Edit `.claude/skills/invoke-csharp-engineer/SKILL.md`: replace all `xUnit` occurrences (L3, 16, 40, 43) with MSTest; invoke global `csharpier` (drop `dotnet csharpier check`/`dotnet tool run csharpier` if present). Acceptance: `rg -n "xUnit|dotnet csharpier check|dotnet tool run csharpier" .claude/skills/invoke-csharp-engineer/SKILL.md` returns no matches; `rg -n "MSTest" <file>` present. (AC-01, AC-09.)

### Phase 5 — Edit `.github/agents/*`, `.github/instructions/*`, and `AGENTS.md`

- [x] [P5-T1] Edit `.github/agents/orchestrator.agent.md`: remove or replace the `python-typed-engineer` delegation references (L16, L208) consistent with the C#/PowerShell roster (no Python/TS workers). Acceptance: `rg -n "python-typed-engineer|typescript-engineer" .github/agents/orchestrator.agent.md` returns no matches. (AC-05.)
- [x] [P5-T2] Edit `.github/instructions/csharp-code-change.instructions.md`: replace `msbuild TaskMaster.sln` (L41–42, 50–51) with `dotnet build OpenClaw.MailBridge.sln` forms; invoke global `csharpier` (drop `dotnet tool run csharpier`/`dotnet csharpier check`); remove `Directory.Build.props` analyzer-config claims. Acceptance: `rg -n "msbuild TaskMaster|Directory\.Build\.props|dotnet csharpier check|dotnet tool run csharpier" <file>` returns no matches; `rg -n "dotnet build|OpenClaw.MailBridge.sln|csharpier" <file>` present. (AC-06, AC-09.)
- [x] [P5-T3] Edit `.github/instructions/csharp-unit-test.instructions.md`: replace `msbuild TaskMaster.sln` (L46–47) with `dotnet build OpenClaw.MailBridge.sln` and `vstest.console.exe ...` (L48) with `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Acceptance: `rg -n "msbuild TaskMaster|vstest\.console" <file>` returns no matches; `rg -n "dotnet test|mailbridge.runsettings" <file>` present. (AC-06.)
- [x] [P5-T4] Edit `.github/instructions/general-unit-test.instructions.md`: align the coverage thresholds (L39–40) from 80%/90% to canonical line >= 85% / branch >= 75%. Acceptance: `rg -n "80%|90%" <file>` returns no coverage-gate matches; `rg -n "85%|75%" <file>` present. (AC-04.)
- [x] [P5-T5] Edit `.github/instructions/powershell-unit-test.instructions.md`: replace the `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` reference (L22) with the actual `Invoke-Pester -Path tests/scripts` invocation, OR qualify the PoshQC path as not yet present. Acceptance: any retained `pester.runsettings.psd1` mention has an explicit not-present qualification; `rg -n "tests/scripts|Invoke-Pester" <file>` present. (AC-02.)
- [x] [P5-T6] Hand-edit `AGENTS.md` (generator script absent; Option C — hand-edit source instructions and `AGENTS.md` together this pass): replace `msbuild TaskMaster.sln` (L445–446, 454–455, 628–629) and `vstest.console.exe` (L630) with the `dotnet build`/`dotnet test` forms against `OpenClaw.MailBridge.sln`; align coverage thresholds (L349–350) from 80%/90% to 85%/75%; qualify the `pester.runsettings.psd1` path (L1254) as not yet present or replace with the `Invoke-Pester -Path tests/scripts` form. Record in the change description that AGENTS.md is hand-edited because `scripts/dev-tools/sync-agents-from-instructions.ps1` is absent (follow-up). Acceptance: `rg -n "msbuild TaskMaster|vstest\.console" AGENTS.md` returns no matches; `rg -n "80%|90%" AGENTS.md` returns no coverage-gate matches; `rg -n "dotnet build|dotnet test|OpenClaw.MailBridge.sln|85%|75%" AGENTS.md` present; any `pester.runsettings.psd1` mention is qualified. (AC-04, AC-06.)
- [x] [P5-T7] After P5-T6, confirm no touched file exceeds the 500-line limit; if `AGENTS.md` or any instruction file would exceed 500 lines as a result of an edit, note the overage and split per `general-code-change.md` (Markdown documentation files are exempt from the 500-line cap, but reusable script/policy files are not — record which exemption applies). Acceptance: line counts recorded for each edited file in this phase; any non-exempt file over 500 lines flagged. (Spec constraint.)

### Phase 6 — Final Consistency Verification (AC-01..AC-10)

Each task runs the spec's Verification check and writes a QA-gate artifact to
`evidence/qa-gates/` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. These tasks
are unconditional; `SKIPPED` is not a valid completion.

- [x] [P6-T1] AC-01 — residual marker scan. Run `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`. Write to `evidence/qa-gates/ac01-marker-scan.md`. Acceptance: zero matches, OR every match is in a retained file that explicitly qualifies the term as not-applicable/not-present. (AC-01.)
- [x] [P6-T2] AC-02 — referenced-path existence/qualification. Run `Test-Path mailbridge.runsettings` (expect True); `rg -n "pester.runsettings.psd1" .claude .github AGENTS.md` and confirm each hit is qualified as not-present; `rg -n "Test-BaselineProvenance.ps1" .claude` and confirm `.claude/rules/benchmark-baselines.md` qualifies it. Write to `evidence/qa-gates/ac02-path-checks.md`. Acceptance: `mailbridge.runsettings` True; every absent-path reference qualified. (AC-02.)
- [x] [P6-T3] AC-03 — `quality-tiers.yml` completeness. Confirm `Test-Path quality-tiers.yml` True; parse YAML and assert each of the nine solution projects has a tier in {T1,T2,T3,T4} and no listed project is absent from `OpenClaw.MailBridge.sln`. Write to `evidence/qa-gates/ac03-tier-classification.md`. Acceptance: all nine present with valid tiers; no extraneous project. (AC-03.)
- [x] [P6-T4] AC-04 — threshold and tool-name agreement. Run `rg -n "85%|75%|80%|90%" .claude/rules AGENTS.md` (expect 85%/75% only, no 80%/90% gate); `rg -n "MSTest|Moq|FluentAssertions" .claude/rules .claude/skills .claude/agents AGENTS.md` (present); `rg -n "xUnit|NSubstitute" .claude .github AGENTS.md` (no matches). Write to `evidence/qa-gates/ac04-consistency.md`. Acceptance: thresholds and tool names agree as specified. (AC-04.)
- [x] [P6-T5] AC-05 — deletions and worker references. Confirm `Test-Path` False for all eight `.claude/` removed files and eleven `.github/agents/*` REMOVE-list files; run `rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md` (no matches). Write to `evidence/qa-gates/ac05-deletions.md`. Acceptance: all nineteen paths absent; no removed-worker reference remains. (AC-05.)
- [x] [P6-T6] AC-06 — AGENTS.md and instructions C# commands. Run `rg -n "msbuild TaskMaster|vstest\.console" AGENTS.md .github/instructions` (no matches); `rg -n "dotnet build|dotnet test|OpenClaw.MailBridge.sln" AGENTS.md` (present). Write to `evidence/qa-gates/ac06-commands.md`. Acceptance: corrected commands present, stale commands absent. (AC-06.)
- [x] [P6-T7] AC-07 — orchestrator memory file. Confirm `Test-Path .claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` True and non-empty; confirm `.claude/agents/orchestrator.md` still cites it. Write to `evidence/qa-gates/ac07-memory-file.md`. Acceptance: file present, non-empty, citation resolves. (AC-07.)
- [x] [P6-T8] AC-08 — `docs/ci.research.md` and quality-tiers.md citation. Confirm `Test-Path docs/ci.research.md` True with a section 1; run `rg -n "ci.research.md" .claude/rules/quality-tiers.md` (citation resolves to present file). Write to `evidence/qa-gates/ac08-ci-research.md`. Acceptance: file present with section 1; no dangling citation. (AC-08.)
- [x] [P6-T9] AC-09 — csharpier and analyzer-config claims. Run `rg -n "dotnet csharpier check|dotnet tool run csharpier|Directory\.Build\.props" .claude/skills/csharp-qa-gate/SKILL.md .claude/skills/invoke-csharp-engineer/SKILL.md .github/instructions/csharp-code-change.instructions.md` (no matches); `rg -n "csharpier" <same files>` shows the global form. Write to `evidence/qa-gates/ac09-csharpier.md`. Acceptance: global csharpier invoked, no `Directory.Build.props` analyzer-authority claim. (AC-09.)
- [x] [P6-T10] AC-10 — corrected-command smoke. Run `dotnet build OpenClaw.MailBridge.sln` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` from repo root. Write both invocations to `evidence/qa-gates/ac10-command-smoke.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Acceptance for this AC: the named solution and `mailbridge.runsettings` paths resolve (the commands are valid for this repo). A full green test run is a supporting signal, not a gate for this documentation change; record the build/test outcome but do not treat a pre-existing unrelated test failure as a blocker for AC-10. (AC-10.)

### Phase 7 — Documentation, Status, and Closeout

- [x] [P7-T1] Update `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md` and `issue.md` Scope/AC checkboxes to reflect completed items, and record any deviations from scope (for example, a touched file at risk of the 500-line cap, or a qualified-rather-than-replaced path reference). Acceptance: spec/issue updated; deviations listed or "none". (Spec Rollout.)
- [x] [P7-T2] Write the issue-update mirror to `evidence/issue-updates/issue-66.<timestamp>.md` per the evidence conventions (`Timestamp:`, exact text, `PostedAs:`), summarizing the harness correction and listing the deferred follow-ups (generator script, benchmark validator, `.config/dotnet-tools.json`, `.github/agents/*` REVIEW decision). Acceptance: mirror artifact present with required fields. (Spec Rollout.)
- [x] [P7-T3] Compile the closeout consistency note referencing all ten AC qa-gate artifacts under `evidence/qa-gates/` and the Phase 0 baselines under `evidence/baseline/`, into `evidence/other/closeout-summary.md`. Acceptance: every AC maps to a present, field-complete qa-gate artifact; if any is missing or incomplete, verdict is INCOMPLETE, not PASS. (Fail-closed evidence rule.)

---

## AC-to-Phase Traceability

| AC | Description | Implementing tasks | Verifying task |
|----|-------------|--------------------|----------------|
| AC-01 | Zero residual markers | P3-T1..T3, P3-T6, P4-T1, P4-T3, P4-T4 | P6-T1 |
| AC-02 | Referenced paths exist or qualified | P3-T4, P3-T5, P5-T5 | P6-T2 |
| AC-03 | `quality-tiers.yml` classifies all 9 projects | P2-T3, P2-T4 | P6-T3 |
| AC-04 | Thresholds/tool names agree (85/75, MSTest/Moq/FA) | P3-T2, P4-T1, P5-T4, P5-T6 | P6-T4 |
| AC-05 | TS/Python files removed; no removed-worker refs | P1-T1..T18, P4-T2, P5-T1 | P6-T5 |
| AC-06 | AGENTS.md/instructions use dotnet build/test | P5-T2, P5-T3, P5-T6 | P6-T6 |
| AC-07 | Orchestrator memory file exists; citation resolves | P2-T2, P4-T2 | P6-T7 |
| AC-08 | `docs/ci.research.md` present; citation resolves | P2-T1, P3-T1 | P6-T8 |
| AC-09 | Global csharpier; no `Directory.Build.props` claim | P4-T1, P4-T3, P4-T4, P5-T2 | P6-T9 |
| AC-10 | Corrected `dotnet` commands resolve | P5-T2, P5-T3, P5-T6 | P6-T10 |

## Sequencing Invariants

- Phase 2 (create `docs/ci.research.md`, `quality-tiers.yml`, orchestrator memory file) MUST complete
  before Phase 3 rule edits that cite those files and before Phase 4 P4-T2 (which retains the memory
  citation).
- Phase 1 deletions MUST complete before Phase 6 AC-05 verification.
- Phase 5 source-instruction edits and the `AGENTS.md` hand-edit MUST occur in the same change set
  (Option C) so AGENTS.md and its source instructions do not drift; AC-06 verifies both.

## File-size Note

All edits are targeted and Markdown documentation files are exempt from the 500-line cap.
`quality-tiers.yml` is a small config map and will not approach 500 lines. P5-T7 explicitly checks
each edited file's line count; if a non-exempt reusable file would exceed 500 lines, the executor
records the overage and proposes a split rather than committing the overage.

## Out-of-scope (not delivered by this plan)

- `scripts/dev-tools/sync-agents-from-instructions.ps1` (AGENTS.md generator) — deferred follow-up.
- `scripts/benchmarks/Test-BaselineProvenance.ps1` (benchmark validator) — qualified as absent, not authored.
- `.config/dotnet-tools.json` (CSharpier local-tool manifest) — global csharpier used instead.
- `.github/agents/*` REVIEW-classified personas (beast-mode set, `hlbpa`, `mentor`,
  `commentary-remediation`) — separate human decision.
