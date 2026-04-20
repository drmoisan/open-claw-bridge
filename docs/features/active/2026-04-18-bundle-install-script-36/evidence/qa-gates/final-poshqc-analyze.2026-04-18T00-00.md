# Final QA Gate — PoshQC Analyze

Timestamp: 2026-04-18T00-00
Command: `Invoke-PoshQCAnalyze -Root <repo> -ScanFolders @('scripts','tests')`
EXIT_CODE: 0
Output Summary: PASS. PSScriptAnalyzer reported zero findings across all 26 PowerShell files under `scripts/` and `tests/`. Diagnostic count: 0.

## Full Output

```
PSScriptAnalyzer passed: no findings under C:\Users\DanMoisan\repos\open-claw-bridge
```

## File-level suppression summary

Two rule suppressions applied, each with explicit justification:

- `scripts/Install.Helpers.psm1` — `PSUseSingularNouns` on `Copy-BundleContents`. Justification: spec and plan mandate the noun "Contents" because the helper copies multiple subtrees (executables/ and docker/).
- `tests/scripts/Install.Helpers.Tests.ps1` and `tests/scripts/Install.Tests.ps1` — `PSAvoidGlobalVars`. Justification: mock script blocks and `function global:docker` shims run in the orchestrator script scope; `$global:` is required to share a call log across scopes. Mirrors the pattern in `tests/scripts/Publish.Tests.ps1`.

These are the only suppressions; no policy-wide waivers or silent skips.
