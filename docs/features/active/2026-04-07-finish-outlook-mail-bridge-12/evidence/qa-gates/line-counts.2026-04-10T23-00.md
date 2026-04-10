# Line Counts — QA Gate

Timestamp: 2026-04-10T23-00
Command: `(Get-Content <file>).Count` for each file
Output Summary:

| File | Lines | Under 500? |
|---|---|---|
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 495 | Yes |
| `src/OpenClaw.MailBridge/OutlookComHelpers.cs` | 104 | Yes |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs` | 346 | Yes |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Pipe.cs` | 356 | Yes |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs` | 309 | Yes |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs` | 357 | Yes |

All affected files are under the 500-line limit.
