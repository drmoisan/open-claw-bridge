Timestamp: 2026-04-06T21-12
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~Bridge_application_run_async_should_return_two_for_invalid_settings_from_in_memory_store"
EXIT_CODE: 1
Output Summary: The new regression test failed as expected because `BridgeApplication.RunAsync` still routes through disk-bound `LoadSettings`, which throws `System.IO.IOException` for the `memory://invalid.json` path instead of returning exit code `2`.
