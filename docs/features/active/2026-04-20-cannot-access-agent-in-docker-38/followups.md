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
