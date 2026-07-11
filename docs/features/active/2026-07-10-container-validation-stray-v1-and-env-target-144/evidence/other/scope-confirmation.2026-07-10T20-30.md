# Implementation Scope Confirmation (Issue #144, P1-T1)

- Timestamp: 2026-07-10T20-30

## In-Scope Production PowerShell Files (2)

1. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
2. `scripts/Invoke-OpenClawContainerPathValidation.ps1`

## In-Scope Documentation Files (2, Markdown, cap-exempt)

1. `README.md`
2. `docs/mailbridge-runbook.md`

## In-Scope Test Files (5)

1. `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` (modified — AC1/AC2)
2. `tests/scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` (new — AC3)
3. `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` (new — AC3)
4. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (modified — AC7 aggregation count + fake-docker token-exec handler)
5. `tests/scripts/Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1` (new — AC7)

## Batch Grouping

- **Batch A (AC1–AC3):** 2 production PS files + 3 test files (items 1, 2, 3 above).
- **Batch B (AC6 + AC7):** the same 2 production PS files + 2 test files (items 4, 5 above) + 2 Markdown documentation files (cap-exempt).

## AC5 Constraint

No path under `src/OpenClaw.HostAdapter/**` may be modified in either batch. This is verified at Phase 2 (P2-T5).
