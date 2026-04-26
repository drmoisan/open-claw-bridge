---
Timestamp: 2026-04-23T12-40
Command: git add .github/workflows/ci.yml && git ls-files .github/workflows/ci.yml && git diff --cached --name-only -- .github/workflows/ci.yml
EXIT_CODE: 0
---

# verify-ci-tracked — AC-7 end-state evidence

Output:
```
warning: in the working copy of '.github/workflows/ci.yml', LF will be replaced by CRLF the next time Git touches it
.github/workflows/ci.yml
.github/workflows/ci.yml
```

Output Summary:
- `git ls-files .github/workflows/ci.yml` returns the exact literal path, confirming `ci.yml` is tracked in the index.
- `git diff --cached --name-only -- .github/workflows/ci.yml` also returns the path, confirming the file is staged for commit.
- The LF->CRLF warning is a normal autocrlf notice on Windows and does not affect tracking.
- AC-7 is satisfied.
