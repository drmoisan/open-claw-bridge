---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command "$c = Get-Command actionlint -ErrorAction SilentlyContinue; if ($null -eq $c) { 'MISSING' } else { & actionlint -version }"
EXIT_CODE: 0
---

# actionlint Availability

Output:
```
1.7.11
installed by downloading from release page
built with go1.25.7 compiler for windows/amd64
```

Output Summary:
- actionlint 1.7.11 is installed on PATH at `/c/Users/DanMoisan/AppData/Local/Microsoft/WinGet/Packages/rhysd.actionlint_Microsoft.Winget.Source_8wekyb3d8bbwe/actionlint`.
- DF-3 fallback not required: Phase 3 will run `actionlint` locally via `scripts/dev-tools/run-actionlint.ps1`.
