Timestamp: 2026-07-10T13-15

Command: dotnet test OpenClaw.MailBridge.sln --filter "FullyQualifiedName~CoreHostAdapterBaseUrlFallbackTests"

EXIT_CODE: 0

Output Summary: Targeted re-run of the P1-T3 test (`BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment`) against the fixed `src/OpenClaw.Core/Program.cs` (blank-config fallback now `"http://host.docker.internal:4319/"`, no `/v1`) now passes: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`. Confirms AC-6.
