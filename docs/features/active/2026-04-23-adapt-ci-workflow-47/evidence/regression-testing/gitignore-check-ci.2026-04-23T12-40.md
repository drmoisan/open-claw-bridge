---
Timestamp: 2026-04-23T12-40
Command: git check-ignore -v .github/workflows/ci.yml
EXIT_CODE: 0
---

# gitignore-check (ci.yml unignored) — AC-1 evidence

Primary command output:
```
.gitignore:79:!.github/workflows/ci.yml	.github/workflows/ci.yml
```

Supporting verification (`git status --porcelain=v1 .github/workflows/`):
```
?? .github/workflows/ci.yml
```

Output Summary:
- The matching rule is the negation pattern `!.github/workflows/ci.yml` at line 79 of `.gitignore`. `git check-ignore`'s documented behavior is to exit 0 and print the last matching pattern; a leading `!` indicates the file is NOT ignored.
- Corroborating proof: `git status` now shows `ci.yml` as untracked (`??`), confirming the file is visible to git and stageable.
- This matches the intent of AC-1. The plan's predicted exit code of `1 (unmatched)` was a plan-time estimate; the actual exit code with a `!` negation pattern is `0` with the negation rule printed. The substantive acceptance criterion — "the file is no longer ignored" — is satisfied: the file is trackable (see P1-T5 staging proof).
- The rest of `.github/` remains ignored (see P1-T4 for scope preservation).
