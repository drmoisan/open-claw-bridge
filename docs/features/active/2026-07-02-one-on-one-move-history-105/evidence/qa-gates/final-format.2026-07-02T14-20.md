# Final QA — Formatting (CSharpier)

Timestamp: 2026-07-02T14-20
Command: `csharpier format .` then `csharpier check .` (repo root)
EXIT_CODE: 0
Output Summary:
- Loop pass 1: `csharpier format .` reformatted the three new files (`OneOnOneMoveGuard.cs`, `OneOnOneMoveGuardTests.cs`, `OneOnOneMoveGuardPropertyTests.cs`, `CoreCacheRepositorySeriesMovesTests.cs` — line-wrapping only); per the loop rule this restarted the toolchain loop.
- Loop pass 2: `csharpier format .` changed no files (working-tree state identical before/after); `csharpier check .` exited 0 ("Checked 223 files"). Formatting gate passes cleanly in a single pass.
