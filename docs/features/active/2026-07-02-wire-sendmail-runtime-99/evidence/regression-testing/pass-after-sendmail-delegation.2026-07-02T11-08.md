# Pass-After Evidence — SendMailAsync Delegation (P3-T4)

Timestamp: 2026-07-02T11-08
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13 (OpenClaw.Core.Tests.dll).
- All five Phase 1 delegation tests now pass against the wired implementation:
  1. `SendMailAsync_Success_DelegatesToClientOnceWithCallerToken`
  2. `SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage`
  3. `SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped`
  4. `SendMailAsync_CanceledToken_PropagatesOperationCanceled`
  5. `SendMailAsync_MapsAgentRequestToWireRequest`
- The 8 pre-existing read-delegation tests continue to pass.
- Fail-before evidence: `evidence/regression-testing/expect-fail-sendmail-delegation.2026-07-02T10-58.md` (EXIT_CODE 1, all five failing on NotSupportedException); pass-after is this artifact.
