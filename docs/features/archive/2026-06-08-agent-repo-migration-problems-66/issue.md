# agent-repo-migration-problems (Issue #66)

- Date captured: 2026-06-08
- Author: drmoisan
- Status: Promoted -> docs/features/active/agent-repo-migration-problems/ (Issue #66)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #66
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/66
- Last Updated: 2026-06-08
- Work Mode: full-bug

## Summary

The agentic Claude/Copilot harness (`.claude/`, `.github/agents`, `.github/instructions`, `AGENTS.md`) was copied from another repository ("drm-copilot" / "TaskMaster", a No-COM Python/TypeScript/.NET project) and was not adapted to this repository. Many rules, agent definitions, skills, and instructions select the wrong tools (xUnit/NSubstitute instead of MSTest/Moq, a "No-COM" architecture that bans the COM interop this repo depends on) and reference files, solutions, and tooling that do not exist here (`TaskMaster.sln`, `quality-tiers.yml`, `Directory.Build.props`, `docs/ci.research.md`, the PoshQC settings file, and others). Agents acting on these documents receive incorrect guidance, and tier-based CI gates cannot function.

## Environment

- OS/version: Windows (repository is a Windows-first .NET 10 solution, `OpenClaw.MailBridge.sln`).
- Python version: n/a. This repository is .NET 10 (`global.json` `10.0.201`) + PowerShell + Outlook COM. The imported harness assumes Python/TypeScript and a No-COM .NET stack in many places.
- Command/flags used: agent startup reads `CLAUDE.md`/`AGENTS.md`/`.claude/rules/*`; review and QA-gate skills invoke the C# and PowerShell toolchains; promotion/orchestration skills invoke MCP tools.
- Data source or fixture: `.claude/` (rules, agents, skills, hooks), `.github/` (agents, instructions, skills), `AGENTS.md`, `quality-tiers.yml` (absent).

## Steps to Reproduce

1. Open any agent that reads the migrated policy set (for example the C# engineer/QA path or the orchestrator).
2. Have it consult `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md` (pre-fix), `.claude/rules/quality-tiers.md`, the `csharp-qa-gate` / `invoke-csharp-engineer` skills, or `AGENTS.md`.
3. Observe instructions that name xUnit/NSubstitute, a No-COM architecture, `TaskMaster.sln`, `Directory.Build.props`, `quality-tiers.yml`, `docs/ci.research.md`, or `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`.
4. Attempt to follow them against this repository's actual stack (MSTest/Moq/FluentAssertions, COM bridge, `dotnet build`/`dotnet test`, `mailbridge.runsettings`).

## Expected Behavior

The migrated agent harness describes this repository's real stack and references only files and tooling that exist here:

- C# tooling: MSTest + Moq + FluentAssertions; CSharpier; `dotnet build`/`dotnet test` against `OpenClaw.MailBridge.sln`; coverage via `mailbridge.runsettings`.
- Architecture: a COM-based bridge (`OpenClaw.MailBridge`) with the real six-project graph; Outlook COM is allowed and confined, not banned.
- All referenced config/tooling paths exist, or the rule states that they are not yet present.

## Actual Behavior

Verified conflicts (live config only; historical `docs/features/**` artifacts are excluded):

**Already fixed in this thread:**

- `.claude/rules/csharp.md` — was "No-COM .NET foundation: xUnit, NSubstitute"; claimed a centralized analyzer stack (Meziantou, SonarAnalyzer, Roslynator, AsyncFixer, SecurityCodeScan, BannedApiAnalyzers) in `Directory.Build.props`/`Directory.Packages.props`, plus `BannedSymbols.txt`, Stryker.NET, CsCheck, and Verify.Xunit. None of those exist here. **Rewritten** to MSTest/Moq/FluentAssertions, .NET 10, COM-confined, real `dotnet`/CSharpier commands, with an explicit note of absent infrastructure.
- `.claude/rules/architecture-boundaries.md` — was "No-COM architecture": banned `Microsoft.Office.Interop.Outlook`, required Office.js/Graph-only data access, referenced TypeScript `dependency-cruiser`, `src/taskpane/`, and `TaskMaster.Domain`/`TaskMaster.Application`. **Rewritten** to the real acyclic project-reference graph and COM-confinement rules.

**Remaining — `.claude/rules`:**

- `.claude/rules/quality-tiers.md` — tier examples are the other repo's domain: "No-COM architecture", `SpamBayes`/`Triage` classifiers, `TaskMaster.Domain`/`TaskMaster.Application`, Outlook task pane UI, Office.js/Graph SDK wrappers (lines 13–15, 46). Declares `docs/ci.research.md` as the tier source of truth (**missing**) and requires `quality-tiers.yml` at repo root (**missing**).
- `.claude/rules/typescript.md` — an entire TypeScript standards file; there is no TypeScript in this repository.
- `.claude/rules/general-code-change.md` and `general-unit-test.md` — multi-language assumptions (Black/Ruff/Pyright, Prettier/ESLint, `fast-check`/`hypothesis`, `dependency-cruiser`, `oasdiff`) and references to `quality-tiers.yml`.
- `.claude/rules/powershell.md` — references `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` (**missing**). The `mcp__drm-copilot__run_poshqc_*` MCP tools are available, but the repo-side PoshQC settings path they cite is absent.
- `.claude/rules/benchmark-baselines.md` and `ci-workflows.md` — reference `scripts/benchmarks/Test-BaselineProvenance.ps1` (**missing**) and issues `#26`/`#30` whose provenance should be confirmed against this repo's history.

**Remaining — agent/skill harness:**

- `.claude/agents/csharp-typed-engineer.md`, `.claude/skills/csharp-qa-gate/SKILL.md`, `.claude/skills/invoke-csharp-engineer/SKILL.md` — specify the "CSharpier → .NET Analyzers → Nullable → **xUnit**" toolchain, **NSubstitute** mocks, `dotnet csharpier check .`, and `Directory.Build.props`-centralized analyzer config. This repo uses MSTest/Moq/FluentAssertions and has no `Directory.Build.props`.
- `.config/dotnet-tools.json` is **missing**, so `dotnet csharpier`/`dotnet tool run csharpier` (used by the C# skills) will not resolve; CSharpier is only available as a global tool.
- `.github/agents/` contains a wholesale-imported set including Python/React/Next.js agents (`expert-nextjs-developer`, `expert-react-frontend-engineer`, `pytest-unit-test-coding`, `python-*`) irrelevant to a C#/PowerShell repository.

**Remaining — `AGENTS.md` and `.github/instructions` (generated policy):**

- `AGENTS.md` C# commands reference `msbuild TaskMaster.sln …` (lines 445–446, 454–455, 628–629) and `vstest.console.exe` (line 630). The solution is `OpenClaw.MailBridge.sln` and the repo builds/tests with `dotnet build`/`dotnet test`. `AGENTS.md` is generated from `.github/instructions/csharp-*.instructions.md`, so the source instructions must be corrected and `AGENTS.md` regenerated.
- Two divergent policy systems now coexist: `.github/instructions/*` → `AGENTS.md` (coverage line ≥80% / new ≥90%, MSTest, `TaskMaster.sln`) versus `.claude/rules/*` (coverage line ≥85% / branch ≥75%, references `quality-tiers.yml`). They disagree on coverage thresholds and tooling names.

**Missing referenced paths (confirmed absent):** `quality-tiers.yml`, `docs/ci.research.md`, `scripts/powershell/PoshQC/` (and `…/settings/pester.runsettings.psd1`), `.config/dotnet-tools.json`, `tests/coverlet.runsettings` (actual file is `mailbridge.runsettings`), `scripts/benchmarks/Test-BaselineProvenance.ps1`, `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`.

## Logs / Screenshots

- [x] Attached minimal evidence (verified quotes)
- Snippet:
  - `.claude/rules/csharp.md:10` (pre-fix): "It targets the No-COM .NET foundation: xUnit, NSubstitute, FluentAssertions …"
  - `.claude/skills/csharp-qa-gate/SKILL.md:3`: "Executes the full CSharpier -> .NET Analyzers -> Nullable Analysis -> xUnit toolchain …"
  - `.claude/rules/quality-tiers.md:14`: "Examples: `TaskMaster.Domain`, `TaskMaster.Application`, mail-item DTOs …"
  - `AGENTS.md:445`: "`msbuild TaskMaster.sln /t:Build …`"
  - `.claude/rules/powershell.md:18`: "Use repo config at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`." (path missing)

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

Agents that follow the migrated policy select the wrong test framework and mocking library, attempt to honor a No-COM architecture that contradicts the product's core COM bridge, invoke commands against a nonexistent solution/tooling, and rely on a tier-classification gate (`quality-tiers.yml` / `docs/ci.research.md`) that cannot run. The product runtime is unaffected; the defect is in the development/agent harness and CI policy.

## Suspected Cause / Notes

The `.claude/` and `.github/` agent system was copied from the "drm-copilot" / "TaskMaster" repository (a No-COM, Python + TypeScript + .NET project) without per-file adaptation. The provenance is visible in residual terms: "No-COM", `TaskMaster.*`, SpamBayes/Triage, Office.js/taskpane, xUnit/NSubstitute, `dependency-cruiser`, and references to that repo's CI research and PoshQC layout. Files to inspect are listed under Actual Behavior. Note that `AGENTS.md` is generated — fix `.github/instructions/*.instructions.md` and rerun `scripts/dev-tools/sync-agents-from-instructions.ps1` rather than editing `AGENTS.md` directly.

## Proposed Fix / Validation Ideas

- [ ] Unit coverage areas
  - Decide the single source of truth for policy (the `.claude/rules/*` set vs the `.github/instructions/*` → `AGENTS.md` set) and reconcile coverage thresholds and tool names between them.
  - Rewrite `.claude/rules/quality-tiers.md` tier examples to this repo's projects (`OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`, `OpenClaw.HostAdapter`, `OpenClaw.Core`, etc.); fix or remove the `docs/ci.research.md` source-of-truth reference.
  - Create `quality-tiers.yml` classifying the six C# projects (and PowerShell scripts), or amend the rules that mandate it until it exists.
  - Remove or repurpose `.claude/rules/typescript.md` (no TypeScript in this repo).
  - Update `.claude/rules/general-code-change.md` and `general-unit-test.md` to the C#/PowerShell toolchains actually used; drop Python/TS-only tooling.
  - Update `.claude/rules/powershell.md` to point at a PoshQC settings path that exists, or add `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`; confirm the `mcp__drm-copilot__*` PoshQC tools resolve in this environment.
  - Update the C# harness (`.claude/agents/csharp-typed-engineer.md`, `.claude/skills/csharp-qa-gate/SKILL.md`, `.claude/skills/invoke-csharp-engineer/SKILL.md`) from xUnit/NSubstitute/`Directory.Build.props`/`dotnet csharpier check` to MSTest/Moq/FluentAssertions/global CSharpier/`dotnet build`/`dotnet test`.
  - Fix `.github/instructions/csharp-*.instructions.md` (replace `TaskMaster.sln`/`vstest.console.exe` with `OpenClaw.MailBridge.sln` and `dotnet build`/`dotnet test`), then regenerate `AGENTS.md`.
  - Triage the imported `.github/agents/*` set; remove agents for stacks this repo does not use (Python/React/Next.js).
  - Confirm or remove `scripts/benchmarks/Test-BaselineProvenance.ps1` references in `benchmark-baselines.md`/`ci-workflows.md`; verify issue/PR numbers (#26/#30) belong to this repo.
  - Add `.config/dotnet-tools.json` (csharpier) or change skills to call global CSharpier.
  - Reconcile coverage runsettings references (`tests/coverlet.runsettings` → `mailbridge.runsettings`).
  - Add the missing orchestrator memory file `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md` or remove the startup citation.
- [ ] Integration scenario to retest
  - Run a representative C# change through the QA-gate skill and confirm it builds/tests with the corrected toolchain and `OpenClaw.MailBridge.sln`.
  - Run a PowerShell change through the PoshQC MCP toolchain and confirm settings resolve.
- [ ] Manual verification notes
  - Re-scan `.claude/`, `.github/`, and `AGENTS.md` for residual markers: `No-COM`, `TaskMaster`, `xunit`, `nsubstitute`, `Office.js`, `taskpane`, `dependency-cruiser`, `Directory.Build.props`, `quality-tiers.yml`, `ci.research.md`.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch