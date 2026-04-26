Timestamp: 2026-04-11T15:29:00-04:00
Command: mcp_drmcopilotext_run_poshqc_analyze
WorkspaceRoot: c:\Users\DanMoisan\repos\open-claw-bridge
TargetFiles:
- scripts/build-msix.ps1
- scripts/New-MsixDevCert.ps1
- tests/scripts/build-msix.Tests.ps1
- tests/scripts/New-MsixDevCert.Tests.ps1
EXIT_CODE: 1
Output Summary:
- MCP analyzer invocation at repository-root scope exited non-zero with `PSScriptAnalyzer reported 7 issue(s).`
- The MCP wrapper did not accept a multi-path `ScanFolders` input for the exact four-file scope.
- Supplemental verification used the same bundled `PoshQC` module and settings with the four targeted files as the effective file list.
- Findings for `scripts/build-msix.ps1`: none reported.
- Findings for `scripts/New-MsixDevCert.ps1`: none reported.
- Findings for `tests/scripts/build-msix.Tests.ps1`: none reported.
- Findings for `tests/scripts/New-MsixDevCert.Tests.ps1`: none reported.
