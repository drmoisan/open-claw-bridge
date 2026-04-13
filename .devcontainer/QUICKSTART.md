# Dev Container Quickstart

## First Use

### Local Docker

1. Start Docker Desktop.
2. Open the repository in VS Code.
3. Run `Dev Containers: Reopen in Container`.
4. Use `.devcontainer/devcontainer.json` or `.devcontainer/local/devcontainer.json`.
5. Wait for `post-create.sh` to finish.
6. Verify the environment:

```bash
bash .devcontainer/verify-container.sh
```

7. For the compose-backed `OpenClaw.Core` workflow, copy `.env.example` to `.env` and run:

```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml up openclaw-dev
```

This local-only path expects `host.docker.internal` so the container can reach the Windows HostAdapter.

### GitHub Codespaces

1. Create a Codespace for the repository.
2. Use the standard root config or explicitly choose `.devcontainer/codespaces/devcontainer.json`.
3. Wait for the post-create step to finish.
4. Verify the environment:

```bash
bash .devcontainer/verify-container.sh
```

## Daily Commands

```bash
pwsh -File scripts/Build.ps1
pwsh -File scripts/Test.ps1
dotnet build OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -f net10.0 -p:EnableWindowsTargeting=true
```

## Important Limitation

The container can restore and build the Windows-targeted projects, but it is still a Linux environment. Do not expect these to work inside the container:

- Outlook COM
- classic Outlook integration checks
- running `OpenClaw.MailBridge`
- running `OpenClaw.MailBridge.Client`

Use a Windows host for those flows.

The compose-backed `OpenClaw.Core` path is also local-only. It is intended for Docker Desktop on the Windows host, not for Codespaces-only execution.

## Rebuild

When you change the Dockerfile or any `devcontainer.json` file:

1. Run `Dev Containers: Rebuild Container`.
2. Wait for the post-create step to rerun.
3. Re-run `bash .devcontainer/verify-container.sh`.
