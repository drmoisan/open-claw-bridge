# Phase 0 — Policy Reads Evidence

- Timestamp: 2026-04-27T09-30
- Feature: docs/features/active/2026-04-26-install-stale-hostadapter-detection
- Plan: docs/features/active/2026-04-26-install-stale-hostadapter-detection/split-plan.2026-04-27T09-30.md

## Policy Order

1. .claude/rules/general-code-change.md
2. .claude/rules/general-unit-test.md
3. .claude/rules/powershell.md

## Files Read

- .claude/rules/general-code-change.md
- .claude/rules/general-unit-test.md
- .claude/rules/powershell.md

## Confirmation — File Size Limit

`general-code-change.md` line 40 states: "No production code, test code, or reusable script file may exceed 500 lines." Markdown documentation files are exempt; tests are not. The current `tests/scripts/Install.Helpers.Tests.ps1` exceeds the cap and must be split.

## Confirmation — Mandatory Toolchain Order

Per `general-code-change.md` lines 27-36 and `powershell.md` "Toolchain" section:

1. Format — `mcp__drmCopilotExtension__run_poshqc_format`
2. Lint — `mcp__drmCopilotExtension__run_poshqc_analyze`
3. Type-check — N/A for PowerShell
4. Test — `mcp__drmCopilotExtension__run_poshqc_test`

Restart from step 1 on any failure or any file modification by an earlier step.

## Confirmation — Mock Parameter-Name Parity Rule

Per `powershell.md` Mocking Rules: "mock signatures must match production named parameters exactly". The Compose tests use a global `function global:docker { ... }` shim and `Mock` calls keyed by named parameters; the mechanical split must preserve these byte-for-byte so parameter parity is unchanged.

## Confirmation — Pester Test Standards

Per `general-unit-test.md` and `powershell.md`:
- Tests organized by Describe/Context/It; the new sibling file mirrors the source file's outer Describe with its `BeforeAll` / `AfterAll` import-module pair.
- No temporary files, no external dependencies introduced.
- No coverage regression: this split is mechanical and adds no production code.
