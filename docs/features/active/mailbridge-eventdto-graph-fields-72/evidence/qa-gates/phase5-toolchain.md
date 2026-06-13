# Phase 5 — Toolchain (Core Cache Persistence)

Timestamp: 2026-06-13T03-21

Phase boundary: build-green and test-green in a single clean pass.

## Stage results

1. Format — `csharpier format .` then `csharpier check .` → EXIT 0. 157 files clean.
2. Lint/analyzers — EXIT 0, 0 warnings, 0 errors.
3. Type-check (nullable, TreatWarningsAsErrors) → EXIT 0, 0 warnings, 0 errors.
4. Architecture — changes confined to `OpenClaw.Core` (CoreCacheRepository + new Schema partial); no new ProjectReference; no COM pulled into Core's closure. Intact.
5. Test — below.

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0

Output Summary:
- Tests PASS. Failed: 0, Passed: 456 (HostAdapter 71, Core 181, MailBridge 204), Skipped: 3, Total: 459. Core.Tests grew 178 -> 181 (three new CoreCacheRepositoryGraphFields tests: full populated round-trip, empty-categories round-trip, idempotent double InitializeAsync).
- Coverage (cobertura): Core.Tests line 89.10% (1431/1606), branch 77.48% (327/422). Threshold (line >= 85%, branch >= 75%): PASS.

AC4 (both caches) now fully verified: bridge cache (Phase 4) and Core cache (Phase 5) round-trip all nine fields with idempotent migrations.

Scope note: the Core migration adds the nine Graph-field columns plus `last_modified_utc` only. No `response_status` column is added (deferred to issue #80 per spec Non-Goals); an in-code comment in `CoreCacheRepository.Schema.cs` cites #80.
