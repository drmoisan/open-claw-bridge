# Capability 3 - web_search Provider Schema Pin (AC-14)

Timestamp: 2026-07-12T23-05

## Source of the pin

Source used: **documented research fallback shape** (research section D.2), NOT a direct
read of child B's aligned image.

Rationale:
- Child B (`installer-image-version-alignment`, wave 0) provides matched Control UI +
  gateway container images, but B's files are NOT present in this worktree (confirmed:
  no B installer/image artifacts on this branch), and this plan does not read B's
  artifacts.
- The current committed seed `deploy/docker/openclaw-assistant/openclaw.json` contains no
  `plugins` / provider block from which a version-specific provider key schema could be
  determined in this worktree (it has `gateway`, `session`, `tools.profile = coding`,
  and `agents` only).
- Therefore the version-dependent specifics are pinned against the documented research
  fallback shape rather than assumed from an unavailable image.

## Pinned schema (research section D.2)

- Provider name: `firecrawl` (the example provider returned by upstream `openclaw config`
  in the research; a `web_search` provider under the plugins/tools section).
- Config key path: `plugins.entries.firecrawl.config.webSearch.apiKey`
- SecretRef env var: `WEB_SEARCH_API_KEY`, referenced as the interpolation
  `${WEB_SEARCH_API_KEY}` (mirrors how `gateway.auth.token` references
  `${OPENCLAW_GATEWAY_TOKEN}`). No literal API key is written to the config.
- The API key value itself is human-held (external SaaS-issued) and supplied by the
  operator into `.env`/secrets; the runbook (capability 4) documents that handoff.

## Notes

- The provisioning SHAPE (provider entry + `${...}` SecretRef + `.env`/secrets-backed key)
  is what this feature fixes; exact upstream key names remain version-dependent and would
  be re-pinned against B's aligned image if/when B's schema becomes available. The script
  accepts `-ProviderName` and `-ApiKeyEnvVar` parameters so the pin can be re-targeted
  without code changes.
