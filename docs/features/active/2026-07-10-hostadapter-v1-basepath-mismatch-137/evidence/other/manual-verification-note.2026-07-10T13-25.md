Timestamp: 2026-07-10T13-25

This note documents required future manual/integration verification for Issue #137. This verification is NOT automated in this pass.

Reason: end-to-end Docker-stage installer verification requires publishing a bundle and running the real installer against a live Docker Desktop stack, which is outside the scope of the automated unit-test toolchain gates run in this plan and is not deterministic in a CI/unit-test context (external process, external Docker daemon dependency).

Required command sequence (to be run manually by an operator or in a dedicated integration environment):

1. `scripts/Publish.ps1` — publish a fresh bundle (e.g. to `artifacts/publish/<version>/`).
2. `Install.ps1 -DockerEnvFilePath (Join-Path $operatorConfig '.env') -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')` — run the full install flow WITHOUT `-SkipDocker`, using an operator `.env` copied from the corrected `.env.example` (left at its defaults, i.e. `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319`, no `/v1`).

Expected outcome:
- The HostAdapter preflight probe requests `GET http://127.0.0.1:4319/status` (no `/v1` segment) and receives a 200/valid envelope.
- `Install.ps1` proceeds past the preflight stage into the Docker stage without throwing the "HostAdapter preflight failed before starting Docker" exception.
- The Docker stack (`docker compose up`) starts successfully using the corrected `docker-compose.yml`/`docker-compose.dev.yml` defaults.

Status: NOT PERFORMED in this automated pass. This is an outstanding follow-up recorded per the plan's Phase 4 and the Test Strategy section of `spec.md`.
