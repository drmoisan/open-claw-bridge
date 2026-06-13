# Final QA — 500-Line File-Size Cap Check

Timestamp: 2026-06-13T13-34
Command: wc -l <each touched production and test file>; git show HEAD:src/OpenClaw.Core/CoreCacheRepository.cs | wc -l
EXIT_CODE: 0

## Touched files and line counts
| File | Lines | <= 500 |
|---|---|---|
| src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs | 170 | yes |
| src/OpenClaw.MailBridge/IMessageSource.cs | 56 | yes |
| src/OpenClaw.MailBridge/ComMessageSource.cs | 314 | yes |
| src/OpenClaw.MailBridge/OutlookScanner.cs | 497 | yes |
| src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs | 273 | yes |
| src/OpenClaw.MailBridge/CacheRepository.cs | 480 | yes |
| src/OpenClaw.MailBridge/CacheRepository.Schema.cs | 132 | yes |
| src/OpenClaw.MailBridge/CacheRepository.Readers.cs | 120 | yes |
| src/OpenClaw.Core/CoreCacheRepository.cs | 699 | NO (pre-existing) |
| src/OpenClaw.Core/CoreCacheRepository.Schema.cs | 241 | yes |
| src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs | 189 | yes |
| tests/OpenClaw.MailBridge.Tests/ComMessageSourceTests.cs | 154 | yes |
| tests/OpenClaw.MailBridge.Tests/CacheRepositoryMessageFieldsTests.cs | 127 | yes |
| tests/OpenClaw.MailBridge.Tests/OutlookScannerMessageFieldsTests.cs | 274 | yes |
| tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs | 495 | yes |
| tests/OpenClaw.MailBridge.Tests/MailBridgeMessageSourceTestDoubles.cs | 36 | yes |
| tests/OpenClaw.Core.Tests/CoreCacheRepositoryMessageFieldsTests.cs | 154 | yes |
| tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperTests.cs | 341 | yes |

## Output Summary
- 17 of 18 touched files are at or below the 500-line cap.
- New files created by this feature are all well under the cap (IMessageSource.cs 56,
  ComMessageSource.cs 314, MailBridgeMessageSourceTestDoubles.cs 36, the new test files 127-274).
- OutlookScanner.cs (was at the 500 cap) decreased to 497 by routing net-new bodies into
  ComMessageSource.cs and OutlookScanner.Attendees.cs.
- MailBridgeRuntimeTestDoubles.cs was brought back under the cap (495) by extracting the three
  net-new issue-#73 standalone doubles into MailBridgeMessageSourceTestDoubles.cs.

## Out-of-scope finding (reported, not silently expanded)
- src/OpenClaw.Core/CoreCacheRepository.cs is 699 lines, over the 500-line cap. This is a
  PRE-EXISTING violation: the file was 687 lines at HEAD (verified via
  `git show HEAD:src/OpenClaw.Core/CoreCacheRepository.cs | wc -l`) before this feature began, and
  the spec (Constraints & Risks) explicitly notes it as "687, already over via partials". This
  feature added a net +12 lines (16 insertions, 4 deletions) consisting of the mandatory in-method
  edits (one MigrateMessagesSchemaAsync call, four reader named-argument lines, and the messages
  INSERT/VALUES/ON CONFLICT SQL expansion); the net-new parameter-binding body was routed into the
  CoreCacheRepository.Schema.cs partial per plan P5-T6 to minimize growth. Bringing this file under
  500 lines requires a broad extraction refactor of unrelated existing methods
  (UpsertEventsAsync/ReadEvent), which is outside the approved plan scope. Recommend a follow-up
  cycle to split CoreCacheRepository.cs.
