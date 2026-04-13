# Codespaces Setup

GitHub Codespaces can use the standard root config in this repo:

- `.devcontainer/devcontainer.json`

If you want to choose a config explicitly, select:

- `.devcontainer/codespaces/devcontainer.json`

## Steps

1. Open the repository on GitHub.
2. Start creating a Codespace.
3. Let GitHub use the default dev container config, or choose the codespaces alias config.
4. Wait for provisioning to finish.
5. Run:

```bash
bash .devcontainer/verify-container.sh
```

## Expected Result

You should have:

- `.NET SDK 10.0.201`
- PowerShell
- Git and GitHub CLI
- shell tooling such as `shellcheck`, `shfmt`, and `actionlint`

## Codespaces Limitation

Codespaces is still Linux. It can restore, build, and run the cross-platform tests, but it cannot validate Outlook COM or run the Windows-targeted bridge like a native Windows machine.

The compose-backed `OpenClaw.Core` workflow is local-only and expects `host.docker.internal` on Docker Desktop so the container can call the Windows HostAdapter. Keep that path documented here, but run it from a local Windows workstation rather than inside Codespaces.
