# File-Size Cap — Phase 4 (P4-T5)

Timestamp: 2026-06-05T22-09

Command: `(Get-Content scripts/Install.Preflight.psm1 | Measure-Object -Line).Lines`

EXIT_CODE: 0

Output Summary:
- `scripts/Install.Preflight.psm1` = 336 lines (<= 499: True). Under the 500-line cap after adding the bounded-polling refactor plus the `Get-HostAdapterBridgeReadyClassification` internal helper.
