# Resolved Target Microsoft.Data.Sqlite Version — Issue #92

Timestamp: 2026-07-01T19-46

Command: dotnet list <probe>.csproj package --include-transitive  (per-version probe projects, one PackageReference to Microsoft.Data.Sqlite at each candidate version)

EXIT_CODE: 0

Resolved target: Microsoft.Data.Sqlite 9.0.0
Transitive SQLitePCLRaw.lib.e_sqlite3 at that target: 2.1.10 (>= 2.1.10 -> clears GHSA-2m69-gcr7-jv3q)

Output Summary (probe results):
- MDS 8.0.12 -> SQLitePCLRaw.lib.e_sqlite3 2.1.6 (advisory NOT cleared)
- MDS 8.0.13 -> SQLitePCLRaw.lib.e_sqlite3 2.1.6 (advisory NOT cleared)
- MDS 8.0.14 -> SQLitePCLRaw.lib.e_sqlite3 2.1.6 (advisory NOT cleared)
- MDS 8.0.20 -> SQLitePCLRaw.lib.e_sqlite3 2.1.6 (advisory NOT cleared)
- MDS 8.0.28 (latest 8.0.x servicing) -> SQLitePCLRaw.lib.e_sqlite3 2.1.6 (advisory NOT cleared)
- MDS 9.0.0 -> SQLitePCLRaw.lib.e_sqlite3 2.1.10 (advisory CLEARED)

Rationale:
- The entire Microsoft.Data.Sqlite 8.0.x servicing line (verified through 8.0.28) keeps its transitive SQLitePCLRaw.lib.e_sqlite3 pinned at 2.1.6, which does not clear GHSA-2m69-gcr7-jv3q. Staying on 8.0.x cannot satisfy AC-1/AC-2.
- Microsoft.Data.Sqlite 9.0.0 is the first (lowest) stable release whose transitive SQLitePCLRaw.lib.e_sqlite3 is 2.1.10, satisfying the ">= 2.1.10" requirement. This is the lowest current maintained version that clears the advisory.
- 9.0.0 is selected as the minimal-jump target. This matches the issue's stated expectation (the 9.0.x line references SQLitePCLRaw 2.1.10+).
- Available higher lines (9.0.1..9.0.17, 10.0.x) also clear the advisory but represent a larger version jump than necessary; 9.0.0 is the minimal choice.
