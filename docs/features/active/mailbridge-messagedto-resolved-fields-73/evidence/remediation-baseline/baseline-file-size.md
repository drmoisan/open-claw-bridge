# Baseline — File-Size State (Issue #73, Cycle 1)

Timestamp: 2026-06-14T09-17
Command: wc -l src/OpenClaw.Core/CoreCacheRepository.cs src/OpenClaw.Core/CoreCacheRepository.Schema.cs src/OpenClaw.MailBridge/ComMessageSource.cs tests/OpenClaw.MailBridge.Tests/ComMessageSourceTests.cs
EXIT_CODE: 0

Output Summary (line counts):
- src/OpenClaw.Core/CoreCacheRepository.cs: 699 lines (EXCEEDS the 500-line cap; RF-2 target).
- src/OpenClaw.Core/CoreCacheRepository.Schema.cs: 241 lines (within cap).
- src/OpenClaw.MailBridge/ComMessageSource.cs: 314 lines (within cap).
- tests/OpenClaw.MailBridge.Tests/ComMessageSourceTests.cs: 154 lines (within cap).

Confirmation: CoreCacheRepository.cs = 699 > 500. RF-2 file-size violation confirmed.
