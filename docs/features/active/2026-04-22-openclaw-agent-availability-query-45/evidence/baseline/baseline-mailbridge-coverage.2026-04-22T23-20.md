# Baseline — targeted MailBridge coverage

Timestamp: 2026-04-22T23-20
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/baseline-mailbridge
EXIT_CODE: 0

## Output Summary

- Test results: Passed 152 / Failed 0 / Skipped 3 / Total 155 (Duration 12 s).
- Top-line (all projects exercised by this test run): line-rate 0.8911, lines-covered 1383 / lines-valid 1552 (89.11%).

### Targeted project — `OpenClaw.MailBridge`

**MailBridge line coverage: 1050 / 1208 = 86.92%** (baseline targeted).

### Per-file baseline coverage for the two files this plan modifies

`OutlookScanner.cs` (aggregate of all classes in file):

| Class / state-machine | Covered | Valid | Rate |
|---|---|---|---|
| `OpenClaw.MailBridge.OutlookScanner` | 166 | 179 | 92.74% |
| `OutlookScanner/NormalizedMessage` | 1 | 1 | 100.00% |
| `OutlookScanner/NormalizedEvent` | 6 | 6 | 100.00% |
| `OutlookScanner/<>c` | 2 | 2 | 100.00% |
| `<EnumerateItems>d__20` | 17 | 21 | 80.95% |
| `<ExecuteScanAsync>d__13` | 61 | 61 | 100.00% |
| `<ScanAsync>d__9` | 3 | 3 | 100.00% |
| `<ScanCalendarAsync>d__11` | 1 | 1 | 100.00% |
| `<ScanCalendarFolderAsync>d__15` | 37 | 43 | 86.05% |
| `<ScanInboxAsync>d__10` | 1 | 1 | 100.00% |
| `<ScanInboxFolderAsync>d__14` | 29 | 35 | 82.86% |

File aggregate `OutlookScanner.cs`: 324 / 353 = 91.78%.

`CacheRepository.cs` (aggregate of all classes in file):

| Class / state-machine | Covered | Valid | Rate |
|---|---|---|---|
| `ScanStateSnapshot` | 4 | 5 | 80.00% |
| `CacheRepository` | 139 | 150 | 92.67% |
| `<GetEventAsync>d__15` | 0 | 9 | 0.00% |
| `<GetMessageAsync>d__12` | 0 | 9 | 0.00% |
| `<GetScanStateAsync>d__8` | 9 | 9 | 100.00% |
| `<GetScanStateSnapshotAsync>d__16` | 0 | 6 | 0.00% |
| `<InitializeAsync>d__6` | 10 | 10 | 100.00% |
| `<ListCalendarWindowAsync>d__14` | 23 | 23 | 100.00% |
| `<ListMessagesAsync>d__17` | 15 | 15 | 100.00% |
| `<ListRecentMeetingRequestsAsync>d__11` | 0 | 14 | 0.00% |
| `<ListRecentMessagesAsync>d__10` | 13 | 13 | 100.00% |
| `<TouchScanStateAsync>d__7` | 10 | 10 | 100.00% |
| `<UpsertEventAsync>d__13` | 42 | 42 | 100.00% |
| `<UpsertMessageAsync>d__9` | 38 | 38 | 100.00% |

File aggregate `CacheRepository.cs`: 303 / 353 = 85.84%.

### Cobertura Artifact

- `artifacts/coverage/baseline-mailbridge/84637f7f-8756-4726-bdb0-7f6416024573/coverage.cobertura.xml`
