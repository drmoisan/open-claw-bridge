# AC-7 HARD GATE — Restore-Graph Coherence — Issue #92

Timestamp: 2026-07-01T20-30

Command: dotnet restore OpenClaw.MailBridge.sln

EXIT_CODE: 0

Output Summary:
- Restore SUCCEEDED for all 9 projects (6 src + 3 tests). No unresolvable version conflict.
- No NU1605 (downgrade), no NU1107 (unresolvable), no downgrade/conflict diagnostic in output.
- The SQLitePCLRaw 3.x native override (`SQLitePCLRaw.bundle_e_sqlite3` 3.0.0) produced a coherent restore graph against `Microsoft.Data.Sqlite` 8.0.11 (unchanged) across the full solution.
- AC-7 restore gate: PASSED. Execution continues to P2-T1. No forcing, no suppression applied.
