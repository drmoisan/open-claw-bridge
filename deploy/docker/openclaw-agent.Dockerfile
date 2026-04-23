# syntax=docker/dockerfile:1.7

ARG OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest
FROM ${OPENCLAW_AGENT_IMAGE}

# Pre-install the ACP runtime so the embedded agent does not need runtime `npx`
# fetches or writable npm cache (container is read_only with noexec tmpfs).
USER root
RUN npm install -g @zed-industries/codex-acp@0.11.1 \
    && npm cache clean --force \
    && rm -rf /root/.npm /tmp/.npm /tmp/npm-* 2>/dev/null || true
USER 1654

# codex-acp (which wraps the Rust codex CLI) refuses to store its config under
# /tmp and cannot write to the read-only root FS. Point it at a path inside the
# existing /workspace named volume; the entrypoint creates the directory.
ENV CODEX_HOME=/workspace/.codex

# The upstream gateway spawns the ACP backend with `npx @zed-industries/codex-acp@^0.11.1`
# which always consults an npm cache even when the package is globally installed.
# Default cache is $HOME/.npm; HOME is `/` for user 1654 and the root FS is read_only,
# so npm fails with ENOENT on /.npm. Redirect the cache to the /workspace volume.
ENV NPM_CONFIG_CACHE=/workspace/.npm-cache

# Bake the assistant workspace into the image so the runtime no longer needs a
# host bind mount for its configuration, instructions, and skills.
COPY --chown=1654:1654 deploy/docker/openclaw-assistant /opt/openclaw-assistant-seed
COPY --chown=1654:1654 deploy/docker/openclaw-agent-entrypoint.sh /usr/local/bin/openclaw-agent-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/openclaw-agent-entrypoint.sh"]
CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]