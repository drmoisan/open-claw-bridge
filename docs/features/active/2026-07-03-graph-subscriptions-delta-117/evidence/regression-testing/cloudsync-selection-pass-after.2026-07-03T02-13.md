# CloudSync Selection Tests — Pass After Program.cs Guard (P6-T7)

Timestamp: 2026-07-03T02-13
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CloudSyncSelectionTests"
EXIT_CODE: 0
Output Summary:
- All selection tests green: Failed 0, Passed 2, Total 2.
- `OptIn_MapsTheRouteAndRegistersTheThreeWorkers` now passes: with `OpenClaw:CloudSync:Enabled=true` plus valid Graph/CloudAuth/CloudSync settings the handshake returns 200 and the three CloudSync workers (SubscriptionRenewalWorker, DeltaReconciliationWorker, NotificationDispatchWorker) are registered.
- `FlagAbsent_NotificationsRouteIsNotMappedAndNoCloudSyncWorkersRun` still passes: the flag-absent composition root maps no CloudSync route and registers no CloudSync workers.
- Pairs with the fail-before evidence `cloudsync-selection-expect-fail.2026-07-03T02-12.md`.
