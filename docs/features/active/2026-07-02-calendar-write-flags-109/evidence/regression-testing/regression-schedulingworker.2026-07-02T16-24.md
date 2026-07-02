# Regression — SchedulingWorker Subset (Unchanged Tests)

Timestamp: 2026-07-02T16-24
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerTests|FullyQualifiedName~SchedulingWorkerAuditTests"
EXIT_CODE: 0

Verification of zero test-file modifications:
- Command: git status --porcelain tests/OpenClaw.Core.Tests/Agent/Runtime/
- EXIT_CODE: 0
- Output: (empty — no modified, added, or deleted files under tests/OpenClaw.Core.Tests/Agent/Runtime/)

Output Summary:
- OpenClaw.Core.Tests.dll: Passed! Failed: 0, Passed: 16, Skipped: 0, Total: 16 (all SchedulingWorkerTests and SchedulingWorkerAuditTests, including acting-flags assertions, pass unchanged).
- OpenClaw.MailBridge.Tests.dll / OpenClaw.HostAdapter.Tests.dll: no tests match the filter (expected; the subset lives in OpenClaw.Core.Tests).
- Confirms AC-3 / AC-U3: existing SchedulingWorker gating and audit behavior unchanged with zero regression-test modifications.
