# 2026-06-08-agent-repo-migration-problems (Spec)

- **Issue:** #66
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-08T12-00
- **Status:** Approved
- **Version:** 1.0

## Context
The agentic Claude/Copilot harness (`.claude/`, `.github/agents`, `.github/instructions`, `AGENTS.md`) was copied from another repository ("drm-copilot" / "TaskMaster", a No-COM Python/TypeScript/.NET project) and was not adapted to this repository. Many rules, agent definitions, skills, and instructions select the wrong tools (xUnit/NSubstitute instead of MSTest/Moq, a "No-COM" architecture that bans the Outlook COM interop this repository depends on) and reference files, solutions, and tooling that do not exist here (`TaskMaster.sln`, `quality-tiers.yml`, `Directory.Build.props`, `docs/ci.research.md`, the PoshQC settings file, and others). Agents acting on these documents receive incorrect guidance, and tier-based CI gates cannot function.

This is a documentation/harness correction for Issue #66. It does not change product runtime behavior. The single source of truth is `.claude/rules/*`; `.github/instructions/*` and `AGENTS.md` are corrected to agree with it.

The real stack, confirmed against `OpenClaw.MailBridge.sln`:

- Solution: `OpenClaw.MailBridge.sln` at repo root. Six production projects under `src/` (`OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`, `OpenClaw.MailBridge.Client`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.HostAdapter`, `OpenClaw.Core`) and three test projects under `tests/` (`OpenClaw.MailBridge.Tests`, `OpenClaw.HostAdapter.Tests`, `OpenClaw.Core.Tests`).
- .NET 10 SDK pinned to `10.0.201` in `global.json`. `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Tests` target `net10.0-windows`; the remainder target `net10.0`.
- Test framework: MSTest. Mocking: Moq. Assertions: FluentAssertions. Coverage collection: `coverlet.collector`.
- Build/test: `dotnet build`/`dotnet test`. Coverage runsettings: `mailbridge.runsettings` at repo root.
- Formatting: CSharpier, available as a global tool only (no `.config/dotnet-tools.json`).
- Architecture: a COM-based bridge. Outlook COM is allowed and confined to `OpenClaw.MailBridge`, not banned.
- Coverage thresholds (canonical, from `.claude/rules/*`): line >= 85%, branch >= 75%.

Environment:
- OS/version: Windows (Windows-first .NET 10 solution, `OpenClaw.MailBridge.sln`).
- Python version: n/a. This repository is .NET 10 (`global.json` `10.0.201`) + PowerShell + Outlook COM. The imported harness assumes Python/TypeScript and a No-COM .NET stack in many places.
- Command/flags used: agent startup reads `CLAUDE.md`/`AGENTS.md`/`.claude/rules/*`; review and QA-gate skills invoke the C# and PowerShell toolchains; promotion/orchestration skills invoke MCP tools.
- Data source or fixture: `.claude/` (rules, agents, skills, hooks), `.github/` (agents, instructions, skills), `AGENTS.md`, `quality-tiers.yml` (absent).

Impact / Severity:
- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

Agents that follow the migrated policy select the wrong test framework and mocking library, attempt to honor a No-COM architecture that contradicts the product's core COM bridge, invoke commands against a nonexistent solution/tooling, and rely on a tier-classification gate (`quality-tiers.yml` / `docs/ci.research.md`) that cannot run. The product runtime is unaffected; the defect is in the development/agent harness and CI policy.


## Repro & Evidence
Steps to Reproduce:
1. Open any agent that reads the migrated policy set (for example the C# engineer/QA path or the orchestrator).
2. Have it consult `.claude/rules/quality-tiers.md`, the `csharp-qa-gate` / `invoke-csharp-engineer` skills, or `AGENTS.md`.
3. Observe instructions that name xUnit/NSubstitute, a No-COM architecture, `TaskMaster.sln`, `Directory.Build.props`, `quality-tiers.yml`, `docs/ci.research.md`, or `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`.
4. Attempt to follow them against this repository's actual stack (MSTest/Moq/FluentAssertions, COM bridge, `dotnet build`/`dotnet test`, `mailbridge.runsettings`).

Expected:
The migrated agent harness describes this repository's real stack and references only files and tooling that exist here:

- C# tooling: MSTest + Moq + FluentAssertions; CSharpier (global tool); `dotnet build`/`dotnet test` against `OpenClaw.MailBridge.sln`; coverage via `mailbridge.runsettings`.
- Architecture: a COM-based bridge (`OpenClaw.MailBridge`) with the real project-reference graph; Outlook COM is allowed and confined, not banned.
- All referenced config/tooling paths exist, or the rule explicitly states that they are not yet present.

Actual:
Verified conflicts (live config only; historical `docs/features/**` artifacts are excluded). The authoritative, evidence-backed inventory is `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md` (Sections 1–8). Key confirmed markers and absences:

**Already fixed in a prior thread:**

- `.claude/rules/csharp.md` — rewritten to MSTest/Moq/FluentAssertions, .NET 10, COM-confined, real `dotnet`/CSharpier commands, with an explicit note of absent infrastructure (research Section 8, csharp.md row: no additional action required).
- `.claude/rules/architecture-boundaries.md` — rewritten to the real acyclic project-reference graph and COM-confinement rules (research Section 8: no action required).

**Remaining markers (research Sections 1–6, file:line):**

- `.claude/rules/quality-tiers.md` L9 (`docs/ci.research.md`, `quality-tiers.yml`), L13 (`No-COM`, `SpamBayes`, `Triage`), L14 (`TaskMaster`), L15 (`Office.js`, `taskpane`), L20 (`quality-tiers.yml`).
- `.claude/rules/typescript.md` — entire file is source-repo residue (no TypeScript here).
- `.claude/rules/general-code-change.md` L29 (`quality-tiers.yml`), L38 (`dependency-cruiser`), multi-language toolchain list.
- `.claude/rules/general-unit-test.md` L70 (`Office.js`).
- `.claude/rules/powershell.md` L18 (`scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, absent).
- `.claude/rules/benchmark-baselines.md` L29 (`scripts/benchmarks/Test-BaselineProvenance.ps1`, absent).
- `.claude/skills/csharp-qa-gate/SKILL.md` L3,22,57,70,73 (`xUnit`), L30,34 (`Directory.Build.props`).
- `.claude/skills/invoke-csharp-engineer/SKILL.md` L3,16,40,43 (`xUnit`).
- `.claude/agents/csharp-typed-engineer.md` L3,23 (`xUnit`), L35 (`xUnit`, `NSubstitute`), L36 (`xUnit`).
- `.github/instructions/csharp-code-change.instructions.md` L41–42,50–51 (`msbuild TaskMaster.sln`).
- `.github/instructions/csharp-unit-test.instructions.md` L46–47 (`msbuild TaskMaster.sln`), L48 (`vstest.console.exe`).
- `.github/instructions/general-unit-test.instructions.md` L39–40 (coverage 80%/90%, conflicts with canonical 85%/75%).
- `.github/instructions/powershell-unit-test.instructions.md` L22 (`pester.runsettings.psd1` path, absent).
- `AGENTS.md` (generated) L349–350 (80%/90%), L445–446,454–455,628–629 (`msbuild TaskMaster.sln`), L630 (`vstest.console.exe`), L1254 (`pester.runsettings.psd1`).
- `.github/agents/*` — Python/React/Next.js/TDD-Jest personas with no corresponding stack (research Section 6 REMOVE list); `.github/agents/orchestrator.agent.md` L16,208 delegate to `python-typed-engineer`.

**Missing referenced paths (research Section 2, confirmed absent):** `quality-tiers.yml`, `docs/ci.research.md`, `scripts/powershell/PoshQC/` and `…/settings/pester.runsettings.psd1`, `.config/dotnet-tools.json`, `scripts/benchmarks/Test-BaselineProvenance.ps1`, `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`, `scripts/dev-tools/sync-agents-from-instructions.ps1`. The coverage runsettings file is `mailbridge.runsettings` (present), not `tests/coverlet.runsettings`.

Logs / Screenshots:
- [x] Attached minimal evidence (verified quotes)
- Snippet:
  - `.claude/skills/csharp-qa-gate/SKILL.md:3`: "Executes the full CSharpier -> .NET Analyzers -> Nullable Analysis -> xUnit toolchain …"
  - `.claude/rules/quality-tiers.md:14`: "Examples: `TaskMaster.Domain`, `TaskMaster.Application`, mail-item DTOs …"
  - `AGENTS.md:445`: "`msbuild TaskMaster.sln /t:Build …`"
  - `.claude/rules/powershell.md:18`: "Use repo config at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`." (path missing)
  - `.claude/agents/orchestrator.md:155`: cites `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` (file missing)


## Scope & Non-Goals
- In scope: Correct the agent harness documents (`.claude/rules`, `.claude/agents`, `.claude/skills`, `.github/agents`, `.github/instructions`, `AGENTS.md`) so they describe this repository's real stack and reference only files and tooling that exist (or explicitly state they are not yet present); create the four supporting files enumerated in the Scope checklist; establish `.claude/rules/*` as the single source of truth and reconcile `.github/instructions/*` and `AGENTS.md` to it.
- Out of scope / non-goals: Authoring the `AGENTS.md` generator script, the benchmark validator script, and the `.config/dotnet-tools.json` local-tool manifest (see "Out of scope / follow-ups"). No product source, test source, build configuration, or CI workflow file is modified. Harness hook files are not modified.
- Explicitly excluded systems, integrations, or datasets: historical `docs/features/**` archive artifacts (provenance context only); the runtime COM bridge and its tests; the `.github/agents/*` REVIEW-classified files (beast-mode personas, `hlbpa`, `mentor`, `commentary-remediation`) which require a separate human decision and are not touched here.


## Single Source of Truth (Operator-Confirmed 2026-06-08)

`.claude/rules/*` is canonical. `.github/instructions/*` and `AGENTS.md` are corrected to match it. Confirmed parameters:

- Coverage thresholds: line >= 85%, branch >= 75% (uniform across tiers).
- Test stack: MSTest + Moq + FluentAssertions.
- Solution: `OpenClaw.MailBridge.sln`. Build/test via `dotnet build` / `dotnet test`.
- Formatting: global `csharpier`. The `dotnet csharpier check` / `dotnet tool run csharpier` forms and `Directory.Build.props` analyzer-config claims are dropped; analyzer behavior is described as SDK/analyzer defaults this repository actually uses.
- Tier source-of-truth: a concise new `docs/ci.research.md` (section 1) describes the tier system; `.claude/rules/quality-tiers.md` points at it. `quality-tiers.yml` is created at repo root.


## Scope (Per-Area Change Checklist)

Every file to edit, delete, or create. Each item cites the research finding it resolves (`artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`, by section). These checkbox items are tracked per the acceptance-criteria-tracking skill alongside the `AC-NN` items below.

### `.claude/rules`
- [x] Edit `.claude/rules/quality-tiers.md` — replace T1–T4 examples (L13 No-COM/SpamBayes/Triage, L14 TaskMaster.Domain/Application, L15 Office.js/taskpane) with the real OpenClaw projects; point the tier source-of-truth reference (L9) at the new `docs/ci.research.md` section 1; retain the `quality-tiers.yml` requirement (L9, L20) now that the file is created. (Research S1, S8 quality-tiers.md row.)
- [x] Delete `.claude/rules/typescript.md` — entire file is source-repo residue; no TypeScript exists here. (Research S1, S8 typescript.md row; decision 4.)
- [x] Delete `.claude/rules/typescript-suppressions.md` — TypeScript residue. (Decision 4.)
- [x] Delete `.claude/rules/python.md` — Python residue; no Python exists here. (Research Open Decision 5; decision 5.)
- [x] Delete `.claude/rules/python-suppressions.md` — Python residue. (Decision 5.)
- [x] Edit `.claude/rules/general-code-change.md` — scope the toolchain examples (L35–41) to C#/PowerShell; remove the `dependency-cruiser` example (L38); keep the `quality-tiers.yml` reference (L29) now that the file exists. (Research S1, S8 general-code-change.md row.)
- [x] Edit `.claude/rules/general-unit-test.md` — replace the `Office.js` host-boundary example (L70) with a repo-appropriate boundary (COM/HTTP); scope property-based and tool examples to C#/PowerShell. (Research S1, S8 general-unit-test.md row.)
- [x] Edit `.claude/rules/powershell.md` — remove or qualify the `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` reference (L18) as not yet present; document the actual Pester invocation (`tests/scripts`, `Invoke-Pester` as in `ci.yml:69`). (Research S2, S3 PowerShell layout, S8 powershell.md row.)
- [x] Edit `.claude/rules/benchmark-baselines.md` — qualify the rule text to state `scripts/benchmarks/Test-BaselineProvenance.ps1` is not yet present; do not author the script. (Research S7, S8 benchmark-baselines.md row; decision 6.)
- [x] No change to `.claude/rules/ci-workflows.md` — issue/PR provenance (#26/#30) verified as belonging to this repository; content is correct. (Research S7, S8 ci-workflows.md row.)
- [x] No additional change to `.claude/rules/csharp.md` and `.claude/rules/architecture-boundaries.md` — already corrected. (Research S8.)

### `.claude/agents`
- [x] Edit `.claude/agents/csharp-typed-engineer.md` — replace `xUnit` (L3, 23, 35, 36) with MSTest and `NSubstitute` (L35) with Moq. (Research S1, S8 csharp-typed-engineer.md row.)
- [x] Delete `.claude/agents/python-typed-engineer.md` — Python residue. (Research S8 python-typed-engineer.md row; decision 5.)
- [x] Delete `.claude/agents/typescript-engineer.md` — TypeScript residue. (Decision 4.)
- [x] Edit `.claude/agents/orchestrator.md` — strip Python/TypeScript worker references; ensure the cited memory file (L155) exists (created below). (Research S6, Open Decision 5; decision 5.)

### `.claude/skills`
- [x] Edit `.claude/skills/csharp-qa-gate/SKILL.md` — replace all `xUnit` occurrences (L3, 22, 57, 70, 73) with MSTest; remove `Directory.Build.props` as the active analyzer authority (L30, 34) and describe SDK/analyzer defaults instead; invoke global `csharpier` (drop `dotnet csharpier check`). (Research S1, S3 CSharpier config, S8 csharp-qa-gate row; decision 8.)
- [x] Edit `.claude/skills/invoke-csharp-engineer/SKILL.md` — replace all `xUnit` occurrences (L3, 16, 40, 43) with MSTest; invoke global `csharpier`. (Research S1, S8 invoke-csharp-engineer row; decision 8.)

### `.github/agents`
- [x] Delete `.github/agents/expert-nextjs-developer.agent.md` — Next.js stack not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/expert-react-frontend-engineer.agent.md` — React stack not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/pytest-unit-test-coding.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/python-atomic-executor.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/python-atomic-planning.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/python-execution-only-typed.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/python-orchestrator.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/python-typed-engineer.agent.md` — Python not present. (Research S6 REMOVE.)
- [x] Delete `.github/agents/tdd-red.agent.md` — Jest/TypeScript; not applicable. (Research S6 REMOVE.)
- [x] Delete `.github/agents/tdd-green.agent.md` — Jest/TypeScript; not applicable. (Research S6 REMOVE.)
- [x] Delete `.github/agents/tdd-refactor.agent.md` — Jest/TypeScript; not applicable. (Research S6 REMOVE.)
- [x] Delete `.github/agents/typescript-engineer.agent.md` — TypeScript not present. (Decision 4.)
- [x] Edit `.github/agents/orchestrator.agent.md` — remove/replace the `python-typed-engineer` delegation references (L16, 208) consistent with Python removal. (Research S6 KEEP-with-edit, Open Decision 9; decisions 5 and 9.)

### `.github/instructions`
- [x] Edit `.github/instructions/csharp-code-change.instructions.md` — replace `msbuild TaskMaster.sln` (L41–42, 50–51) with `dotnet build OpenClaw.MailBridge.sln` forms; invoke global `csharpier` (drop `dotnet tool run csharpier` / `dotnet csharpier check`); remove `Directory.Build.props` analyzer-config claims. (Research S5, S8 csharp-code-change row; decision 8.)
- [x] Edit `.github/instructions/csharp-unit-test.instructions.md` — replace `msbuild TaskMaster.sln` (L46–47) with `dotnet build OpenClaw.MailBridge.sln`, and `vstest.console.exe …` (L48) with `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. (Research S5, S8 csharp-unit-test row.)
- [x] Edit `.github/instructions/general-unit-test.instructions.md` — align coverage thresholds (L39–40) to canonical line >= 85% / branch >= 75%. (Research S5, S8 general-unit-test.instructions row.)
- [x] Edit `.github/instructions/powershell-unit-test.instructions.md` — remove/qualify the `pester.runsettings.psd1` path (L22); document the actual `Invoke-Pester` invocation. (Research S2, S3, S8 powershell-unit-test.instructions row.)

### `AGENTS.md`
- [x] Hand-edit `AGENTS.md` in the same change as the source instructions (the generator script is absent; deferred — see follow-ups): replace `msbuild TaskMaster.sln` (L445–446, 454–455, 628–629) and `vstest.console.exe` (L630) with the `dotnet build`/`dotnet test` forms against `OpenClaw.MailBridge.sln`; align coverage thresholds (L349–350) to 85%/75%; qualify the `pester.runsettings.psd1` path (L1254). (Research S4 Option C, S5, S8 AGENTS.md rows; decision 3.)

### New files
- [x] Create `quality-tiers.yml` at repo root — classify every real project. Proposed mapping (confirm against `OpenClaw.MailBridge.sln`): T1 = `OpenClaw.HostAdapter`, `OpenClaw.Core`; T2 = `OpenClaw.MailBridge.Contracts`, `OpenClaw.MailBridge`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Client`; T3 = the Outlook-COM-confined surface within `OpenClaw.MailBridge`; T4 = DI/bootstrap/build/scripts. Test projects classified with their production peers. (Research S2, S3, S8 New Files; decision 2.)
- [x] Create `docs/ci.research.md` — a concise section 1 describing the T1–T4 tier system, cited by `.claude/rules/quality-tiers.md` as the source of truth. (Research S2, Open Decision 2; decision 2.)
- [x] Create `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` — populated from the orchestrator's inline strict-handoff content (`.claude/agents/orchestrator.md` strict delegation chain), so the L155 citation resolves. (Research S8 New Files, Open Decision 7; decision 7.)

Note on the `quality-tiers.yml` project mapping: the exact project-to-tier assignment is confirmed against `OpenClaw.MailBridge.sln`, which contains the six production projects and three test projects listed in Context. The mapping above is the proposed assignment; the implementation must encode each of these nine real projects with a valid tier and must not list any project absent from the solution.


## Out of scope / follow-ups

The following are explicitly deferred to follow-up work and are NOT delivered by this change:

1. `scripts/dev-tools/sync-agents-from-instructions.ps1` — the `AGENTS.md` generator script referenced by the `AGENTS.md` header. It has never existed in this repository (research Section 4). This change hand-edits `AGENTS.md` and the source instructions together (Option C). Creating the generator so the header is truthful is a follow-up.
2. `scripts/benchmarks/Test-BaselineProvenance.ps1` — the benchmark baseline validator referenced by `.claude/rules/benchmark-baselines.md`. This change qualifies the rule text to state the script is not yet present (decision 6). Authoring the validator is a follow-up.
3. `.config/dotnet-tools.json` — the CSharpier local-tool manifest. This change does NOT create it; instead all skills/instructions invoke global `csharpier` (decision 8). Pinning CSharpier as a local dotnet tool is a follow-up.

The `.github/agents/*` REVIEW-classified files (research Section 6: the five beast-mode personas, `hlbpa`, `mentor`, `commentary-remediation`) require a separate human decision and are out of scope for this change.


## Root Cause Analysis
The `.claude/` and `.github/` agent system was copied from the "drm-copilot" / "TaskMaster" repository (a No-COM, Python + TypeScript + .NET project) without per-file adaptation. The provenance is visible in residual terms: "No-COM", `TaskMaster.*`, SpamBayes/Triage, Office.js/taskpane, xUnit/NSubstitute, `dependency-cruiser`, and references to that repository's CI research and PoshQC layout. `AGENTS.md` is generated from `.github/instructions/*.instructions.md`, but the generator script (`scripts/dev-tools/sync-agents-from-instructions.ps1`) is absent (research Section 4), so the source instructions and `AGENTS.md` must both be corrected by hand in the same change until the generator is implemented.


## Proposed Fix

### Design summary (what changes where):
Correct each migrated harness document to describe the real stack and existing paths, per the Scope checklist. Establish `.claude/rules/*` as canonical and reconcile `.github/instructions/*` and `AGENTS.md` to it. Delete the TypeScript and Python residue files. Create `quality-tiers.yml`, `docs/ci.research.md`, and the orchestrator memory file so retained rules and citations resolve.

### Boundaries and invariants to preserve:
- No product source, test source, build config, CI workflow, or hook file is modified.
- Outlook COM remains allowed and confined to `OpenClaw.MailBridge`; no rule may reintroduce a No-COM ban.
- The canonical coverage thresholds (line >= 85%, branch >= 75%) must be stated identically wherever coverage is named.
- Every retained rule must reference only paths that exist, or explicitly state the path is not yet present.

### Dependencies or blocked work:
- `docs/ci.research.md` must exist before `.claude/rules/quality-tiers.md` can cite it as the source of truth.
- `quality-tiers.yml` must exist before the `quality-tiers.yml` references in retained rules are valid.
- The orchestrator memory file must exist before the `orchestrator.md:155` citation resolves.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
See the Scope checklist (edits, deletions, creations grouped by area). Each item cites its research finding.

#### Functions/classes/CLI commands impacted:
None in product code. Command strings inside harness documents change from `msbuild TaskMaster.sln …` / `vstest.console.exe …` / `dotnet tool run csharpier …` to `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, and global `csharpier .`.

#### Data flow and validation changes:
None. This is a documentation/policy correction.

#### Error handling and logging updates:
None.

#### Rollback/feature-flag considerations (if applicable):
Rollback is `git revert` of the documentation change. No runtime flag involved.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:
`quality-tiers.yml` is a YAML map from project name to tier (T1–T4). `docs/ci.research.md` is Markdown with a section 1 describing the tier system. The orchestrator memory file is a Markdown memory note.

#### Required configuration keys and defaults:
`quality-tiers.yml` must contain an entry for every project in `OpenClaw.MailBridge.sln`. Coverage defaults stated in harness docs: line >= 85%, branch >= 75%.

#### Backward-compatibility expectations:
Agents reading the corrected harness must find consistent tool names and existing paths. No external consumer depends on these documents' prior (incorrect) content.

#### Performance constraints (latency/throughput/memory):
Not applicable.

## Assumptions, Constraints, Dependencies
- Assumptions: the `mcp__drm-copilot__run_poshqc_*` MCP tools' runtime fallback behavior is a runtime question and not determinable from repo files; the spec only corrects the repo-side path reference. CSharpier is installed globally in the agent environment.
- Constraints: no product/test/CI/build/hook files may be modified; tonality and file-size policies in `.claude/rules/*` apply to all authored content.
- External dependencies: none beyond the existing .NET 10 SDK and CSharpier global tool.

## Data / API / Config Impact
- User-facing or API changes: none.
- Data or migration considerations: none.
- Logging/telemetry updates (if any): none.
- Compatibility notes: harness command strings change to the `dotnet`/global-`csharpier` toolchain; coverage thresholds unified at 85%/75%.

## Test Strategy
This is a documentation/harness change; verification is by grep-based marker scans, file-existence checks, and a representative `dotnet build`/`dotnet test` smoke using the corrected commands. See the Verification section for the exact checks mapped to each `AC-NN`.

- Manual verification: re-scan `.claude/`, `.github/`, and `AGENTS.md` for the residual marker set after the change.
- Integration smoke: run the corrected C# toolchain commands against `OpenClaw.MailBridge.sln` to confirm they are valid for this repository.


## Acceptance Criteria

Each item is individually verifiable and tracked per the acceptance-criteria-tracking skill. Greppable checks are given in the Verification section.

- [x] **AC-01** — Zero residual markers remain in `.claude/`, `.github/`, and `AGENTS.md` for the marker set {`No-COM`, `TaskMaster`, `xUnit`, `NSubstitute`, `Office.js`, `taskpane`, `dependency-cruiser`, `Directory.Build.props`, `vstest.console`, `msbuild TaskMaster`}. (Historical `docs/features/**` is excluded.)
- [x] **AC-02** — Every filesystem path referenced by a retained `.claude/rules/*`, `.github/instructions/*`, or `AGENTS.md` either exists in the repository OR the referencing document explicitly states the path is not yet present. This covers at minimum `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` and `scripts/benchmarks/Test-BaselineProvenance.ps1` (both qualified as not-yet-present) and `mailbridge.runsettings` (present and referenced).
- [x] **AC-03** — `quality-tiers.yml` exists at repo root and classifies every project in `OpenClaw.MailBridge.sln` (the six production projects and three test projects) with a valid tier in {T1, T2, T3, T4}. No project is listed that is absent from the solution, and no solution project is omitted.
- [x] **AC-04** — `.claude/rules/*` and `AGENTS.md` agree on coverage thresholds (line >= 85%, branch >= 75%) and on tool names (MSTest, Moq, FluentAssertions, CSharpier, `dotnet build`/`dotnet test`, `OpenClaw.MailBridge.sln`). No document states the prior 80%/90% thresholds and no document names xUnit or NSubstitute as the C# test or mocking framework.
- [x] **AC-05** — The removed TypeScript and Python harness files are gone: `.claude/rules/typescript.md`, `.claude/rules/typescript-suppressions.md`, `.claude/rules/python.md`, `.claude/rules/python-suppressions.md`, `.claude/agents/typescript-engineer.md`, `.claude/agents/python-typed-engineer.md`, `.github/agents/typescript-engineer.agent.md`, and the `.github/agents/*` REMOVE-list personas (`expert-nextjs-developer`, `expert-react-frontend-engineer`, `pytest-unit-test-coding`, `python-atomic-executor`, `python-atomic-planning`, `python-execution-only-typed`, `python-orchestrator`, `python-typed-engineer`, `tdd-red`, `tdd-green`, `tdd-refactor`). No retained harness file references a removed worker (no `python-typed-engineer` or `typescript-engineer` delegation remains in `.claude/agents/orchestrator.md` or `.github/agents/orchestrator.agent.md`).
- [x] **AC-06** — `AGENTS.md` C# commands use `dotnet build` / `dotnet test` against `OpenClaw.MailBridge.sln`. No occurrence of `msbuild TaskMaster.sln` or `vstest.console.exe` remains in `AGENTS.md` or in `.github/instructions/csharp-*.instructions.md`.
- [x] **AC-07** — The orchestrator-cited memory file `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` exists and contains the strict-handoff delegation content, so the citation at `.claude/agents/orchestrator.md:155` resolves.
- [x] **AC-08** — `docs/ci.research.md` exists with a section 1 describing the tier system, and `.claude/rules/quality-tiers.md` cites it as the tier source of truth (no dangling `docs/ci.research.md` reference).
- [x] **AC-09** — The C# QA-gate and engineer skills (`.claude/skills/csharp-qa-gate/SKILL.md`, `.claude/skills/invoke-csharp-engineer/SKILL.md`) and `.github/instructions/csharp-code-change.instructions.md` invoke global `csharpier` and do not invoke `dotnet csharpier check` / `dotnet tool run csharpier`, and do not claim `Directory.Build.props`-centralized analyzer configuration.
- [x] **AC-10** — A representative `dotnet build OpenClaw.MailBridge.sln` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (the corrected toolchain) are valid commands for this repository — the solution and runsettings paths they name resolve.


## Verification

How each AC is checked. Run from repo root. Historical `docs/features/**` is excluded from marker scans because it is provenance, not live policy.

- **AC-01** — Grep the residual marker set, excluding documentation archives:
  - `rg -n "No-COM|TaskMaster|xUnit|NSubstitute|Office\.js|taskpane|dependency-cruiser|Directory\.Build\.props|vstest\.console|msbuild TaskMaster" .claude .github AGENTS.md`
  - Expected: no matches. Any match must be in a retained file that explicitly qualifies the term as not-applicable/not-present, otherwise it is a failure.
- **AC-02** — For each path referenced by a retained rule, confirm existence with `Test-Path` (PowerShell) or `ls`; for the known-absent paths (`scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, `scripts/benchmarks/Test-BaselineProvenance.ps1`), grep the referencing document to confirm it contains an explicit "not yet present"/"absent" qualification:
  - `rg -n "pester.runsettings.psd1" .claude .github AGENTS.md` and confirm each hit is qualified.
  - `rg -n "Test-BaselineProvenance.ps1" .claude` and confirm `.claude/rules/benchmark-baselines.md` qualifies it.
  - `Test-Path mailbridge.runsettings` returns True.
- **AC-03** — Confirm `Test-Path quality-tiers.yml` is True. Parse the YAML and assert each of the nine solution projects (`OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`, `OpenClaw.MailBridge.Client`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.HostAdapter`, `OpenClaw.Core`, `OpenClaw.MailBridge.Tests`, `OpenClaw.HostAdapter.Tests`, `OpenClaw.Core.Tests`) has a tier in {T1,T2,T3,T4}, and that no listed project is absent from `OpenClaw.MailBridge.sln`.
- **AC-04** — `rg -n "85%|75%|80%|90%" .claude/rules AGENTS.md` and confirm coverage statements read 85%/75% with no 80%/90% gate remaining; `rg -n "MSTest|Moq|FluentAssertions" .claude/rules .claude/skills .claude/agents AGENTS.md` present; `rg -n "xUnit|NSubstitute" .claude .github AGENTS.md` returns no matches (subsumed by AC-01).
- **AC-05** — Confirm each removed file path is absent (`Test-Path` returns False) for the eight `.claude/` files and eleven `.github/agents/*` REMOVE-list files. Then `rg -n "python-typed-engineer|typescript-engineer" .claude/agents/orchestrator.md .github/agents/orchestrator.agent.md` returns no matches.
- **AC-06** — `rg -n "msbuild TaskMaster|vstest\.console" AGENTS.md .github/instructions` returns no matches; `rg -n "dotnet build|dotnet test|OpenClaw.MailBridge.sln" AGENTS.md` confirms the corrected commands are present.
- **AC-07** — `Test-Path .claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` returns True and the file is non-empty; `.claude/agents/orchestrator.md:155` still cites it.
- **AC-08** — `Test-Path docs/ci.research.md` returns True with a section 1; `rg -n "ci.research.md" .claude/rules/quality-tiers.md` confirms the citation resolves to the now-present file.
- **AC-09** — `rg -n "dotnet csharpier check|dotnet tool run csharpier|Directory\.Build\.props" .claude/skills/csharp-qa-gate/SKILL.md .claude/skills/invoke-csharp-engineer/SKILL.md .github/instructions/csharp-code-change.instructions.md` returns no matches; `rg -n "csharpier" ` on the same files shows the global invocation form.
- **AC-10** — Run the smoke commands and confirm they resolve against this repository:
  - `dotnet build OpenClaw.MailBridge.sln`
  - `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Success criterion for this AC is that the named solution and runsettings paths resolve (the commands are valid for this repo); a full green test run is a supporting signal, not a gate for this documentation change.


## Risks & Mitigations
- Technical or operational risks: Hand-editing `AGENTS.md` and its source instructions separately risks drift between them until the generator script exists. Mitigation: edit both in the same change and verify with AC-06; record the generator as a follow-up.
- A `quality-tiers.yml` tier assignment may be debated. Mitigation: the proposed mapping is documented and confirmable against `OpenClaw.MailBridge.sln`; AC-03 enforces completeness and validity, not a specific debated assignment.
- Mitigations and rollbacks: revert the documentation change via `git revert`; no runtime impact.

## Rollout & Follow-up
- Release/rollout steps: merge the documentation/harness correction; no deployment.
- Post-fix monitoring or clean-up tasks: open follow-ups for the generator script, the benchmark validator, the `.config/dotnet-tools.json` manifest, and a human decision on the `.github/agents/*` REVIEW-classified files.
- Links: Issue #66 (https://github.com/drmoisan/open-claw-bridge/issues/66); research `artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`.

## Execution Deviations (recorded at closeout 2026-06-08)

All in-scope Scope-checklist and AC items (AC-01..AC-10) are delivered and verified. Deviations and notes:

- Qualified-rather-than-replaced path references (as the spec permits): `pester.runsettings.psd1` in
  `.claude/rules/powershell.md`, `.github/instructions/powershell-unit-test.instructions.md`, and
  `AGENTS.md` are qualified as not-yet-present and accompanied by the real
  `Invoke-Pester -Path tests/scripts` invocation; `Test-BaselineProvenance.ps1` in
  `.claude/rules/benchmark-baselines.md` is qualified as not-yet-present (script not authored).
- `quality-tiers.yml` classifies `OpenClaw.MailBridge` with exactly one tier (T2, its managed surface).
  The Ground-truth note also associates the Outlook-COM-confined surface of that project with T3; because
  a YAML project key takes one tier, the COM-confined-surface T3 description is captured in
  `docs/ci.research.md` section 1 rather than as a second YAML entry. AC-03 requires one valid tier per
  project, which is satisfied.
- File-size: no non-exempt file exceeds 500 lines; `AGENTS.md` (1053 lines) is a Markdown documentation
  file and is exempt. No file required a split.
- OUT-OF-SCOPE finding (not delivered; recommended follow-up): `.github/agents/csharp-typed-engineer.agent.md`
  L173-175 still contains unqualified `msbuild TaskMaster.sln` / `vstest.console.exe` command blocks
  identical to those corrected elsewhere. This file is gitignored, so the canonical AC-01 scan does not
  flag it and AC-01 passes as written; the plan did not enumerate it for edit. Recommend a follow-up
  cycle to apply the same `dotnet build` / `dotnet test` correction.


## Scope Extension — Version-Control Tracking and Full Python/TypeScript Removal (operator-directed 2026-06-08, Option 1A)

During delivery it was found that the repository `.gitignore` excludes the entire agent harness from
version control: `.claude` (line 83) and `.github/*` except `.github/workflows/{ci.yml,publish.yml}`
(lines 76-80). Verified: zero tracked files under `.claude/`; `.github/agents`, `.github/instructions`,
`.github/prompts`, and `.github/skills` are untracked. Consequently the bulk of the Issue #66 harness
correction lived in untracked files invisible to a PR, to the git-diff-based feature-review, and to the
team. The operator directed **Option 1A**: bring the harness under version control AND complete the
Python/TypeScript ecosystem removal so the now-tracked harness is internally consistent (no dangling
references to removed workers/rules/skills/hooks).

This extension is the consequence of, and completes, decision 5 (remove Python residue) and decision 4
(remove TypeScript residue) across the full now-tracked surface, and it satisfies the existing AC-05
("no retained harness file references a removed worker") which previously passed only because the
referencing files were gitignored and therefore unscanned.

### Additional scope
- **`.gitignore`** — un-ignore `.claude/` and `.github/{agents,instructions,prompts,skills}`; keep
  `artifacts/` ignored; add a forward-looking ignore for `.claude/settings.local.json`.
- **Delete (Python skills)** — `.claude/skills/python-change-budget-router/`,
  `.claude/skills/python-qa-gate/`, `.claude/skills/invoke-python-engineer/`.
- **Delete (Python/TS prompts)** — `.github/prompts/orchestrate-python-work.prompt.md`,
  `.github/prompts/javascript-typescript-jest.prompt.md`.
- **Delete (Python hooks)** — `.claude/hooks/check-python-test-purity.ps1`,
  `.claude/hooks/enforce-python-batch-budget.ps1`.
- **Edit `.claude/settings.json`** — remove `Agent(python-typed-engineer)`, `Agent(typescript-engineer)`,
  the three Python `Skill(...)` allowances, the two Python hook command wirings, and the
  `python-typed-engineer`/`typescript-engineer` entries from the subagent matcher. The file must remain
  valid JSON and the remaining hooks must still resolve.
- **Edit (shared skills)** — remove Python/TypeScript references from
  `.claude/skills/policy-compliance-order/SKILL.md`, `.claude/skills/remediation-handoff-atomic-planner/SKILL.md`,
  and `.claude/skills/translate-copilot-to-claude/SKILL.md` (swap Python/TS examples for C#/PowerShell or
  remove), so no skill names a removed worker or a deleted rules file.
- **Fix residual** — `.github/agents/csharp-typed-engineer.agent.md` L173-175: replace `msbuild TaskMaster.sln`
  / `vstest.console.exe` with the `dotnet build` / `dotnet test` forms against `OpenClaw.MailBridge.sln`,
  matching the corrections already applied elsewhere.

### Additional acceptance criteria
- [x] **AC-11** — `.gitignore` no longer excludes the harness: `.claude/` and `.github/{agents,instructions,prompts,skills}`
  are tracked (`git ls-files` returns the harness files), while `artifacts/` remains ignored. `git check-ignore`
  returns no match for representative harness paths (`.claude/rules/csharp.md`, `.github/agents/orchestrator.agent.md`).
- [x] **AC-12** — The full now-tracked harness contains no genuine (unqualified) residual marker for the
  AC-01 set. The only permitted matches are explicit prohibitions ("Do not introduce xUnit"), qualified
  not-present statements, generic illustrative framework lists in CI best-practice docs, or agent-memory
  provenance. Specifically, `.github/agents/csharp-typed-engineer.agent.md` contains no `msbuild TaskMaster.sln`
  or `vstest.console.exe`.
- [x] **AC-13** — No tracked harness file references a removed Python/TypeScript worker, skill, rule, or hook:
  `rg -n "python-typed-engineer|typescript-engineer|python-qa-gate|invoke-python-engineer|python-change-budget-router|check-python-test-purity|enforce-python-batch-budget|rules/python\.md|rules/typescript\.md"`
  over the tracked `.claude/` and `.github/` trees returns no active reference (agent-memory provenance excepted).
- [x] **AC-14** — `.claude/settings.json` parses as valid JSON; it no longer lists the removed agents, Python
  skills, or Python hooks; and every hook command path it still references exists on disk.
- [x] **AC-15** — The Python/TS ecosystem files are deleted: the three Python skill directories, the two
  Python/TS prompts, and the two Python hooks are absent (`Test-Path` False).

### Verification (extension)
- **AC-11** — `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md` returns nothing;
  `git ls-files .claude | wc -l` is non-zero; `git check-ignore artifacts/orchestration/orchestrator-state.json` still matches.
- **AC-12** — the AC-01 grep over the full tree (no `--no-ignore` needed once tracked); manually confirm each
  remaining hit is in the permitted-exception set.
- **AC-13** — the AC-13 grep returns only agent-memory provenance hits.
- **AC-14** — `pwsh -Command "Get-Content .claude/settings.json -Raw | ConvertFrom-Json"` succeeds; grep confirms removals; each remaining hook `command` path resolves with `Test-Path`.
- **AC-15** — `Test-Path` False for each deleted skill dir, prompt, and hook.
