# Baseline — PoshQC Format (pre-change)

Timestamp: 2026-07-12T09-05

Command: `mcp__drm-copilot__run_poshqc_format` (scan_folders: `scripts/Install.ps1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`)

EXIT_CODE: 0

Output Summary: Tool returned `ok:true` ("Ran bundled PoshQC format ... with 3 selected scan folder(s)."). `git status --short` immediately after the run shows no modifications to any of the three scanned files, confirming the pre-change state is already format-clean (0 files changed by the formatter).
