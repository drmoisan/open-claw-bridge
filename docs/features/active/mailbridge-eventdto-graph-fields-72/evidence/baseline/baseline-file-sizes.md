# Baseline — File Line Counts (500-line cap sizing)

Timestamp: 2026-06-13T03-05

Recorded with `wc -l` against the working tree before any Phase 1+ edits.

| File | Lines | Status vs 500-line cap |
|---|---|---|
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | 157 | Under cap; headroom for nine new EventDto parameters. |
| `src/OpenClaw.MailBridge/CacheRepository.cs` | 465 | Under cap; 35 lines of headroom. Watch P4 additions; split to a partial if it crosses 500. |
| `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` | 84 | Under cap; ample headroom for ReadEvent extension. |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 507 | PRE-EXISTING OVER-CAP (507 > 500). Must not grow; new helpers go in `OutlookScanner.GraphFields.cs` (P2). |
| `src/OpenClaw.MailBridge/ResponseShaper.cs` | 65 | Under cap. |
| `src/OpenClaw.Core/CoreCacheRepository.cs` | 691 | PRE-EXISTING OVER-CAP (691 > 500). Split schema/migration helpers to `CoreCacheRepository.Schema.cs` (P5) to avoid worsening. |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` | 169 | Under cap. |

Decisions for sizing:
- `OutlookScanner.cs` (507): new splitter + seriesMasterId helpers MUST land in new partial `OutlookScanner.GraphFields.cs`; only the nine named-argument additions occur in `OutlookScanner.cs` (these replace existing `null` placeholders, near net-zero growth).
- `CacheRepository.cs` (465): DDL/migration/parameter additions will push it over 500; plan P4-T6 prescribes splitting schema/migration into a new partial (`CacheRepository.Schema.cs`).
- `CoreCacheRepository.cs` (691): already over cap; P5-T6 prescribes extracting schema/migration into `CoreCacheRepository.Schema.cs`.

EXIT_CODE: 0
Output Summary: Line counts recorded. Two pre-existing over-cap files identified: OutlookScanner.cs (507) and CoreCacheRepository.cs (691). Split strategy defined per plan.
