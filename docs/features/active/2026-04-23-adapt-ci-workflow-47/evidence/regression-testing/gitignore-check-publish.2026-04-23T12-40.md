---
Timestamp: 2026-04-23T12-40
Command: git check-ignore -v .github/workflows/publish.yml
EXIT_CODE: 1
---

# gitignore-check (publish.yml still trackable)

Primary command output: (empty stdout; exit 1 = no ignore match)

Supporting: `git ls-files .github/workflows/publish.yml` -> `.github/workflows/publish.yml`

Output Summary:
- `publish.yml` is unmatched by any ignore pattern (`git check-ignore` exits 1 with empty stdout).
- It also remains in `git ls-files`, confirming its tracked state is preserved after the `.gitignore` rewrite.
- The explicit re-include `!.github/workflows/publish.yml` (line 80 of the new `.gitignore`) keeps the file visible to git even for a fresh clone.
