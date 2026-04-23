---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command "Get-Content -LiteralPath .github/workflows/ci.yml | Set-Content -LiteralPath ...ci-yml-before.yml"
EXIT_CODE: 0
---

# ci.yml Baseline State (pre-adaptation)

Baseline copy: `evidence/baseline/ci-yml-before.2026-04-23T12-40.yml`

Output Summary:
- Total lines: 318
- Top-level jobs (from `^  [a-z0-9_-]+:$`): `quality-checks7` (line 11), `security-scan` (line 86), `docs-validation` (line 110), `build-check` (line 141), `poshqc` (line 170), `shell-coverage` (line 216), `drm-copilot-extension-tests` (line 295).
- These exactly match the plan-expected job set. All will be removed by Phase 2.
