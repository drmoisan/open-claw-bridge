Timestamp: 2026-04-11T15:27:00-04:00
Command: mcp_drmcopilotext_run_poshqc_format
WorkspaceRoot: c:\Users\DanMoisan\repos\open-claw-bridge
TargetFiles:
- scripts/build-msix.ps1
- scripts/New-MsixDevCert.ps1
- tests/scripts/build-msix.Tests.ps1
- tests/scripts/New-MsixDevCert.Tests.ps1
ExecutionScope: Repository root via MCP formatter because the tool wrapper did not accept multi-path ScanFolders input.
EXIT_CODE: 0
Output Summary:
- Ran bundled PoshQC format against `c:\Users\DanMoisan\repos\open-claw-bridge`.
- The targeted files were included in the requested baseline formatting scope.
- `git diff --name-only -- scripts/build-msix.ps1 scripts/New-MsixDevCert.ps1 tests/scripts/build-msix.Tests.ps1 tests/scripts/New-MsixDevCert.Tests.ps1` returned no changes immediately after the formatter run.
