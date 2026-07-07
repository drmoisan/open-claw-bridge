# QA Gate — Final C# Test / Architecture-Boundary / Coverage (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final-2026-07-07T05-56`
EXIT_CODE: 0

Output Summary:
- Tests: all pass. Failed: 0. Passed: 1377 (OpenClaw.HostAdapter.Tests 100; OpenClaw.Core.Tests 930; OpenClaw.MailBridge.Tests 347). Skipped: 5 (COM/publish host-bound, expected). OpenClaw.Core.Tests grew from the 893 baseline to 930 (+37 new F19 tests including the mocked-Graph contract suite, the local-adapter negative test, the service-seam suite, the worker gate/eligibility/exclusivity/dedupe/failure suites, and the three CsCheck property tests).
- Architecture-boundary (NetArchTest) suites run inside OpenClaw.Core.Tests and pass unmodified (no domain-to-adapter reference introduced; the worker partial is host-neutral).
- Coverage (Cobertura), OpenClaw.Core package: line-rate 0.9929 (99.29%), branch-rate 0.9228 (92.28%). Both well above the uniform thresholds (line >= 85%, branch >= 75%).
- New-file coverage:
  - `GraphHostAdapterClient.ProposeNewTime.cs`: line 100%, branch 100%.
  - `SchedulingWorker.ProposeNewTime.cs`: line 100%, branch 93.75%.
- Modified-file (executable additions) coverage:
  - `HostAdapterHttpClient.cs`: line 100%, branch 100%.
  - `HostAdapterSchedulingService.cs`: line 100%, branch 100%.
  - `SchedulingWorker.Pipeline.cs`: line 100% (the single added `EvaluateProposeNewTimeAsync` await is a straight-line statement; the file's 50% branch figure reflects only pre-existing branches unchanged by F19).

Raw intermediates: `artifacts/csharp/final-2026-07-07T05-56/`. The OpenClaw.Core package figures come from `bb3efd5f-35aa-4470-9343-083eea855132/coverage.cobertura.xml`.

Verdict: PASS.
