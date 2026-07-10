# Seam and Hermeticity Checks (Issue #142, P5-T6)

Timestamp: 2026-07-10T19-10

## Seam boundary (AC9)
Command: grep -nE "& docker |docker @" scripts/Publish.Docker.psm1 scripts/Install.Docker.psm1
EXIT_CODE: 0
Output Summary:
- The only executable-invocation statement is `$output = & docker @DockerArgs 2>&1`:
  - scripts/Publish.Docker.psm1:78 (inside function Invoke-DockerExe)
  - scripts/Install.Docker.psm1:67 (inside function Invoke-DockerExe)
- All other matches (lines 38/63 Publish, 29/54 Install) are comment-help text, not code.
- Conclusion: every docker invocation introduced by this fix routes through the module-scoped Invoke-DockerExe seam. PASS.

## Hermeticity (AC11)
Command: grep -nE "function global:docker|New-TemporaryFile|GetTempPath|$env:TEMP" tests/scripts/Publish.Docker.Tests.ps1 tests/scripts/Install.Docker.Tests.ps1 tests/scripts/Install.DockerStage.Tests.ps1
EXIT_CODE: 1 (no matches)
Output Summary:
- Zero matches. No global:docker shim, no temp-file usage in any of the three new/relocated docker test files. Tests mock only the Invoke-DockerExe seam. PASS.
