# Phase 2 — QA Gate: Test + Coverage

Timestamp: 2026-06-12T23-15

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Result: PASS in a single clean toolchain pass (format -> build -> typecheck -> test).
- Totals: 428 passed, 0 failed, 3 skipped.
  - OpenClaw.HostAdapter.Tests: 74 passed.
  - OpenClaw.Core.Tests: 178 passed (HostAdapterHttpClient tests updated to assert Graph-shaped paths, new base URL, $filter/$top/startDateTime/endDateTime/meetingMessageType, and users/me/{...} segments).
  - OpenClaw.MailBridge.Tests: 176 passed, 3 skipped.
- Coverage headline (cobertura, whole assembly):
  - OpenClaw.Core.Tests: line-rate 0.8936 (89.36%), branch-rate 0.7758 (77.58%).
  - OpenClaw.HostAdapter.Tests: line-rate 0.8462 (84.62%), branch-rate 0.6237 (62.37%).
- Note: whole-assembly rates; changed-code delta computed in P3-T5. Core line rate moved 89.32% -> 89.36% from baseline (no regression). MailboxId mirror and Graph-shaped paths are exercised by the updated client tests.

Coverage attachment paths:
- tests/OpenClaw.Core.Tests/TestResults/1cd9a520-86a3-4c38-970e-b4562820cebe/coverage.cobertura.xml
- tests/OpenClaw.HostAdapter.Tests/TestResults/939b880f-995c-4c88-96e3-a430109c4b46/coverage.cobertura.xml
