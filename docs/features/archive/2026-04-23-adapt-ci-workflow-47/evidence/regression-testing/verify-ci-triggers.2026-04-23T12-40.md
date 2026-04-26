---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command 'Get-Content -LiteralPath .github/workflows/ci.yml -TotalCount 20 | Out-String'
EXIT_CODE: 0
---

# verify-ci-triggers — AC-6 evidence

First 20 lines of `.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [main, development]
  pull_request:
    branches: [main, development]
  workflow_dispatch:

jobs:
  dotnet-build-test:
    name: .NET Build + Test
    runs-on: windows-latest
    steps:
      - name: Check out repository
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
```

Output Summary:
- `on.push.branches: [main, development]` — present on line 5 (triggers on direct pushes to main and development).
- `on.pull_request.branches: [main, development]` — present on line 7 (triggers on PRs targeting main and development).
- `on.workflow_dispatch:` — present on line 8 (manual trigger support).
- Workflow yaml is well-formed (validated by `actionlint` in `evidence/regression-testing/actionlint.2026-04-23T12-40.md`).
- All AC-6 requirements are satisfied.
