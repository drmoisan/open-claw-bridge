# PoshQC Analyze — Bicep Secret-Scan Script + Test (P5-T4)

- Timestamp: 2026-07-07T02-32
- Command: `mcp__drm-copilot__run_poshqc_analyze` (scan_folders: `["scripts", "tests/scripts"]`)
- EXIT_CODE: 0
- Output Summary: **0 errors.** First analyze run against the two new files surfaced 2 Warning-severity findings (`PSUseBOMForUnicodeEncodedFile` — non-ASCII em-dash characters in comments; `PSUseSingularNouns` — the plan-mandated function name `Test-OpenClawBicepParameterSecrets`). Restarted the loop from format per policy after fixing the source: replaced em-dashes with ASCII hyphens (removing the encoding trigger) and added a `[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = ...)]` on the function, consistent with the repo's existing precedent (`scripts/powershell/modules/OpenClawRbac/Grant-OpenClawRbacRoles.ps1`) for a plan/spec-mandated plural function name. The re-run after this fix reported `ok: true` with no findings (0 errors, 0 warnings).
