---
Timestamp: 2026-04-23T12-58
Reviewer: feature-review agent
Scope: full branch diff (development @ 83459c2 .. chore/adapt-ci-workflow-47 @ ec21f22)
Runtime files reviewed:
  - .github/workflows/ci.yml (added)
  - .gitignore (modified)
---

# Code Review — Issue #47 (adapt CI workflow)

## Review Scope

Runtime artifacts reviewed:

1. `.github/workflows/ci.yml` — GitHub Actions workflow (89 lines, added).
2. `.gitignore` — ignore-pattern update (+5 / -1 lines).

Documentation under `docs/features/active/2026-04-23-adapt-ci-workflow-47/**` was read for context only and is not subject to a best-practices code review in this pass.

## Strengths

### `.github/workflows/ci.yml`

- **Clear job separation.** Three top-level jobs (`dotnet-build-test`, `powershell-quality`, `actionlint`) each have a single purpose and minimal inter-job coupling (no `needs:` chain). This matches the "separation of concerns" principle from `general-code-change.md`.
- **Fail-fast discipline.**
  - `dotnet build ... /warnaserror` treats warnings as errors (line 27).
  - `Invoke-ScriptAnalyzer` step explicitly fails the job when results are non-empty via `Write-Error ...; exit 1` (lines 57-61), rather than silently passing on warnings.
  - `Invoke-Pester -CI` sets exit code based on test failures.
  - `actionlint` fails the job on any finding via its default exit behavior.
- **Pinned action versions.** `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`, and `actionlint v1.7.7` are pinned to major or exact versions. This matches supply-chain hygiene expectations for CI workflows.
- **Reuse of repo conventions.** `runs-on: windows-latest` for .NET and PowerShell jobs mirrors `publish.yml` precedent (Outlook-COM dependence), so runner choice is consistent across workflows.
- **Artifact upload with sensible `if-no-files-found`.** `dotnet-test-results` uses `warn` (test output should exist) and `pester-results` uses `ignore` (artifacts are opportunistic).
- **No superfluous caching or matrix complexity.** The workflow deliberately avoids multi-version matrices for .NET (the SDK is pinned via `global.json`) and PowerShell, keeping CI simple.

### `.gitignore`

- **Minimal surgical change.** Replaces the overly-broad `.github/` pattern with a content-based exclude (`.github/*`) plus two explicit negations for the two tracked workflows.
- **Explicit re-include for `publish.yml`.** Prevents accidental un-tracking of the previously tracked workflow even on a fresh clone.

## Observations and Suggestions (non-blocking)

### `ci.yml`

1. **actionlint downloader uses `bash <(curl ...)` over HTTPS to GitHub.** Line 85 fetches and executes the download script via process substitution. This is the upstream-recommended install method but executes arbitrary code at a version pin (`v1.7.7`). The risk is low for a pinned tag, but an alternative is to use the `rhysd/actionlint` GitHub Action directly (e.g., `uses: reviewdog/action-actionlint@v1` with a pinned SHA) if supply-chain hygiene becomes a concern. No change required for this PR.
2. **No `concurrency:` block.** If multiple PRs or rapid pushes are expected, adding `concurrency: { group: ${{ github.workflow }}-${{ github.ref }}, cancel-in-progress: true }` would prevent queue buildup. Not a regression relative to `publish.yml`, which also omits this. Optional future enhancement.
3. **`dotnet test` result upload path.** `TestResults/**` captures per-project `.trx` and coverage outputs but does not explicitly scope to coverage XML. For reviewer convenience, consider adding a second upload step that narrows to `TestResults/**/*.cobertura.xml` when a future feature integrates coverage reporting. Not required now; `Out of Scope` of the issue per line 33 of `issue.md`.
4. **Pester step does not upload coverage by default.** The current `Invoke-Pester -Path tests/scripts -Output Detailed -CI` does not configure coverage (`-CodeCoverage`). The `artifacts/pester/**` upload step will therefore usually be a no-op (hence `if-no-files-found: ignore`). This is consistent with the amended AC-4, which does not require Pester coverage. A future issue could enable `-CodeCoverage` with a runsettings file; not scope for #47.
5. **`Install-Module` is not hash-pinned.** Pester is version-pinned to `5.7.1`, but PSScriptAnalyzer is installed without a `-RequiredVersion`. A pinned version would make CI output deterministic across runs. Minor observation.
6. **Line 89 whitespace.** The file ends with `run: ./actionlint -color` on line 89 with a trailing newline. Well-formed.

### `.gitignore`

7. **Ordering is intentional and works but is subtle.** The sequence `.github/*` (line 76) then `!.github/workflows/` (line 77) then `.github/workflows/*` (line 78) then `!.github/workflows/ci.yml` / `!.github/workflows/publish.yml` (lines 79-80) relies on Git's "last-matching-rule wins" semantics. The inline comment density is low. Consider a brief comment above the block explaining the intent, for example `# Keep .github/workflows/{ci.yml,publish.yml} tracked; ignore everything else under .github/.` Not blocking.
8. **`.claude` pattern (line 83) is unchanged.** Noted for context; the branch preserves the existing pattern.

## Risks and Follow-ups

- **First CI run may surface latent PowerShell debt.** If `scripts/` or `tests/scripts/` contain existing PSScriptAnalyzer Warning-or-Error findings, the `powershell-quality` job will fail on merge. The issue summary acknowledges this as an expected outcome (the risk is already called out at `pr_context.summary.txt` line 13). Not a code-quality defect introduced by this PR.
- **First CI run may surface latent Pester failures.** Same class of risk for existing `tests/scripts/**` Pester specs.
- **Local policy-compliance gap for PowerShell.** Local execution of PSScriptAnalyzer and Pester against the repo was deferred (DF-1 authorized skip) because the PoshQC wrapper is absent. This is acceptable under the plan contract but means the first push to `main`/`development` is the first real verification of the PowerShell job configuration. This is inherent to chore-style CI bootstraps.

## Style / Readability

- YAML is consistently indented (2-space). Step names are descriptive. Reasonable use of multi-line `run:` blocks for composite commands.
- No dead code, no unused inputs, no unused outputs.
- No magic strings beyond version pins.

## Verdict

**PASS**. The change is small, focused, readable, and aligned with the repository's existing CI conventions. Observations above are non-blocking suggestions for future hardening. No remediation required for merge.
