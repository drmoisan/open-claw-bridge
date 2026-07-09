# Baseline — PoshQC Analyze (Cycle 2, Pre-Fix)

- Timestamp: 2026-07-09T19-13
- Command: `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root, pre-fix repository state)
- EXIT_CODE: 0
- Output Summary: PASS (tool-level success signal). Tool response: `{"ok":true,"tool":"run_poshqc_analyze","workspace_root":"C:\\Users\\DanMoisan\\repos\\open-claw-bridge","summary":"Ran bundled PoshQC analyze against the workspace."}`. Consistent with cycle 1 precedent (`poshqc-analyze.2026-07-07T15-31.md`), this MCP tool interface returns only `ok`/`summary` fields and does not expose a per-severity diagnostic breakdown. `ok: true` indicates the analyzer run completed without a tool-level failure; treated as 0 errors for the pre-fix baseline.
