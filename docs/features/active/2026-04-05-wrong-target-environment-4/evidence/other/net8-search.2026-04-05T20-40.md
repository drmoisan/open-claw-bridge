Timestamp: 2026-04-05T21:00:35.6646806-04:00
Command: rg -n "net8\.0-windows|net8\.0|\.NET 8|dotnet 8" .
EXIT_CODE: 0
Output Summary: Repo-wide search still finds historical `.NET 8` references in the active issue file and the audit plan text; no current project file targets `net8.0-windows`.

```text
.\docs\features\active\2026-04-05-wrong-target-environment-4\plan.2026-04-05T20-40.md:22:- [ ] [P1-T2] Record a repo-wide search for `net8.0-windows`, `net8.0`, `.NET 8`, and `dotnet 8`; if the search is empty, include `SearchScope:`, `SearchPatterns:`, and `SearchResult:` fields in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/net8-search.2026-04-05T20-40.md`.
.\docs\features\active\2026-04-05-wrong-target-environment-4\plan.2026-04-05T20-40.md:25:- [ ] [P2-T1] Record a successful `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal` run in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/regression-testing/dotnet-test-success.2026-04-05T20-40.md`, and confirm the output references `net10.0-windows` rather than `net8.0-windows`.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:14:The MailBridge solution targeted `net8.0-windows` even though the workspace and local machine were configured for .NET 10.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:15:Opening the workspace triggered test discovery against a `net8.0-windows` testhost that could not start because `Microsoft.NETCore.App` 8.0.x was not installed.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:26:1. Open the `open-claw-bridge` workspace on a machine with .NET 10 SDK/runtime installed but without .NET 8 runtime.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:28:3. Observe the testhost startup failure for `bin\Debug\net8.0-windows\testhost.exe`.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:47:  Testhost process for source(s) 'C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\bin\Debug\net8.0-windows\OpenClaw.MailBridge.Tests.dll' exited with error: You must install or update .NET to run this application.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:48:  App: C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\bin\Debug\net8.0-windows\testhost.exe
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:63:Project target frameworks were still pinned to `net8.0-windows` while `global.json` and the machine environment were aligned to .NET 10.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:76:- Retarget all MailBridge projects from `net8.0-windows` to `net10.0-windows`.
.\docs\features\active\2026-04-05-wrong-target-environment-4\issue.md:78:- Re-open the workspace and confirm automatic test discovery succeeds without requiring the .NET 8 runtime.
```
