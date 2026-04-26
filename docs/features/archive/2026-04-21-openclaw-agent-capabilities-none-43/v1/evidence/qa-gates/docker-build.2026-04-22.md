# Docker Build Evidence — P2-T1

Timestamp: 2026-04-22T10:55:00Z
Command: docker compose build openclaw-agent
EXIT_CODE: 0

## Output Summary

Build completed without errors. Image `openclaw/agent:pre-mvp` built in 74.2s.

Key build steps:
- [1/4] FROM ghcr.io/openclaw/openclaw:latest — base image pulled
- [2/4] RUN npm install -g @zed-industries/codex-acp@0.11.1 — codex-acp installed globally (CACHED)
- [3/4] COPY deploy/docker/openclaw-assistant → /opt/openclaw-assistant-seed (includes updated openclaw.json with "profile": "coding")
- [4/4] COPY entrypoint script

Final image manifest: sha256:d25ae6f5c59504ecf716a10ff377bd27f0359352b8854a2ee54094e3c83bd947
Config: sha256:aac0c0670496affd6c075e9856405082a20ab5f1102cd30f6a15c3a4c8c9a4ec

No errors or warnings. Build exited with code 0.
