# Remediation QA Gate — Post-Split File Sizes

Timestamp: 2026-06-16T08-02
Command: `for f in $(git diff --name-only 0cb7de6..HEAD | grep -E '\.cs$'); do wc -l "$f"; done | sort -rn` (plus explicit `wc -l` of the two new untracked partials)
EXIT_CODE: 0

Output Summary: Every changed `.cs` file is now <= 500 lines. The R-1 violation is resolved. The largest changed test file after the split is `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.RunAsync.cs` at 268 lines; the largest changed file overall is the pre-existing `src/OpenClaw.MailBridge/OutlookScanner.cs` at 465 lines (unchanged by this remediation).

`MailBridgeProgramTests.cs` was split into three partial-class files (P1-T1 extracted send-mail; P1-T2 was applied because the base was still > 500 after P1-T1, extracting the RunAsync exit-code-mapping section):
- `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs` — 264 lines (Parse + Build sections; `[TestClass] public partial class`)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.SendMail.cs` — 71 lines (two `Build_WhenCommandIsSendMail_*` tests; `public partial class`)
- `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.RunAsync.cs` — 268 lines (RunWithResponse helper + all `RunAsync_*` tests; `public partial class`)

No test methods were added, deleted, weakened, or skipped; methods were moved verbatim with their attributes. Combined original 573 lines = 264 + 71 + 268 minus the duplicated using/namespace/class headers, consistent with a behavior-preserving split.

Tracked changed `.cs` files, largest first (all <= 500):
```
465 src/OpenClaw.MailBridge/OutlookScanner.cs
438 src/OpenClaw.MailBridge/PipeRpcWorker.cs
436 src/OpenClaw.HostAdapter/Program.cs
285 tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs
270 tests/OpenClaw.HostAdapter.Tests/HostAdapterSendMailTests.cs
264 tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs
253 src/OpenClaw.MailBridge.Client/Program.cs
250 src/OpenClaw.Core/HostAdapterHttpClient.cs
... (all remaining <= 222)
```

New untracked partials:
```
268 tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.RunAsync.cs
 71 tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.SendMail.cs
```
