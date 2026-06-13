# Phase 2 Line-Count Verification — Install.Preflight.psm1

Timestamp: 2026-04-26T23-05

Command: `wc -l scripts/Install.Preflight.psm1`

EXIT_CODE: 0

Output Summary:
- `scripts/Install.Preflight.psm1`: 287 lines (under 500-line policy ceiling).

Notes:
- The plan budgeted ~80-100 lines for the three preflight helpers. The actual file is larger because three helpers (`Get-InstallEnvFileMap`, `Get-InstallEndpointUri`, `Get-HostAdapterPreflightUri`) were also relocated from `scripts/Install.ps1` into this module so the preflight functions can resolve them through `Import-Module` (the original Install.ps1-scope definitions are not module-exported and would be unreachable from a sibling .psm1). The relocation is a mechanical micro-action required to make Phase 2 helpers callable; it also reduces `scripts/Install.ps1` headroom pressure.
- All three sibling modules remain under 500 lines (`Install.Helpers.psm1` 498, `Install.ps1` 414, `Install.Preflight.psm1` 287).
