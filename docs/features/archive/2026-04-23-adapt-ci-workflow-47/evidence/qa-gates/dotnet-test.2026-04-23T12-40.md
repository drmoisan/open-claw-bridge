---
Timestamp: 2026-04-23T12-40
Command: dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults
EXIT_CODE: 0
---

# Final QA — dotnet test (with coverage)

Output (tail):
```
Passed!  - Failed:     0, Passed:    71, Skipped:     0, Total:    71, Duration: 462 ms - OpenClaw.HostAdapter.Tests.dll
Passed!  - Failed:     0, Passed:    51, Skipped:     0, Total:    51, Duration: 772 ms - OpenClaw.Core.Tests.dll
Passed!  - Failed:     0, Passed:   152, Skipped:     3, Total:   155, Duration: 12 s - OpenClaw.MailBridge.Tests.dll
```

Coverage cobertura files produced: 3
Per-project line-rate:
- OpenClaw.MailBridge (1201fbc5-...): covered=1164, valid=1302, line-rate=0.8940 (89.40%)
- OpenClaw.HostAdapter (e18de27a-...): covered=1063, valid=1260, line-rate=0.8436 (84.36%)
- OpenClaw.Core (f51f91bd-...): covered=900, valid=1142, line-rate=0.7880 (78.80%)

Output Summary:
- Test counts: Passed 274, Failed 0, Skipped 3, Total 277 (three tests skipped by design: one `Com_active_object_create_and_logon_should_throw_on_non_windows` and two Publish-output ones).
- Weighted overall line coverage: **3127 / 3704 = 84.42%**
- Policy floor (C# repo-wide >= 80%): **PASS** (84.42% > 80.00%, margin 4.42 pp).
- `NewCodeCoverage: N/A` — no C# code was added by this plan (only the YAML workflow file and `.gitignore` changed).
