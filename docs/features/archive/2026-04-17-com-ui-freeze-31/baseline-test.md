# Phase 0 — Baseline Test

- **Timestamp:** 2026-04-17T13:12:00Z
- **Command:** `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"`
- **EXIT_CODE:** 1
- **Output Summary:**
  - Total: 118, Passed: 114, Failed: 1, Skipped: 3
  - Pre-existing failure: `RequiredIconAssets_AllExist` in `MsixPackageTests.cs` (missing `Wide310x150Logo.png` asset, unrelated to COM yield work)
  - Line coverage by project:
    - OpenClaw.MailBridge.Tests: **83.83%** line, 67.56% branch
    - OpenClaw.Core.Tests: 70.80% line, 51.40% branch
    - OpenClaw.HostAdapter.Tests: 59.05% line, 37.50% branch
  - **Baseline coverage headline (MailBridge): 83.83% line coverage**
