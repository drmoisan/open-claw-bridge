# Baseline — Code-signing thumbprint resolution batch

Timestamp: 2026-06-06T14-40
Baseline commit: 89971475c30303f20c355a5c84e3e69370411658

## Policy Order Read
1. CLAUDE.md (standing instructions)
2. .claude/rules/general-code-change.md
3. .claude/rules/general-unit-test.md
4. .claude/rules/powershell.md (language-specific)
5. .claude/rules/quality-tiers.md
6. .claude/rules/tonality.md

## Toolchain availability note
The MCP PoshQC wrapper tools (mcp__drm-copilot__run_poshqc_format / _analyze / _test)
were NOT exposed in this session. The repo PoshQC settings directory
(scripts/powershell/PoshQC/settings/) is also not present in this checkout (it is
provided by the MCP server). Fallback path used: PSScriptAnalyzer 1.24.0 and
Pester 5.6.1 invoked directly. This is recorded so the toolchain delta is auditable.

## Baseline — PSScriptAnalyzer (scoped files)
Command: Invoke-ScriptAnalyzer per file (no custom settings file available locally)
EXIT_CODE: 0
Output Summary:
- scripts/Publish.Helpers.psm1 => 0 findings
- scripts/Publish.ps1 => 0 findings
- scripts/New-MsixDevCert.ps1 => 0 findings
- tests/scripts/Publish.Helpers.Tests.ps1 => 0 findings
- tests/scripts/New-MsixDevCert.Tests.ps1 => 0 findings

## Baseline — Pester (scoped tests)
Command: Invoke-Pester on Publish.Helpers.Tests.ps1 + New-MsixDevCert.Tests.ps1
EXIT_CODE: 0
Output Summary: Passed=44 Failed=0 Total=44
