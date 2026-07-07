---
name: ci-powershell-qc-flake
description: ci.yml "PowerShell QC" job flakes with an Invoke-ScriptAnalyzer "parameter 'Function' not found" error because PSScriptAnalyzer is installed unpinned; a same-head re-dispatch clears it.
metadata:
  type: project
---

`ci.yml`'s "PowerShell QC" job installs PSScriptAnalyzer with `Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force` — with NO `-RequiredVersion`, so each run pulls whatever the gallery serves as latest. This produces an intermittent failure in the "Analyze PowerShell (Invoke-ScriptAnalyzer)" step: `Invoke-ScriptAnalyzer: A parameter cannot be found that matches parameter name 'Function'.` The error is raised INSIDE PSScriptAnalyzer (the step's own `Invoke-ScriptAnalyzer -Path $path -Recurse -Severity Warning, Error` has no `-Function` param), and the step then hangs ~15 minutes before the runner reports exit 1. Observed 2026-07-07 on F17/#120 (run 28841186831) immediately after passing on the identical head 7 minutes earlier.

**Why:** version drift of the unpinned PSScriptAnalyzer between two ephemeral-runner jobs. It is not caused by the branch diff — confirm by checking `git diff <lastPass>..<head> -- scripts/ tests/scripts/` is empty (C#/docs-only features never touch the analyzed tree).

**How to apply:** When a wave-2+ child's CI fails ONLY on "PowerShell QC" with this signature and `scripts/**`/`tests/scripts/**` is unchanged from the last passing head, diagnose it as environmental/transient, capture the reasoning in `remediation-inputs.<ts>.md` (Blocking), and re-dispatch `ci.yml` at the same head ONCE as the probe before planning any code fix — a green probe closes the remediation cycle (blocking_count 0). Durable fix candidate (a real workflow-file remediation, routed through R1-R5 + `modified-workflow-needs-green-run`): pin `PSScriptAnalyzer -RequiredVersion` in `.github/workflows/ci.yml`. Note `ci.yml` triggers only on PRs to `main`/`development`, so integration-targeted PRs need `gh workflow run ci.yml --ref <branch>` and `gh run watch <id> --exit-status` to gate. Related: [[openclaw-delivery-loop]].
