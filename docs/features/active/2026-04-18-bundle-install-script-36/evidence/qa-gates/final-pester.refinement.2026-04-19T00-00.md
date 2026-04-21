# Final PoshQC Pester + Coverage (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `Invoke-Pester` (Pester 5.6.1) against `tests/scripts/` with `CodeCoverage.Enabled=$true` and `CodeCoverage.Path` restricted to the 5 in-scope production files.
EXIT_CODE: 0
Output Summary:
- Tests: TotalCount=150, Passed=150, Failed=0, Skipped=0, Duration ~11.7s.
- Repo-scoped (5 in-scope files) line coverage: 95.47% (548/574 commands executed).
- Per-file coverage:
  - `scripts/Install.ps1`: 91.01% (81/89) — refinement-changed file, >= 90%.
  - `scripts/Install.Helpers.psm1`: 95.92% (188/196) — refinement-changed file, >= 90%.
  - `scripts/Uninstall.ps1`: 93.75% (45/48) — unchanged file, unchanged coverage.
  - `scripts/Publish.ps1`: 97.14% (68/70) — refinement-changed file, >= 90%.
  - `scripts/Publish.Helpers.psm1`: 97.08% (166/171) — refinement-changed file, >= 90%.
- Coverage evidence XML: `artifacts/pester/coverage-final.refinement.xml` (JaCoCo format).
- Acceptance: repo-wide >= 80% PASS; per-file targeted >= 90% on every refinement-changed file PASS; zero test failures PASS; zero regressions vs Phase B baseline (150 tests pass, up from 143).
