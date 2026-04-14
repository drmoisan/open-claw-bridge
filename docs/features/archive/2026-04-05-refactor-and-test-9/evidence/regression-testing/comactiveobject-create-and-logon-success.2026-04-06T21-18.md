Timestamp: 2026-04-06T21-18
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~Com_active_object_create_and_logon_should_return_core_result_when_platform_probe_is_true"
EXIT_CODE: 1
Output Summary: The new regression test failed as expected because `CreateAndLogonOutlook` still calls `OperatingSystem.IsWindows()` directly, so the test seam's platform-probe method was never invoked and `PlatformProbeCalls` remained `0`.
