# Baseline PoshQC Pester + Coverage (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `Invoke-Pester` (Pester 5.6.1) against `tests/scripts/` with `CodeCoverage.Enabled=$true` and `CodeCoverage.Path` restricted to the 5 in-scope production files.
EXIT_CODE: 0
Output Summary:
- Tests: TotalCount=143, Passed=143, Failed=0, Skipped=0, Duration ~11.4s.
- Repo-scoped (5 in-scope files) line coverage: 95.26% (543/570 commands executed).
- Per-file coverage:
  - `scripts/Install.ps1`: 90.29% (93/103)
  - `scripts/Install.Helpers.psm1`: 96.32% (183/190)
  - `scripts/Uninstall.ps1`: 93.75% (45/48)
  - `scripts/Publish.ps1`: 97.06% (66/68)
  - `scripts/Publish.Helpers.psm1`: 96.89% (156/161)
- Coverage evidence XML: `artifacts/pester/coverage-baseline.refinement.xml` (JaCoCo format).
- Repo-wide line coverage (with these 5 files as scope) well above the 80% floor; every refinement-changed file already >= 90%.
