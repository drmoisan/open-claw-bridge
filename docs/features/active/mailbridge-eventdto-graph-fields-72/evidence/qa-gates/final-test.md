# Final QA — Test + Coverage

Timestamp: 2026-06-13T03-26

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary:
- Tests PASS. Failed: 0, Passed: 459 (HostAdapter.Tests 71, Core.Tests 184, MailBridge.Tests 204), Skipped: 3, Total: 462.
- Skipped (platform/publish-gated, same as baseline): Com_active_object_create_and_logon_should_throw_on_non_windows; PublishOutput_BridgeDirectory_ContainsBridgeExecutable; PublishOutput_ClientDirectory_ContainsClientExecutable.

## Post-change coverage (cobertura)

| Module | Line | Branch |
|---|---|---|
| OpenClaw.MailBridge.Tests | 93.55% (973/1040) | 85.47% (259/303) |
| OpenClaw.Core.Tests | 89.09% (1430/1605) | 77.59% (329/424) |

Threshold (line >= 85%, branch >= 75%): PASS for both modules.

## Per-changed-file coverage (no regression on changed lines)

| File | Line | Branch |
|---|---|---|
| `OpenClaw.MailBridge.Contracts/Models/EventDto` (BridgeContracts.cs) | 100% | 100% |
| `OpenClaw.MailBridge.Contracts/Models/EventSensitivityLabel.cs` | 100% | 100% |
| `OpenClaw.MailBridge/OutlookScanner.cs` | 92.12% | 88.63% |
| `OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` | 100% | 100% |
| `OpenClaw.MailBridge/ResponseShaper.cs` | 100% | 100% |
| `OpenClaw.MailBridge/CacheRepository.cs` | 90.0% | 83.82% |
| `OpenClaw.MailBridge/CacheRepository.Readers.cs` | 96.1% | 83.33% |
| `OpenClaw.MailBridge/CacheRepository.Schema.cs` | 100% | 100% |
| `OpenClaw.Core/CoreCacheRepository.cs` | 97.44% | 91.83% |
| `OpenClaw.Core/CoreCacheRepository.Schema.cs` | 100% | 100% |
| `OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` | 92.7% | 80.0% |

All changed/new files are at or above the 85%/75% thresholds; the new files added by #72 are at 100% line/branch.
