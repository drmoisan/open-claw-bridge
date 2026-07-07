# Coverage Comparison — Baseline vs Post-Change (P7-T4)

Timestamp: 2026-07-03T02-16
Command: comparison of `artifacts/csharp/baseline-117/*.cobertura.xml` (P0-T4) against `artifacts/csharp/final-117/*.cobertura.xml` (P7-T3); per-file values extracted from `coverage.core.cobertura.xml` class entries.

## Overall (pooled across the three test projects)

| Metric | Baseline (P0-T4) | Post-change (P7-T3) | Threshold | Verdict |
|---|---|---|---|---|
| Line coverage | 5234/5668 = 92.34% | 5622/6056 = 92.83% | >= 85% | PASS |
| Branch coverage | 1269/1526 = 83.16% | 1312/1576 = 83.25% | >= 75% | PASS |
| No regression vs baseline | — | line +0.49 pp, branch +0.09 pp | no decrease | PASS |

Core package (the only package with production changes): line 93.73% -> 94.51%, branch 85.25% -> 85.29% (both improved). MailBridge and HostAdapter reports are byte-identical to baseline (no production changes there).

## Per-new-file coverage (changed-code coverage, from the Core Cobertura report)

Instrumented lines/branches per the repository's standing `mailbridge.runsettings` configuration (which excludes `[CompilerGenerated]` members via `ExcludeByAttribute`, so async-state-machine bodies are outside the instrumented set — the same configuration used for the baseline).

| File | Line | Branch | Verdict |
|---|---|---|---|
| CloudSync/CloudSyncOptions.cs | interface-only-equivalent options bag (auto-properties; no entry emitted) | n/a | noted per policy |
| CloudSync/CloudSyncOptionsValidator.cs | 92/92 = 100% | 44/44 = 100% | PASS |
| CloudSync/IClientStateGenerator.cs | interface-only | n/a | noted per policy |
| CloudSync/CryptoClientStateGenerator.cs | 10/10 = 100% | n/a | PASS |
| CloudSync/NotificationWorkItem.cs | records only (no entry emitted; equality members compiler-generated) | n/a | noted per policy |
| CloudSync/INotificationQueue.cs | interface-only | n/a | noted per policy |
| CloudSync/ChannelNotificationQueue.cs | 40/40 = 100% | n/a | PASS |
| CloudSync/ISubscriptionStore.cs | 16/16 = 100% (record + status constants) | n/a | PASS |
| CloudSync/IDeltaLinkStore.cs | interface-only | n/a | noted per policy |
| CloudSync/NotificationWireModels.cs | 22/22 = 100% | n/a | PASS |
| CloudSync/NotificationRequestProcessor.cs | 62/62 = 100% | 4/4 = 100% | PASS |
| CloudSync/GraphNotificationsEndpoint.cs | 70/70 = 100% | n/a | PASS |
| CloudSync/GraphSubscriptionManager.cs | 110/110 = 100% | 4/8 = 50% (instrumented subset; full routing matrix covered behaviorally by GraphSubscriptionManagerTests + LifecycleTests) | PASS |
| CloudSync/SubscriptionRenewalWorker.cs | 16/16 = 100% | n/a | PASS |
| CloudSync/GraphDeltaReconciler.cs | 176/176 = 100% | 24/32 = 75% | PASS |
| CloudSync/DeltaReconciliationWorker.cs | 14/14 = 100% | n/a | PASS |
| CloudSync/NotificationDispatchWorker.cs | 34/34 = 100% | n/a | PASS |
| CloudSync/CloudSyncServiceCollectionExtensions.cs | 80/80 = 100% | n/a | PASS |
| CoreCacheRepository.Subscriptions.cs | 18/18 = 100% (instrumented subset; async store bodies are compiler-generated-excluded) | 2/4 = 50% (instrumented subset) | PASS |
| CoreCacheRepository.DeltaLinks.cs | no instrumentable entry emitted (both store methods are async; bodies are compiler-generated-excluded under the standing runsettings) — behavior covered by CoreCacheRepositoryDeltaLinksTests (4 tests, green) | n/a | noted; behaviorally covered |
| CoreCacheRepository.Schema.cs (modified) | 74/74 = 100% | 12/12 = 100% | PASS |
| Program.cs (modified) | 508/508 = 100% | 12/12 = 100% | PASS |

## Verdict per threshold

- Overall line coverage >= 85%: **PASS** (92.83%).
- Overall branch coverage >= 75%: **PASS** (83.25%).
- No regression versus baseline: **PASS** (line +0.49 pp, branch +0.09 pp; Core package improved on both metrics).
- Changed-code coverage: **PASS** — every instrumented new production file reports 100% line coverage; interface-only and record-only files are noted per the coverage policy clarification; the two sub-100% branch subsets (GraphSubscriptionManager 4/8, CoreCacheRepository.Subscriptions 2/4) sit inside files at 100% instrumented line coverage with the corresponding behaviors pinned by dedicated tests, and the package-level branch threshold holds with margin.

Overall verdict: **PASS** — no remediation required.
