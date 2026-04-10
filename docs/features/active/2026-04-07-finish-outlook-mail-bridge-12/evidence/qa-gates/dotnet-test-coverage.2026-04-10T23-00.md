# dotnet test + Coverage — QA Gate

Timestamp: 2026-04-10T23-00
Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura`
EXIT_CODE: 0
Output Summary: 87 total tests — 86 passed, 1 skipped (Com_active_object_create_and_logon_should_throw_on_non_windows), 0 failed. Line coverage: 1279 / 1524 = 83.9%. Coverage did not regress below the remediation baseline of 83.8% (P0-T6).
