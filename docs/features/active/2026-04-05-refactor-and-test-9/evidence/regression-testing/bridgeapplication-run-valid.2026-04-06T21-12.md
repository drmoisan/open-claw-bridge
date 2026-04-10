Timestamp: 2026-04-06T21-12
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~Bridge_application_run_async_should_use_host_for_valid_settings_from_in_memory_store"
EXIT_CODE: 1
Output Summary: The new regression test failed as expected because `BridgeApplication.RunAsync` still calls disk-bound `LoadSettings`, which throws `System.IO.IOException` for the `memory://valid.json` path before the host can be built or run.
