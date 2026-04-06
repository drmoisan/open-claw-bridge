# Codex Web Setup Script Change Plan

## Objective

Fix `.codex/codex-web-setup.sh` so a Linux-based Codex Web bootstrap does not fail after successful dependency installation solely because the repository's build and test tasks require Windows-only Visual Studio tooling.

## Assumptions

- The requested defect is the non-zero exit caused by the Windows-only verification block shown in the provided log.
- Codex Web should complete environment bootstrap on Linux even when full restore/build/test parity remains unavailable there.
- Real setup failures such as missing `dotnet`, `pwsh`, `dotnet-coverage`, or `coverage.config` should still fail the script.

## Planned Changes

1. Refactor the verification path in `.codex/codex-web-setup.sh` so general prerequisite checks remain mandatory.
2. Detect non-Windows PowerShell hosts and downgrade the Visual Studio-specific task verification from a hard failure to a warning.
3. Update the script's repo notes so they describe partial verification on Linux instead of an expected non-zero exit.
4. Update `.github/workflows/codex-web-setup-test.yml` so CI expects the new Linux warning-and-success behavior rather than the previous hard failure.
5. Add non-default-branch CI triggers so the setup workflow can run from branch pushes and pull requests, not only from `workflow_dispatch` on the default branch.

## Verification

1. Run a syntax check for `.codex/codex-web-setup.sh`.
2. Run the repository PowerShell quality commands required by policy.
3. If practical, run a focused command path that exercises the non-Windows verification branch without reinstalling dependencies.
4. Run `actionlint` against the updated workflow definition.
