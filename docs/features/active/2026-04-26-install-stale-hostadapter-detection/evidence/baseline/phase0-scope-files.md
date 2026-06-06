# Phase 0 Scope Files

Timestamp: 2026-04-26T22-36

Command: `wc -l scripts/Install.ps1 scripts/Install.Helpers.psm1 tests/scripts/Install.Tests.ps1 tests/scripts/Install.Force.Tests.ps1`

EXIT_CODE: 0

Output Summary:
- scripts/Install.ps1: 500 lines
- scripts/Install.Helpers.psm1: 467 lines
- tests/scripts/Install.Tests.ps1: 456 lines
- tests/scripts/Install.Force.Tests.ps1: 163 lines
- Total: 1586 lines

Note: Plan front matter records 433 / 410 line counts for the two production files; observed counts at the start of execution are 500 / 467. The 500-line policy ceiling still applies — Install.ps1 is at the ceiling, and net deletions in P3-P5 must keep it at or below 500.
