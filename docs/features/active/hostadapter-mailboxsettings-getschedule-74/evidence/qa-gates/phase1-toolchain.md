# Phase 1 — Toolchain Gate (DTO relocation)

Timestamp: 2026-06-13T10-30

All five stages run in order with restart-on-change semantics. The loop was restarted once:
the initial test run (stage 5) surfaced a pre-existing namespace-architecture test
(`AgentArchitectureBoundaryTests`) that over-broadly banned the relocated
`OpenClaw.HostAdapter.Contracts` package; that test was reconciled with the locked Design A
(see Deviation note below), and the full loop was rerun to a single clean pass.

## Stage 1 — Format
Command: csharpier format . ; csharpier check .
EXIT_CODE: 0
Output Summary: Formatted 160 files; `csharpier check .` reports "Checked 160 files" with 0 unformatted. Clean.

## Stage 2 — Lint / Analyzers
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s).

## Stage 3 — Nullable Type-Check
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s).

## Stage 4 — Architecture Verification
Command: grep -i ProjectReference on the three in-scope csproj files
EXIT_CODE: 0
Output Summary: ProjectReference edges unchanged from baseline (P0-T5). No new edges.
OpenClaw.HostAdapter.Contracts still depends only on OpenClaw.MailBridge.Contracts.
OpenClaw.HostAdapter does not reference OpenClaw.Core or the COM host. OpenClaw.Core depends
only on OpenClaw.HostAdapter.Contracts.

## Stage 5 — Test + Coverage
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: PASS. 475 passed, 0 failed, 3 skipped.
- OpenClaw.HostAdapter.Tests: 74 passed.
- OpenClaw.Core.Tests: 185 passed (was 184; +1 new architecture test).
- OpenClaw.MailBridge.Tests: 216 passed, 3 skipped.
Coverage (cobertura):
- Core: line-rate 0.8913 (89.13%), branch-rate 0.7759 (77.59%).
- HostAdapter: line-rate 0.8330 (83.30%), branch-rate 0.6028 (60.28%).
(The relocation moved DTO definitions from Core to HostAdapter.Contracts; Core line/branch
unchanged. HostAdapter line dipped slightly because the new SchedulingContracts.cs records add
lines that are not yet exercised until the Phase 4 route tests; this is restored in later
phases. The changed-code gate is verified at final QA, P9-T5/P9-T6.)

## Deviation (escalated; minimal mechanically-necessary reconciliation)
The plan did not enumerate `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs`.
Its banned-dependency list contained the prefix `"OpenClaw.HostAdapter"`, which (via
NetArchTest prefix matching) also banned `"OpenClaw.HostAdapter.Contracts"`. The locked
Design A relocates the D6 DTOs into `OpenClaw.HostAdapter.Contracts` and the
`ISchedulingService` contract (namespace `OpenClaw.Core.Agent`, non-runtime) returns them, so
the non-runtime deterministic surface must reference `OpenClaw.HostAdapter.Contracts`. This is
permitted by the authoritative project-graph boundary (.claude/rules/architecture-boundaries.md
Rule 6: OpenClaw.Core -> OpenClaw.HostAdapter.Contracts) and the edge already existed at
baseline. The test was reconciled by: (1) removing the over-broad `OpenClaw.HostAdapter` prefix
from the outright-ban list (which still bans OpenClaw.MailBridge, Outlook COM, and
System.Runtime.InteropServices), and (2) adding a new positive test
`DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation` that inspects actual
dependency namespaces and asserts every `OpenClaw.HostAdapter*` dependency is under
`OpenClaw.HostAdapter.Contracts` (so an accidental reference to the host implementation still
fails). The guard's protective intent is preserved while permitting the contracts package the
locked design requires. Full detail: evidence/regression-testing/scope-change-architecture-boundary-test.md.
