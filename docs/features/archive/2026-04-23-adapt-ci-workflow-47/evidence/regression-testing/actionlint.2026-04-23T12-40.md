---
Timestamp: 2026-04-23T12-40
Commands:
  - pwsh -NoProfile -File scripts/dev-tools/run-actionlint.ps1
  - actionlint .github/workflows/ci.yml
  - actionlint .github/workflows/publish.yml
EXIT_CODE: 0 (all three)
---

# actionlint — AC-5 evidence

Tool version:
```
actionlint 1.7.11
installed by downloading from release page
built with go1.25.7 compiler for windows/amd64
```

Command 1 — repo wrapper:
```
$ pwsh -NoProfile -File scripts/dev-tools/run-actionlint.ps1
(no output)
EXIT=0
```

Command 2 — explicit ci.yml:
```
$ actionlint .github/workflows/ci.yml
(no output)
EXIT=0
```

Command 3 — explicit publish.yml:
```
$ actionlint .github/workflows/publish.yml
(no output)
EXIT=0
```

Output Summary:
- actionlint exits 0 with empty output on the rewritten `.github/workflows/ci.yml`.
- actionlint also passes on the unchanged `publish.yml` (sanity check).
- The repo wrapper `scripts/dev-tools/run-actionlint.ps1` passes silently.
- AC-5 is satisfied.
