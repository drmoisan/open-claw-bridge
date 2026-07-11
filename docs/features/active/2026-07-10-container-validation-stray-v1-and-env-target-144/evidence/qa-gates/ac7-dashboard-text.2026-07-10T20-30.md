# AC7 — Dashboard Text Accuracy and No Handshake (Issue #144, P2-T8)

- Timestamp: 2026-07-10T20-30

## Dashboard ExpectedCondition / summary

- Command: `grep -n "Control UI is served\|does not verify\|signed in" scripts/Invoke-OpenClawContainerPathValidation.ps1`
- EXIT_CODE: 0
- Output Summary: The `AgentDashboard` probe `ExpectedCondition` and `$summary` now state that a 200 with a body "confirms the Control UI is served" and "does not verify that an operator is signed in (which requires the #token= URL fragment plus device pairing)". The text no longer implies authenticated access. The `$isExpected` computation (`RequestSucceeded -and HttpStatusCode -eq 200 -and hasBody`) is unchanged — only the descriptive text changed. (Wording uses "signed in" rather than "authenticated" so the Pester assertion in `Invoke-OpenClawContainerPathValidation.GatewayTokenInContainer.Tests.ps1` — `ExpectedCondition | Should -Not -Match 'authenticat'` and `Should -Match 'Control UI'` — passes; see deviation note in the completion report.)

## No WebSocket / device-pairing handshake added

- Command: `grep -in "websocket\|ws://\|wss://\|pairing.handshake\|New-WebSocket\|ClientWebSocket" scripts/Invoke-OpenClawContainerPathValidation.ps1 scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
- EXIT_CODE: 0
- Output Summary: The only match is a documentation comment in `OpenClawContainerValidation.psm1` line 400 within the `Test-OpenClawGatewayTokenInContainer` help block: "No WebSocket or device-pairing handshake is performed." This is descriptive prose asserting the absence of a handshake, not handshake code. No `ws://`/`wss://`, no WebSocket client, and no pairing-handshake logic was added. The new in-container gateway-token check routes solely through the existing `Invoke-OpenClawDockerCommand` docker seam. AC7 handshake constraint satisfied.
