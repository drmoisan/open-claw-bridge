# Fail-Before Evidence — Deliberate Shaper Assertion Changes (P3-T1, [expect-fail])

Timestamp: 2026-07-02T09-17
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~ResponseShaperTests|FullyQualifiedName~ResponseShaperEventBodyFullTests"`
EXIT_CODE: 1
Output Summary:
- Compilation succeeded; failures are behavioral against the current (pre-P3-T4) `ResponseShaper`, which still sets `IsRedacted = true` in safe mode and does not yet suppress the extended field set.
- Result: Failed! — Failed: 4, Passed: 8, Skipped: 0, Total: 12.
- Failing tests (all four deliberately-changed tests, as expected):
  - `ShapeMessage_in_safe_mode_should_suppress_protected_fields_without_setting_is_redacted` (was `ShapeMessage_in_safe_mode_should_redact_sender_fields_and_clear_preview`)
  - `ShapeEvent_in_safe_mode_should_clear_preview_and_suppress_without_setting_is_redacted` (was `ShapeEvent_in_safe_mode_should_clear_preview_and_redact`)
  - `ShapeEvent_in_safe_mode_should_null_body_full_and_preserve_is_redacted` (was `ShapeEvent_in_safe_mode_should_null_body_full_and_set_redacted`)
  - `ShapeEvent_in_safe_mode_should_null_all_three_attendee_fields` (IsRedacted assertion inverted)
- Rationale comment recorded at each change site: the conflation defect — `IsRedacted` becomes the exclusive sensitivity-redaction signal; `ProtectedFieldsAvailable = false` becomes the suppression signal (spec "Existing tests whose behavior deliberately changes" table).
- `CreateMessage` helper updated to populate `ToJson`/`CcJson`/`SenderEmailResolved`/`FromEmailAddress` so the new null assertions are meaningful.
- File sizes: `ResponseShaperTests.cs` 158 lines; `ResponseShaperEventBodyFullTests.cs` 134 lines (both <= 500).
