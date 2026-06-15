Timestamp: 2026-06-15T08-28
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
  OpenClaw.HostAdapter.Tests:   Passed=89,  Failed=0, Skipped=0
  OpenClaw.Core.Tests:          Passed=206, Failed=0, Skipped=0
  OpenClaw.MailBridge.Tests:    Passed=235, Failed=0, Skipped=3  (3 skips are pre-existing COM/publish skips)
  Total passed: 530, Total failed: 0

Per-project aggregate coverage (post-extraction):
  OpenClaw.Core.Tests:       line=89.6%  branch=78.4%  (PASS: >= 85% / >= 75%)
  OpenClaw.MailBridge.Tests: line=92.0%  branch=83.3%  (PASS: >= 85% / >= 75%)
  OpenClaw.HostAdapter.Tests: line=86.9% branch=66.0%  (pre-existing; not changed by RF-2)

RF-2 regression verdict: PASS. Passed count (530) >= baseline (530, from P0-T5).
No public API or behavior change. Extraction is behavior-preserving.
