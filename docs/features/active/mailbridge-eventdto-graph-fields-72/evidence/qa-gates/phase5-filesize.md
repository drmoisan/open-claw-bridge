# Phase 5 — File Size (500-line cap)

Timestamp: 2026-06-13T03-21

| File | Pre-P5 lines | Post-P5 lines | Status |
|---|---|---|---|
| `src/OpenClaw.Core/CoreCacheRepository.cs` | 691 (pre-existing over-cap) | 687 | PRE-EXISTING OVER-CAP, NOT worsened (687 < 691). The DDL block (~75 lines) was moved into the new `CoreCacheRepository.Schema.cs` partial; the new parameter bindings + JSON helpers (~70 lines) net to a small decrease. |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` | n/a (new) | 158 | Under 500. Holds `CreateTablesSql`, `GraphFieldColumns`, `MigrateEventsSchemaAsync`, `EventsColumnExistsAsync`. |

Notes:
- `CoreCacheRepository.cs` is a pre-existing over-cap file (691 lines at baseline per `evidence/baseline/baseline-file-sizes.md`). Per P5-T6 the schema/migration was extracted to a new partial to avoid worsening the condition. Post-change it is 687 lines — slightly below its pre-existing count, so the over-cap is not worsened.
- The new partial is well under 500 lines.

EXIT_CODE: 0
Output Summary: PASS. CoreCacheRepository.cs reduced 691 -> 687 (pre-existing over-cap not worsened); new partial 158 lines (under cap).
