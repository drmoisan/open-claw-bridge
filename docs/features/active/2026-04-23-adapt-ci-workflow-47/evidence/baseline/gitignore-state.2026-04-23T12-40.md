---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command "Get-Content -LiteralPath .gitignore | Set-Content -LiteralPath ...gitignore-before.txt"
EXIT_CODE: 0
---

# .gitignore Baseline State

Baseline copy: `evidence/baseline/gitignore-before.2026-04-23T12-40.txt`

Exact `.github/` line (pre-fix):
```
76:.github/
```

Output Summary:
- Total lines in `.gitignore`: 79
- Single-line pattern at line 76: `.github/` (matches entire directory subtree)
- This single-line pattern is what currently causes `ci.yml` to be silently ignored.
