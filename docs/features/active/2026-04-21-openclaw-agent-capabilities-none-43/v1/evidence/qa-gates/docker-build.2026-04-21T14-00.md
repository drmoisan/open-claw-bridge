# Evidence — Docker image rebuild (P5-T3)

Timestamp: 2026-04-21T14:54:00Z

Command: `docker compose build openclaw-agent`

EXIT_CODE: 0

Output Summary:
- Build performed three times during this phase (initial + two amendments).
- Final image digest: `sha256:510ce16bc0d5b78ca83ff2c7174bc25f4ea5ad646abcc6999f4c0be42c0d04e6` (manifest list).
- Final image ID: `openclaw/agent:pre-mvp`.
- New layer added by the RUN line: `npm install -g @zed-industries/codex-acp@0.11.1 && npm cache clean --force`.
- `added 2 packages in 4s` reported by npm during install. No warnings beyond the standard "using --force Recommended protections disabled" for `npm cache clean`.
- Amendments Phase 4B and Phase 4C added three `ENV` lines (`CODEX_HOME`, `NPM_CONFIG_CACHE`) and do not require an extra layer because they are metadata-only.
- Existing `COPY --chown=1654:1654` layers and `ENTRYPOINT` / `CMD` declarations preserved byte-identical vs pre-change baseline.
