# Reviewer Coverage Re-run — feature-review (issue #18, co-delivers #20)

Timestamp: 2026-07-02T09-45
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review"` at branch head `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff`, preceded by removal of any stale results in the target directory.
EXIT_CODE: 0
Output Summary:

## Test results

- Tests: 652 total — 647 passed, 0 failed, 5 skipped (same environment-gated COM/publish skips as baseline).
  - OpenClaw.MailBridge.Tests: 334 passed, 5 skipped (339 total)
  - OpenClaw.Core.Tests: 213 passed
  - OpenClaw.HostAdapter.Tests: 100 passed
- Identical to executor final run (`final-test-coverage.2026-07-02T09-25.md`).

## Pooled and per-package coverage (reviewer cobertura, independently parsed)

| Scope | Line | Branch |
|---|---|---|
| Pooled (3 reports) | 90.51% (4149/4584) | 79.60% (925/1162) |
| OpenClaw.MailBridge package | 93.59% (1533/1638) | 87.32% (413/473) |

Pooled values match the executor's post-change evidence to the hundredth (90.51% / 79.60%).

## Per-changed-file coverage (MailBridge cobertura, per-line max across duplicate class entries)

| File | Status | Line | Branch | Verdict vs uniform gates (line >= 85%, branch >= 75%) |
|---|---|---|---|---|
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | modified | 90.73% (137/151) | 90.00% (36/40) | PASS (changed lines 361-368, 396 covered; uncovered lines pre-existing) |
| `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` | NEW | 100.00% (109/109) | **71.43% (10/14)** | **FAIL — new-file branch coverage below 75%** |
| `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` | modified | 100.00% (65/65) | 100.00% (8/8) | PASS |
| `src/OpenClaw.MailBridge/ResponseShaper.cs` | modified | 100.00% (54/54) | 100.00% (6/6) | PASS |

## Uncovered branches in the new file (`OutlookScanner.Redaction.cs`)

- Line 63 (`NormalizeSensitiveMessage`, the `new MessageDto(...)` expression): 3/6 conditions covered. Uncovered: the `isMeeting == true` arms of both ternaries (`isMeeting ? "meeting" : "mail"` and `MeetingMessageType: isMeeting ? GetOptionalInt(item, "MeetingType") : null`) and the true short-circuit of `GetOptionalBool(item, "Attachments") || GetOptionalBool(item, "HasAttachments")`. Every sensitive-message normalization test uses a non-meeting `IPM.Note` double; no test scans a sensitive meeting-typed message.
- Line 170 (`IsMeetingItem` fallback): 1/2 conditions covered. The `string.IsNullOrWhiteSpace(messageClass) == true` short-circuit (null/whitespace `MessageClass`) is never exercised on this line.

Executor's `coverage-comparison.2026-07-02T09-25.md` reported per-file LINE coverage only (Redaction.cs 109/109 = 100.00%) and did not measure per-file BRANCH coverage, which masked the new-file branch shortfall behind the passing package aggregate (87.32%).

Raw cobertura reports for this run are retained under `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review/<guid>/coverage.cobertura.xml`.
