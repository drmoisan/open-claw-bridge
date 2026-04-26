Timestamp: 2026-04-25T00:00Z
Command: mcp_drmcopilotext_run_poshqc_test
EXIT_CODE: 1
Failure: does not install MSIX when the HostAdapter status probe throws on unreachable endpoint — Expected $true to be $false because Invoke-MsixInstall was found in the call log (MSIX is installed before the preflight guard executes in the current production code). Correctly fails against pre-fix production code.
