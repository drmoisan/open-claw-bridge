Timestamp: 2026-06-15T08-55
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
  Test totals: Passed=558, Failed=0, Skipped=3 (pre-existing COM/publish-output skips)
    - OpenClaw.HostAdapter.Tests: Passed=89,  Failed=0, Skipped=0
    - OpenClaw.Core.Tests:        Passed=206, Failed=0, Skipped=0
    - OpenClaw.MailBridge.Tests:  Passed=263, Failed=0, Skipped=3

  Per-project aggregate coverage:
    - OpenClaw.MailBridge.Tests:  line=93.9%  branch=87.0%  (PASS: >= 85% / >= 75%)
    - OpenClaw.Core.Tests:        line=89.6%  branch=78.4%  (PASS: >= 85% / >= 75%)
    - OpenClaw.HostAdapter.Tests: line=86.9%  branch=66.0%  (pre-existing; not changed by this feature)

  ComMessageSource.cs specific:  line=94.7%  branch=93.5%  (PASS: >= 85% / >= 75%)

  Note: OpenClaw.HostAdapter.Tests branch=66.0% is below the 75% threshold. This is a
  pre-existing condition not introduced by this feature (RF-1 and RF-2 changes are confined to
  OpenClaw.MailBridge and OpenClaw.Core). Reported as an out-of-scope finding.
