# Reviewer Coverage Re-Run and Per-File Parse (feature review, issue #107)

Timestamp: 2026-07-02T15-50
Reviewer: feature-review agent (independent re-run at branch head `c5b19de6a118aa7bd2dbf5a2df2c350ffceb4c63`)
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-outbound-audit-log-107/evidence/qa-gates/coverage-review"`
EXIT_CODE: 0

## Test Results (reviewer run)

- OpenClaw.Core.Tests: 360 passed / 360 (0 failed)
- OpenClaw.HostAdapter.Tests: 100 passed / 100 (0 failed)
- OpenClaw.MailBridge.Tests: 347 passed, 5 skipped / 352 (0 failed; the 5 skips are the pre-existing environment-gated COM/publish tests, identical to baseline)
- Solution total: 807 passed, 0 failed, 5 skipped, 812 total — identical to executor evidence `final-dotnet-test-coverage.2026-07-02T15-33.md`

## Raw Reports

Three fresh cobertura reports under this directory:
- `03a530d0-3ee9-42d9-8968-0cf8ea3959df/coverage.cobertura.xml`
- `77858319-73e1-48b0-8da0-6e6858b78cc1/coverage.cobertura.xml`
- `a12198b9-f190-4a22-ac90-f5d7ed849553/coverage.cobertura.xml`

Parsing method: pooled per (filename, line) with max-hits dedupe across reports and across duplicate `<class>` entries per partial-class file; branch coverage from `condition-coverage` attributes pooled the same way. This dedupe method halves the executor's raw summed counts but produces identical percentages.

## Pooled and Per-Package Results (reviewer-parsed)

| Scope | Line | Branch |
|---|---|---|
| Pooled (all packages) | 4242/4381 = 96.83% | 1004/1116 = 89.96% |
| OpenClaw.Core | 1669/1689 = 98.82% | 417/453 = 92.05% |
| OpenClaw.HostAdapter | 1017/1031 = 98.64% | 170/190 = 89.47% |
| OpenClaw.MailBridge | 1227/1318 = 93.10% | 304/352 = 86.36% |
| OpenClaw.MailBridge.Client | 95/105 = 90.48% | 54/58 = 93.10% |
| OpenClaw.MailBridge.Contracts | 211/215 = 98.14% | 59/63 = 93.65% |
| OpenClaw.HostAdapter.Contracts | 23/23 = 100.00% | n/a |

Baseline (reviewer-parsed from executor baseline cobertura `artifacts/csharp/baseline-107/`, same method): pooled line 4196/4335 = 96.79%, branch 998/1110 = 89.91%; OpenClaw.Core line 1623/1643 = 98.78%, branch 411/447 = 91.95%. Percentages identical to executor evidence.

## Per Changed/New Production File (reviewer-parsed, line AND branch)

| File | Status | Line | Branch |
|---|---|---|---|
| Agent/Contracts/ActionAuditRecord.cs | NEW | 15/15 = 100.00% | no branch points |
| Agent/Contracts/ActionAuditResultCode.cs | NEW | not instrumented (const-string-only static class; no executable code) | n/a |
| Agent/Contracts/IActionAuditLog.cs | NEW | interface-only, omitted per policy | n/a |
| Agent/Contracts/ISchedulingService.cs | MODIFIED | interface-only, omitted per policy | n/a |
| Agent/Runtime/HostAdapterSchedulingService.cs | MODIFIED | 4/4 = 100.00% | no branch points |
| Agent/Runtime/SchedulingWorker.Audit.cs | NEW | 16/16 = 100.00% | no branch points |
| Agent/Runtime/SchedulingWorker.Pipeline.cs | MODIFIED | 20/20 = 100.00% | 2/4 = 50.00% (see note) |
| Agent/Runtime/SchedulingWorker.cs | MODIFIED | 10/10 = 100.00% | no branch points |
| CoreCacheRepository.AuditLog.cs | NEW | 13/13 = 100.00% | 6/6 = 100.00% |
| CoreCacheRepository.Schema.cs | MODIFIED | 37/37 = 100.00% | 6/6 = 100.00% |
| Program.cs (OpenClaw.Core) | MODIFIED | 240/240 = 100.00% | no branch points |

### Note on SchedulingWorker.Pipeline.cs branch 2/4

The two half-covered conditions sit on head lines 298 (`MailboxUpn()` `InternalDomain.Length > 0` ternary) and 305 (`BuildProposalReply` `slots.Count == 0` ternary). Reviewer verified both are pre-existing, unchanged lines: the baseline cobertura (`artifacts/csharp/baseline-107/35ae4f12-*/coverage.cobertura.xml`) shows the identical two conditions at 50% (1/2) on old line numbers 233 and 240, and `git show <merge-base>:...SchedulingWorker.Pipeline.cs` confirms the same source text at those baseline lines. No coverage regression on changed lines: every instrumented line and branch added or modified by this feature is covered.

### Note on async-body instrumentation

`mailbridge.runsettings` (byte-identical to base on this branch) sets `ExcludeByAttribute` including `CompilerGeneratedAttribute`, so async method bodies (compiled to state machines) contribute zero instrumented lines. Affected here: `CoreCacheRepository.AuditLog.cs` (`RecordAsync`, `GetByMessageIdAsync`, `EnsureAuditLogSchemaAsync` bodies; the 13 instrumented lines are the sync guards/helpers) and `WriteAuditSafelyAsync` in `SchedulingWorker.Audit.cs`. Behavioral verification substitutes per the accepted #99/#103/#105 disposition: 23 dedicated repository/property tests (all branches: 12 guard DataRows, null/non-null optionals, ordering, tie-break, non-UTC normalization, restart, both migration paths, lazy ensure) and the red/green worker-audit pair (`schedulingworker-audit-expect-fail.2026-07-02T15-26.md` EXIT 1 with all 8 audit tests failing before emission wiring; pass-after EXIT 0) covering all four emission points and both resilience catch paths.

## Verdict

- Repo-wide line >= 85%: PASS (96.83% pooled; 98.82% Core)
- Repo-wide branch >= 75%: PASS (89.96% pooled; 92.05% Core)
- New files line >= 85% / branch >= 75%: PASS (every instrumented new file 100.00% line; only new file with branch points, `CoreCacheRepository.AuditLog.cs`, 100.00% branch; uninstrumented async bodies behaviorally verified as above)
- Modified files line >= 85% / branch >= 75% / no regression on changed lines: PASS (all instrumented modified files 100.00% line; the only partial branches are two pre-existing unchanged conditions, identical at baseline)
