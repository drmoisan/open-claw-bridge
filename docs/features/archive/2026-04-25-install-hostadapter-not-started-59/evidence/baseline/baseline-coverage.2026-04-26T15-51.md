# Baseline PowerShell Coverage Evidence

Timestamp: 2026-04-26T16:15:43Z
SearchScope: `artifacts/pester/powershell-coverage.xml`; `artifacts/pester/coverage-final.refinement.xml`
SearchPatterns:
- `Install.ps1`
- `Install.Helpers.psm1`
- line coverage counters for Install-layer classes
- report-level line coverage counter
SearchResult:
- `artifacts/pester/powershell-coverage.xml` contains no `Install.ps1` or `Install.Helpers.psm1` entries.
- `artifacts/pester/coverage-final.refinement.xml` contains both `Install.ps1` and `Install.Helpers.psm1` entries and supplies the Install-layer numeric values.
Output Summary: Baseline overall PowerShell line coverage is 95.98% (430 covered / 18 missed) from `artifacts/pester/coverage-final.refinement.xml`. Baseline Install-layer line coverage is 95.26% (201 covered / 10 missed) from `artifacts/pester/coverage-final.refinement.xml`. Primary artifact recorded for this workflow: `artifacts/pester/powershell-coverage.xml`. Supplementary artifact required because the primary artifact lacks Install-layer entries: `artifacts/pester/coverage-final.refinement.xml`.
