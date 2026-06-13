# PowerShell Coverage Delta Verification

Timestamp: 2026-04-26T20:17:01Z
SearchScope: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-coverage.2026-04-26T15-51.md`; `artifacts/pester/powershell-coverage.xml`; `artifacts/pester/coverage-final.refinement.xml`
SearchPatterns:
- baseline overall coverage value
- baseline Install-layer coverage value
- final overall coverage counters
- final Install-layer coverage counters
- `Install.ps1`
- `Install.Helpers.psm1`
SearchResult:
- Baseline overall PowerShell line coverage: 95.98% from `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-coverage.2026-04-26T15-51.md`.
- Baseline Install-layer line coverage: 95.26% from `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-coverage.2026-04-26T15-51.md`.
- Final overall PowerShell line coverage: 95.98% (430 covered / 18 missed) from `artifacts/pester/coverage-final.refinement.xml`.
- Final Install-layer line coverage: 95.26% (201 covered / 10 missed) from `artifacts/pester/coverage-final.refinement.xml`.
- `artifacts/pester/powershell-coverage.xml` remains insufficient for Install-layer coverage because it contains no `Install.ps1` or `Install.Helpers.psm1` entries.
Output Summary: Coverage delta verification PASS. Overall coverage threshold verdict: PASS (95.98% >= 80%). Changed/new-code coverage verdict for the Install-layer target: PASS (95.26% >= 90%). Baseline-to-final coverage delta: 0.00 percentage points overall and 0.00 percentage points for the Install-layer target. Primary artifact path recorded: `artifacts/pester/powershell-coverage.xml`. Supplementary artifact path required for Install-layer proof: `artifacts/pester/coverage-final.refinement.xml`.
