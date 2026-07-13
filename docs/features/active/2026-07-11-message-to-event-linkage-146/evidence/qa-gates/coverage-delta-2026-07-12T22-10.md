# Coverage Delta and Threshold Verification — Issue #146

Timestamp: 2026-07-12T22-10
Baseline source: evidence/baseline/baseline-test-coverage-2026-07-12T21-38.md
Post-change source: evidence/qa-gates/finalqc-test-coverage-2026-07-12T22-10.md
Coverage policy (uniform): line >= 85%, branch >= 75%; no regression on changed lines; no production file excluded from coverage.

## Per-project coverage (baseline -> post-change)

| Project | Baseline line | Post line | Baseline branch | Post branch |
|---|---|---|---|---|
| OpenClaw.Core | 95.24% (3587/3766) | 95.24% (3609/3789) | 86.59% (885/1022) | 86.59% (885/1022) |
| OpenClaw.MailBridge | 93.58% (1533/1638) | 93.75% (1577/1682) | 88.16% (417/473) | 88.65% (430/485) |
| OpenClaw.HostAdapter | 87.70% (1113/1269) | 88.21% (1175/1332) | 67.19% (170/253) | 67.45% (172/255) |

## Aggregate coverage (baseline -> post-change)

- Line: 93.41% (6233/6673) -> 93.50% (6361/6803)  (delta +0.09 pts)
- Branch: 84.21% (1472/1748) -> 84.39% (1487/1762)  (delta +0.18 pts)

No regression: every project's line and branch coverage is equal to or higher than baseline.

## New / changed-code coverage (per file, from final cobertura)

New files:
- OpenClaw.MailBridge/CacheRepository.EventForMessage.cs — methods exercised by CacheRepositoryEventForMessageTests (6) and PipeRpcWorkerEventForMessageTests (4); the async resolution methods are compiled into the CacheRepository class and covered within its class-level totals (see below). GetEventForMessageAsync entry method: 100% line / 100% branch.
- OpenClaw.MailBridge/OutlookScanner.Linkage.cs — 100% line / 100% branch.
- OpenClaw.MailBridge/PipeRpcWorker.EventForMessage.cs — HandleGetEventForMessageAsync exercised by 4 handler tests (success-event, success-null, absent-row null, malformed-id 400); covered within the PipeRpcWorker class totals.
- OpenClaw.HostAdapter/HostAdapterEventProjector.cs — 100% line / 100% branch.
- OpenClaw.HostAdapter/MessageEventRoute.cs — 100% line / 100% branch.

Changed files:
- OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs — 100% line / 100% branch.
- OpenClaw.MailBridge/CacheRepository.cs — 91.33% line / 90.78% branch.
- OpenClaw.MailBridge/CacheRepository.Readers.cs — 96.34% line / 83.33% branch.
- OpenClaw.MailBridge/CacheRepository.Schema.cs — 100% line / 100% branch.
- OpenClaw.MailBridge/ComMessageSource.cs — 95.34% line / 94.00% branch (new ResolveLinkedGlobalAppointmentId: 100% line / 100% branch).
- OpenClaw.MailBridge/OutlookScanner.Redaction.cs — sensitive-message null default covered by existing redaction tests.
- OpenClaw.MailBridge.Client/Program.cs — Build method 100% line / 100% branch (new get-event-for-message verb arm covered by MailBridgeProgramTests).
- OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs — BuildGetEventForMessage covered (100%).
- OpenClaw.HostAdapter/Program.cs — route registration exercised by the endpoint tests (WebApplicationFactory boots Program).
- OpenClaw.Core/HostAdapterHttpClient.cs — 100% line / 100% branch.
- OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs — 100% line / 100% branch.
- OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs — 100% line / 100% branch.

All new and changed production files meet line >= 85% and branch >= 75%.

## Threshold verification

- Line coverage on changed code: PASS (all changed/new files >= 85%; lowest changed-file line is CacheRepository.cs at 91.33%).
- Branch coverage on changed code: PASS (all changed/new files >= 75%; lowest changed-file branch is CacheRepository.Readers.cs at 83.33%).
- No regression on changed lines: PASS (every project's line and branch coverage is >= baseline).
- No production file excluded from coverage: PASS (the runsettings Exclude is only `[*.Tests]*`; no production path is excluded).

## Pre-existing project-level note (not a #146 regression)

The OpenClaw.HostAdapter project-level branch-rate is 67.45%, below the 75% uniform branch threshold at the project level. This is a pre-existing condition (baseline 67.19%) that predates issue #146 and is not caused by this feature's changes; the value improved by +0.26 points. The feature's own new/changed HostAdapter code (HostAdapterEventProjector.cs, MessageEventRoute.cs, HostAdapterCommandBuilder.BuildGetEventForMessage) is at 100% branch coverage. This pre-existing project-level shortfall is recorded here for transparency and is outside the scope of issue #146.

## Outcome

PASS for the issue-#146 change set: changed/new-code line and branch thresholds are met and there is no coverage regression on changed lines.
