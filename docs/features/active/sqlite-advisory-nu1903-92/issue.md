# sqlite-advisory-nu1903

- Work Mode: minor-audit
- Canonical issue: 92
- Tier: T1 (OpenClaw.Core and OpenClaw.MailBridge data layer)

## Problem / Why

The required `.NET Build + Test` CI check fails repo-wide with `NU1903`: the
transitive package `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 carries a known
high-severity advisory (GHSA-2m69-gcr7-jv3q). The build step runs
`dotnet build ... -c Release --no-restore /warnaserror`, and NuGet audit
promotes `NU1903` to a build error. This blocks every PR (including #90 and #91),
because the advisory affects `main` itself.

Root cause: `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 is pulled transitively by
`Microsoft.Data.Sqlite` 8.0.11, referenced in:
- `src/OpenClaw.Core/OpenClaw.Core.csproj`
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
(and flowing into their test projects).

## Implementation Intent (Option B — SQLitePCLRaw 3.x native override)

REVISED after execution finding: NuGet audit flags the entire SQLitePCLRaw 2.1.x
line (incl. 2.1.11) for GHSA-2m69-gcr7-jv3q, and NO `Microsoft.Data.Sqlite`
release (8.x/9.x/10.x) transitively pulls a cleared version. Only the SQLitePCLRaw
3.x line clears it (`lib.e_sqlite3` 3.50.3 via `bundle_e_sqlite3` 3.0.0). Operator
chose the genuine native fix (Option B), accepting that this is an unsupported
combination with current `Microsoft.Data.Sqlite` and must be runtime-verified.

Approach: add a direct `PackageReference` that forces the transitive
`SQLitePCLRaw.lib.e_sqlite3` to the 3.x line (>= 3.50.3) identically in BOTH
`src/OpenClaw.Core/OpenClaw.Core.csproj` and
`src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` — e.g. a direct
`SQLitePCLRaw.bundle_e_sqlite3` 3.0.0 reference (add `SQLitePCLRaw.core` 3.x
and/or bump `Microsoft.Data.Sqlite` only if required to produce a coherent
restore graph). The engineer resolves the exact minimal working package set
during execution and records it.

Constraints:
- No advisory suppression (no `NoWarn NU1903`, no `NuGetAuditMode`/`NuGetAuditSuppress`). Rejected.
- Keep the added SQLitePCLRaw versions identical (lockstep) across both csproj.
- Minimal change: only the SQLite/SQLitePCLRaw dependency references. No unrelated
  dependency churn and no product-code changes unless the override forces a
  narrowly-scoped, justified, test-covered adjustment.
- HARD RUNTIME GATE (unsupported combo): the fix is accepted ONLY if
  (a) `dotnet restore` produces a coherent graph with no unresolvable version
  conflict, (b) `dotnet build -c Release /warnaserror` shows 0 NU1903 and no new
  advisories, AND (c) the SQLite-backed cache/DB tests actually pass at runtime
  (the MSTest suites that open/read/write the SQLite cache in OpenClaw.Core.Tests
  and OpenClaw.MailBridge.Tests). If any of these fail due to the SQLitePCLRaw
  3.x / Microsoft.Data.Sqlite core mismatch, STOP and surface to the operator —
  do NOT force it and do NOT silently fall back to suppression.

## Acceptance Criteria

- AC-1 A direct SQLitePCLRaw reference (`SQLitePCLRaw.bundle_e_sqlite3` 3.x, plus
  any companion package required for a coherent graph) is added identically to
  both `src/OpenClaw.Core/OpenClaw.Core.csproj` and
  `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`, restoring the transitive
  `SQLitePCLRaw.lib.e_sqlite3` to the 3.x line (>= 3.50.3).
- AC-2 `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror` completes
  with 0 `NU1903` errors and introduces no new package advisories (no new
  `NUxxxx`).
- AC-3 No advisory suppression is introduced (`NoWarn`/`NuGetAuditMode`/`NuGetAuditSuppress`
  unchanged).
- AC-4 `dotnet test OpenClaw.MailBridge.sln -c Release` passes, INCLUDING the
  SQLite-backed cache/DB tests that exercise the native `e_sqlite3` provider at
  runtime (this is the mandatory runtime verification of the unsupported combo);
  line coverage >= 85% and branch coverage >= 75%; no regression on changed lines.
- AC-5 No product-code behavior change, or — if the override forces one — it is
  minimal, justified, and test-covered.
- AC-6 Full C# toolchain passes in a single clean pass: CSharpier format ->
  analyzers/build (`/warnaserror`) -> nullable -> architecture tests -> MSTest tests.
- AC-7 HARD GATE: if no coherent restore graph exists, or the SQLite-backed tests
  fail at runtime due to the SQLitePCLRaw 3.x / Microsoft.Data.Sqlite mismatch,
  the work STOPS and is surfaced to the operator (no forcing, no silent fallback
  to suppression).

## Dependencies / Risks

- T1 data layer: the SQLite native `e_sqlite3` bundle changes with the bump;
  exercise the cache/DB paths (unit + integration) to confirm no runtime
  regression.
- Projects target `net10.0` / `net10.0-windows`; `Microsoft.Data.Sqlite` 9.0.x
  is forward-compatible but must be verified to restore.
- Delivered as its own PR onto `main` (Option C). After merge, PR #91
  (`chore/update-agents`) will be rebased onto `main` to inherit this fix.

## Verification Steps

- Run the full C# toolchain (CSharpier, build with analyzers, nullable,
  architecture tests, xUnit) per `.claude/rules/csharp.md`.
- Reproduce the CI gate locally: `dotnet build OpenClaw.MailBridge.sln -c Release
  /warnaserror` and confirm 0 NU1903.
- Capture baseline, targeted-verification, and end-state evidence artifacts under
  `docs/features/active/sqlite-advisory-nu1903-92/evidence/`.

## Evidence Checklist
- [x] baseline
- [x] targeted verification
- [x] end-state
