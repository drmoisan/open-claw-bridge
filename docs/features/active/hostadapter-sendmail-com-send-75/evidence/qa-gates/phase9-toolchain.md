# Phase 9 — Toolchain Gate (Integration excluded)

Timestamp: 2026-06-16T09-02

Phase 9 adds the gated real-COM integration tests. The non-integration toolchain gate is green.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 191 files. No CSharpier changes beyond the new test file.

## Stage 2 — Lint / Analyzers
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: 0 Error(s), 0 Warning(s).

## Stage 3 — Type-check (nullable)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: 0 Error(s).

## Stage 4 — Architecture-boundary tests
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 2. COM remains confined to OpenClaw.MailBridge.

## Stage 5 — Test + Coverage (Integration excluded)
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"
EXIT_CODE: 0
Output Summary: HostAdapter.Tests 100 passed; Core.Tests 210 passed; MailBridge.Tests 277 passed
(3 pre-existing platform/publish skips). The 2 [TestCategory("Integration")] COM tests are excluded
by the filter (gated-skip recorded in evidence/regression-testing/integration-com-send.md).
