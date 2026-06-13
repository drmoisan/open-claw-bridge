# AC-12 Property-Test Density Re-Verification — Issue #70 FIX-1

Timestamp: 2026-06-09T13-19
Command: rg -l "\.Sample\(" tests/OpenClaw.Core.Tests/Agent/*PropertyTests.cs ; rg -n "Normalize\(|DependencyScorer.Score|TriageEngine.Triage|OwnerPriorityClassifier.Classify|RecurringMeetingClassifier.Classify|MovePolicy.CanMove|SlotProposer.ProposeTimes" tests/OpenClaw.Core.Tests/Agent/*PropertyTests.cs
EXIT_CODE: 0

Output Summary: All seven AC-12 pure functions are each mapped to at least one CsCheck
property test using `.Sample(...)`. Property-test files containing `.Sample(`:
MeetingContextNormalizerPropertyTests.cs, TriagePropertyTests.cs, PriorityPropertyTests.cs,
SlotProposerPropertyTests.cs, RecurringMeetingClassifierPropertyTests.cs (new).

## Per-function mapping (function -> covering PropertyTests file / method)

| # | Pure function | Covering property test file | Method |
|---|---|---|---|
| 1 | MeetingContextNormalizer.Normalize | MeetingContextNormalizerPropertyTests.cs | normalize property (line 94: `MeetingContextNormalizer.Normalize(...)`) |
| 2 | DependencyScorer.Score | TriagePropertyTests.cs | Score_IsAlwaysNonNegative |
| 3 | TriageEngine.Triage | TriagePropertyTests.cs | Triage_AlwaysReturnsOneOfFiveDecisions |
| 4 | OwnerPriorityClassifier.Classify | PriorityPropertyTests.cs | property using `OwnerPriorityClassifier.Classify(ctx, Policy)` (line 65) |
| 5 | RecurringMeetingClassifier.Classify | RecurringMeetingClassifierPropertyTests.cs (NEW) | Classify_AlwaysReturnsDefinedKind, Classify_PartitionInvariants_Hold |
| 6 | MovePolicy.CanMove | PriorityPropertyTests.cs | CanMove_Forum_DeniedForNonOwnerNonMeetingOwnerRequester (uses `.Sample`, line 100-101) |
| 7 | SlotProposer.ProposeTimes | SlotProposerPropertyTests.cs | EveryReturnedSlot_RespectsWorkingHoursBlocksAndMinNotice |

Verdict: PASS. All seven AC-12 pure functions, including the previously-uncovered
RecurringMeetingClassifier.Classify, now each have at least one CsCheck property test. The
FIX-1 gap (AC-12 / T1 property-test density) is closed.
