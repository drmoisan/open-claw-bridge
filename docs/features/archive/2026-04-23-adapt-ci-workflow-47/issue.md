# Chore — Adapt `.github/workflows/ci.yml` to OpenClaw .NET + PowerShell stack (Issue #47)

- Promotion type: feature
- Branch: `chore/adapt-ci-workflow-47`
- Base branch: `development`
- Created (UTC): 2026-04-23
- Associated GitHub issue: https://github.com/drmoisan/open-claw-bridge/issues/47

- Issue: #47
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/47
- Last Updated: 2026-04-23
- Status: Promoted -> `docs/features/active/2026-04-23-adapt-ci-workflow-47/`
- Work Mode: minor-audit

## Summary

A copy of `.github/workflows/ci.yml` was added from an unrelated Python/shell-heavy repository. The file uses Poetry, Black, Ruff, Pyright, pytest, safety, bats + kcov, a Python CLI (`atomic-executor`/`shell-qc`), and a `drm-copilot` VS Code extension — none of which exist in this repository. The file is also being silently ignored because `.gitignore` line `.github/` excludes the parent directory, which prevents any descendant re-include from taking effect.

This chore adapts the workflow to match the actual stack (C#/.NET 10 + PowerShell 7 + Docker) and fixes the `.gitignore` pattern so the file is tracked.

## Acceptance Criteria

- [x] **AC-1** — `.gitignore` is updated so `.github/workflows/ci.yml` is no longer ignored. Evidence: `git check-ignore -v .github/workflows/ci.yml` returns empty (exit 1, unmatched). The rest of `.github/` (extensions, copilot-instructions, etc.) remains ignored unless explicitly re-included. Re-include `publish.yml` explicitly to preserve its tracked state.
- [x] **AC-2** — `.github/workflows/ci.yml` is rewritten so it contains only jobs relevant to this repository: .NET build + test + coverage, PoshQC (format, analyze, test), and `actionlint` validation. All Python, Node, bats, kcov, codecov, and extension-specific jobs from the copied template are removed. Evidence: diff against the copied-in template confirms removals; final file names jobs matching the repo.
- [x] **AC-3** — The .NET workflow job uses `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x` matching `global.json`. The job runs on `windows-latest` (Outlook-COM-dependent tests are Windows-bound; existing `publish.yml` precedent). `dotnet restore`, `dotnet build -c Release /warnaserror`, and `dotnet test --collect:"XPlat Code Coverage"` all appear in the job. Evidence: `verify-ci-dotnet-job.<timestamp>.md`.
- [x] **AC-4** — The PowerShell workflow job runs format / analyze / test against the repo's PowerShell surface using the tooling that actually exists in this repo. Preflight finding: there is no `scripts/powershell/PoshQC/PoshQC.psm1` wrapper module in this repository — the PoshQC interface referenced by the copied template is MCP-extension-based (`mcp__drmCopilotExtension__run_poshqc_*`) and is not invocable by a GitHub Actions runner. The CI job must therefore call `Invoke-ScriptAnalyzer` and `Invoke-Pester` directly: install Pester v5.x and PSScriptAnalyzer via `Install-Module -Scope CurrentUser`; run `Invoke-ScriptAnalyzer -Path scripts/, tests/scripts/ -Recurse -Severity Warning,Error` and fail on any non-empty result; run `Invoke-Pester -Path tests/scripts/ -Output Detailed -CI` and upload any emitted JUnit/coverage artifacts under `artifacts/pester/` if present (`if-no-files-found: ignore`). The job does NOT reference the non-existent PoshQC wrapper. Evidence: `verify-ci-powershell-job.<timestamp>.md`.
- [x] **AC-5** — The workflow passes `actionlint` validation on the host. Evidence: `actionlint.<timestamp>.md` with `actionlint` output attached and exit code 0.
- [x] **AC-6** — The workflow yaml is well-formed and triggers on `push` and `pull_request` against `main` and `development`. Evidence: `cat .github/workflows/ci.yml | head -20` in `verify-ci-triggers.<timestamp>.md`.
- [x] **AC-7** — The file is tracked in git after the `.gitignore` fix. Evidence: `git ls-files .github/workflows/ci.yml` returns the path in `verify-ci-tracked.<timestamp>.md`.

## Out of Scope

- Setting up Codecov or other third-party reporting integrations.
- Running the new workflow against GitHub Actions runners (that happens after merge on PR).
- Modifying `publish.yml`.
- Adding Python, Node, or shell-test infrastructure to the repo.

## References

- Copied-in (pre-adaptation) template: `.github/workflows/ci.yml`
- Existing workflow precedent: `.github/workflows/publish.yml`
- .NET SDK pinned: `global.json` (`10.0.201`)
- PoshQC module: `scripts/powershell/PoshQC/PoshQC.psm1`
- actionlint wrapper: `scripts/dev-tools/run-actionlint.ps1`
