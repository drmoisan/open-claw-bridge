# File Size Check — 500-Line Cap (P5-T2)

Timestamp: 2026-07-02T11-16
Command: wc -l <all touched production and test files>
EXIT_CODE: 0
Output Summary: All seven touched files are under the 500-line cap — PASS.

| File | Lines | <= 500 |
|---|---|---|
| src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs | 143 | PASS |
| src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs | 243 | PASS |
| src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs | 20 | PASS |
| tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs | 415 | PASS |
| tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperTests.cs | 425 | PASS |
| tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperPropertyTests.cs | 107 | PASS |
| tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs | 310 | PASS |
