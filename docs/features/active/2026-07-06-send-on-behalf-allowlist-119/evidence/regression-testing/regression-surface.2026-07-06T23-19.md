# Regression Surface Verification (issue #119, P3-T4)

Timestamp: 2026-07-06T23-19
Command: `git diff -U0 -- tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs` and `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0

## Output Summary

### Named existing tests textually unmodified
Diff analysis of `GraphHostAdapterClientSendMailTests.cs` confirms the changed hunks are
confined to (1) `using` additions, (2) the shared `Client` private helper, (3) new
private helpers (`DenyClient`, `RecordingLogger`, `NeverInvokedHandler`), and (4) the new
authorization contract tests appended after the original end of file. No diff hunk falls
inside the following existing test method bodies, which are therefore textually unmodified:

- `SendMail_PrincipalEqualsAssistant_OmitsFrom` (self-send regression)
- `SendMail_TerminalStatus_MapsPerTheD5Matrix` (D5 error-mapping: 400 -> INVALID_REQUEST, 401 -> UNAUTHORIZED)
- `SendMail_ThrottledExhaustion_MapsToThrottledWithGraphCodePassthrough` (throttling)
- `SendMail_PostsToTheAssistantMailbox`, `SendMail_PrincipalDiffersFromAssistant_InjectsFrom`, `SendMail_SaveToSentItemsFalse_PassesThrough`, `SendMail_Accepted202EmptyBody_YieldsOkTrueDataNull`

Note: the shared `Client` helper (not a test) gained a default allowlist seeded with the
configured principal so existing on-behalf send tests keep exercising the send path under
the new fail-closed gate; the test method bodies are unchanged. The shared `Service`
helper in `CloudGraphContractParityTests.cs` (not a test) likewise gained the principal in
its allowlist so `FailurePropagationFlow_SendMailFailureEnvelopeThrowsWithTheMappedCode`
still reaches the Graph 400 -> INVALID_REQUEST path; that test's assertion is unchanged.

### Architecture and parity suites green with the new type in scope
Full solution run result:
- `OpenClaw.HostAdapter.Tests`: Passed 100, Failed 0, Skipped 0.
- `OpenClaw.Core.Tests`: Passed 745, Failed 0, Skipped 0 — includes the namespace-prefix
  `CloudGraphArchitectureBoundaryTests` (the new `SendOnBehalfAuthorizer` falls under the
  existing `OpenClaw.Core.CloudGraph` rules automatically) and `CloudGraphContractParityTests`
  (the `IHostAdapterClient` surface is untouched).
- `OpenClaw.MailBridge.Tests`: Passed 347, Failed 0, Skipped 5.
- Verdict: architecture-boundary and contract-parity suites pass; D5 error-mapping and
  throttling tests pass with an allowlisted configuration.
