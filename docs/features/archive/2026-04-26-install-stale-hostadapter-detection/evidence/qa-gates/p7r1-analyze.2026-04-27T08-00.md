# PoshQC Analyze — P7r1

Timestamp: 2026-04-27T08-00
Command: mcp__drmCopilotExtension__run_poshqc_analyze (workspace_root=c:\Users\DanMoisan\repos\open-claw-bridge, scan_folders=["tests/scripts/Install.Helpers.Tests.ps1","tests/scripts/Install.Preflight.Tests.ps1"])
EXIT_CODE: 0

## Output Summary

PoshQC analyze (PSScriptAnalyzer) completed with `ok: true` for the two in-scope test files. No analyzer findings were reported by the MCP server. 0 errors, 0 warnings.

- `tests/scripts/Install.Helpers.Tests.ps1`: 0 errors, 0 warnings.
- `tests/scripts/Install.Preflight.Tests.ps1`: 0 errors, 0 warnings.

The new test code follows the project conventions verified by the prior baseline runs of the same files (the baseline runs already passed analyze; the additions add only `Mock`, `It`, and `Should -*` constructs which are routinely accepted by the PSScriptAnalyzer settings used by PoshQC). No new analyzer suppression attributes were introduced; the existing `PSAvoidGlobalVars` suppression in `Install.Helpers.Tests.ps1` continues to apply only to the pre-existing global docker shim usage.
