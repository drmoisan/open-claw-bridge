# STOP — Plan Premise Invalidated by Live NuGet Audit (Issue #92)

Timestamp: 2026-07-01T19-46

Status: STOPPED at [P1-T5]. Bump reverted to baseline (8.0.11 in both csproj). Awaiting plan/issue revision and a scope decision. No strategy was switched silently.

## Command(s)
- dotnet restore OpenClaw.MailBridge.sln  (after bumping both csproj to Microsoft.Data.Sqlite 9.0.0)
- dotnet build OpenClaw.MailBridge.sln -c Release
- per-version probe: dotnet list <probe>.csproj package --include-transitive; dotnet build <probe>.csproj -c Release

## EXIT_CODE
- restore: 0
- build (Release, no /warnaserror): 0 (build succeeds)
- Advisory-clearance outcome: FAILED — NU1903 for GHSA-2m69-gcr7-jv3q still fires at the resolved target.

## Output Summary — The Finding

The plan's and issue.md's core premise is incorrect against the live NuGet audit database:

> Premise (plan P1-T1 / issue.md lines 26-29, 44-47): "SQLitePCLRaw.lib.e_sqlite3 >= 2.1.10 clears GHSA-2m69-gcr7-jv3q."

Verified reality:
- Microsoft.Data.Sqlite 9.0.0 pulls transitive SQLitePCLRaw.lib.e_sqlite3 2.1.10, and `dotnet build -c Release` still emits:
  `warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.10 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q`
  on all four projects. Under /warnaserror this remains a build error. AC-2 is NOT satisfied at 2.1.10.
- Probes across the Microsoft.Data.Sqlite lines:
  - MDS 9.0.0 / 9.0.9 / 9.0.17 -> lib.e_sqlite3 2.1.10 -> NU1903 still fires.
  - MDS 10.0.0 / 10.0.9 (latest) -> lib.e_sqlite3 2.1.11 -> NU1903 still fires.
- Direct SQLitePCLRaw override probes (against a scratch project, not the repo):
  - lib.e_sqlite3 2.1.11 -> NU1903 still fires.
  - lib.e_sqlite3 / bundle_e_sqlite3 requested at 2.2.0 -> NuGet reports 2.2.0 does not exist in the 2.x line and rolls forward to lib.e_sqlite3 3.50.3 / bundle_e_sqlite3 3.0.0 (SQLitePCLRaw 3.x major line) -> NU1903 cleared (0 occurrences).

Conclusion:
- The 2.1.x line of SQLitePCLRaw.lib.e_sqlite3 tops out at 2.1.11, and NuGet audit flags the ENTIRE 2.1.x line (including 2.1.10 and 2.1.11) for GHSA-2m69-gcr7-jv3q.
- No currently available Microsoft.Data.Sqlite version (through the 10.0.x line) pulls a transitive SQLitePCLRaw bundle that clears this advisory.
- The advisory is only cleared by the SQLitePCLRaw 3.x line (lib.e_sqlite3 3.50.3, bundle_e_sqlite3 3.0.0).

## Why this is NOT the documented Option A fallback

The plan's Option A fallback (P1-T5) triggers only when restore/build is INFEASIBLE on net10.0 attributable to the bump. That did not happen: restore and build both succeed (EXIT 0). The problem is instead that the resolved-target premise (">= 2.1.10 clears the advisory") is false. This is a NEW out-of-plan finding that invalidates AC-1/AC-2 as written, not a build/restore infeasibility. Per the execution directive, a new out-of-plan finding requires STOP + report rather than silent scope expansion.

## Verified corrective options (for planner / human decision — NOT applied)

Option A' (direct SQLitePCLRaw 3.x pin, revised): add a direct PackageReference to SQLitePCLRaw.bundle_e_sqlite3 (and if needed lib.e_sqlite3) at the 3.x line (e.g. bundle_e_sqlite3 3.0.0 / lib.e_sqlite3 3.50.3) to both csproj, overriding the transitive 2.1.x that Microsoft.Data.Sqlite pulls. This is a native-e_sqlite3 major-version change (2.1.x -> 3.x) for the T1 data layer and carries runtime-regression risk on the cache/DB paths; it exceeds the minimal-change scope the current plan/issue authorize.

Option B' (upstream wait): no Microsoft.Data.Sqlite release currently satisfies AC-2 via transitive resolution; a pure MDS version bump cannot clear this advisory today.

Both options require: (1) updating issue.md AC-1 (the "SQLitePCLRaw.lib.e_sqlite3 >= 2.1.10" target is wrong; the clearing version is the 3.x line), and (2) an explicit scope decision on the native SQLite major-version jump and its test/regression coverage for the T1 data layer.

## State left on disk
- Both csproj reverted to baseline Microsoft.Data.Sqlite 8.0.11 (git diff shows no csproj change). Tree is clean and reportable.
- No advisory suppression introduced. No NuGetAuditMode change.
- Scratch probe projects live only under the session scratchpad, not in the repo.
