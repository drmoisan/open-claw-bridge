# Diff-Scope Verification (issue #119, P5-T7)

Timestamp: 2026-07-06T23-21
Command: `git status --short src tests | grep -vE "TestResults|/bin/|/obj/"`
EXIT_CODE: 0

## Output Summary

### Changed production files (exactly the four allowed)
- `M  src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs`
- `M  src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs`
- `M  src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`
- `??  src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs` (new)

### Changed test files (all under tests/OpenClaw.Core.Tests/CloudGraph/)
- `??  SendOnBehalfAuthorizerTests.cs` (new)
- `??  SendOnBehalfAuthorizerPropertyTests.cs` (new)
- `M  GraphHostAdapterClientSendMailTests.cs` (extended, per plan)
- `M  GraphAdapterOptionsValidatorTests.cs` (extended, per plan)
- `M  GraphServiceCollectionExtensionsTests.cs` (extended, per plan)
- `M  CloudGraphContractParityTests.cs` (fixture-only extension — see note)

### Prohibited-path check: NONE touched
Zero changes to any prohibited path:
`src/OpenClaw.HostAdapter.Contracts/**`, `src/OpenClaw.MailBridge*/**`,
`src/OpenClaw.Core/Agent/**`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`,
`src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs`,
`src/OpenClaw.Core/Program.cs`, and `quality-tiers.yml`.

### Scope note (in-directory extension beyond the enumerated list)
The Global Constraints diff scope names `tests/OpenClaw.Core.Tests/CloudGraph/` and
enumerates two new files plus extensions to three named test files. A sixth file in the
same allowed directory, `CloudGraphContractParityTests.cs`, was also modified: its shared
`Service` helper gained the principal in the allowlist so the existing on-behalf send-mail
parity test still reaches the Graph 400 -> INVALID_REQUEST path under the new fail-closed
gate. This is a mechanically necessary fixture update caused by the intended behavior
change (fail-closed deny); the test assertions are unchanged, the change is confined to the
allowed CloudGraph test directory, and no prohibited path was touched.

Verdict: PASS on production scope (exactly the four allowed files) and on all prohibited
paths (none touched). The parity-test fixture extension is within the allowed test directory
and is documented as a required consequence of the behavior change.
