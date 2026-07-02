# Scope and Size Verification (Issue #105, P4-T5)

Timestamp: 2026-07-02T14-25
Command: `git diff --name-only origin/main` + `git status --porcelain` (untracked) + `wc -l` + `grep` scans (repo root)
EXIT_CODE: 0

## (a) Production diff scope — PASS

`git rev-parse HEAD origin/main` returns one unique hash (branch head equals `origin/main`; all feature work is in the working tree), so the working-tree diff is the complete feature diff.

Production files in the diff (modified tracked + new untracked under `src/`):
- `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (modified — DDL append)
- `src/OpenClaw.Core/Program.cs` (modified — one DI registration)
- `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` (new)
- `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` (new)
- `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` (new)

Exactly the five files listed in the plan Conventions section. No files under `src/OpenClaw.HostAdapter/`, `src/OpenClaw.MailBridge/`, any `*.Contracts` wire surface, or Runtime workers (`SchedulingWorker*.cs`) appear in the diff. Non-production diff entries are the three new test files and the feature-folder docs/evidence.

## (b) File size (500-line cap) — PASS

| File | Lines |
|---|---|
| `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs` | 44 |
| `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs` | 161 |
| `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs` | 110 |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` | 254 |
| `src/OpenClaw.Core/Program.cs` | 332 |
| `tests/OpenClaw.Core.Tests/CoreCacheRepositorySeriesMovesTests.cs` | 321 |
| `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardTests.cs` | 352 |
| `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardPropertyTests.cs` | 265 |

All under 500 lines.

## (c) No temp files in tests — PASS

`grep -in "GetTempPath|GetTempFileName|Path.Combine.*temp|File.Create|File.Write"` over the three new test files: zero matches (exit 1). `CoreCacheRepositorySeriesMovesTests.cs` uses only the in-memory shared-cache SQLite pattern (`Mode=Memory;Cache=Shared` connection string present).

## (d) No clock reads in new production files — PASS

`grep -in "DateTime.Now|DateTime.UtcNow|TimeProvider"` over `ISeriesMoveHistory.cs`, `OneOnOneMoveGuard.cs`, `CoreCacheRepository.SeriesMoves.cs`: zero matches (exit 1). All timestamps are caller-supplied.

## Verdict

(a) PASS, (b) PASS, (c) PASS, (d) PASS.
