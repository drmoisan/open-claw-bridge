# Baseline NU1903 Fail-State Reuse Check — Issue #92

Timestamp: 2026-07-01T20-30

Command: (validation of existing artifact) — read docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-nu1903-build.md

EXIT_CODE: 0

Output Summary:
- Reused baseline artifact `baseline-nu1903-build.md` (2026-07-01T19-46) is present and schema-complete.
- It records the fail-before state: `Command: dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror`, `EXIT_CODE: 1` (build FAILED).
- NU1903 promoted to error on four projects, quoting `error NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q` for src/OpenClaw.MailBridge, src/OpenClaw.Core, tests/OpenClaw.MailBridge.Tests, tests/OpenClaw.Core.Tests.
- Working tree still matches the reverted baseline (Microsoft.Data.Sqlite 8.0.11, transitive lib.e_sqlite3 2.1.6), so the recorded non-zero fail-before state remains valid.
- Reuse is VALID; no recapture required. This establishes the fail-before state for AC-2.
