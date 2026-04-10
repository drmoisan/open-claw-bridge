Timestamp: 2026-04-06T21-11
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~Bridge_application_load_settings_should_return_default_settings_when_store_is_missing_without_touching_disk"
EXIT_CODE: 1
Output Summary: The new regression test failed as expected because `BridgeApplication.LoadSettings` still calls `Directory.CreateDirectory` on the `memory://bridge.settings.json` path and throws `System.IO.IOException` instead of returning `BridgeSettings.Default`.
