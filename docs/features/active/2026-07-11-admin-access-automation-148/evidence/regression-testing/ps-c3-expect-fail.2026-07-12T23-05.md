# Capability 3 - Expect-Fail (production script absent)

Timestamp: 2026-07-12T23-05

Command: Invoke-Pester -Configuration (Run.Path = tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1, PassThru)
EXIT_CODE: non-zero (test failure) - PassedCount=0, FailedCount=6, TotalCount=6

Output Summary:
All six capability-3 `It` blocks FAIL because the production script
`scripts/Set-OpenClawWebSearchProvider.ps1` does not yet exist. This is the expected
[expect-fail] baseline for the test-first sequence. The six behaviors under test:
(a) provisioning a seed with no provider entry adds a web_search (firecrawl) provider
    entry whose API key is a SecretRef ${WEB_SEARCH_API_KEY} interpolation, not a literal;
(b) re-running on an already-provisioned seed yields identical JSON with no duplicate (idempotent);
(c) -WhatIf writes nothing (ShouldProcess gate);
(d) invalid input JSON throws explicitly (message names JSON);
(e) a missing referenced provider-key env var (WEB_SEARCH_API_KEY) throws explicitly;
(f) the resulting JSON round-trips through ConvertFrom-Json and preserves the pre-existing
    gateway.auth.token=${OPENCLAW_GATEWAY_TOKEN} and tools.profile=coding entries.

Next: implement scripts/Set-OpenClawWebSearchProvider.ps1 (P3-T4..P3-T6) to turn these green.
