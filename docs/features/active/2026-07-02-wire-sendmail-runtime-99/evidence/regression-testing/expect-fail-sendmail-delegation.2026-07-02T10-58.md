# Expect-Fail Evidence — SendMailAsync Delegation Tests (P1-T2)

Timestamp: 2026-07-02T10-58
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests"
EXIT_CODE: 1
Output Summary:
- Result: Failed! - Failed: 5, Passed: 8, Skipped: 0, Total: 13 (OpenClaw.Core.Tests.dll).
- The five new delegation tests fail as expected against the current `NotSupportedException` implementation of `HostAdapterSchedulingService.SendMailAsync`:
  1. `SendMailAsync_Success_DelegatesToClientOnceWithCallerToken` — System.NotSupportedException ("Outbound mail is not yet exposed by the HostAdapter/MailBridge surface. This endpoint is deferred to issues #74/#75 ...").
  2. `SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage` — expected InvalidOperationException, found NotSupportedException.
  3. `SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped` — expected HttpRequestException, found NotSupportedException.
  4. `SendMailAsync_CanceledToken_PropagatesOperationCanceled` — expected OperationCanceledException, found NotSupportedException.
  5. `SendMailAsync_MapsAgentRequestToWireRequest` — System.NotSupportedException.
- All 8 pre-existing tests in `HostAdapterSchedulingServiceTests` still pass (read-method delegation unaffected).
- Failure cause in every case is the production `NotSupportedException` thrown by `SendMailAsync` (fail-before condition confirmed).
