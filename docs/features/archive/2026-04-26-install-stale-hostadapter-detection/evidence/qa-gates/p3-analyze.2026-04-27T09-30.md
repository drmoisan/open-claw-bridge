# P3-T2 — PoshQC analyzer

- Timestamp: 2026-04-27T09-30
- Command: `mcp__drmCopilotExtension__run_poshqc_analyze` with `workspace_root=c:\Users\DanMoisan\repos\open-claw-bridge` and `scan_folders=["tests/scripts/Install.Helpers.Tests.ps1", "tests/scripts/Install.Helpers.Compose.Tests.ps1"]`
- EXIT_CODE: 0

## Output Summary

MCP returned `ok: true`, summary: "Ran bundled PoshQC analyze against 'c:\\Users\\DanMoisan\\repos\\open-claw-bridge' with 2 selected scan folder(s)."

Analyzer reported no errors (MCP wrapper returns `ok: true` and a non-error summary). No autofix activity occurred against the two target files: post-analyze `(Get-Content).Count` is `374` (Install.Helpers.Tests.ps1) and `179` (Install.Helpers.Compose.Tests.ps1), identical to the post-format sizes.

The two target files retain the same `[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidGlobalVars', ...)]` declaration as the original source, so analyzer suppressions for the global `docker` shim are preserved verbatim.

Acceptance: analyzer exits 0, no new findings against the target files vs baseline, no loop restart required.
