# Remediation Baseline — Test and Coverage (Cycle 1, Issue #117)

Timestamp: 2026-07-03T09-06
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "artifacts/csharp/remediation-baseline-117"
EXIT_CODE: 0
Output Summary:
- Tests: 1154 passed, 0 failed, 5 skipped (pre-existing environment-gated COM/publish skips), 1159 total.
  - OpenClaw.Core.Tests: 707/707 passed
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped
  - OpenClaw.HostAdapter.Tests: 100/100 passed
- Pooled line coverage: 5622/6056 = 92.83% (gate >= 85%: PASS)
- Pooled branch coverage: 1312/1576 = 83.25% (gate >= 75%: PASS)
- Matches the reviewer reference point exactly (92.83% line / 83.25% branch).
- Parsing convention: dedupe duplicate class entries per file+line within each cobertura report (max hits / max condition arms), then sum the deduped per-report totals across the three reports.
- Raw cobertura reports retained under `artifacts/csharp/remediation-baseline-117/` (3 reports).
