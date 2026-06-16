# Phase 2 — Implementers of IHostAdapterClient Lacking SendMailAsync

Timestamp: 2026-06-16T07-12
Command: dotnet build OpenClaw.MailBridge.sln -c Debug
EXIT_CODE: 1 (expected; resolved in Phase 7)

## Output Summary

Adding `SendMailAsync` to `IHostAdapterClient` produces exactly one compile break:

- `src/OpenClaw.Core/HostAdapterHttpClient.cs(12,5): error CS0535: 'HostAdapterHttpClient' does not implement interface member 'IHostAdapterClient.SendMailAsync(SendMailRequest, string?, CancellationToken)'`

This is the single production implementer; it is implemented in Phase 7 (P7-T2).

## Test doubles

All test usages of `IHostAdapterClient` are Moq-generated mocks (`Mock<IHostAdapterClient>`), which auto-implement new interface members. No hand-written stub class implements the interface, so no test double requires a manual `SendMailAsync` addition. Files using `Mock<IHostAdapterClient>`:
- `tests/OpenClaw.Core.Tests/MessagePollingWorkerTests.cs`
- `tests/OpenClaw.Core.Tests/CorePollerTests.cs`
- `tests/OpenClaw.Core.Tests/CalendarPollingWorkerTests.cs`
- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs`

The solution build is expected to remain non-zero until P7-T2 lands the `HostAdapterHttpClient.SendMailAsync` implementation. This task is documentation-only.
