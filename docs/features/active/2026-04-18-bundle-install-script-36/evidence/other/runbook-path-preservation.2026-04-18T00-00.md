# Runbook Path Preservation Evidence

Timestamp: 2026-04-18T00-00
Command: `rg -n 'Install Path' docs/mailbridge-runbook.md`
EXIT_CODE: 0
Output Summary: PASS. All pre-existing `Install Path A`, `Install Path B`, and `Install Path C` section headings are still present verbatim. New `Install Path D: Scripted Bundle Install` section is present exactly once. Cross-reference lines appended at the ends of Path B and Path C (and only there). Zero removals of pre-existing Path A/B/C content.

## Raw grep output

```
46:## Install Path A: Published Binaries Plus Scheduled Task
166:## Install Path B: MSIX Package
329:For the scripted bundle flow that wraps these steps together with docker compose, see Install Path D below.
363:## Install Path C: Additive HostAdapter Plus Docker Core
469:For the scripted bundle flow that automates the compose stage (plus the MSIX install and rollback), see Install Path D below.
471:## Install Path D: Scripted Bundle Install
546:- A working HostAdapter with a valid token file (as configured in Install Path C above).
```

## Verification checklist

- (a) Presence of pre-existing `Install Path A:`: PASS (line 46).
- (a) Presence of pre-existing `Install Path B:`: PASS (line 166).
- (a) Presence of pre-existing `Install Path C:`: PASS (line 363).
- (b) Presence of new `Install Path D:`: PASS (line 471; exactly one occurrence).
- (c) Zero removals of pre-existing Path A content: PASS (Path A section body preserved verbatim from baseline).
- Path B cross-reference at end of Path B: PASS (line 329).
- Path C cross-reference at end of Path C: PASS (line 469).
- Line 546 inside "Optional OpenClaw Assistant Service" still references Path C as expected (unchanged from baseline).

## Additional troubleshooting rows added (P4-T3)

Three new rows appended to the Troubleshooting table:

- `No prior install recorded` (uninstall with absent `install-record.json`).
- `Docker Desktop is not running or not installed.` (install with docker stage enabled).
- `Manifest integrity check failed for bundle '<path>'.` (hash/size mismatch or missing files under the bundle root).

Each row cites the literal remediation text emitted by the script.
