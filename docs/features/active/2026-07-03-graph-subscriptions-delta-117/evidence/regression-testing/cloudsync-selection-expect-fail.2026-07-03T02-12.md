# [expect-fail] CloudSync Selection Tests — Red Before Program.cs Guard (P6-T5)

Timestamp: 2026-07-03T02-12
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CloudSyncSelectionTests"
EXIT_CODE: 1
Output Summary:
- `OptIn_MapsTheRouteAndRegistersTheThreeWorkers` FAILED as expected: "Expected response.StatusCode to be HttpStatusCode.OK {value: 200} because the opt-in composition root maps the webhook handshake, but found HttpStatusCode.NotFound {value: 404}." The D-6 opt-in guard does not yet exist in `Program.cs`, so `POST /graph/notifications` is unmapped and no CloudSync workers are registered.
- `FlagAbsent_NotificationsRouteIsNotMappedAndNoCloudSyncWorkersRun` PASSED: the flag-absent composition root is unchanged (404, no CloudSync hosted services).
- Totals: Failed 1, Passed 1, Total 2.
- This is the required fail-before evidence for the `[expect-fail]` task; pass-after evidence follows the P6-T6 Program.cs change (see `cloudsync-selection-pass-after.<ts>.md`).
