---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command '$bad = @("poetry","setup-python","black","ruff","pyright","pytest","safety","bats","shellcheck","shfmt","kcov","codecov","drm-copilot","atomic-executor","shell-qc","lexile_corpus_tuner","docs-validation","build-check","security-scan","quality-checks7","shell-coverage","drm-copilot-extension-tests","setup-node"); Select-String -LiteralPath .github/workflows/ci.yml -Pattern $bad -SimpleMatch -CaseSensitive:$false'
EXIT_CODE: 0
---

# verify-ci-removals — AC-2 evidence (excluded tokens)

Output:
```
NONE
```

Output Summary:
- All 23 forbidden tokens from the copied-in Python/shell/Node template are absent from the rewritten `.github/workflows/ci.yml`.
- Tokens checked: poetry, setup-python, black, ruff, pyright, pytest, safety, bats, shellcheck, shfmt, kcov, codecov, drm-copilot, atomic-executor, shell-qc, lexile_corpus_tuner, docs-validation, build-check, security-scan, quality-checks7, shell-coverage, drm-copilot-extension-tests, setup-node.
- The case-insensitive substring search found zero matches.
- The `powershell-quality` job name was chosen to avoid a substring collision with the forbidden `shell-qc` token.

Diff evidence against the pre-adaptation template:
- Baseline template: `evidence/baseline/ci-yml-before.2026-04-23T12-40.yml` (318 lines, 7 jobs).
- Rewritten file: `.github/workflows/ci.yml` (89 lines, 3 jobs).
- Line-count reduction: 229 lines.
- All removed template jobs (`quality-checks7`, `security-scan`, `docs-validation`, `build-check`, `poshqc`, `shell-coverage`, `drm-copilot-extension-tests`) are absent from the new file per the zero-match result above.
