# Pester Baseline With Coverage (P0-T8)

Timestamp: 2026-06-05T22-09

Command: `Invoke-Pester` (Pester 5.6.1) with a `New-PesterConfiguration` over `tests/scripts`, CodeCoverage enabled on `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Install.Preflight.psm1`. Mirrors the `Test PowerShell (Invoke-Pester)` CI step (`Invoke-Pester -Path tests/scripts`), with coverage added.

TOOLING NOTE: Plan named `mcp__drmCopilotExtension__run_poshqc_test` against `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`. That runsettings file and the PoshQC MCP server are absent from this working tree. Coverage scope is set to the three feature-touched Install production files, which is the meaningful denominator for the >=90% per-file gate (AC-08); repository-wide PowerShell coverage is dominated by these files in `scripts/`.

EXIT_CODE: 0

Output Summary:
- Tests total: 216
- Passed: 216
- Failed: 0
- Skipped: 0
- Line/command coverage over the three Install production files: 90.42% (491 of 543 analyzed commands executed).
- All Pester tests pass at baseline; coverage value is present and numeric (not UNVERIFIED).
