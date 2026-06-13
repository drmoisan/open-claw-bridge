# Baseline — Test + Coverage

Timestamp: 2026-06-13T03-05

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Test result: PASS. Failed: 0, Passed: 425 (HostAdapter.Tests 71, Core.Tests 178, MailBridge.Tests 176), Skipped: 3, Total: 428.
- Skipped (platform/publish-gated): Com_active_object_create_and_logon_should_throw_on_non_windows; PublishOutput_BridgeDirectory_ContainsBridgeExecutable; PublishOutput_ClientDirectory_ContainsClientExecutable.

Coverage headline (cobertura, XPlat Code Coverage / coverlet):
- OpenClaw.MailBridge.Tests module: line-rate 93.22% (880/944 lines), branch-rate 83.08% (226/272 branches).
- OpenClaw.Core.Tests module: line-rate 89.32% (1373/1537 lines), branch-rate 77.58% (308/397 branches).

Threshold check (line >= 85%, branch >= 75%): both modules PASS at baseline.

Note: the "Code Coverage" (Vanguard) collector reports "No code coverage data available. Profiler was not initialized." in this CLI/Git Bash context; the authoritative coverage is the coverlet XPlat cobertura output, consistent with the runsettings note that the XPlat collector is the CLI path.
