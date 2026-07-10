# PowerShell Analyze Baseline — Issue #135

Timestamp: 2026-07-07T15-31

Command: `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = `C:\Users\DanMoisan\repos\open-claw-bridge`)

EXIT_CODE: 0

Output Summary: PASS (tool-level success signal). Tool response: `{"ok":true,"tool":"run_poshqc_analyze","workspace_root":"C:\\Users\\DanMoisan\\repos\\open-claw-bridge","summary":"Ran bundled PoshQC analyze against 'C:\\Users\\DanMoisan\\repos\\open-claw-bridge'."}`. The MCP tool interface returns only `ok`/`summary` fields and does not expose a per-severity diagnostic breakdown in its response payload. `ok: true` indicates the analyzer run completed without a tool-level failure; no per-severity error/warning/information counts were returned by this interface for the pre-fix baseline.
