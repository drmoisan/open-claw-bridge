# Phase 4 — File Size (500-line cap)

Timestamp: 2026-06-13T03-18

| File | Pre-P4 lines | Post-P4 lines | Status |
|---|---|---|---|
| `src/OpenClaw.MailBridge/CacheRepository.cs` | 465 | 460 | Under 500. DDL constant + migration helpers moved to the new `CacheRepository.Schema.cs` partial, so adding the eight INSERT/UPDATE columns and parameter bindings kept the file below cap. |
| `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` | n/a (new) | 80 | Under 500. Holds `CreateTablesSql`, `GraphFieldColumns`, `MigrateEventsSchemaAsync`, `AddEventsColumnAsync`, `EventsColumnExistsAsync`. |
| `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` | 84 | 116 | Under 500. `ReadEvent` extended with nine trailing columns + `GetCategories` JSON helper. |

EXIT_CODE: 0
Output Summary: PASS. No CacheRepository file exceeds 500 lines (460 / 80 / 116). Schema/migration split into a partial per P4-T6.
