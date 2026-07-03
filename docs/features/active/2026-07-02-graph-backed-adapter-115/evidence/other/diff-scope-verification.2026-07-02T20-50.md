# Diff-Scope Verification (P7-T2)

Timestamp: 2026-07-02T20-50
Command: git fetch origin main; git merge-base HEAD origin/main; git diff --name-only <merge-base> HEAD; git status --porcelain=v1 --untracked-files=all
EXIT_CODE: 0
Output Summary:
- Merge base with origin/main: ffbb1a077ade1b41ac01d292a0eda948db0479eb. No commits exist on the branch yet (the orchestrator commits), so the committed diff is empty; the working-tree change set is authoritative.
- Full changed-file set (1 modified + 40 untracked), all confined to the allowed scope:
  - Modified: `src/OpenClaw.Core/Program.cs` (backend-selection conditional block only; allowed).
  - New production: 12 files under `src/OpenClaw.Core/CloudGraph/` (allowed).
  - New tests: 19 files under `tests/OpenClaw.Core.Tests/CloudGraph/` (allowed).
  - Feature folder: 9 files under `docs/features/active/2026-07-02-graph-backed-adapter-115/` (allowed).
- Zero changes under `src/OpenClaw.Core/Agent/`, `src/OpenClaw.HostAdapter/`, `src/OpenClaw.HostAdapter.Contracts/`, `src/OpenClaw.MailBridge/`, `src/OpenClaw.MailBridge.Contracts/`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, and zero diff to any `docker-compose*` file.

Full changed-file list:
```
 M src/OpenClaw.Core/Program.cs
?? docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-build.2026-07-02T20-04.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-format.2026-07-02T20-04.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-test-coverage.2026-07-02T20-04.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/phase0-instructions-read.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/regression-testing/graph-backend-selection-fail-before.2026-07-02T20-45.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/issue.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/plan.2026-07-02T19-38.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md
?? docs/features/active/2026-07-02-graph-backed-adapter-115/user-story.md
?? src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs
?? src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs
?? src/OpenClaw.Core/CloudGraph/GraphEventMapper.cs
?? src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Calendar.cs
?? src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs
?? src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs
?? src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs
?? src/OpenClaw.Core/CloudGraph/GraphMessageMapper.cs
?? src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs
?? src/OpenClaw.Core/CloudGraph/GraphSchedulingMapper.cs
?? src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs
?? src/OpenClaw.Core/CloudGraph/GraphWireModels.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphArchitectureBoundaryTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphBackendSelectionTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperPropertyTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientCalendarTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientMessagesTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSchedulingTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientStatusTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperPropertyTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphPayloadFixtures.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorErrorMatrixTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorRetryTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphSchedulingMapperTests.cs
?? tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs
```
(Raw command intermediates under `artifacts/csharp/` are gitignored and intentionally not part of the change set.)
