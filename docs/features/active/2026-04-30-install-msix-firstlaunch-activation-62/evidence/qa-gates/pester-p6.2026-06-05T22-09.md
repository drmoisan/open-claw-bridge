# Pester — Phase 6 Full Suite (P6-T15)

Timestamp: 2026-06-05T22-09

Command: `Invoke-Pester` (Pester 5.6.1) over the full `tests/scripts` directory (mirrors the CI `Invoke-Pester -Path tests/scripts` step).

TOOLING NOTE: Plan named `mcp__drmCopilotExtension__run_poshqc_test` for the entire repository. The PoshQC MCP is absent; `tests/scripts` is the repository's PowerShell test root per CI. Used `Invoke-Pester -Path tests/scripts` directly.

EXIT_CODE: 0

Output Summary:
- Total: 230, Passed: 230, Failed: 0, Skipped: 0.
- Wall time: 18.6s for the entire suite — confirms no real `Start-Sleep` against the wall clock (the bounded-polling and timeout-exhaustion tests use injected NowProvider/DelayProvider seams).
- Baseline (P0-T8) was 216 passing. Net +14 tests: 8 bounded-polling (P6-T1..T8) + 4 Stage 8b launch (P6-T9..T12) + 2 Invoke-MsixAppActivate (P6-T13..T14). The 2 pre-existing not-ready tests were modified in place (now retryable->timeout with deterministic seams) rather than added.
- Regression-fix note: `tests/scripts/Install.Force.Tests.ps1` BeforeEach was given an `Invoke-MsixAppActivate` mock because the Phase 5 Stage 8b production change made its full-path `& Install.ps1 -Force` invocations reach the new activation step. Without the mock those tests would invoke real `Start-Process`, violating determinism. This is a regression fix to an existing test, kept in a separate batch from the three in-scope Phase 6 test files to respect the 3-test-file per-batch cap.
