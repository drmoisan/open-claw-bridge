# Final QA — Tests with Coverage

Timestamp: 2026-07-02T11-30
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --no-build
EXIT_CODE: 0
Output Summary:
- Test results: 590 passed, 0 failed, 5 skipped, 595 total.
  - OpenClaw.HostAdapter.Tests: 100 passed / 100 total
  - OpenClaw.Core.Tests: 213 passed / 213 total (baseline 210 pre-existing tests all pass unchanged,
    plus the 3 new CoreCacheRepositoryResponseStatusTests — satisfies AC-3)
  - OpenClaw.MailBridge.Tests: 277 passed, 5 skipped (same 5 environment-gated skips as baseline) / 282 total
- Pooled post-change coverage across the three coverage.cobertura.xml reports:
  line 90.26% (4029/4464), branch 79.36% (911/1148).
- OpenClaw.Core package (targeted module): line-rate 0.9861 (98.61%), branch-rate 0.9168 (91.68%).
- Cobertura source paths:
  - tests/OpenClaw.Core.Tests/TestResults/9aaeb2f5-b798-417f-b163-0bcb93264520/coverage.cobertura.xml
  - tests/OpenClaw.MailBridge.Tests/TestResults/15d48b95-6d9a-4f46-99ec-3ca53110e0c8/coverage.cobertura.xml
  - tests/OpenClaw.HostAdapter.Tests/TestResults/a84c5bf3-5927-476c-b069-fc6801fe206b/coverage.cobertura.xml
- Raw copies preserved at artifacts/csharp/post-2026-07-01T22-16/.
- Toolchain loop status for this final pass: csharpier check . EXIT 0 (P3-T1), dotnet build EXIT 0
  with 0 warnings/0 errors (P3-T2), this test run EXIT 0 — all stages green in a single pass with
  no file changes between stages.
