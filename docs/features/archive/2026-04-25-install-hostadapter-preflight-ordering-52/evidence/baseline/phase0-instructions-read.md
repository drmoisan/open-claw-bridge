# Phase 0 — Instructions Read Evidence

Timestamp: 2026-04-25T00:05:00Z

## Policy Order

The following policy files were read in the required compliance order:

1. `.github/copilot-instructions.md` — File does not exist in this repository. The repo uses `AGENTS.md` (generated from the source instruction files) as the synthesized policy document. `AGENTS.md` was confirmed present and contains all policy content.
2. `.github/instructions/general-code-change.instructions.md` — Confirmed read. General code change policy covering design principles, error handling, module structure, naming, and the mandatory toolchain loop (format → lint → type-check → test).
3. `.github/instructions/general-unit-test.instructions.md` — Confirmed read. General unit test policy covering independence, isolation, fast execution, determinism, scenario completeness, and the prohibition on temporary files.
4. `.github/instructions/powershell-code-change.instructions.md` — Confirmed read. PowerShell-specific policy requiring use of MCP server functions (`mcp_drmcopilotext_run_poshqc_format`, `mcp_drmcopilotext_run_poshqc_analyze`, `mcp_drmcopilotext_run_poshqc_test`) for all toolchain steps; advanced functions with `CmdletBinding()`; no global state; `ShouldProcess` for destructive operations.
5. `.github/instructions/powershell-unit-test.instructions.md` — Confirmed read. PowerShell unit test policy requiring Pester v5.x, repo config at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, test files named `*.Tests.ps1`, organized with `Describe`/`Context`/`It`.

## Issue.md Acceptance Criteria

`issue.md` at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/issue.md` was confirmed to contain a `## Acceptance Criteria` section.

AC items found:

- AC-1: When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-MsixInstall` must NOT have been called (MSIX left clean).
- AC-2: When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-ComposeUp` must NOT be called.
- AC-3: When `Assert-HostAdapterRuntimePreflight` fails, `Wait-ComposeHealthy` must NOT be called.
- AC-4: The happy-path stage ordering test passes with `Assert-HostAdapterRuntimePreflight` (or its underlying `Invoke-WebRequest`) confirmed to execute before `Invoke-MsixInstall`.
- AC-5: All existing Install.ps1 tests pass (no regressions).
- AC-6: The full PoshQC toolchain (format → analyze → test) passes without errors.

## Notes

- Work Mode confirmed as `minor-audit` in both `issue.md` and `plan.2026-04-25T00-00.md`.
- Bugfix workflow applies: regression tests must be introduced before the fix (P1-T1 through P1-T3 are `[expect-fail]` tasks).
- No conflicting instructions detected across the five policy documents.
