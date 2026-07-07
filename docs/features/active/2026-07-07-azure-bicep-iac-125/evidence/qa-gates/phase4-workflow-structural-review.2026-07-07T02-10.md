# Phase 4 Workflow Structural Review — CLI-Unavailable Fallback

- Timestamp: 2026-07-07T02-10
- Command: `git diff .github/workflows/ci.yml` (structural diff check); manual YAML structure review of `.github/workflows/_bicep-validate.yml`.
- EXIT_CODE: 0
- Output Summary: overall result **PASS**.

## `_bicep-validate.yml` (new file)

- `on:` block declares both `workflow_call:` and `workflow_dispatch:` — present.
- `jobs:` block declares one job (`bicep-build`) on `runs-on: windows-latest` — present.
- Steps: `actions/checkout@v4`, then `bicep build deploy/azure/main.bicep`, then `./scripts/Test-OpenClawBicepParameterSecrets.ps1 -Path deploy/azure/parameters` (`shell: pwsh`) — present, correctly indented under `steps:`.
- No tooling-install step is present, consistent with the research artifact's finding that Azure CLI and Bicep are preinstalled on `windows-latest`.
- No deliberately-failing nested command exists in this workflow's `run:` blocks (both steps are expected to succeed on the happy path against the real, valid templates and the clean parameter file), so `.claude/rules/ci-workflows.md`'s exit-code-reset requirement does not apply here (per the research artifact §6's rule-applicability check).

## `ci.yml` edit

- `git diff .github/workflows/ci.yml` shows exactly one added job block (`bicep-validate`, referencing `uses: ./.github/workflows/_bicep-validate.yml`), inserted between the `powershell-quality` and `actionlint` jobs.
- The three pre-existing jobs (`dotnet-build-test`, `powershell-quality`, `actionlint`) show no diff lines — byte-identical to their prior content.

## CLI-Unavailable Fallback Rationale

This workflow cannot be executed locally regardless of `bicep`/`az` CLI presence: GitHub Actions `uses:`/`workflow_call:` semantics require a runner. The existing `Workflow Lint` (`actionlint`) job in `ci.yml` will lint `_bicep-validate.yml`'s YAML syntax automatically once this branch is pushed and `ci.yml` is driven — no separate lint step is needed for the workflow file itself. Real execution of this workflow (`bicep build`, `actionlint`) occurs only on a GitHub-hosted runner, exercised via a PR or `gh workflow run ci.yml`; that execution is not claimed as a local result by this task.

## Overall Result: PASS
