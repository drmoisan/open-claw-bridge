# Baseline Dependency-Versions Reuse Check — Issue #92

Timestamp: 2026-07-01T20-30

Command: git diff --stat -- src/OpenClaw.Core/OpenClaw.Core.csproj src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj

EXIT_CODE: 0

Output Summary:
- Reused baseline artifact `baseline-dependency-versions.md` (2026-07-01T19-46) is present and schema-complete.
- It records: direct `Microsoft.Data.Sqlite` 8.0.11 in `src/OpenClaw.Core/OpenClaw.Core.csproj` (line 17) and in `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` (line 23); transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.6.
- `git diff --stat` on both csproj returned EXIT_CODE 0 with NO output (no staged/unstaged changes), confirming the working tree still matches the reverted baseline.
- Direct reads of both csproj confirm `Microsoft.Data.Sqlite` Version="8.0.11" unchanged.
- Reuse is VALID; no recapture required. These baseline dependency figures are authoritative for AC-1 comparison.
