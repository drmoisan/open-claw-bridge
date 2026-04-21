# Follow-ups — issue #38

These items are explicitly **out of scope** for the bug fix delivered by this PR and should be opened as separate tracking issues.

## 1. Stale `8181` references in archived feature folders

Research §2.9 flagged obsolete `8181` port references in the v1 and v2 feature folders:

- `docs/features/active/2026-04-16-openclaw-agent-docker-30/v1/`
- `docs/features/active/2026-04-16-openclaw-agent-docker-30/v2/`

These are archival and do not affect the current deployment. Audit and cleanup should happen as a separate docs pass.

## 2. AGENTS.md generator header

The `AGENTS.md` file at the repository root contains a header block referencing a sync script:

```
pwsh -File scripts/dev-tools/sync-agents-from-instructions.ps1
```

The referenced script does not exist in the repository. The current PR edited `AGENTS.md` directly (per the orchestrator scope lock). A future task should either:

- implement `scripts/dev-tools/sync-agents-from-instructions.ps1` so the generator header is truthful, or
- remove the generator header from `AGENTS.md` so the file is treated as a direct-edit document.

## 3. Upstream-binary-path verification for onboarding

Research §3.1 flagged that the onboarding flow assumes the upstream image exposes the gateway CLI at `dist/index.js`. This PR proceeds with that assumption; empirical verification against the current `${OPENCLAW_AGENT_IMAGE}` tag is a separate manual verification step. If upstream renames the binary path, `scripts/Invoke-OpenClawAgentOnboarding.ps1` will fail fast with a clear error category; operators can then re-run the script after the upstream path is corrected.

## 4. Non-remediation findings from 2026-04-21 review

The 2026-04-21 code and policy review (see `code-review.2026-04-21T00-00.md`, `policy-audit.2026-04-21T00-00.md`, and `remediation-inputs.2026-04-21T00-00.md`) identified the following non-blocker findings. They are deferred from the R1/R2/R3 remediation cycle and should be opened as separate tracking issues.

- Medium — `scripts/Invoke-OpenClawAgentOnboarding.ps1:113-152, 212-221` — Anthropic API key passed as a plaintext argv element to docker compose run; upstream onboarding contract requires plaintext delivery, so the in-memory window is an upstream constraint, not introduced by this branch. Track a future upstream change to accept a file-based secret.
- Medium — `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:61-72, 95-138` — `Get-OpenClawContentPreview` conflates a whitespace-only body with an absent body; probes that depend on body presence may misclassify upstream behavior. Add a whitespace-vs-absent distinction with regression tests.
- Medium — `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:211-230, 309-336` — `Test-OpenClawGatewayTokenPresence` does not strip surrounding quotes from `.env` values; a quoted token will be probed literally with quotes. Add quote-stripping with regression tests.
- Medium — `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1:1-10` — script-scope `SuppressMessageAttribute` is broader than required; narrow the suppressions to the specific `It`/`BeforeEach` blocks that require them.
- Medium — `scripts/Invoke-OpenClawAgentOnboarding.ps1:41, 169, 199-221` — `SupportsShouldProcess` is advertised at script level but the `docker compose run` side effect is invoked unconditionally; either honor `ShouldProcess` around the docker call or remove the advertisement.
- Low — `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:268-302` — `Invoke-OpenClawHostAdapterInContainerProbe` relies on a `curl -w "%{http_code}"` format-string contract; brittle against upstream output changes. Add a malformed-output regression test and consider a fallback parser.
- Low — `scripts/Invoke-OpenClawContainerPathValidation.ps1:33-39` — stale-module-version scenario is not explicitly handled: if `OpenClawContainerValidation` is already loaded at an older version, the current implementation reuses it. Add a version-check-and-warn path.
- Info — `.env.example` (token block) — add a `# Do not surround the value in quotes; docker compose expands the raw value.` comment to prevent operator confusion that could produce a quoted token.
- Info — `scripts/Invoke-OpenClawAgentOnboarding.ps1:160` — the token-extraction regex does not strip outer quotes from upstream output; if upstream prints a quoted token line, the quotes would be preserved in `.env`. Add defensive stripping with regression tests.
