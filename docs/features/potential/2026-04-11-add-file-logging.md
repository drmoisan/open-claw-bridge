---
title: "add-file-logging - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-33"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# add-file-logging (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The bridge process runs as a Windows Task Scheduler task in the primary user's interactive session. It currently configures console-only logging (`AddSimpleConsole`). When the task runs unattended, standard output is not captured anywhere, so diagnostic information is permanently lost. Operators and support personnel have no persistent file to inspect after a failure, degraded state, or unexpected restart. The design audit (section 8, deviation #9) classifies this as a High-severity gap and a blocking exit criterion.

## Proposed Behavior

Add a file logging provider to `BridgeApplication.BuildHost` that writes structured log output to:

```
%LOCALAPPDATA%\OpenClaw\MailBridge\logs\bridge.log
```

- The `logs\` directory must be created automatically if it does not exist at startup.
- Console logging (`AddSimpleConsole`) must continue to operate alongside file logging.
- Log entries must include timestamp, log level, category, and message — no message body, subject, sender name, sender email, or preview text may appear in the log (logging hygiene already audited as PASS for content; this must be preserved).
- The file logging provider must not introduce a new NuGet package dependency. A custom `ILoggerProvider`/`ILogger` implementation is preferred, writing via `StreamWriter` with append mode and a per-entry flush.
- Log level applied to the file provider must respect the configured `LogLevel` value in `bridge.settings.json`.
- A write failure in the file provider must not crash or block the bridge host; the provider should swallow I/O exceptions internally and continue.

## Acceptance Criteria (early draft)

- [ ] On bridge startup, `%LOCALAPPDATA%\OpenClaw\MailBridge\logs\bridge.log` is created (or appended to) without error.
- [ ] The `logs\` directory is created if it does not already exist; no startup failure occurs when the directory is absent.
- [ ] Log entries written to `bridge.log` include timestamp, level, category, and message text.
- [ ] No log entry contains message body, subject line, sender name, sender email, or body preview text.
- [ ] Console logging continues to emit output when a console is attached (no regression to existing behaviour).
- [ ] The configured `LogLevel` governs which entries appear in `bridge.log` (entries below the threshold are suppressed).
- [ ] An I/O failure writing to the log file (e.g., disk full, path inaccessible) does not terminate the bridge process.
- [ ] No new NuGet package is added to `OpenClaw.MailBridge.csproj` to satisfy this feature.
- [ ] The existing installer preflight check (lines 206–209 of `install-mailbridge.ps1`) that asserts no message bodies appear in the log file continues to pass.

## Constraints & Risks

- **No new dependencies.** `Microsoft.Extensions.Logging` does not include a built-in file provider. A lightweight custom `ILoggerProvider` must be implemented inside the `OpenClaw.MailBridge` project. Third-party packages (Serilog, NLog, `Microsoft.Extensions.Logging.Log4Net.AspNetCore`) must not be introduced without explicit approval.
- **Log rotation not in scope.** `bridge.log` will grow unbounded. If log rotation is eventually required, it must be a separate feature. Operators should be made aware of this limitation in the runbook.
- **Concurrent access.** Multiple simultaneous bridge instances would write to the same file path. The installer and Task Scheduler configuration prevent multiple instances in normal operation, but the file provider implementation should use a file lock or serialized write queue to be safe.
- **PII hygiene must be preserved.** The file provider must not alter the content of log messages. Existing structured logging at the call sites already avoids PII; the provider implementation must not add or transform message content.
- **Path resolution.** `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` must be used instead of expanding `%LOCALAPPDATA%` via environment variable substitution, for consistency with the existing settings path resolution in `BridgeApplication.cs:16-21`.

## Test Conditions to Consider

- [ ] **Unit — directory creation:** Verify that `FileLoggerProvider` creates the `logs\` directory when it does not exist, using a path injected via constructor (no filesystem access in tests; mock or abstract the directory/file creation).
- [ ] **Unit — log level filtering:** Verify that entries below the configured minimum level are not written.
- [ ] **Unit — I/O resilience:** Verify that an exception thrown by the underlying writer is caught internally and does not propagate to the caller.
- [ ] **Unit — entry format:** Verify that a log entry includes timestamp, level, category, and message, and does not include a hardcoded body or subject field.
- [ ] **Integration — startup creates file:** After `BuildHost` and a brief startup, assert that `bridge.log` exists at the expected path.
- [ ] **Installer preflight:** Confirm `install-mailbridge.ps1` lines 206–209 (regex check for message bodies in log) continues to pass after file logging is active.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/add-file-logging/` folder from the template

