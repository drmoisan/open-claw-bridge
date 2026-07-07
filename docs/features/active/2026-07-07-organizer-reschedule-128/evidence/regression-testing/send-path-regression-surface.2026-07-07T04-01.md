# Send-Path Regression Surface Verification (Issue #128, P3-T7)

Timestamp: 2026-07-07T04-01
Command: `git diff origin/main -- <pre-existing SchedulingWorker test files> src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`
EXIT_CODE: 0

Output Summary:

Zero non-mechanical modifications to the pre-existing send-path tests; `SchedulingWorker.Audit.cs` and its `BuildActingFlags` are textually unmodified.

Verified files:

- `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs` — UNCHANGED (empty `git diff`). `BuildActingFlags` (the send-path ActingFlags snapshot `SendEnabled=<bool>;CalendarWriteEnabled=<bool>`) is byte-identical to pre-F18.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs` — only added line: `new Mock<ISeriesMoveHistory>().Object,` (P3-T2 mechanical ctor-arg addition).
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerAuditTests.cs` — only added line: `new Mock<ISeriesMoveHistory>().Object,`.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerDedupeTests.cs` — only added line: `new Mock<ISeriesMoveHistory>().Object,`.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerFallbackTests.cs` — only added line: `new Mock<ISeriesMoveHistory>().Object,`.

Each pre-existing worker test file carries exactly one added line (the mechanical mock), no behavioral edits. The full test run is green: OpenClaw.Core.Tests 892 passed (includes the pre-existing send-path audit/dedupe suites: SchedulingWorkerTests, SchedulingWorkerAuditTests, SchedulingWorkerDedupeTests, SchedulingWorkerFallbackTests, all passing unmodified in behavior), OpenClaw.HostAdapter.Tests 100 passed, OpenClaw.MailBridge.Tests 347 passed / 5 skipped. The send path and its persisted `ActingFlags` are unchanged (AC-2).
