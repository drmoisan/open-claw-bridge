# Final QA — C# Test + Coverage Gate (issue #119, P5-T5)

Timestamp: 2026-07-06T23-21
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0

## Output Summary

All test suites pass, including architecture-boundary, unit, property, and contract-parity suites:

- OpenClaw.Core.Tests: Failed 0, Passed 745, Skipped 0, Total 745 (includes
  `CloudGraphArchitectureBoundaryTests`, `CloudGraphContractParityTests`, the new
  `SendOnBehalfAuthorizerTests` / `SendOnBehalfAuthorizerPropertyTests`, and the extended
  validator / binding / send-mail authorization tests).
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352.
- Aggregate: 1192 passed, 5 skipped, 0 failed.

### Numeric post-change coverage (OpenClaw.Core assembly — the module under change)

- Line coverage: 94.61% (3035 / 3208 lines).
- Branch coverage: 86.08% (742 / 862 branches).

### Per-file coverage of changed files (OpenClaw.Core Cobertura)

- `CloudGraph/SendOnBehalfAuthorizer.cs` (new): line 100%, branch 87.5%.
- `CloudGraph/GraphAdapterOptionsValidator.cs`: line 100%, branch 100%.
- `CloudGraph/GraphHostAdapterClient.SendMail.cs`: line 100%, branch 100%.
- `CloudGraph/GraphAdapterOptions.cs`: plain auto-property options bag; its single changed line
  (the `AllowedPrincipalMailboxUpns` initializer) executes on every options construction and
  is not separately reported as a coverage class (no executable branches).

Verdict: PASS. Both uniform thresholds hold (line >= 85%, branch >= 75%).

Raw intermediate: `artifacts/csharp/core-coverage.post-change.2026-07-06T23-21.cobertura.xml`.
