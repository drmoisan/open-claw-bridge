# Phase 8 — Toolchain Gate

Timestamp: 2026-06-16T08-50

Phase 8 adds the 202 success factory (`HostAdapterResponses.AcceptedNoContent`, D-A),
`HostAdapterCommandBuilder.BuildSendMail` (flat `--key value` args, JSON recipient arrays D-C,
default `--save-to-sent-items true` AC-08), the `MailRoutes.cs` route
(`POST /users/{assistantMailbox}/sendMail`, `[FromBody]` binding D-B, ready->validate->dispatch
order, 202 success / 400 INVALID_REQUEST / 409 BRIDGE_NOT_READY / 502 mapping), the one-line
`app.MapMailRoutes()` registration, and 7 tests (5 endpoint + 2 BuildSendMail). All five stages pass.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 190 files. No CSharpier changes beyond new/edited files.

## Stage 2 — Lint / Analyzers (full solution)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: 0 Error(s), 0 Warning(s).

## Stage 3 — Type-check (nullable, full solution)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: 0 Error(s).

## Stage 4 — Architecture-boundary tests
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 2. `OpenClaw.HostAdapter` does not reference
`OpenClaw.MailBridge` and performs no COM (Rule 5); the route shells out to the client process via
`HostAdapterCommandBuilder`/`IHostAdapterProcessRunner`. No new ProjectReference edge. COM remains
confined to OpenClaw.MailBridge.

## Stage 5 — Test + Coverage (Integration excluded)
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"
EXIT_CODE: 0
Output Summary: HostAdapter.Tests 100 passed (+11 vs baseline); Core.Tests 210 passed; MailBridge.Tests
277 passed (3 skipped). Per-project cobertura:
- Core: line 89.61%, branch 78.44%
- HostAdapter: line 87.70%, branch 67.19%
- MailBridge: line 93.08%, branch 86.92%
Combined: line (1502+1113+1413)/(1676+1269+1518) = 4028/4463 = 90.25%; branch
(342+170+399)/(436+253+459) = 911/1148 = 79.35%. Both above the uniform gates (line >= 85%,
branch >= 75%).

### send-mail endpoint coverage (AC-02, AC-03, AC-07)
- valid -> 202 ok:true data:null; invocations status,send-mail
- no recipients -> 400 INVALID_REQUEST, no send-mail invocation
- invalid contentType -> 400 INVALID_REQUEST, no send-mail invocation
- bridge not ready (starting) -> 409 BRIDGE_NOT_READY, no send-mail invocation
- runner failure (InternalError) -> 502
- BuildSendMail arg sequence: verb send-mail + ordered --key value incl JSON recipient arrays
- BuildSendMail default save-to-sent-items=true, empty cc/bcc arrays "[]"

## File-size check (all < 500)
- MailRoutes.cs = 149; Program.cs = 436; HostAdapterCommandBuilder.cs = 115; HostAdapterResponses.cs = 136.
