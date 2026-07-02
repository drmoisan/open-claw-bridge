# Expect-Fail Evidence — Candidate Widening (P2-T1)

Timestamp: 2026-07-02T13-11
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CacheSchedulingCandidateSourceTests"`
EXIT_CODE: 1
Output Summary:
- Failed: 2, Passed: 2, Total: 4 (OpenClaw.Core.Tests.dll)
- Failing tests (expected — production still filters `item_kind = 'meeting'`):
  - `GetCandidateMessageIds_returns_mail_alongside_meeting_within_window` (mail-inclusion assertion)
  - `GetCandidateMessageIds_preserves_recency_ordering_across_kinds` (mail rows absent from result)
- Passing tests (behavior-preserving pre/post change): lookback exclusion, limit cap.
- This is the required fail-before state for P2-T2 (kind literal `"meeting"` -> `"all"`).
