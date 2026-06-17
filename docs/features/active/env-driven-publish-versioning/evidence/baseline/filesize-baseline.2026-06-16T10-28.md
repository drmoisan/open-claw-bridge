# Baseline — File Size (line counts near the 500-line cap)

Timestamp: 2026-06-16T11-26
Command: wc -l on the in-scope production scripts and the test file to be split
EXIT_CODE: 0

Output Summary (baseline line counts):
- scripts/Publish.Helpers.psm1: 581 lines (OVER the 500-line cap before any change).
- scripts/Publish.ps1: 213 lines.
- scripts/New-MsixDevCert.ps1: 169 lines.
- tests/scripts/Publish.Helpers.Tests.ps1: 541 lines (OVER the 500-line cap before any change).
- tests/scripts/Publish.Tests.ps1: 255 lines.
- tests/scripts/New-MsixDevCert.Tests.ps1: 111 lines.

Extraction rationale established:
- Publish.Helpers.psm1 (581) is already over cap, so new .env/version helpers cannot be added to it. New helpers go into a new dedicated module scripts/Publish.Env.psm1 (plan P1-T1..T4).
- Publish.Helpers.Tests.ps1 (541) is already over cap, so the Resolve-CertThumbprint context is extracted into a new sibling tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1 before extending the cert tests (plan P1-T7).
