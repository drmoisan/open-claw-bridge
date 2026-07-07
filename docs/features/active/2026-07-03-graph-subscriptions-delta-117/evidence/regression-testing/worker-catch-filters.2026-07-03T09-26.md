# Regression Evidence — Worker Loop Catch-Filter Broadening (Remediation Cycle 1, Issue #117, fix item 5 / CR-117-03)

Timestamp: 2026-07-03T09-26
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~WorkerTests"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 43, Skipped: 0, Total: 43 (OpenClaw.Core.Tests.dll) — all worker suites (NotificationDispatchWorkerTests, SubscriptionRenewalWorkerTests, DeltaReconciliationWorkerTests) including the pre-existing shutdown/cancellation and failure-continuation tests, proving stop-path behavior is unchanged.
- Production changes (verified `git diff --numstat`: 1 insertion / 1 deletion per file; `dotnet build` exit 0, 0 warnings / 0 errors):
  - `NotificationDispatchWorker.cs` line 59, `SubscriptionRenewalWorker.cs` line 44, `DeltaReconciliationWorker.cs` line 46: `catch (Exception ex) when (ex is not OperationCanceledException)` -> `catch (Exception ex) when (!stoppingToken.IsCancellationRequested)`.
- New tests (one per worker, all deterministic via FakeTimeProvider / cooperative yields, no real waits):
  - `NotificationDispatchWorkerTests.Loop_continues_with_warning_when_the_inner_call_throws_TaskCanceledException_without_stop_requested`
  - `SubscriptionRenewalWorkerTests.Loop_continues_with_warning_when_the_sweep_throws_TaskCanceledException_without_stop_requested`
  - `DeltaReconciliationWorkerTests.Loop_continues_with_warning_when_the_reconcile_throws_TaskCanceledException_without_stop_requested`
- Each asserts a Warning log and that a subsequent iteration executes (next item processed / next sweep renews / next tick reconciles), then stops the worker cleanly.
- Implementation note: Moq cannot proxy the internal `ISubscriptionStore` (no `InternalsVisibleTo("DynamicProxyGenAssembly2")`), so the renewal-worker test uses a hand-rolled throw-once decorator over `FakeSubscriptionStore` inside the test file.
- File sizes: NotificationDispatchWorkerTests.cs 281 lines, SubscriptionRenewalWorkerTests.cs 344 lines, DeltaReconciliationWorkerTests.cs 243 lines — all under 500.
