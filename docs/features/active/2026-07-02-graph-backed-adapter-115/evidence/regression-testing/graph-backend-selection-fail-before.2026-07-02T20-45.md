# Graph Backend Selection — Fail-Before / Pass-After Evidence (P6-T3 / P6-T5)

## Fail-before record (P6-T3, [expect-fail])

Timestamp: 2026-07-02T20-45
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --filter "FullyQualifiedName~GraphBackendSelectionTests"
EXIT_CODE: 1
Output Summary:
- Total 2 tests: 1 passed, 1 failed (the expected red-before state).
- PASSED: `DefaultPath_GraphAdapterAbsent_ResolvesHostAdapterHttpClient` — with the flag absent the composition root resolves `HostAdapterHttpClient` (default path unchanged).
- FAILED (expected): `OptInPath_GraphAdapterEnabled_ResolvesGraphHostAdapterClient` — "Expected type to be OpenClaw.Core.CloudGraph.GraphHostAdapterClient because OpenClaw:GraphAdapter:Enabled=true selects the Graph backend, but found OpenClaw.Core.HostAdapterHttpClient."
- Cause: `src/OpenClaw.Core/Program.cs` has no backend-selection conditional block yet (added in P6-T4).

## Pass-after record (P6-T5)

Timestamp: 2026-07-02T20-47
Command: dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --filter "FullyQualifiedName~GraphBackendSelectionTests"
EXIT_CODE: 0
Output Summary:
- Total 2 tests: 2 passed, 0 failed.
- `DefaultPath_GraphAdapterAbsent_ResolvesHostAdapterHttpClient` — PASS (default path unchanged after the P6-T4 conditional).
- `OptInPath_GraphAdapterEnabled_ResolvesGraphHostAdapterClient` — PASS (the P6-T4 backend-selection block in `Program.cs` routes `Enabled=true` to `AddGraphHostAdapterClient`).
- Note: the opt-in test supplies configuration via `IWebHostBuilder.UseSetting` rather than `ConfigureAppConfiguration` because factory-added configuration callbacks run after `Program.cs` reads `builder.Configuration` under minimal hosting; the intent (configuration-driven opt-in) is unchanged.

