# syntax=docker/dockerfile:1.7

ARG OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest
FROM ${OPENCLAW_AGENT_IMAGE}

# Bake the assistant workspace into the image so the runtime no longer needs a
# host bind mount for its configuration, instructions, and skills.
COPY --chown=1654:1654 deploy/docker/openclaw-assistant /opt/openclaw-assistant-seed
COPY --chown=1654:1654 deploy/docker/openclaw-agent-entrypoint.sh /usr/local/bin/openclaw-agent-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/openclaw-agent-entrypoint.sh"]
CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]