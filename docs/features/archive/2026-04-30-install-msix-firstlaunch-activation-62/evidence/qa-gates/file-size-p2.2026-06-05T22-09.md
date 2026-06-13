# File-Size Cap — Phase 2 (P2-T8)

Timestamp: 2026-06-05T22-09

Command: `(Get-Content <path> | Measure-Object -Line).Lines -lt 500` for each touched file.

EXIT_CODE: 0

Output Summary:
- `installer/Package.appxmanifest` = 45 lines (< 500: True)
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` = 291 lines (< 500: True)
- Both files remain under the 500-line cap.
