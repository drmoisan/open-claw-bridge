# Regression Evidence — ReadSubscription Expiration Fail-Fast (Remediation Cycle 1, Issue #117, fix item 3(a))

Timestamp: 2026-07-03T09-14
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CoreCacheRepositorySubscriptionsTests"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9 (OpenClaw.Core.Tests.dll)
- New test `GetSubscriptionAsync_with_unparseable_expiration_throws_naming_the_id_and_column` passes: a second `SqliteConnection` on the same shared-cache connection string corrupts `expiration_utc` to `'not-a-timestamp'`; `GetSubscriptionAsync` throws `InvalidOperationException` whose message contains `sub-1` and `expiration_utc`.
- Production change (P2-T1): `ReadSubscription` in `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` no longer contains the `DateTimeOffset.MinValue` sentinel (grep count 0); it now reads `subscription_id` first and throws on an unparseable expiration. `dotnet build OpenClaw.MailBridge.sln` exit 0, 0 warnings / 0 errors.
- All 8 pre-existing subscription-store tests pass unmodified (round-trip, unknown-id, replace, status update, delete idempotency, schema-ensure idempotency, double InitializeAsync, restart survival).
