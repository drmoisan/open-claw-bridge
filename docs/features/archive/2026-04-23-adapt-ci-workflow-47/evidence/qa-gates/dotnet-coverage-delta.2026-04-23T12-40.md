---
Timestamp: 2026-04-23T12-40
Command: manual comparison of baseline vs post-change coverage
EXIT_CODE: 0
---

# C# Coverage Delta

Baseline (from `evidence/baseline/dotnet-baseline.2026-04-23T12-40.md`):
- BaselineCoverage: **84.40%** (3126 / 3704 lines)

Post-change (from `evidence/qa-gates/dotnet-test.2026-04-23T12-40.md`):
- PostChangeCoverage: **84.42%** (3127 / 3704 lines)

Delta: **+0.02 pp** (+1 covered line attributable to test-run variance in OpenClaw.MailBridge; no source changes were made to .NET code).

Threshold checks (per `.claude/rules/csharp.md`):
- Repo-wide floor: **>= 80% required** -> 84.42% **PASS** (headroom 4.42 pp)
- NoRegression: **true** (post-change 84.42% >= baseline 84.40%)

NewCodeCoverage: **N/A (no C# code added)**

Output Summary:
- Baseline: 84.40%
- Post-change: 84.42%
- Delta: +0.02 pp (within noise tolerance; the plan changed only `.gitignore` and `.github/workflows/ci.yml`)
- NoRegression: true
- NewCodeCoverage: N/A (no C# code added)
- Result: PASS
