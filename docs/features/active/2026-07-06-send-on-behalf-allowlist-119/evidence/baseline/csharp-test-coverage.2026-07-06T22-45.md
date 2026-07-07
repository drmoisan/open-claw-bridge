# Baseline — C# Test + Architecture-Boundary + Coverage

Timestamp: 2026-07-06T22-45
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-2026-07-06T22-45`
EXIT_CODE: 0

Output Summary: PASS. All test projects green.

- OpenClaw.Core.Tests: Failed 0, Passed 616, Skipped 0, Total 616 (includes `CloudGraphArchitectureBoundaryTests` and `CloudGraphContractParityTests`).
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352 (5 skipped are COM/publish-output Windows-desktop tests unrelated to this feature).
- Aggregate: 1063 passed, 5 skipped, 0 failed.

Numeric coverage headline (OpenClaw.Core assembly — the module under change; Cobertura `coverage.cobertura.xml`, results id `4457f48c-...`):

- Line coverage: 93.73% (2588 / 2761 lines).
- Branch coverage: 85.25% (682 / 800 branches).

Baseline coverage of the files this feature will touch (per-class, OpenClaw.Core Cobertura):

- `CloudGraph/GraphAdapterOptionsValidator.cs`: line 100%, branch 100%.
- `CloudGraph/GraphHostAdapterClient.SendMail.cs`: line 100%, branch 100%.
- `CloudGraph/GraphAdapterOptions.cs`: plain auto-property options bag (no executable branches; not separately reported as a coverage class).
- `SendOnBehalfAuthorizer.cs`: not yet present (new file).

Other assemblies (context only): OpenClaw.HostAdapter run line 93.58% / branch 88.16%; OpenClaw.MailBridge run line 87.70% / branch 67.19%. The module under change (OpenClaw.Core) is the coverage target for this T1 feature.

Raw intermediates: `artifacts/csharp/baseline-2026-07-06T22-45/` (Cobertura XML per test project plus the MailBridge native `.coverage`).
