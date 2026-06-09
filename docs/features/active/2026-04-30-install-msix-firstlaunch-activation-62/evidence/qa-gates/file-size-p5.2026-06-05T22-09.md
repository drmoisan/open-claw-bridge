# File-Size Cap — Phase 5 (P5-T2)

Timestamp: 2026-06-05T22-09

Command: `(Get-Content scripts/Install.ps1 | Measure-Object -Line).Lines`

EXIT_CODE: 0

Output Summary:
- `scripts/Install.ps1` = 392 lines (<= 499: True). Under the 500-line cap after inserting the Stage 8b protocol-activation block.
