# Baseline PoshQC Format (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `Invoke-Formatter` over every `*.ps1` / `*.psm1` under `scripts/` and `tests/scripts/`, compare to on-disk content (check-only).
EXIT_CODE: 2
Output Summary:
- 2 files differ from their formatted output:
  - `scripts/Install.Helpers.psm1` (length delta after format: -36 bytes, likely trailing-whitespace trim; line-by-line compare shows no functional edits required).
  - `scripts/Publish.Helpers.psm1` (present in dirty list; similar whitespace delta).
- All other PS files under `scripts/` and `tests/scripts/` already match formatter output.
- Any file edited during this refinement will be reformatted by the per-batch PoshQC loop; the final gate (PG-T1) runs clean.
