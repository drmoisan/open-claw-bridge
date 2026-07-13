# Capability 2 - Expect-Fail (production script absent)

Timestamp: 2026-07-12T23-05

Command: Invoke-Pester -Configuration (Run.Path = tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1, PassThru)
EXIT_CODE: non-zero (test failure) - PassedCount=0, FailedCount=9, TotalCount=9

Output Summary:
All nine capability-2 `It` blocks FAIL because the production script
`scripts/Invoke-OpenClawDeviceTokenRotation.ps1` does not yet exist. This is the
expected [expect-fail] baseline for the test-first sequence. The nine behaviors under test:
(a) with -Force + existing file, a new non-empty base64url secret is written BEFORE any docker restart;
(b) restarts openclaw-core then openclaw-agent via Invoke-OpenClawDockerCommand with @('restart',<name>);
(c) -WhatIf performs no file write and no restart (ShouldProcess gate);
(d) without -Force, an already-valid token file is not rotated and no restart occurs (idempotent no-op);
(e) an unwritable token file throws explicitly;
(f) a docker restart failure (Succeeded=$false) throws explicitly;
(g) an absent host token file throws directing to the runbook and creates no placeholder;
(h) the token value never appears in verbose/debug/warning/information/error streams;
(i) the generated secret matches the base64url charset (shape only).

Next: implement scripts/Invoke-OpenClawDeviceTokenRotation.ps1 (P2-T3..P2-T7) to turn these green.
