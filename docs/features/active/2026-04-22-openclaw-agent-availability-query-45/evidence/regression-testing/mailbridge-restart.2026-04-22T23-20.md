# P5-T3 — Restart MailBridge (operator runbook + evidence template)

Timestamp: 2026-04-22T23-20

## Status

PENDING_MANUAL_VERIFY

## Operator Runbook

MailBridge runs on the operator's Windows workstation (outside Docker). It must be stopped and relaunched so the new scanner reads `AppointmentItem.ResponseStatus` and the new cache migration adds the `response_status` column to the local SQLite database.

### Steps

1. Stop any running MailBridge instance (Task Manager or `taskkill /IM OpenClaw.MailBridge.exe /F`). Close the MailBridge process started by either the Outlook session or a previous manual run.

2. Start MailBridge from source or from the published build. If running from source during verification:

   ```
   dotnet run --project src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj
   ```

   If a packaged build is installed, launch it via the operator's normal start method (desktop shortcut or published script).

3. Observe the MailBridge log output (console or log file). Wait for at least one complete scan cycle. The success log line is similar to `Scan completed. LastCalendarScanUtc=...`.

4. Confirm the HostAdapter reports a fresh scan:

   ```
   curl -H "Authorization: Bearer <token>" http://127.0.0.1:4319/v1/status
   ```

   The response JSON must show `"cacheStale": false` and a recent `"lastCalendarScanUtc"`.

5. Record in this artifact:

   ```
   Start command: <exact command/script used>
   EXIT_CODE: <0 if the process started cleanly; a non-zero value with first error line otherwise>
   First scan observed at: <ISO-8601 local time>
   LastCalendarScanUtc: <value from GET /v1/status>
   cacheStale: <false expected>
   Completed: <ISO-8601 timestamp>
   ```

### Expected Outcome

- MailBridge process running and stable.
- `GET /v1/status` returns `cacheStale: false` within one scan interval (default 300 s per `openclaw-core` compose settings).
- Local SQLite database (`%LOCALAPPDATA%/OpenClaw/MailBridge/cache.db`) now contains a `response_status` column (verifiable with any SQLite inspection tool: `PRAGMA table_info(events);`).
