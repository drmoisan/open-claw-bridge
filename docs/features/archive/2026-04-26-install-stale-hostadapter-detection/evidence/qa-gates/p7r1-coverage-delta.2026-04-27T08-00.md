# P7r1 Coverage Delta and Changed-Line Coverage (Post-Remediation)

Timestamp: 2026-04-27T08-00
Command: mcp__drmCopilotExtension__run_poshqc_test (workspace_root=c:\Users\DanMoisan\repos\open-claw-bridge, scan_folders=["tests/scripts"]); coverage XML produced by `pwsh -NoProfile -File artifacts/pester/run-r1-coverage.ps1` writing JaCoCo XML to `artifacts/pester/install-layer-coverage.r1.xml`.
EXIT_CODE: 0

## Output Summary

Post-remediation per-module coverage:

- `scripts/Install.Helpers.psm1`: 140 / 148 lines = **94.59%** (>= 90.0%; PASS).
- `scripts/Install.Preflight.psm1`: 98 / 108 lines = **90.74%** (>= 90.0%; PASS).

Both modules satisfy the AC-14b changed-line coverage threshold of >= 90%.

## Per-file table

| File | Lines Covered | Lines Total | % | Threshold (>= 90% on changed) |
| --- | --- | --- | --- | --- |
| `scripts/Install.Preflight.psm1` (new) | 98 | 108 | 90.74% | PASS |
| `scripts/Install.Helpers.psm1` (touched) | 140 | 148 | 94.59% | PASS |
| `scripts/Install.ps1` (touched) | 9 | 152 | 5.9% | Carve-out — see AC-14a; documented measurement artifact |

Aggregate changed-line coverage on the two modules in scope (excluding the documented `Install.ps1` carve-out): (98 + 140) / (108 + 148) = 238 / 256 = **92.97%**.

Comparison to pre-remediation baseline (sourced from `evidence/qa-gates/p7-coverage-delta.md` / `artifacts/pester/install-layer-coverage.xml`):

| File | Baseline % | Post-remediation % | Delta |
| --- | --- | --- | --- |
| `scripts/Install.Helpers.psm1` | 89.2% | 94.59% | +5.4 percentage points |
| `scripts/Install.Preflight.psm1` | 89.8% | 90.74% | +0.9 percentage points |

The improvements correspond to the new defensive-branch tests added in remediation Phases 2 and 3:

- `Get-ProcessMainModulePath` catch branch (Helpers line 479) covered by P2-T1.
- `Get-ProcessMainModulePath` MainModule-null branch (Helpers lines 474-476) covered by P2-T2.
- `Get-ListeningProcessId` empty-listener branch (Helpers lines 459, 461) covered by P2-T3.
- `Get-ListeningProcessId` happy-path branch (Helpers lines 460, 462) covered by P2-T4.
- `Assert-HostAdapterBridgeReadyPreflight` JSON-parse-failure catch branch (Preflight line 267) covered by P3-T1.
- `Format-HostAdapterPreflightFailure` only-code and only-message boundary cases (Preflight lines 130-131) covered by P3-T2c and P3-T2d.

## Verdict

AC-14a: PASS
AC-14b: PASS

AC-14a verdict basis: this remediation does not authorize any change to `scripts/Install.ps1`, the per-file 5.9% figure remains as documented in `evidence/qa-gates/p7-coverage-delta.md` Analysis section, and the AC-14a carve-out (issue.md amendment in remediation P1-T1) reflects the structural test-fixture measurement artifact that is deferred to the follow-up potential entry created by P5-T4. No new repository-wide regression has been introduced beyond that already-documented baseline.

AC-14b verdict basis: both `scripts/Install.Helpers.psm1` (94.59%) and `scripts/Install.Preflight.psm1` (90.74%) now exceed the 90% threshold per the post-remediation coverage XML cited above.

## Source artifacts

- Pre-remediation baseline (Install layer scope): `artifacts/pester/install-layer-coverage.xml` (2026-04-26).
- Post-remediation Install-layer scope: `artifacts/pester/install-layer-coverage.r1.xml` (2026-04-27).
- Post-remediation MCP test JUnit: `artifacts/pester/pester-junit.xml` (2026-04-27, scoped to `tests/scripts`, 215 tests, 0 failures).
- Driver script (preserved for reproducibility): `artifacts/pester/run-r1-coverage.ps1`.
