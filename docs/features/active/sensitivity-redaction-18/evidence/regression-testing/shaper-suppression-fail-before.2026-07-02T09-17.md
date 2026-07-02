# Fail-Before Evidence — Safe-Mode Suppression Tests (P3-T2, [expect-fail])

Timestamp: 2026-07-02T09-17
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~ResponseShaperSafeModeSuppressionTests"`
EXIT_CODE: 1
Output Summary:
- Compilation succeeded; failures are behavioral against the current (pre-P3-T4) `ResponseShaper`.
- Result: Failed! — Failed: 2, Passed: 4, Skipped: 0, Total: 6.
- Failing (as expected — the extended suppression set is not yet implemented):
  - B1 `ShapeMessage_safe_mode_should_suppress_full_protected_field_set`
  - B3 `ShapeEvent_safe_mode_should_suppress_organizer_categories_and_set_flag`
- Already passing pre-change (noted per plan): B2 `ShapeMessage_safe_mode_should_retain_all_mechanical_fields`, B4 `ShapeEvent_safe_mode_should_retain_location_and_all_mechanical_fields`, B5 `Enhanced_mode_should_pass_through_all_fields_without_forcing_flag`, B6 `Already_null_protected_fields_should_shape_without_error_in_both_modes` — retention, enhanced pass-through, and already-null tolerance are existing behavior.
- File: `tests/OpenClaw.MailBridge.Tests/ResponseShaperSafeModeSuppressionTests.cs` (242 lines, <= 500).
