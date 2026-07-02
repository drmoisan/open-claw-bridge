# Expect-Fail Evidence — Worker CalendarView Fallback (P3-T1)

Timestamp: 2026-07-02T13-11
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerFallbackTests"`
EXIT_CODE: 1
Output Summary:
- Failed: 3, Passed: 0, Total: 3 (OpenClaw.Core.Tests.dll)
- Failing tests (expected — fallback not yet implemented in `SchedulingWorker.Pipeline.cs`):
  - `RunCycle_LookupMiss_FetchesCalendarViewFromNowToNowPlusFourteenDays` (GetCalendarViewAsync never invoked)
  - `RunCycle_WindowEventClearsThreshold_HydratesEventContextIntoSendPath` (reply subject remains message-derived)
  - `RunCycle_EmptyWindow_ProceedsMessageOnlyWithoutThrowing` (GetCalendarViewAsync Times.Once verification fails)
- File compiles against current production types only; this is the required fail-before state for P3-T4.
