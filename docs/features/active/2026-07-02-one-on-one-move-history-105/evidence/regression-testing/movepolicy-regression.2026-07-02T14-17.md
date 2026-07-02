# Regression — MovePolicy and RecurringMeetingClassifier Suites (AC-3)

Timestamp: 2026-07-02T14-17
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~MovePolicyTests|FullyQualifiedName~RecurringMeetingClassifier"` (repo root)
EXIT_CODE: 0
Output Summary:
- 17 passed, 0 failed, 0 skipped (OpenClaw.Core.Tests.dll): all `MovePolicyTests`, `RecurringMeetingClassifierTests`, and `RecurringMeetingClassifierPropertyTests` cases pass unmodified.
- Diff-scope confirmation via `git diff --name-only` plus `git status --porcelain` (untracked):
  - Modified: `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`, `src/OpenClaw.Core/Program.cs`
  - New: `src/OpenClaw.Core/Agent/Contracts/ISeriesMoveHistory.cs`, `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs`, `src/OpenClaw.Core/CoreCacheRepository.SeriesMoves.cs`, `tests/OpenClaw.Core.Tests/CoreCacheRepositorySeriesMovesTests.cs`, `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardTests.cs`, `tests/OpenClaw.Core.Tests/Agent/OneOnOneMoveGuardPropertyTests.cs`, feature-folder docs/evidence
  - Confirmed NOT modified: `src/OpenClaw.Core/Agent/MovePolicy.cs`, `src/OpenClaw.Core/Agent/RecurringMeetingClassifier.cs`, `tests/OpenClaw.Core.Tests/Agent/MovePolicyTests.cs`, `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierTests.cs`, `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierPropertyTests.cs` (none appear in the diff or untracked lists)
- This regression evidence complements the P2-T3 delegation-equivalence property (`CanMove_NonOneOnOneKinds_DelegateToMovePolicyUnchanged`).
