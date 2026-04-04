# Dev Container Config Guide

## Recommended Config

Use `.devcontainer/devcontainer.json` unless you have a reason to choose an alias file.

It is the standard config for this repo and is suitable for:

- local VS Code Dev Containers
- GitHub Codespaces

## Alias Configs

- `.devcontainer/local/devcontainer.json`
  Convenience alias for explicit local selection.
- `.devcontainer/codespaces/devcontainer.json`
  Convenience alias for explicit Codespaces selection.

The alias configs intentionally mirror the standard root config. They exist only to keep selection explicit when that helps your workflow.

## Shared Assets

- `.devcontainer/Dockerfile`
  Shared container image customization.
- `.devcontainer/post-create.sh`
  Shared bootstrap for restore, build, and cross-platform test execution.
- `.devcontainer/verify-container.sh`
  Shared validation script.

## Why The Container Is Linux

The dev container gives the repo a consistent, portable environment for editing and validation. It does not replace Windows-specific work. This repo still needs Windows for runtime validation of the Outlook bridge.
