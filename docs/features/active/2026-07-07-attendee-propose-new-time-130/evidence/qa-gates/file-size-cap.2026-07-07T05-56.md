# QA Gate — 500-Line File-Size Cap (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `wc -l` over every new/modified production and new test file
EXIT_CODE: 0

Output Summary: All 15 touched files are <= 500 lines; maximum is 352. PASS.

| Lines | File |
|---|---|
| 352 | tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientProposeNewTimeTests.cs |
| 344 | src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs (modified) |
| 338 | tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeTests.cs |
| 315 | src/OpenClaw.Core/HostAdapterHttpClient.cs (modified) |
| 296 | tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeEdgeTests.cs |
| 255 | src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs (new) |
| 249 | src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs (modified) |
| 213 | tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerProposeNewTimeIntentPropertyTests.cs |
| 213 | src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs (modified) |
| 181 | tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceProposeNewTimeTests.cs |
| 136 | src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs (modified) |
| 91 | tests/OpenClaw.Core.Tests/HostAdapterHttpClientProposeNewTimeTests.cs |
| 67 | src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs (new) |
| 63 | src/OpenClaw.Core/Agent/SentActionKey.cs (modified) |
| 55 | src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs (modified) |

Maximum: 352 (GraphHostAdapterClientProposeNewTimeTests.cs). The pre-existing 616-line
`HostAdapterHttpClientTests.cs` is excluded per plan scope — it is not touched by this feature.
