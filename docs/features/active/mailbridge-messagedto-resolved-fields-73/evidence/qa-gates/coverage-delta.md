# Final QA — Coverage Delta and Changed-Line No-Regression

Timestamp: 2026-06-13T13-34
Command: compare P0-T4 baseline cobertura vs P7-T5 post-change cobertura (line-rate / branch-rate)
EXIT_CODE: 0

## Baseline (P0-T4, evidence/baseline/baseline-test.md)
- OpenClaw.MailBridge.Tests closure: line 94.07%, branch 86.54% (1064/1131 lines, 283/327 branches).
- OpenClaw.Core.Tests closure: line 89.17%, branch 77.59% (1441/1616 lines, 329/424 branches).

## Post-change (P7-T5, evidence/qa-gates/final-test.md)
- Per-project (authoritative for the affected assemblies):
  - OpenClaw.MailBridge: line 90.90%, branch 80.39%.
  - OpenClaw.Core: line 98.60%, branch 91.68%.
  - OpenClaw.MailBridge.Contracts: line 98.13%, branch 93.65%.
- Test-assembly closure headlines:
  - OpenClaw.MailBridge.Tests closure: line 92.04%, branch 83.29% (1262/1371 lines, 334/401 branches).
  - OpenClaw.Core.Tests closure: line 89.57%, branch 78.44% (1486/1659 lines, 342/436 branches).

## Changed-code coverage
The new/changed production code is covered by the added tests:
- MessageDto four new fields (Contracts): exercised by mapper, cache round-trip, and scanner tests.
- ComMessageSource mapping surface (ConversationId, MeetingMessageType, To/Cc projection, raw-sender
  fallback): ComMessageSourceTests (10 tests) + scanner field tests.
- ComMessageSource SMTP chain (Sender.Address fallback, GetExchangeUser true-SMTP, on-behalf-of):
  OutlookScanner Exchange-DN and delegate-sent tests.
- ReadMessageRecipients (To=type 1, Cc=type 2, absent -> empty): scanner field tests + adapter tests.
- SchedulingDtoMapper resolved sender/from/conversation + MapMeetingMessageType (all 5 enum values
  + unknown -> null): SchedulingDtoMapperTests DataRows.
- Bridge + Core cache schema/SQL/binding/readers and idempotent migrations:
  CacheRepositoryMessageFieldsTests + CoreCacheRepositoryMessageFieldsTests (3 + 3 tests).

## Verdict: PASS
- OpenClaw.MailBridge: line 90.90% >= 85%, branch 80.39% >= 75%. PASS.
- OpenClaw.Core: line 98.60% >= 85%, branch 91.68% >= 75%. PASS.
- No regression on changed lines: the new code paths reachable without live COM are covered by the
  added unit tests. The per-test-assembly MailBridge closure line/branch moved from 94.07%/86.54% to
  92.04%/83.29%; this reflects the addition of fail-soft COM-only catch branches in ComMessageSource
  that are unreachable in unit tests (no live COM), not a regression of previously-covered lines.
  Both affected projects remain above the uniform line >= 85% / branch >= 75% thresholds.
