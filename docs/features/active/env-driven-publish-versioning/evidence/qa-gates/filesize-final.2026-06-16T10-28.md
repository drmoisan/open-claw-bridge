# Final QC — File Size (500-line cap)

Timestamp: 2026-06-16T12-15
Command: wc -l on every changed/created production and test file
EXIT_CODE: 0

## Production files
- scripts/Publish.Env.psm1 (NEW): 243 lines. PASS (<= 500).
- scripts/Publish.Helpers.psm1: 597 lines. OVER CAP. See note below.
- scripts/Publish.ps1: 248 lines. PASS (<= 500).
- scripts/New-MsixDevCert.ps1: 211 lines. PASS (<= 500).

## Test files
- tests/scripts/Publish.Env.Tests.ps1 (NEW): 181 lines. PASS (<= 500).
- tests/scripts/Publish.Helpers.Tests.ps1: 484 lines. PASS (<= 500) — previously 541 (over cap); the Resolve-CertThumbprint context was extracted to the sibling file below.
- tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1 (NEW): 134 lines. PASS (<= 500).
- tests/scripts/Publish.Tests.ps1: 395 lines. PASS (<= 500).
- tests/scripts/New-MsixDevCert.Tests.ps1: 163 lines. PASS (<= 500).

## Note: scripts/Publish.Helpers.psm1 is over the 500-line cap (597)
This file was already 581 lines at baseline (filesize-baseline.2026-06-16T10-28.md), i.e. over the
500-line cap BEFORE this feature. The plan's Scope and Design Notes explicitly document this
pre-existing overage and adopt the new module scripts/Publish.Env.psm1 as the mitigation so that
the new .env/version helpers are not added to the already-over-cap file. Plan task P1-T6 (mandated
by the plan) added only the new -DotEnvThumbprint parameter and its documentation to the existing
Resolve-CertThumbprint function, which raised the count from 581 to 597. The plan does not task
reducing Publish.Helpers.psm1 below 500 (that would require a broader extraction/refactor outside
the approved scope).

FINDING (flagged for the orchestrator): P2-T5's acceptance text lists scripts/Publish.Helpers.psm1
among files to verify <= 500, but the plan's own design deliberately leaves this pre-existing
over-cap file unrefactored. The two cannot both hold. The accurate state is recorded here: 597
lines, a +16 increment over the pre-existing 581, attributable solely to the plan-mandated
parameter addition. Bringing this file under 500 would require expanding scope (extracting one or
more existing functions into another module), which the executor did not do per the anti-scope-creep
directive. All NEWLY created files and all split test files are <= 500.

---

## Phase 3 — File-size cap remediation (Publish.Helpers.psm1 extraction)

Timestamp: 2026-06-16T15-08
Command: wc -l on every changed/created production and test file in Phase 3
EXIT_CODE: 0

The Phase 2 FINDING above (scripts/Publish.Helpers.psm1 over the 500-line cap at
597) is RESOLVED by this phase. The orchestrator approved a bounded pure
relocation that moved the seven Windows SDK / MSIX functions into the new module
scripts/Publish.Msix.psm1.

### Production files (post-relocation)
- scripts/Publish.Helpers.psm1: 356 lines. PASS (<= 500). NOW UNDER THE CAP (was 597; -241 from the relocation, target ~360 met).
- scripts/Publish.Msix.psm1 (NEW): 265 lines. PASS (<= 500).
- scripts/Publish.ps1: 249 lines. PASS (<= 500).

### Test files (post-relocation)
- tests/scripts/Publish.Helpers.Tests.ps1: 280 lines. PASS (<= 500) — was 484; the seven relocated-function contexts and the makepri/makeappx/signtool shims moved to the new file below.
- tests/scripts/Publish.Msix.Tests.ps1 (NEW): 254 lines. PASS (<= 500).

### Verdict
All Phase 3 changed/created files are <= 500 lines. scripts/Publish.Helpers.psm1
is now under the cap (356), resolving the pre-existing and Phase-2-flagged
over-cap condition. No file in scope exceeds 500 lines.
