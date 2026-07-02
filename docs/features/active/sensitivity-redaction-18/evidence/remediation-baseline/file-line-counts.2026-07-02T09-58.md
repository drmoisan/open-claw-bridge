# Remediation Baseline — Test File Line Counts

Timestamp: 2026-07-02T09-58
Command: `wc -l tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs`
EXIT_CODE: 0
Output Summary:

- `tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs`: 190 lines (`wc -l` newline count; plan expected 191 — the file's final line has no trailing newline, so the editor-visible count is 191; difference is a counting convention, not content drift).
- `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs`: 364 lines (matches plan expectation).
- These counts are the not-to-exceed-500 references for P3-T6.
