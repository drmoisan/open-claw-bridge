# Coverage Delta / No-Changed-Line-Regression — Issue #92

Timestamp: 2026-07-01T20-30

Command:
- Baseline: reused P0-T5 figures (baseline-test-coverage.md, 2026-07-01T19-46).
- Post-change: P2-T7 pooled figures computed from the three coverage.cobertura.xml reports.
- Changed-line surface: `git diff -- src/OpenClaw.Core/OpenClaw.Core.csproj src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`.

EXIT_CODE: 0

Output Summary:
- Baseline pooled: line 90.73% (3486/3842), branch 79.31% (874/1102).
- Post-change pooled: line 90.73% (3486/3842), branch 79.31% (874/1102).
- Delta: line 0.00 pp, branch 0.00 pp. No regression.
- Changed-line coverage: the change is package-reference-only. The entire diff is two added `<PackageReference SQLitePCLRaw.bundle_e_sqlite3 3.0.0 />` lines in the two csproj (verified in no-suppression-check). No product-code (.cs) lines were changed, so there are no changed product-code lines to cover; AC-5 forced no product-code adjustment.
- Both thresholds maintained: line 90.73% >= 85%, branch 79.31% >= 75%. No regression on the edited (csproj-only) surface. Supports AC-4, AC-5.
