# Remediation Baseline — Changed .cs File Sizes

Timestamp: 2026-06-16T07-57
Command: `for f in $(git diff --name-only 0cb7de6..HEAD | grep -E '\.cs$'); do wc -l "$f"; done | sort -rn`
EXIT_CODE: 0

Output Summary: One changed `.cs` file exceeds the 500-line limit: `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs` at 573 lines (R-1). All other changed `.cs` files are <= 500 (next largest is `src/OpenClaw.MailBridge/OutlookScanner.cs` at 465). The 573-line starting state is confirmed.

```
573 tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs   <-- OVER LIMIT (R-1)
465 src/OpenClaw.MailBridge/OutlookScanner.cs
438 src/OpenClaw.MailBridge/PipeRpcWorker.cs
436 src/OpenClaw.HostAdapter/Program.cs
285 tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs
270 tests/OpenClaw.HostAdapter.Tests/HostAdapterSendMailTests.cs
253 src/OpenClaw.MailBridge.Client/Program.cs
250 src/OpenClaw.Core/HostAdapterHttpClient.cs
222 tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs
220 tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs
188 tests/OpenClaw.MailBridge.Tests/OutlookComMailSenderIntegrationTests.cs
172 src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs
171 src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs
170 tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs
163 src/OpenClaw.MailBridge/OutlookComMailSender.cs
161 tests/OpenClaw.Core.Tests/HostAdapterHttpClientSendMailTests.cs
150 tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.SendMail.cs
149 src/OpenClaw.HostAdapter/MailRoutes.cs
144 src/OpenClaw.MailBridge/SendMailRpcHandler.cs
136 src/OpenClaw.HostAdapter/HostAdapterResponses.cs
133 src/OpenClaw.MailBridge/BridgeApplication.cs
131 tests/OpenClaw.HostAdapter.Tests/MailContractsTests.cs
115 src/OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs
69 tests/OpenClaw.MailBridge.Tests/OutlookComMailSenderGuardTests.cs
50 src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs
50 src/OpenClaw.HostAdapter.Contracts/MailContracts.cs
47 tests/OpenClaw.MailBridge.Tests/OutlookApplicationProviderTests.cs
44 src/OpenClaw.MailBridge/IOutlookMailSender.cs
34 src/OpenClaw.MailBridge/OutlookApplicationProvider.cs
32 tests/OpenClaw.MailBridge.Tests/SendMailTestDoubles.cs
26 src/OpenClaw.MailBridge/IOutlookApplicationProvider.cs
```
