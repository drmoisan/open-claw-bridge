# Non-Goal Byte-Identical Invariants (Issue #144, P2-T6)

- Timestamp: 2026-07-10T20-30
- Command: `git diff --name-only -- tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1 tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1 tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`
- EXIT_CODE: 0
- Output Summary: Zero output lines. All three non-goal files remain byte-identical: `Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1`, `Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`, and `fixtures/OpenClawContainerValidation.Fixtures.psm1` were not modified. As predicted in the plan, these tests assert only their own probe (not the aggregate `SupportingDiagnostics` count), so the new `GatewayTokenInContainer` probe — which their default fake dockers route to the "Unexpected docker command" branch — does not break them; they continue to pass in the full-suite run (P2-T3).
