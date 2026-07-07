# Phase 0 Baseline 03 — Architecture-Boundary Tests (Issue #120)

Timestamp: 2026-07-06T23-11
Command: `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"` (run from repository root)
EXIT_CODE: 0

Output Summary: Passed. Failed: 0, Passed: 11, Skipped: 0, Total: 11
(OpenClaw.Core.Tests.dll). The MailBridge.Tests and HostAdapter.Tests assemblies reported
"No test matches the given testcase filter" (expected — the architecture-boundary tests
live in OpenClaw.Core.Tests). All 11 existing architecture-boundary assertions
(`CloudGraphArchitectureBoundaryTests` and any others matching the filter) pass at
baseline. The new `ScopeValidationArchitectureBoundaryTests` (plan P5-T4) will add to
this count in Phase 6.
