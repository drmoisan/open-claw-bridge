# Baseline — Test + Coverage

Timestamp: 2026-06-13T13-34
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0

Output Summary:
- Test result: Passed. Failed: 0.
  - OpenClaw.HostAdapter.Tests: 89 passed / 89 total
  - OpenClaw.Core.Tests: 193 passed / 193 total
  - OpenClaw.MailBridge.Tests: 216 passed / 3 skipped / 219 total
  - Aggregate: 498 passed, 3 skipped, 501 total.
- Coverage (cobertura, per-test-assembly closure):
  - OpenClaw.MailBridge.Tests closure: line-rate 0.9407 (94.07%), branch-rate 0.8654 (86.54%);
    lines-covered 1064 / lines-valid 1131; branches-covered 283 / branches-valid 327.
  - OpenClaw.Core.Tests closure: line-rate 0.8917 (89.17%), branch-rate 0.7759 (77.59%);
    lines-covered 1441 / lines-valid 1616; branches-covered 329 / branches-valid 424.
- Both meet uniform thresholds at baseline (line >= 85%, branch >= 75%).

Note: HostAdapter.Tests emitted a binary .coverage attachment; MailBridge and Core emitted
coverage.cobertura.xml used for the numeric headline above.
