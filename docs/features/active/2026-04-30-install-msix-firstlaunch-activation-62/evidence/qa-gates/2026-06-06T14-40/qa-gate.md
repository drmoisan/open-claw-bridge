# QA Gate — Code-signing thumbprint resolution batch

Timestamp: 2026-06-06T14-40

## Toolchain availability
The MCP PoshQC wrappers (mcp__drm-copilot__run_poshqc_format / _analyze / _test) were
NOT exposed in this session, and the repo PoshQC settings directory
(scripts/powershell/PoshQC/settings/) is not present in this checkout (provided by the
MCP server). Fallback toolchain used: PSScriptAnalyzer 1.24.0 and Pester 5.6.1 invoked
directly. The committed repo style is produced by the MCP PoshQC formatter, whose
pipeline-indentation differs from stock Invoke-Formatter; pre-existing lines were left
untouched to avoid unrelated churn (see format note below).

## Scope (files touched)
- installer/Package.appxmanifest (XML)
- src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj (XML)
- scripts/Publish.Helpers.psm1 (production)
- scripts/Publish.ps1 (production)
- scripts/New-MsixDevCert.ps1 (production)
- tests/scripts/Publish.Helpers.Tests.ps1 (test)
- tests/scripts/Publish.Tests.ps1 (test, required regression fix)

## Stage 1 — Format
Command: Invoke-Formatter -ScriptDefinition on each touched .ps1/.psm1
EXIT_CODE: 0
Output Summary: All added code format-clean. The only residual format difference is
pre-existing pipeline indentation at scripts/Publish.Helpers.psm1 lines 42-43 inside
Find-WindowsSdkTool, which is byte-identical to HEAD and was not authored by this change.
Reformatting it would conflict with the committed MCP PoshQC style. Diff confirmed to be
exactly those two pre-existing lines only.

## Stage 2 — Analyze (PSScriptAnalyzer)
Command: Invoke-ScriptAnalyzer -Path <each scoped file>
EXIT_CODE: 0
Output Summary: 0 findings across all touched files. Baseline was 0; delta = 0.

## Stage 3 — Test (Pester)
Command: Invoke-Pester on tests/scripts (full PowerShell suite)
EXIT_CODE: 0
Output Summary: Passed=239 Failed=0 Skipped=0 Total=239. Baseline full-suite count was
236 (44 in the two scoped files). Delta: +6 new Resolve-CertThumbprint cases in
Publish.Helpers.Tests.ps1, +3 net new orchestrator cases plus 1 restructured-but-preserved
validation case in Publish.Tests.ps1. No failing tests; failing-tests delta = 0.

## Stage 4 — Coverage
Command: Invoke-Pester with CodeCoverage on the three production scripts
EXIT_CODE: 0
Output Summary: Line/command coverage = 88.96% (274/308 commands), >= 85% threshold.
Resolve-CertThumbprint is fully covered. Uncovered lines are all pre-existing host-bound
entry points (New-MsixDevCert main block / elevation / Import-Certificate paths,
ShouldProcess-false branches) plus the single deliberate Invoke-DotnetExe executable
boundary line (scripts/Publish.Helpers.psm1:500), which is intentionally the thinnest I/O
wiring and is always mocked in unit tests per the design-seam rule. No regression on
changed lines.

## Delta summary (zero-regression gate)
- PSScriptAnalyzer delta: 0 new findings.
- Failing-tests delta: 0.
- Coverage delta: scoped coverage 88.96% (>= 85%); no regression on changed lines.

## Verification outcomes
1. dotnet user-secrets list shows Signing:CertThumbprint = 6461584F8CB3A2A384F575918E17D4B4AD8EE733 (PASS).
2. Resolve-CertThumbprint -ProjectPath <abs> -EnvThumbprint '' returned
   6461584F8CB3A2A384F575918E17D4B4AD8EE733 from the real user secret (PASS).
3. Get-ChildItem Cert:\CurrentUser\My\6461584F8CB3A2A384F575918E17D4B4AD8EE733 resolved;
   HasPrivateKey=True; NotAfter=2027-07-06; Subject exactly matches manifest Publisher (PASS).
4. Publish.ps1 -Version 9.9.9.9 -WhatIf (no -CertThumbprint, no -SkipSign) did NOT throw the
   ambiguous-signing error and proceeded through Stage 0a resolution, the gate, and into the
   MSIX/sign stages — proving the resolve-to-sign wiring. Under -WhatIf the dotnet publish /
   makeappx / signtool calls were short-circuited by ShouldProcess, so a full signed package
   was NOT produced in this environment. Windows SDK tools (makeappx/makepri/signtool 10.0.26100.0)
   are present, but a real signed publish would run four dotnet builds and was not executed here.
   Honest status: thumbprint-resolution-through-sign path verified; full signed publish NOT run.
