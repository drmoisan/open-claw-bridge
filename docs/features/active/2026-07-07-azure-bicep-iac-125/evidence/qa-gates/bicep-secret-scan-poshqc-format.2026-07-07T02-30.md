# PoshQC Format — Bicep Secret-Scan Script + Test (P5-T3)

- Timestamp: 2026-07-07T02-30
- Command: `mcp__drm-copilot__run_poshqc_format` (scan_folders: `["scripts", "tests/scripts"]`)
- EXIT_CODE: 0
- Output Summary: `ok: true`. Two format runs were performed: the first pass (before an em-dash-to-hyphen text fix and a PSUseSingularNouns suppression attribute were added) reported no reformatting needed; after the content fix (unrelated to formatting), the loop was restarted from format per policy and the second pass also reported no changes (`git status --porcelain` shows both new files remain untouched by the formatter — file content matches what was authored). Clean pass on the final run.
