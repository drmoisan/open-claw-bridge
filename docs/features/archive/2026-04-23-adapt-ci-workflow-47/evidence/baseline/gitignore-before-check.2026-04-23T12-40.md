---
Timestamp: 2026-04-23T12-40
Commands:
  - git check-ignore -v .github/workflows/ci.yml
  - git check-ignore -v .github/workflows/publish.yml
  - git ls-files .github/workflows/
EXIT_CODE:
  ci.yml check-ignore: 0 (match printed)
  publish.yml check-ignore: 1 (no match — tracked file is excluded from ignore matching)
  git ls-files: 0
---

# Pre-fix .gitignore Behavior

Output:
```
$ git check-ignore -v .github/workflows/ci.yml
.gitignore:76:.github/	.github/workflows/ci.yml

$ git check-ignore -v .github/workflows/publish.yml
(no output, exit 1)

$ git ls-files .github/workflows/
.github/workflows/publish.yml
```

Output Summary:
- `ci.yml` matches the `.gitignore` pattern `.github/` at line 76. It is currently untracked and silently ignored.
- `publish.yml` is reported as "not matched" by `git check-ignore` because it is already tracked (force-added in history) and `check-ignore` does not print matches for tracked files.
- `git ls-files .github/workflows/` shows only `publish.yml` is tracked; `ci.yml` is absent from the index.
- Both findings match the plan's expectation. Phase 1 will rewrite the pattern so `ci.yml` becomes trackable without re-ignoring `publish.yml`.
