# Final QA Gate — C# Tests and Coverage (P8-T5)

Timestamp: 2026-07-02T20-53
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- All tests passed. Totals: 1063 passed, 0 failed, 5 skipped (1068 total).
  - OpenClaw.HostAdapter.Tests: 100 passed
  - OpenClaw.Core.Tests: 616 passed (includes the new architecture-boundary, unit, property, retry, error-matrix, DI, backend-selection, and contract-parity suites; 180 new tests vs the 436 baseline)
  - OpenClaw.MailBridge.Tests: 347 passed / 5 skipped (pre-existing Windows/COM/publish-output skips)
- Post-change pooled coverage (sum across the three Cobertura reports):
  - Line coverage: 5234/5668 = 92.34%
  - Branch coverage: 1269/1526 = 83.16%
- Per-package post-change coverage:
  - OpenClaw.Core (Core.Tests report): line 2588/2761 = 93.73%; branch 682/800 = 85.25%
  - OpenClaw.HostAdapter: line 1113/1269 = 87.71%; branch 170/253 = 67.19% (unchanged from baseline; untouched package)
  - OpenClaw.MailBridge: line 1533/1638 = 93.59%; branch 417/473 = 88.16% (unchanged from baseline; untouched package)
- Raw intermediates copied to `artifacts/csharp/final-2026-07-02T20-53/` (three cobertura XMLs).
- GraphBackendSelectionTests default-path test (`DefaultPath_GraphAdapterAbsent_ResolvesHostAdapterHttpClient`) passed in this run (referenced by P8-T7).
