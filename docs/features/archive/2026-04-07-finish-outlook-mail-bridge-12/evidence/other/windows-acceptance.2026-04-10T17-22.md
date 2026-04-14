# Windows Acceptance Evidence

Timestamp: 2026-04-10T17:22
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1 -ReadyTimeoutSeconds 180
EXIT_CODE: 0

## Output Summary

```
PublishedBridgeTargetFramework: net10.0-windows
BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0
PublishedClientTargetFramework: net10.0-windows
ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0
AutomatedSuitesPassed: A,B,C,D,F
OperatorEvidencePath: TestResults\mailbridge-operator-evidence.txt
```

## Automated Suite Results

- Suite A (Framework Evidence): PASS — `PublishedBridgeTargetFramework: net10.0-windows`, `PublishedClientTargetFramework: net10.0-windows`, `BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0`, `ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0`
- Suite B (Bridge Readiness): PASS — bridge reached `state=ready` with `outlookConnected=true`
- Suite C (Message/Calendar RPC): PASS — `list-messages`, `get-message`, `list-meeting-requests`, `list-calendar`, `get-event` commands responded
- Suite D (Safe-Mode Privacy): PASS — safe-mode privacy assertions passed
- Suite F (Hygiene Loop): PASS — 25 sequential status + list-messages calls completed without error

## Environment

- OS: Windows 11
- .NET Runtime: Microsoft.NETCore.App 10.0.0
- Outlook: Classic Outlook (Office16) — COM/MAPI connected
- Scheduled Task: OpenClaw MailBridge (InteractiveToken, LeastPrivilege)
- Install Path: C:\Program Files\OpenClaw\MailBridge
- Pipe Name: openclaw_mailbridge_v1

## Defects Fixed Before This Run

1. Named-pipe race condition: removed `server.Disconnect()` from `HandleClientAsync` finally block, added `WaitForPipeDrain()` in `WriteResponse` after `FlushAsync`.
2. Outlook COM Logon dialog hang: changed `Logon("", "", Type.Missing, Type.Missing)` to `Logon("", "", false, false)` to suppress the "Choose Profile" dialog in headless/scheduled-task execution.
3. Settings deserialization: case-insensitive JSON property binding.
4. ScanWorker shutdown race: graceful stop when host disposes STA executor.
5. PipeRpcWorker client lifetime: dedicated async session via `await using`.
6. Client empty-response handling: structured InternalError response for empty payloads.
7. Client buffer/read-mode: increased buffer to 65KB, set `ReadMode = PipeTransmissionMode.Message`.
8. Installer retry timing: `Wait-BridgeStatusPreflight` polling fix.
9. OutlookScanner fallback: catches exceptions when Outlook is running but COM unavailable.
10. Server INTERNAL_ERROR response: returns structured JSON when handler throws.
