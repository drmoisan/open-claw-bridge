# Baseline — Profile Grep (Pre-Fix)

- Timestamp: 2026-04-22T10:53:00Z
- Command: `Select-String -Pattern '"profile"' deploy/docker/openclaw-assistant/openclaw.json`
  (Equivalent to: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json` on Unix)
- EXIT_CODE: 0

## Output Summary

`deploy\docker\openclaw-assistant\openclaw.json:15:    "profile": "minimal"`

`"profile": "minimal"` confirmed on line 15 of `deploy/docker/openclaw-assistant/openclaw.json`.

This is the **pre-fix state**. The fix (Phase 1, P1-T1) will change this value to `"coding"`.
The fix has **not** been applied at the time of this baseline capture.
