# Resolved SQLitePCLRaw 3.x Package Set (Option B) — Issue #92

Timestamp: 2026-07-01T20-30

Command:
- Scratch probe csproj (net10.0, Exe) with `Microsoft.Data.Sqlite` 8.0.11 (unchanged) + a single direct `SQLitePCLRaw.bundle_e_sqlite3` 3.0.0 reference.
- `dotnet restore probe.csproj`
- `dotnet list probe.csproj package --include-transitive`
- `dotnet build probe.csproj -c Release -warnaserror`

EXIT_CODE: 0

Output Summary:
- Restore EXIT_CODE 0. Coherent graph, no version conflict.
- RESOLVED MINIMAL PACKAGE SET (added to product csproj): exactly ONE direct reference is required:
  - `SQLitePCLRaw.bundle_e_sqlite3` Version="3.0.0"
  - `Microsoft.Data.Sqlite` remains at 8.0.11 (NO bump required).
  - NO direct `SQLitePCLRaw.core` reference required.
- Resolved transitive graph on the probe:
  - Microsoft.Data.Sqlite 8.0.11 (top-level, unchanged)
  - SQLitePCLRaw.bundle_e_sqlite3 3.0.0 (new direct)
  - Microsoft.Data.Sqlite.Core 8.0.11 (transitive)
  - SQLitePCLRaw.config.e_sqlite3 3.0.0 (transitive)
  - SQLitePCLRaw.core 3.0.0 (transitive — the direct bundle 3.0.0 wins over the 2.1.6 that MDS.Core 8.0.11 would otherwise pull)
  - SQLitePCLRaw.lib.e_sqlite3 3.50.3 (transitive) — CONFIRMED >= 3.50.3, clears GHSA-2m69-gcr7-jv3q
  - SQLitePCLRaw.provider.e_sqlite3 3.0.0 (transitive)
- `dotnet build -c Release -warnaserror` on the probe: Build succeeded, 0 Warning(s), 0 Error(s), 0 NU1903 occurrences.
- Conclusion: the direct `SQLitePCLRaw.bundle_e_sqlite3` 3.0.0 reference alone forces transitive `SQLitePCLRaw.lib.e_sqlite3` to 3.50.3 with a coherent restore. This exact single-package set is added identically (lockstep) to both product csproj in P1-T2/P1-T3. No advisory suppression used.
