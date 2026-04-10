# Remediation Baseline — Line Counts

- Timestamp: 2026-04-10T23-00
- Command: `(Get-Content <file>).Count` for each file
- Output Summary:
  - `src/OpenClaw.MailBridge/OutlookScanner.cs`: 580 lines (exceeds 500-line limit)
  - `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`: 687 lines (exceeds 500-line limit)
  - `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs`: 652 lines (exceeds 500-line limit)
