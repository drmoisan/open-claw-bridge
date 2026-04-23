---
Timestamp: 2026-04-23T12-40
Command: git add .gitignore .github/workflows/ci.yml && git ls-files .github/workflows/ci.yml
EXIT_CODE: 0
---

# ci.yml Tracked (AC-7 initial stage)

Output:
```
.github/workflows/ci.yml
```

Output Summary:
- `.gitignore` and `.github/workflows/ci.yml` were staged.
- `git ls-files .github/workflows/ci.yml` returns the exact literal path, confirming the file is tracked in the index.
- This is the AC-7 proof artifact for the initial staging after the `.gitignore` fix. Phase 3 P3-T2 produces the end-state AC-7 proof after the Phase 2 rewrite.
