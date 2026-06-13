# Phase 1 — QA Gate: Test + Coverage

Timestamp: 2026-06-12T23-11

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Result: PASS in a single clean toolchain pass (format -> build -> typecheck -> test).
- Totals: 428 passed, 0 failed, 3 skipped.
  - OpenClaw.HostAdapter.Tests: 74 passed (71 prior + 2 new meeting-requests/plain-messages dispatch tests + 1 new DefaultAdapterVersion test).
  - OpenClaw.Core.Tests: 178 passed (unchanged in Phase 1).
  - OpenClaw.MailBridge.Tests: 176 passed, 3 skipped (platform/publish-output).
- Coverage headline (cobertura, whole assembly):
  - OpenClaw.HostAdapter.Tests: line-rate 0.8462 (84.62%), branch-rate 0.6237 (62.37%).
  - OpenClaw.Core.Tests: line-rate 0.8932 (89.32%), branch-rate 0.7758 (77.58%).
- Note: whole-assembly cobertura rates include code outside the changed surface. The repository gate (line >= 85% / branch >= 75%) and no-regression rule apply to changed code; the changed-code delta is computed in P3-T5. The HostAdapter assembly rate moved 84.99% -> 84.62% (line) and 62.88% -> 62.37% (branch) from baseline, attributable to added but not-yet-fully-exercised branch arms (verified per-file in P3-T5).
- New HostAdapter.Tests added: meeting-requests dispatch (meetingMessageType ne null), plain-messages dispatch, DefaultAdapterVersion == "1.0.0".

Coverage attachment path: tests/OpenClaw.HostAdapter.Tests/TestResults/f0fe595f-e5c7-4a1b-a1a1-fbe68cdf4eac/coverage.cobertura.xml
