# 2026-06-08-agent-repo-migration-problems (Plan — Scope Extension, Option 1A)

- **Issue:** #66
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-08T11-00
- **Status:** Draft
- **Version:** 2.0 (Scope Extension)
- **Work Mode:** full-bug (spec.md required and present; user-story.md not required)

## Authoritative Inputs

- Spec (contract): `docs/features/active/2026-06-08-agent-repo-migration-problems-66/spec.md`,
  section "## Scope Extension — Version-Control Tracking and Full Python/TypeScript Removal
  (operator-directed 2026-06-08, Option 1A)" and its acceptance criteria **AC-11..AC-15**. The
  earlier AC-01..AC-10 are already delivered and MUST NOT be regressed by this plan.
- Research (file:line evidence): `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`.
- Prior plan (already executed; not repeated here):
  `docs/features/active/2026-06-08-agent-repo-migration-problems-66/plan.2026-06-08T09-15.md`.

## Change Class and Per-Task Gate Definition (read before executing)

This change is **documentation / policy / config only**. It edits `.gitignore`, deletes Markdown
skill directories and prompt files and two PowerShell hook files, edits one JSON file
(`.claude/settings.json`), edits several Markdown skill files, and edits one Markdown agent file.
There is **NO C# or PowerShell production-source or test-source change**.

Therefore the per-task quality gate is **NOT the language toolchain**. The executor MUST NOT run
Black, Ruff, Pyright, Prettier, ESLint, CSharpier (`csharpier` format), PSScriptAnalyzer,
`Invoke-Pester`, `dotnet build`, or `dotnet test` against these files as a per-task gate. The
per-task gate is **content verification**:

1. Residual / dangling-reference greps (`rg`) across the now-tracked `.claude/` and `.github/`
   trees and `AGENTS.md`, excluding `docs/features/**` (provenance archive, not live policy).
2. Version-control state checks: `git check-ignore` (expect no match for harness paths; expect a
   match for `artifacts/...`) and `git ls-files` (expect non-empty harness file list).
3. Existence checks: `Test-Path` returns `False` for each deleted file/directory; `Test-Path`
   returns `True` for each retained hook command path referenced by `.claude/settings.json`.
4. JSON validity: `pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"`
   exits 0 (valid JSON) after each edit to that file.

No deleted PowerShell hook is re-authored; the two hooks are removed, not corrected, so no
PowerShell toolchain runs against them. This per-task gate definition is stated explicitly so the
executor does not attempt language toolchains against config/Markdown/JSON.

## Coverage Policy Note

Repository coverage policy (line >= 85%, branch >= 75%) applies to C#/PowerShell **code/test**
changes. This change introduces no such code/test change, so no baseline-vs-post-change coverage
delta is produced or gated. Coverage threshold strings that appear in edited documents are treated
as **document content**, not as a numeric coverage measurement of this change.

## Evidence Locations (canonical, non-overridable)

All evidence for this feature is written under
`docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/<kind>/` per
`.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Writing to `artifacts/baselines/`,
`artifacts/qa/`, `artifacts/coverage/`, or any other non-canonical `artifacts/` evidence path is a
policy violation. Sub-kinds used by this plan:

- `evidence/baseline/` — Phase 0 baseline git-ignore/tracking and residual scans, and the
  pre-deletion existence inventory.
- `evidence/qa-gates/` — Phase 6 final-verification AC captures (AC-11..AC-15) and the
  AC-01/AC-05 no-regression capture.
- `evidence/other/` — Phase 0 policy-read evidence and intermediate per-phase scans.
- `evidence/issue-updates/` — issue mirror at closeout (handled by the broader feature lifecycle;
  not re-issued by this extension plan unless the orchestrator directs it).

Each command-bearing evidence artifact MUST record `Timestamp:`, `Command:`, `EXIT_CODE:`, and
`Output Summary:`.

## Fail-closed Evidence Rule

If any required Phase 0 baseline artifact or Phase 6 QA-gate artifact is missing or has incomplete
required fields, the verdict is BLOCKED or INCOMPLETE, never PASS. Do not mark an evidence-backed
task complete without its artifact on disk.

## Ground-truth Reference Data (confirmed in repo state at plan time)

- `.gitignore` currently ignores the harness: the `.github/*` block (lines ~76-80) whitelists only
  `.github/workflows/ci.yml` and `.github/workflows/publish.yml`; the bare `.claude` line (~83)
  ignores all of `.claude/`. `artifacts/` is ignored at line 22 and MUST remain ignored.
- Deletion targets confirmed present:
  - Skill directories: `.claude/skills/python-change-budget-router/` (SKILL.md),
    `.claude/skills/python-qa-gate/` (SKILL.md), `.claude/skills/invoke-python-engineer/` (SKILL.md).
  - Prompts: `.github/prompts/orchestrate-python-work.prompt.md`,
    `.github/prompts/javascript-typescript-jest.prompt.md`.
  - Hooks: `.claude/hooks/check-python-test-purity.ps1`,
    `.claude/hooks/enforce-python-batch-budget.ps1`.
- `.claude/settings.json` (165 lines) contains: `Agent(python-typed-engineer)` (L33) and
  `Agent(typescript-engineer)` (L36) in `permissions.allow`; `Skill(python-change-budget-router *)`
  (L46), `Skill(python-qa-gate *)` (L47), `Skill(invoke-python-engineer *)` (L48); the two
  `Write|Edit` PreToolUse hook command entries for `check-python-test-purity.ps1` (L92-94) and
  `enforce-python-batch-budget.ps1` (L95-98); and `python-typed-engineer|...|typescript-engineer`
  inside the `SubagentStop` matcher string (L133). The remaining hook command paths in the file
  resolve on disk and MUST continue to resolve after the edit.
- Shared-skill references to remove or generalize:
  - `.claude/skills/policy-compliance-order/SKILL.md` L25 (Python rules) and L27 (TypeScript rules)
    in the compliance-order list.
  - `.claude/skills/remediation-handoff-atomic-planner/SKILL.md` L103 enumerates
    `python-typed-engineer`, `typescript-engineer`, `csharp-typed-engineer`, `powershell-typed-engineer`;
    remove the first two, keep the last two.
  - `.claude/skills/translate-copilot-to-claude/SKILL.md` names deleted files in multiple places
    (L157, L159, L160, L201-204, L291-293); replace with examples that reference existing repo
    files (a PowerShell or C# agent/rule/skill/hook) or generalize so no deleted file is named.
- Residual command block: `.github/agents/csharp-typed-engineer.agent.md` L173-175 still contains
  `csharpier .`, two `msbuild TaskMaster.sln ...` lines, and one
  `vstest.console.exe <test-assembly-paths> /EnableCodeCoverage` line. Replace the two `msbuild`
  lines with `dotnet build OpenClaw.MailBridge.sln ...` and the `vstest.console.exe` line with
  `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`,
  consistent with the corrections already applied in `AGENTS.md` and `.github/instructions/csharp-*`.
- No active reference to a removed worker/rule/skill/hook remains in `.claude/agents/*` or
  `.github/agents/*` (the prior plan cleaned both orchestrators); confirmed at plan time. Phase 4
  still includes a full-tree confirmation scan in case anything was reintroduced.
- Permitted-exception note for the marker scans: `.github/instructions/github-actions-ci-cd-best-practices.instructions.md`
  L322 and `AGENTS.md` contain a generic illustrative test-runner list
  ("Jest, Vitest, Pytest, ... XUnit, RSpec") that is acceptable as generic CI guidance, not a
  defect. This is a permitted exception in AC-12 / the AC-01 no-regression scan.

## Sequencing Invariant (critical)

Phase 1 (`.gitignore`) MUST run **first**, before any verification in later phases, so that
`git check-ignore` / `git ls-files` and the residual greps observe the now-tracked harness files.
Deletions (Phase 2), settings edits (Phase 3), shared-skill edits (Phase 4), and the residual fix
(Phase 5) follow. Phase 6 verifies the end state.

---

### Phase 0 — Policy Read & Baseline Capture

- [x] [P0-T1] Read the required policy files in the order defined by `policy-compliance-order` and
  write the policy-read evidence artifact to `evidence/other/phase0-instructions-read.md` with
  `Timestamp:`, `Policy Order:`, and the explicit list of files read: `CLAUDE.md`,
  `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`,
  `.claude/rules/csharp.md`, `.claude/rules/powershell.md`, `.claude/rules/quality-tiers.md`,
  `.claude/rules/tonality.md`. Acceptance: artifact exists with all three fields populated.
  (Supports all ACs; establishes tonality + file-size constraints.)
- [x] [P0-T2] Capture the baseline git-ignore/tracking state. Run
  `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md` (expected: each
  path printed — i.e., currently ignored) and
  `git check-ignore artifacts/orchestration/orchestrator-state.json` (expected: matches) and
  `git ls-files .claude` (expected: empty or near-empty pre-change). Write all outputs to
  `evidence/baseline/baseline-gitignore-state.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`,
  `Output Summary:` recording, for each command, whether the path is currently ignored/tracked.
  Acceptance: artifact records the pre-change ignored state of the harness and the ignored state of
  `artifacts/`. (Baseline for AC-11.)
- [x] [P0-T3] Capture the baseline residual / dangling-reference scan. Run, with `--no-ignore`
  (because the harness is still untracked at this point so default ripgrep would skip it):
  `rg -n --no-ignore "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md|orchestrate-python-work|javascript-typescript-jest" .claude .github`
  and write the full output to `evidence/baseline/baseline-dangling-reference-scan.md` with the four
  required fields; `Output Summary:` lists each file:line hit. Acceptance: artifact records the
  pre-change reference set (expected non-empty: settings.json, the three shared skills, and the
  Python skills/prompts that are about to be deleted). (Baseline for AC-13.)
- [x] [P0-T4] Capture the baseline residual-marker scan for the AC-01 set over the full tree:
  `rg -n --no-ignore "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`.
  Write output to `evidence/baseline/baseline-marker-scan.md` with the four required fields;
  `Output Summary:` notes the residual hits, including the known
  `.github/agents/csharp-typed-engineer.agent.md` L173-175 `msbuild TaskMaster.sln` /
  `vstest.console.exe` block and the permitted generic framework list at
  `.github/instructions/github-actions-ci-cd-best-practices.instructions.md`. Acceptance: artifact
  records the pre-change marker set. (Baseline for AC-12, and the AC-01 no-regression note.)
- [x] [P0-T5] Capture the pre-deletion existence inventory. Run `Test-Path` for each deletion
  target: `.claude/skills/python-change-budget-router/`, `.claude/skills/python-qa-gate/`,
  `.claude/skills/invoke-python-engineer/`, `.github/prompts/orchestrate-python-work.prompt.md`,
  `.github/prompts/javascript-typescript-jest.prompt.md`,
  `.claude/hooks/check-python-test-purity.ps1`, `.claude/hooks/enforce-python-batch-budget.ps1`.
  Write results to `evidence/baseline/baseline-delete-inventory.md` with the four required fields.
  Acceptance: artifact confirms all seven targets are present (`True`) before deletion. (Baseline
  for AC-15.)
- [x] [P0-T6] Record branch and commit baseline (`git rev-parse HEAD`, current branch) into
  `evidence/baseline/baseline-git.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`,
  `Output Summary:`. Acceptance: artifact records the starting commit for rollback traceability.

### Phase 1 — `.gitignore` Tracking Correction (run FIRST)

- [x] [P1-T1] Edit `.gitignore`: in the `.github/*` block (currently lines ~76-80) add negation
  entries so `.github/agents/`, `.github/instructions/`, `.github/prompts/`, and `.github/skills/`
  are tracked, while keeping the existing `.github/workflows/ci.yml` / `publish.yml` whitelist and
  the `.github/workflows/*` ignore as-is. Do NOT alter line 22 (`artifacts/`). Acceptance:
  `git check-ignore .github/agents/orchestrator.agent.md .github/instructions/csharp-code-change.instructions.md .github/prompts/orchestrate-work.prompt.md .github/skills` returns nothing (no match) for each path. (AC-11.)
- [x] [P1-T2] Edit `.gitignore`: remove the bare `.claude` ignore line (~83) so all of `.claude/`
  is tracked, and add `.claude/settings.local.json` as a forward-looking local-only ignore.
  Acceptance: `git check-ignore .claude/rules/csharp.md .claude/settings.json` returns nothing (no
  match); `git check-ignore .claude/settings.local.json` returns a match. (AC-11.)
- [x] [P1-T3] Verify `artifacts/` remains ignored after the `.gitignore` edits. Acceptance:
  `git check-ignore artifacts/orchestration/orchestrator-state.json` still returns a match; record
  the result. (AC-11; guards against accidental over-broad un-ignore.)

### Phase 2 — Delete the Python/TypeScript Ecosystem

- [x] [P2-T1] Delete the directory `.claude/skills/python-change-budget-router/` (including its
  `SKILL.md`). Acceptance: `Test-Path .claude/skills/python-change-budget-router` returns `False`.
  (AC-15.)
- [x] [P2-T2] Delete the directory `.claude/skills/python-qa-gate/` (including its `SKILL.md`).
  Acceptance: `Test-Path .claude/skills/python-qa-gate` returns `False`. (AC-15.)
- [x] [P2-T3] Delete the directory `.claude/skills/invoke-python-engineer/` (including its
  `SKILL.md`). Acceptance: `Test-Path .claude/skills/invoke-python-engineer` returns `False`.
  (AC-15.)
- [x] [P2-T4] Delete `.github/prompts/orchestrate-python-work.prompt.md`. Acceptance:
  `Test-Path .github/prompts/orchestrate-python-work.prompt.md` returns `False`. (AC-15.)
- [x] [P2-T5] Delete `.github/prompts/javascript-typescript-jest.prompt.md`. Acceptance:
  `Test-Path .github/prompts/javascript-typescript-jest.prompt.md` returns `False`. (AC-15.)
- [x] [P2-T6] Delete `.claude/hooks/check-python-test-purity.ps1`. Acceptance:
  `Test-Path .claude/hooks/check-python-test-purity.ps1` returns `False`. The hook is removed, not
  re-authored; no PowerShell toolchain runs against it. (AC-15.)
- [x] [P2-T7] Delete `.claude/hooks/enforce-python-batch-budget.ps1`. Acceptance:
  `Test-Path .claude/hooks/enforce-python-batch-budget.ps1` returns `False`. The hook is removed,
  not re-authored. (AC-15.)

### Phase 3 — Edit `.claude/settings.json` (valid JSON throughout)

After each edit in this phase, re-run
`pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"` and confirm it exits 0
before proceeding.

- [x] [P3-T1] Remove the `Agent(python-typed-engineer)` (L33) and `Agent(typescript-engineer)`
  (L36) entries from `permissions.allow`, preserving JSON validity (no trailing-comma error).
  Acceptance: `pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"` exits 0;
  `rg -n "Agent\(python-typed-engineer\)|Agent\(typescript-engineer\)" .claude/settings.json`
  returns no matches; `Agent(csharp-typed-engineer)` and `Agent(powershell-typed-engineer)` remain
  present. (AC-14.)
- [x] [P3-T2] Remove the `Skill(python-change-budget-router *)` (L46), `Skill(python-qa-gate *)`
  (L47), and `Skill(invoke-python-engineer *)` (L48) entries from `permissions.allow`, preserving
  JSON validity. Acceptance: `ConvertFrom-Json` exits 0;
  `rg -n "python-change-budget-router|python-qa-gate|invoke-python-engineer" .claude/settings.json`
  returns no matches; the PowerShell `Skill(...)` allowances remain present. (AC-14.)
- [x] [P3-T3] Remove the two `Write|Edit` PreToolUse hook command objects that invoke
  `check-python-test-purity.ps1` (L92-94) and `enforce-python-batch-budget.ps1` (L95-98). Delete
  each entire `{ "type": "command", "command": "..." }` object (not just the inner string),
  preserving the surrounding `hooks` array validity. Acceptance: `ConvertFrom-Json` exits 0;
  `rg -n "check-python-test-purity|enforce-python-batch-budget" .claude/settings.json` returns no
  matches; the PowerShell purity/budget hooks, `enforce-evidence-locations.ps1`,
  `enforce-feature-folder-order.ps1`, and `enforce-checkpoint-monotonic.ps1` entries remain.
  (AC-14.)
- [x] [P3-T4] Remove `python-typed-engineer` and `typescript-engineer` from the `SubagentStop`
  matcher string (L133), leaving the remaining matcher alternatives intact (including
  `csharp-typed-engineer`, `powershell-typed-engineer`, and the non-worker agents). Acceptance:
  `ConvertFrom-Json` exits 0;
  `rg -n "python-typed-engineer|typescript-engineer" .claude/settings.json` returns no matches; the
  matcher still contains `csharp-typed-engineer` and `powershell-typed-engineer`. (AC-14.)
- [x] [P3-T5] Verify every remaining hook `command` path in `.claude/settings.json` resolves on
  disk. Enumerate each remaining `command` entry's `.ps1` path and run `Test-Path` for each.
  Acceptance: `Test-Path` returns `True` for every remaining referenced hook path (no dangling hook
  command remains after the deletions in Phase 2 and P3-T3). (AC-14.)

### Phase 4 — Edit Shared Skills to Drop Python/TypeScript Cross-References

- [x] [P4-T1] Edit `.claude/skills/policy-compliance-order/SKILL.md`: remove the Python entry
  (`.claude/rules/python.md`, `.claude/rules/python-suppressions.md`, L25) and the TypeScript entry
  (`.claude/rules/typescript.md`, `.claude/rules/typescript-suppressions.md`, L27) from the
  compliance-order list in section "Required Policy Reading Order (Baseline)", leaving PowerShell
  and C# entries. Acceptance:
  `rg -n "rules/python\.md|python-suppressions|rules/typescript\.md|typescript-suppressions" .claude/skills/policy-compliance-order/SKILL.md`
  returns no matches; the PowerShell and C# rule entries remain. (AC-13.)
- [x] [P4-T2] Edit `.claude/skills/remediation-handoff-atomic-planner/SKILL.md`: in the worker
  enumeration at L103, remove `python-typed-engineer` and `typescript-engineer`, keeping
  `csharp-typed-engineer` and `powershell-typed-engineer`. Acceptance:
  `rg -n "python-typed-engineer|typescript-engineer" .claude/skills/remediation-handoff-atomic-planner/SKILL.md`
  returns no matches; `csharp-typed-engineer` and `powershell-typed-engineer` remain in the
  enumeration. (AC-13.)
- [x] [P4-T3] Edit `.claude/skills/translate-copilot-to-claude/SKILL.md`: replace every example
  that names a deleted file or removed worker with an example that references an existing repo file
  (a PowerShell or C# agent/rule/skill/hook), or generalize the example so no deleted file is
  named. Specifically address: the hook-naming examples (L157), the rule-topic example list that
  names `python.md`/`typescript.md` (L158-159), the agent normalization example
  `python_typed_engineer -> python-typed-engineer` (L160), the mapping-table rows naming
  `.claude/rules/python.md#pytest-rules`, `.claude/hooks/check-python-test-purity.ps1`,
  `.claude/skills/python-qa-gate/SKILL.md`, `.claude/agents/python-typed-engineer.md` (L201-204),
  and the invocation examples naming `.github/agents/python-typed-engineer.agent.md` and
  `python-*-instructions` (L291-293). Use existing analogues such as
  `.claude/hooks/check-powershell-test-purity.ps1`, `.claude/rules/powershell.md`,
  `.claude/skills/powershell-qa-gate/SKILL.md`, `.claude/agents/powershell-typed-engineer.md`,
  `powershell_typed_engineer -> powershell-typed-engineer`, and
  `.github/agents/csharp-typed-engineer.agent.md`. Acceptance:
  `rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md" .claude/skills/translate-copilot-to-claude/SKILL.md`
  returns no matches; the skill still reads coherently with C#/PowerShell examples. (AC-13.)
- [x] [P4-T4] Full-tree confirmation scan for any other active reference to a removed
  worker/rule/skill/hook in tracked `.claude/skills/*`, `.claude/agents/*`, and (KEEP set)
  `.github/agents/*`. Run
  `rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md|orchestrate-python-work|javascript-typescript-jest" .claude/skills .claude/agents .github/agents`.
  For any hit not already handled by P4-T1..P4-T3, add and complete a targeted cleanup edit in this
  task. Acceptance: the scan returns no active reference outside agent-memory provenance (none
  expected in these three trees at plan time). Record the scan output. (AC-13.)

### Phase 5 — Fix the Residual `.github/agents/csharp-typed-engineer.agent.md`

- [x] [P5-T1] Edit `.github/agents/csharp-typed-engineer.agent.md` L173-175: replace the two
  `msbuild TaskMaster.sln /t:Build ...` lines with the corrected
  `dotnet build OpenClaw.MailBridge.sln ...` forms (analyzer/lint build and nullable build), and
  replace `vstest.console.exe <test-assembly-paths> /EnableCodeCoverage` with
  `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`,
  consistent with the corrections already applied in `AGENTS.md` and `.github/instructions/csharp-*`.
  In the same task, scan the rest of this file for any other stale command
  (`msbuild TaskMaster`, `vstest.console`, `dotnet tool run csharpier`, `dotnet csharpier check`,
  `xUnit`, `NSubstitute`, `Directory.Build.props`) and correct each occurrence found. Acceptance:
  `rg -n "msbuild TaskMaster|vstest\.console|dotnet tool run csharpier|dotnet csharpier check" .github/agents/csharp-typed-engineer.agent.md`
  returns no matches;
  `rg -n "dotnet build|dotnet test|OpenClaw.MailBridge.sln|mailbridge.runsettings" .github/agents/csharp-typed-engineer.agent.md`
  confirms the corrected commands are present. (AC-12.)

### Phase 6 — Final Verification (AC-11..AC-15 + AC-01/AC-05 No-Regression)

Each task runs the spec's extension Verification check and writes a QA-gate artifact to
`evidence/qa-gates/` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. These tasks
are unconditional; `SKIPPED` is not a valid completion. Because the harness is now tracked after
Phase 1, the residual greps in this phase run without `--no-ignore`.

- [x] [P6-T1] AC-11 — git-ignore / tracking checks. Run
  `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md` (expect no match
  for each), `git ls-files .claude` (expect non-empty),
  `git ls-files .github/agents .github/instructions .github/prompts .github/skills` (expect
  non-empty), `git check-ignore .claude/settings.local.json` (expect a match), and
  `git check-ignore artifacts/orchestration/orchestrator-state.json` (expect a match). Write to
  `evidence/qa-gates/ac11-gitignore-tracking.md`. Acceptance: harness paths are tracked (no
  ignore match, non-empty `ls-files`), `artifacts/` and `settings.local.json` remain ignored.
  (AC-11.)
- [x] [P6-T2] AC-12 — full-tree residual marker scan with manual exception confirmation. Run
  `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`.
  Manually confirm each remaining hit is in the permitted-exception set (explicit prohibitions such
  as "Do not introduce xUnit"; qualified not-present statements; the generic illustrative framework
  list in `.github/instructions/github-actions-ci-cd-best-practices.instructions.md` L322 and the
  equivalent line in `AGENTS.md`; agent-memory provenance). Specifically confirm
  `rg -n "msbuild TaskMaster|vstest\.console" .github/agents/csharp-typed-engineer.agent.md`
  returns no matches. Write to `evidence/qa-gates/ac12-residual-scan.md` with a per-hit disposition
  table (hit -> permitted-exception class, or FAIL). Acceptance: no genuine (unqualified) residual
  remains; every hit is classified as a permitted exception. (AC-12.)
- [x] [P6-T3] AC-13 — dangling-reference scan. Run
  `rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md"`
  over the tracked `.claude/` and `.github/` trees. Write to
  `evidence/qa-gates/ac13-dangling-reference-scan.md`. Acceptance: the scan returns no active
  reference; only agent-memory provenance hits (if any) are permitted, each confirmed as historical
  provenance in the artifact. (AC-13.)
- [x] [P6-T4] AC-14 — `.claude/settings.json` JSON-parse + removals + hook-path existence. Run
  `pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"` (expect exit 0);
  `rg -n "Agent\(python-typed-engineer\)|Agent\(typescript-engineer\)|python-change-budget-router|python-qa-gate|invoke-python-engineer|check-python-test-purity|enforce-python-batch-budget" .claude/settings.json`
  (expect no matches); `rg -n "python-typed-engineer|typescript-engineer" .claude/settings.json`
  (expect no matches); and `Test-Path` for each remaining hook `command` path in the file (expect
  `True` for all). Write to `evidence/qa-gates/ac14-settings-json.md`. Acceptance: JSON parses, all
  removals confirmed, every remaining hook path resolves. (AC-14.)
- [x] [P6-T5] AC-15 — deletion existence checks. Run `Test-Path` for each of the seven deletion
  targets (three skill directories, two prompts, two hooks). Write to
  `evidence/qa-gates/ac15-deletions.md`. Acceptance: `Test-Path` returns `False` for all seven.
  (AC-15.)
- [x] [P6-T6] AC-01 / AC-05 no-regression confirmation over the now-tracked tree. Re-run the AC-01
  marker scan
  `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
  and the AC-05 removed-worker scan
  `rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md`,
  and confirm no NEW genuine residual appears beyond the permitted-exception set (explicit
  prohibitions, qualified not-present statements, the generic CI framework lists in
  `.github/instructions/github-actions-ci-cd-best-practices.instructions.md` and `AGENTS.md`, and
  agent-memory provenance). The generic illustrative test-runner list
  ("Jest, Vitest, Pytest, ... XUnit, RSpec") is explicitly called out here as a **permitted
  exception, not a defect**. Write to `evidence/qa-gates/ac01-ac05-no-regression.md` with a
  disposition for each hit. Acceptance: AC-01 and AC-05 still hold; no new genuine residual was
  introduced by tracking the previously-gitignored files. (AC-01/AC-05 no-regression.)
- [x] [P6-T7] Compile the closeout consistency note referencing the five extension AC qa-gate
  artifacts (AC-11..AC-15) and the AC-01/AC-05 no-regression artifact under `evidence/qa-gates/`,
  plus the Phase 0 baselines under `evidence/baseline/`, into
  `evidence/other/closeout-summary-extension.md`. Acceptance: every extension AC maps to a present,
  field-complete qa-gate artifact; if any is missing or incomplete, the verdict is INCOMPLETE, not
  PASS. (Fail-closed evidence rule.)

---

## AC-to-Phase Traceability

| AC | Description | Implementing tasks | Verifying task |
|----|-------------|--------------------|----------------|
| AC-11 | `.gitignore` tracks harness; `artifacts/` stays ignored | P1-T1, P1-T2, P1-T3 | P6-T1 |
| AC-12 | No genuine residual marker; `csharp-typed-engineer.agent.md` fixed | P5-T1 | P6-T2 |
| AC-13 | No tracked file references a removed Python/TS worker/skill/rule/hook | P4-T1, P4-T2, P4-T3, P4-T4 | P6-T3 |
| AC-14 | `.claude/settings.json` valid JSON; removals applied; hook paths resolve | P3-T1, P3-T2, P3-T3, P3-T4, P3-T5 | P6-T4 |
| AC-15 | Python/TS ecosystem files deleted | P2-T1..P2-T7 | P6-T5 |
| AC-01/AC-05 (no-regression) | Earlier ACs still hold over now-tracked tree | (none new — verification only) | P6-T6 |

## Sequencing Invariants

- Phase 1 (`.gitignore`) MUST complete before any `git check-ignore` / `git ls-files` /
  default-ripgrep verification in later phases, so checks observe the now-tracked harness.
- Phase 2 deletions MUST complete before P6-T5 (AC-15) and before P3-T5 (remaining-hook-path
  existence) so the deleted Python hooks are no longer referenced.
- Phase 3 edits MUST keep `.claude/settings.json` valid JSON after every individual edit
  (re-run `ConvertFrom-Json` between P3 tasks).
- Phase 4 and Phase 5 edits MUST complete before P6-T2 (AC-12) and P6-T3 (AC-13).

## File-size Note

All edited files are config (`.gitignore`, `.claude/settings.json`) or Markdown documentation
(skill files, the agent file). Markdown documentation files are exempt from the 500-line cap.
`.claude/settings.json` (165 lines at plan time) and `.gitignore` are well under 500 lines and only
shrink or grow by a few lines under these edits. No file is at risk of the 500-line limit; no split
is required. The two deleted PowerShell hooks are removed, not edited, so the PowerShell file-size
rule does not apply to them.

## Out-of-scope (not delivered by this plan)

- Re-authoring or replacing the two deleted Python hooks with PowerShell/C# equivalents — the hooks
  are removed because the Python ecosystem is removed; no replacement is in scope.
- The `.github/agents/*` REVIEW-classified personas (beast-mode set, `hlbpa`, `mentor`,
  `commentary-remediation`) — separate human decision, unchanged here.
- Any C#/PowerShell production-source, test-source, build-config, or CI-workflow change — none is
  made by this documentation/policy/config extension.
- The deferred follow-ups recorded in the prior plan and spec (AGENTS.md generator script,
  benchmark validator script, `.config/dotnet-tools.json`) — unchanged by this extension.
