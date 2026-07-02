# Fail-Before Evidence — Scanner Sensitivity-Normalization Tests (P2-T2, [expect-fail])

Timestamp: 2026-07-02T09-08
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerSensitivityNormalizationTests"`
EXIT_CODE: 1
Output Summary:
- Compilation succeeded (0 warnings, 0 errors); the failures are behavioral, not build failures.
- Result: Failed! — Failed: 10, Passed: 16, Skipped: 0, Total: 26 (OpenClaw.MailBridge.Tests.dll).
- Failing tests (pre-implementation, as expected — the scanner does not yet redact):
  - `Sensitive_message_should_be_fully_redacted` (2) and (3)
  - `Sensitive_event_should_be_fully_redacted` (2) and (3)
  - `Sensitive_message_normalization_should_never_access_protected_members` (2) and (3)
  - `Sensitive_event_normalization_should_never_access_protected_members` (2) and (3)
  - `Message_redaction_should_log_bridge_id_only_at_information_level`
  - `Event_redaction_should_log_bridge_id_only_at_information_level`
- Passing tests (16): the 12 boundary-value tests (`Boundary_sensitivity_message_should_stay_unredacted`, `Boundary_sensitivity_event_should_stay_unredacted` x 6 each) pass because non-sensitive behavior is already correct; the 4 mechanical-retention tests pass because mechanical fields are already populated pre-redaction.
- Test file: `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` (360 lines, <= 500).
