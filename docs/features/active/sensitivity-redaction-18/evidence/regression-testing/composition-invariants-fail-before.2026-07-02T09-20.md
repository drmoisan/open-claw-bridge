# Fail-Before Evidence — Composition Invariant Tests (P3-T3, [expect-fail])

Timestamp: 2026-07-02T09-20
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~ResponseShaperCompositionInvariantTests"`
EXIT_CODE: 1
Output Summary:
- Compilation succeeded; failures are behavioral against the current (pre-P3-T4) `ResponseShaper`, which forces `IsRedacted = false` in enhanced mode and `IsRedacted = true` in safe mode.
- Result: Failed! — Failed: 4, Passed: 1, Skipped: 0, Total: 5.
- Failing (as expected):
  - C1 `Redacted_message_should_survive_enhanced_mode_shaping` (enhanced branch falsifies `IsRedacted`)
  - C1 `Redacted_event_should_survive_enhanced_mode_shaping`
  - C3 `Shapers_should_never_mutate_is_redacted_in_either_mode`
  - C4 `Protected_fields_available_false_should_hold_on_both_paths` (safe mode does not yet force the flag)
- Passing pre-change: C2 `Redacted_dtos_should_keep_is_redacted_through_safe_mode_without_error` (safe mode currently sets `IsRedacted = true`, so an already-true value is coincidentally preserved and re-nulling does not throw).
- File: `tests/OpenClaw.MailBridge.Tests/ResponseShaperCompositionInvariantTests.cs` (176 lines, <= 500).
