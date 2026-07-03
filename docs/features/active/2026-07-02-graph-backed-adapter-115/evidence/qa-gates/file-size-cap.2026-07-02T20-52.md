# QA Gate — 500-Line File Cap (P8-T1)

Timestamp: 2026-07-02T20-52
Command: wc -l src/OpenClaw.Core/CloudGraph/*.cs tests/OpenClaw.Core.Tests/CloudGraph/*.cs src/OpenClaw.Core/Program.cs
EXIT_CODE: 0
Output Summary: PASS. 32 files measured; maximum line count 344 (tie: `src/OpenClaw.Core/Program.cs` and `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientMessagesTests.cs`); every file <= 500 lines. No pre-authorized split contingencies were needed.

Per-file table (lines, path):
| Lines | File |
|---|---|
| 58 | src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs |
| 69 | tests/OpenClaw.Core.Tests/CloudGraph/GraphBackendSelectionTests.cs |
| 70 | src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs |
| 79 | src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs |
| 81 | src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs |
| 95 | src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs |
| 113 | src/OpenClaw.Core/CloudGraph/GraphWireModels.cs |
| 118 | src/OpenClaw.Core/CloudGraph/GraphSchedulingMapper.cs |
| 131 | src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Calendar.cs |
| 143 | src/OpenClaw.Core/CloudGraph/GraphMessageMapper.cs |
| 151 | tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperTests.cs |
| 152 | tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs |
| 159 | tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphArchitectureBoundaryTests.cs |
| 165 | tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperPropertyTests.cs |
| 171 | tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperPropertyTests.cs |
| 172 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientStatusTests.cs |
| 175 | tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperTests.cs |
| 187 | tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorErrorMatrixTests.cs |
| 188 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientCalendarTests.cs |
| 195 | src/OpenClaw.Core/CloudGraph/GraphEventMapper.cs |
| 198 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSchedulingTests.cs |
| 208 | tests/OpenClaw.Core.Tests/CloudGraph/GraphSchedulingMapperTests.cs |
| 217 | tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs |
| 217 | tests/OpenClaw.Core.Tests/CloudGraph/GraphPayloadFixtures.cs |
| 220 | src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs |
| 260 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs |
| 269 | src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs |
| 273 | tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorTests.cs |
| 286 | tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs |
| 320 | tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorRetryTests.cs |
| 344 | src/OpenClaw.Core/Program.cs |
| 344 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientMessagesTests.cs |
