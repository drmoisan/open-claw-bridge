# OpenClaw MailBridge Dev Container

This folder contains the dev container setup for `OpenClaw.MailBridge`.

## Purpose

The container is designed for:

- editing the repository in a consistent Linux environment
- restoring and building the `.NET 10` solution
- running the cross-platform NUnit test target
- working with the repository PowerShell scripts from `pwsh`

The container is not a full replacement for a Windows workstation. The bridge and client projects target `net10.0-windows`, and Outlook COM automation requires Windows plus classic Outlook.

## What The Container Installs

- `.NET SDK 10.0.201`
- PowerShell
- Git and GitHub CLI
- `shellcheck`, `shfmt`, `actionlint`, `jq`, and `sqlite3`
- VS Code extensions for C#, C# Dev Kit, PowerShell, GitHub PRs, GitLens, and Markdown

## Config Layout

- `.devcontainer/devcontainer.json`
  Standard config for Codespaces and local Dev Containers.
- `.devcontainer/local/devcontainer.json`
  Optional alias if you want to select an explicit local config file.
- `.devcontainer/codespaces/devcontainer.json`
  Optional alias if you want to select an explicit Codespaces config file.
- `.devcontainer/Dockerfile`
  Shared base image customization.
- `.devcontainer/post-create.sh`
  Restores, builds, and runs the cross-platform test target after container creation.
- `.devcontainer/verify-container.sh`
  Checks that the toolchain and config files match the repo.

## Quick Start

Local Docker:

1. Open the repo in VS Code.
2. Run `Dev Containers: Reopen in Container`.
3. Use the standard root config, or explicitly select `.devcontainer/local/devcontainer.json`.
4. After the build finishes, run `bash .devcontainer/verify-container.sh`.
5. For the compose-backed `OpenClaw.Core` workflow, copy `.env.example` to `.env`, then run `docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml up openclaw-dev` from the repo root. This local-only path uses `host.docker.internal` so the Linux container can call the Windows HostAdapter without exposing the UI beyond loopback.

GitHub Codespaces:

1. Create a Codespace from the repository.
2. Let Codespaces use `.devcontainer/devcontainer.json`, or explicitly choose `.devcontainer/codespaces/devcontainer.json`.
3. After the environment finishes provisioning, run `bash .devcontainer/verify-container.sh`.
4. The compose-backed `OpenClaw.Core` workflow remains local-only. Codespaces can edit and build the repo, but it does not replace the Docker Desktop plus `host.docker.internal` path used to reach the Windows HostAdapter.

## Common Commands

```bash
pwsh -File scripts/Build.ps1
pwsh -File scripts/Test.ps1
dotnet build OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -f net10.0 -p:EnableWindowsTargeting=true
```

## Windows-Only Work

Do these steps on Windows, not inside the Linux container:

- `dotnet run` for `src/OpenClaw.MailBridge`
- `dotnet run` for `src/OpenClaw.MailBridge.Client`
- Outlook COM validation
- checks that require a local Outlook installation or Windows COM registration

## More Help

- `QUICKSTART.md`
- `CONFIG-GUIDE.md`
- `CODESPACES-SETUP.md`
- `TROUBLESHOOTING.md`
