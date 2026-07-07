# QA Gate — Test Hygiene (banned APIs / live endpoints) (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `grep -rnE 'Thread\.Sleep|Task\.Delay\(|DateTime\.UtcNow|DateTime\.Now|GetTempFileName|GetTempPath|File\.Write|HttpClientHandler|graph\.microsoft\.com'` over the six new F19 test files
EXIT_CODE: 1 (grep: no matches)

Output Summary: Zero violations. None of the banned patterns (`Thread.Sleep`, `Task.Delay(`,
`DateTime.UtcNow`, `DateTime.Now`, `GetTempFileName`, `GetTempPath`, `File.Write`,
`HttpClientHandler`, `graph.microsoft.com`) appear in any of the six new test files.

Determinism is provided by `FakeTimeProvider` (all clock reads and retry-exhaustion time
advancement) and the shared `FakeHttpHandler` with base address
`https://graph.example.test/v1.0/` (no live Graph endpoint). The 429 retry-exhaustion test
advances simulated time via `FakeTimeProvider.Advance`, not wall-clock waits. No temporary
files are created.

Files scanned:
- tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientProposeNewTimeTests.cs
- tests/OpenClaw.Core.Tests/HostAdapterHttpClientProposeNewTimeTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeEdgeTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeIntentPropertyTests.cs
- tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceProposeNewTimeTests.cs

Verdict: PASS.
