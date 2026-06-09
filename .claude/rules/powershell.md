---
paths:
  - "**/*.ps1"
  - "**/*.psm1"
  - "**/*.psd1"
description: PowerShell-specific toolchain and coding standards.
---

# PowerShell Code Standards

This rule file summarizes the PowerShell-specific policies for this repository.

## Toolchain

1. **Formatting — Invoke-Formatter**: Format all PowerShell files via PoshQC. MCP command: `mcp__drm-copilot__run_poshqc_format`
2. **Linting — PSScriptAnalyzer**: Run PoshQC analyzer with repo settings. MCP command: `mcp__drm-copilot__run_poshqc_analyze`. Optional autofix: `mcp__drm-copilot__run_poshqc_analyze_autofix`
3. **Type checking**: Not applicable for PowerShell; skip to testing.
4. **Testing — Pester (v5.x)**: Run tests via MCP. MCP command: `mcp__drm-copilot__run_poshqc_test`. CI runs Pester directly as `Invoke-Pester -Path tests/scripts -Output Detailed -CI` (see `.github/workflows/ci.yml`). Note: a PoshQC repo settings file at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` is not yet present in this repository; do not reference it as an existing config.

Run the toolchain in order: format → analyze → test. Restart from step 1 if any step fails or changes files. Use the MCP server functions; do not substitute VS Code task wrappers.

## Compatibility

- All scripts must be compatible with **PowerShell 7+** (enforced via PSScriptAnalyzer settings).

## Coding Standards

- Prefer **advanced functions** with `CmdletBinding()` and named parameters.
- Add `[Parameter(Mandatory = $true)]` and validation attributes where appropriate.
- Implement **ShouldProcess/SupportsShouldProcess** for state-changing actions.
- Avoid global state and mutable script-scoped variables; pass data explicitly.
- Avoid `Invoke-Expression`, plaintext secrets, and hard-coded credentials/paths.
- Use `Write-Error`/`throw` for failures; avoid silent catch-alls.
- Use approved verbs and descriptive nouns for function names (PSScriptAnalyzer enforces this).
- Keep scripts cohesive and under 500 lines.

## Change Budget

- Direct-mode overall scope: up to 2 production PowerShell files (plus corresponding tests). Requests exceeding this must be routed to `powershell-orchestrator` per `powershell-change-budget-router`.
- Per-batch cap in all modes: at most 3 production files and 3 test files unless an explicit override has been approved.
- If a batch would exceed the cap, split the work into smaller batches.

## Design Seams (Minimal DI)

Introduce the smallest seam that enables reliable mocking. Apply these options in order:

1. **Wrapper function seam (preferred)** — extract external executable calls into a wrapper function:
   - Signature: `Invoke-<Tool>Exe -<Tool>Args <string[]>` (for example `Invoke-GitExe -GitArgs <string[]>`).
   - The wrapper accepts a single array parameter and splats into the executable: `git @GitArgs 2>&1`.
   - Parameter names must not be `Args` (automatic variable collision). Use `GitArgs`, `ToolArgs`, or `Arguments`.
2. **Injectable delegate / ScriptBlock seam** — only when a wrapper is insufficient, add a narrowly-scoped optional delegate or `ScriptBlock` parameter with a safe default. Do not introduce generic runner frameworks.
3. **Adapter seams for non-executable boundaries** — for filesystem, environment, or clock dependencies, introduce tiny helpers or narrow injectable parameters rather than threading raw I/O through domain logic.

## Testing Standards

- Use **Pester** (v5.x) as the test framework.
- Organize tests to mirror code structure (e.g., `tests/scripts/dev-tools/ScriptName.Tests.ps1`).
- Name test files `*.Tests.ps1`.
- Use `Describe`/`Context`/`It` blocks; one behavior per `It`.
- Write focused tests exercising a single function or behavior.
- Mock sparingly; prefer real code paths.
- No external dependencies in unit tests.
- Line coverage must remain >= 85% across all tiers (T1–T4) per `.claude/rules/quality-tiers.md`.
- Branch coverage must remain >= 75% across all tiers (T1–T4).
- Coverage regression on changed lines is a blocking finding.

### Deterministic Test Requirements

Tests must not depend on:

- network access,
- mutable machine PATH or profile state,
- implicit working-directory assumptions,
- external services or live executables.

Tests must produce identical results in Terminal and the VS Code Test Explorer. Assume a different PATH, current working directory, profile, and host when tests are run from Test Explorer; do not rely on ambient environment resolution.

### Mocking Rules

1. **External executable mocking** — never mock `git`, `gh`, `actionlint`, or other executables directly. Mock the wrapper function (for example `Invoke-GitExe`) instead.
2. **Mock signature parity** — mock signatures must match production named parameters exactly. Example:
   - production: `Invoke-GitExe -GitArgs $gitArgs`
   - test mock: `param([string[]]$GitArgs)`
3. **Mock registration order** — register mocks before the code under test can resolve commands, so Test Explorer parity is preserved.
4. **AST/ScriptBlock import order** — when importing script functions via AST or `ScriptBlock` patterns:
   - dot-source the returned `ScriptBlock` in the test scope,
   - import dependencies in the correct order,
   - import wrapper seams before mocking them when executable calls exist.

## Prohibited Behaviors

- Broad refactors across unrelated scripts or modules.
- Introducing generic process-runner frameworks to replace the wrapper seam pattern.
- Creating PSScriptAnalyzer debt and deferring cleanup.
- Weakening assertions merely to make tests pass.
- Adding sleeps, retries, or timing hacks to stabilize flaky tests.
- Claiming success without running the required toolchain.
