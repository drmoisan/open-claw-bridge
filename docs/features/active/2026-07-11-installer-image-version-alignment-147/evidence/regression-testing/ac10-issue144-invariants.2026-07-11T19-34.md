# AC10 — Issue #144 Invariants Preserved

Timestamp: 2026-07-12T10-35

Commands (Grep):
- `Select-String -Path scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1 -Pattern '/status'`
- `Select-String -Path scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1 -Pattern 'Get-OpenClawOperatorEnvFilePath|Resolve-OpenClawDefaultEnvFilePath'`
- `Select-String -Path scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1 -Pattern 'Test-OpenClawGatewayTokenInContainer|Test-OpenClawGatewayTokenPresence'`
- `Select-String -Path scripts/Invoke-OpenClawContainerPathValidation.ps1 -Pattern 'AgentDashboard' -Context 0,3`
- `Select-String -Path tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1 -Pattern '-Global'`
- `git diff --name-only -- scripts/Invoke-OpenClawContainerPathValidation.ps1 tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`

EXIT_CODE: 0 (all greps matched as expected; git diff produced zero output lines)

Output Summary: All five #144 invariants confirmed green:
1. `Invoke-OpenClawHostAdapterInContainerProbe`'s shell command and `ExpectedCondition` still reference `/status` (no `/v1` segment) — confirmed at `OpenClawContainerValidation.psm1` lines 310 and 331 (line numbers post-edit).
2. `Get-OpenClawOperatorEnvFilePath` and `Resolve-OpenClawDefaultEnvFilePath` remain present and exported in `OpenClawContainerValidation.psd1`.
3. `Test-OpenClawGatewayTokenInContainer` and `Test-OpenClawGatewayTokenPresence` both remain present, exported, and distinct in `OpenClawContainerValidation.psd1`.
4. The `AgentDashboard` probe's `ExpectedCondition` in `Invoke-OpenClawContainerPathValidation.ps1` still disclaims operator-authentication verification: "it does not verify that an operator is signed in (which requires the #token= URL fragment plus device pairing)".
5. The shared fixture's `Import-OpenClawContainerValidationModule` still uses `-Global` on `Import-Module` (`tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` line 38).

`git diff --name-only` for `scripts/Invoke-OpenClawContainerPathValidation.ps1` and `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` produced zero output lines — both files are byte-identical to their pre-change state (confirmed non-goals, not touched by this plan).
