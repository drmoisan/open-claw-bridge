# Phase 2 — Toolchain Gate

Timestamp: 2026-06-16T07-18

Phase 2 adds `IHostAdapterClient.SendMailAsync` (interface) and the `SendMail*` DTOs. The
production implementer `HostAdapterHttpClient` (OpenClaw.Core) is intentionally implemented in
Phase 7, so the full-solution build carries one expected break (CS0535 on
`HostAdapterHttpClient`) until P7-T2. No `Mock<IHostAdapterClient>` test double requires a manual
stub (Moq auto-generates the new member), so no temporary stub was added. The contracts project
and the new `MailContractsTests` compile and pass independently.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 177 files. No CSharpier changes beyond the new/edited files.

## Stage 2 — Lint / Analyzers (full solution)
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 1 (expected; documented break)
Output Summary: 0 Warning(s), 1 Error(s). The only error is the expected
`HostAdapterHttpClient.cs(12,5): CS0535 ... does not implement IHostAdapterClient.SendMailAsync`,
resolved in P7-T2. No analyzer warnings.

## Stage 3 — Type-check (nullable) — HostAdapter.Tests scope (independent of Core)
Command: dotnet build tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). The contracts + MailContractsTests are
nullable-clean (a transient CS8602 introduced during authoring was fixed by repeating the
null-forgiving operator on the second dereference).

## Stage 4 — Architecture-boundary tests
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"
EXIT_CODE: 1 (expected; the Core.Tests build depends on Core which carries the CS0535 break)
Output Summary: Cannot build because Core has the documented CS0535 break (resolved P7-T2). No new
ProjectReference edge was added by Phase 2; HostAdapter.Contracts continues to depend only on
MailBridge.Contracts and uses only BCL types (no COM, no OpenClaw.Core using). Architecture is
re-verified green at P7-T4 once Core compiles.

## Stage 5 — Test + Coverage — contracts scope
Command: dotnet test tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj -c Debug --filter "FullyQualifiedName~MailContractsTests"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 4, Skipped 0. Round-trip + Graph-shape camelCase +
BCC-only + default-saveToSentItems(true) all pass.

## Acceptance disposition

Per P2-T5 acceptance, format passes; the contracts and MailContractsTests compile and pass; the
only solution break is the documented Core CS0535 to be resolved at P7-T2 (no temporary stub was
needed because the single implementer is the production class). Full-solution lint/nullable/
architecture/test gates are re-run green in Phases 5/7/8 and the final QA loop (P11).
