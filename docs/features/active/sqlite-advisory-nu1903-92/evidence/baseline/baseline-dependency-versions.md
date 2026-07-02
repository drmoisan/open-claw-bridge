# Baseline Dependency Versions — Issue #92

Timestamp: 2026-07-01T19-46

Command: dotnet list OpenClaw.MailBridge.sln package --include-transitive

EXIT_CODE: 0

Output Summary:
- Direct `Microsoft.Data.Sqlite` version in `src/OpenClaw.Core/OpenClaw.Core.csproj`: 8.0.11 (line 17).
- Direct `Microsoft.Data.Sqlite` version in `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`: 8.0.11 (line 23).
- Transitive `SQLitePCLRaw.lib.e_sqlite3`: 2.1.6 (pulled via Microsoft.Data.Sqlite 8.0.11 -> Microsoft.Data.Sqlite.Core 8.0.11 -> SQLitePCLRaw.bundle_e_sqlite3 2.1.6 -> SQLitePCLRaw.lib.e_sqlite3 2.1.6).
- Related transitive SQLitePCLRaw packages at baseline: bundle_e_sqlite3 2.1.6, core 2.1.6, provider.e_sqlite3 2.1.6.
- The 2.1.6 `SQLitePCLRaw.lib.e_sqlite3` carries advisory GHSA-2m69-gcr7-jv3q; `dotnet list --include-transitive` surfaced NU1903 warnings for OpenClaw.MailBridge, OpenClaw.Core, and both test projects.
