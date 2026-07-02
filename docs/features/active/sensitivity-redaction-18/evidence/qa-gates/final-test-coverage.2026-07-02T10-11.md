# Final QA — dotnet test with coverage (remediation cycle 1)

Timestamp: 2026-07-02T10-11
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "artifacts/csharp/final-qa-2026-07-02T10-11"`
EXIT_CODE: 0
Output Summary:

- Tests: 665 total — 660 passed, 0 failed, 5 skipped (same environment-gated COM/publish skips as baseline).
  - OpenClaw.MailBridge.Tests: 347 passed, 5 skipped (352 total; +13 new tests versus the 339-total baseline)
  - OpenClaw.Core.Tests: 213 passed
  - OpenClaw.HostAdapter.Tests: 100 passed
- Post-change pooled coverage (3 Cobertura reports, summed root counters):
  - Line: 90.51% (4149/4584)
  - Branch: 79.95% (929/1162)
- Raw Cobertura reports staged under `artifacts/csharp/final-qa-2026-07-02T10-11/<guid>/coverage.cobertura.xml` (MailBridge report: `a7e86c94-a5ac-4632-a16b-2a8799c33c78`).
- Source-scoped `git status --porcelain src tests` hash unchanged across the run (ebd881cc1e03597a70322aa31a1bd117).
