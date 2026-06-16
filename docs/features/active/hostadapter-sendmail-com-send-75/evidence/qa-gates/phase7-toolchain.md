# Phase 7 — Toolchain Gate

Timestamp: 2026-06-16T08-25

Phase 7 implements `HostAdapterHttpClient.SendMailAsync` via a new `PostAsync<TBody,TResponse>`
helper (POST + JSON body, token via the existing `TokenReader` seam, CONFIGURATION_ERROR
short-circuit with no HTTP call). This resolves the Core CS0535 break, so the full solution now
builds. A pre-existing `OpenClaw.Core.Agent.SendMailRequest` collides by name with the new
`OpenClaw.HostAdapter.Contracts.SendMailRequest` in three test files that import both namespaces;
disambiguated with a `using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;` alias in each
(the usages intend the agent type). No production code change was required (the affected production
files already resolved unambiguously). Not a contract breaking change: both records coexist.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 188 files. No CSharpier changes beyond new/edited files.

## Stage 2 — Lint / Analyzers (full solution)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). (First clean full-solution analyzer build
since Phase 2; the four CS0104 ambiguity errors were resolved by the alias.)

## Stage 3 — Type-check (nullable, full solution)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Error(s).

## Stage 4 — Architecture-boundary tests
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 2, Skipped 0. `OpenClaw.Core` depends only on
`OpenClaw.HostAdapter.Contracts` (Rule 6); no new ProjectReference edge; COM remains confined to
OpenClaw.MailBridge.

## Stage 5 — Test + Coverage (Core.Tests)
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 210, Skipped 0. +4 send-mail tests vs baseline. Subset
cobertura (default settings, no mailbridge.runsettings exclusions) line 83.45% / branch 68.43% over
the full Core+Blazor assembly closure; the authoritative gate uses `mailbridge.runsettings`
exclusions and the full-solution run in P11-T5 (baseline combined was line 90.21% / branch 78.92%).

### send-mail Core client coverage (AC-03, AC-04)
- POST to users/me/sendMail (method + absolute path)
- Graph-shaped camelCase body serialization (message.subject/body.contentType/toRecipients/address/saveToSentItems)
- 202 -> ok:true, data:null
- missing token -> CONFIGURATION_ERROR with NO HTTP call (handler not invoked)

## File-size check
- src/OpenClaw.Core/HostAdapterHttpClient.cs = 250 lines (< 500).
