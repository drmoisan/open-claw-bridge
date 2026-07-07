# File-Size Cap Verification (issue #119, P5-T1)

Timestamp: 2026-07-06T23-21
Command: `wc -l <each new/modified production and test file>`
EXIT_CODE: 0

## Output Summary

Every new or modified production and test file is at or below the 500-line cap
(`.claude/rules/general-code-change.md`). Maximum observed: 489 lines.

| File | Lines |
|---|---|
| src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs | 101 |
| src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs | 97 |
| src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs | 87 |
| src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs | 106 |
| tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerTests.cs | 132 |
| tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerPropertyTests.cs | 213 |
| tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs | 413 |
| tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs | 183 |
| tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs | 489 |
| tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs | 220 |

Maximum: 489 lines. Verdict: PASS (all files <= 500 lines).

Note: `CloudGraphContractParityTests.cs` was touched as a mechanically necessary fixture
update — its shared `Service` helper gained the principal in the allowlist so the on-behalf
send-mail parity test still reaches the Graph 400 -> INVALID_REQUEST path under the new
fail-closed gate. Its test assertions are unchanged.
