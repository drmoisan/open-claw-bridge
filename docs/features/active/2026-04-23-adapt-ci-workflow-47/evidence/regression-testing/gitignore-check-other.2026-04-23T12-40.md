---
Timestamp: 2026-04-23T12-40
Commands:
  - git check-ignore -v .github/copilot-instructions.md
  - git check-ignore -v .github/instructions/
  - git check-ignore -v .github/prompts/
EXIT_CODE: 0 (all match as expected)
---

# gitignore-check (other .github/ descendants remain ignored)

Outputs:
```
.gitignore:76:.github/*	.github/copilot-instructions.md
.gitignore:76:.github/*	.github/instructions/
.gitignore:76:.github/*	.github/prompts/
```

Output Summary:
- All three representative `.github/` descendants are matched by the new `.github/*` rule at `.gitignore:76`.
- `copilot-instructions.md` is not currently present on disk; `git check-ignore` still reports the rule that would match it.
- The directories `.github/instructions/` and `.github/prompts/` are present on disk and are correctly ignored.
- Scope preservation is confirmed: only `ci.yml` and `publish.yml` are re-included; everything else under `.github/` remains ignored.
