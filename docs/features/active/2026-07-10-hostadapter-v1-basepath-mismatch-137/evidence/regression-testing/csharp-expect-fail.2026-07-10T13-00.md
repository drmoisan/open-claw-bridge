Timestamp: 2026-07-10T13-00

Command: dotnet test OpenClaw.MailBridge.sln --filter "FullyQualifiedName~CoreHostAdapterBaseUrlFallbackTests"

EXIT_CODE: 1

Output Summary: [expect-fail] Targeted run of the new `BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment` test against the current (pre-fix) `src/OpenClaw.Core/Program.cs` fails as expected: `Failed: 1, Passed: 0, Skipped: 0, Total: 1`. Assertion failure message: `Did not expect resolvedBaseUrl "http://host.docker.internal:4319/v1/" to contain "/v1".` This confirms the test correctly exercises the `Program.cs:16-18` `PostConfigure` blank-config fallback branch (via a `WithWebHostBuilder`/`ConfigureAppConfiguration` override binding `OpenClaw:HostAdapter:BaseUrl` to an empty string) and that the current hardcoded fallback resolves to a value containing `/v1`.
