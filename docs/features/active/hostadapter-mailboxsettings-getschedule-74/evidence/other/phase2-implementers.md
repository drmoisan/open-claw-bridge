# Phase 2 — Implementers/Mocks Requiring the Two New Members

Timestamp: 2026-06-13T10-30
Command: dotnet build OpenClaw.MailBridge.sln -c Debug
EXIT_CODE: 1 (expected non-zero until Phase 5 implements the client members)

## Concrete implementers that must add the members (compile errors)

- `src/OpenClaw.Core/HostAdapterHttpClient.cs` — the only concrete `IHostAdapterClient`
  implementation. Build reports CS0535 for both:
  - `IHostAdapterClient.GetMailboxSettingsAsync(string?, CancellationToken)`
  - `IHostAdapterClient.GetFreeBusyAsync(DateTimeOffset, DateTimeOffset, string?, CancellationToken)`
  Implemented in Phase 5 (P5-T1, P5-T2).

## Moq-based mocks of IHostAdapterClient (do NOT fail compilation; Moq auto-stubs)

These test files mock `IHostAdapterClient`. Moq auto-implements unconfigured interface members
returning defaults, so they do not break the build, but the scheduling tests must set up the
two new methods explicitly to assert delegation behavior (Phase 6, P6-T3):

- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — will add
  setups for `GetMailboxSettingsAsync`/`GetFreeBusyAsync` in P6-T3.
- `tests/OpenClaw.Core.Tests/MessagePollingWorkerTests.cs` — mocks `IHostAdapterClient` for the
  polling path; no new setup required (does not call the scheduling methods).
- `tests/OpenClaw.Core.Tests/CorePollerTests.cs` — same; no new setup required.
- `tests/OpenClaw.Core.Tests/CalendarPollingWorkerTests.cs` — same; no new setup required.

## Other production consumers of IHostAdapterClient (no change required by P2)

- `src/OpenClaw.Core/Program.cs`, `src/OpenClaw.Core/MessagePollingWorker.cs`,
  `src/OpenClaw.Core/CalendarPollingWorker.cs`,
  `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — consume the interface but
  do not implement it; `HostAdapterSchedulingService` delegation is wired in Phase 6.

Output Summary: One concrete implementer (`HostAdapterHttpClient`) must add both members
(implemented Phase 5). Four Moq-based test mocks reference the interface; only the scheduling
service test adds explicit setups (Phase 6). The build is expected to fail at this task and is
restored at Phase 5. This artifact is documentation-only.
