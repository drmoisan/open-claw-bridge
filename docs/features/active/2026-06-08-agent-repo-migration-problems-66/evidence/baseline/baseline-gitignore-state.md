# Baseline — Git-ignore / Tracking State (Issue #66 Scope Extension, pre-change)

Timestamp: 2026-06-08T11-06

Command: `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md`
EXIT_CODE: 0
Output:
```
.claude/rules/csharp.md
.github/agents/orchestrator.agent.md
```

Command: `git check-ignore artifacts/orchestration/orchestrator-state.json`
EXIT_CODE: 0
Output:
```
artifacts/orchestration/orchestrator-state.json
```

Command: `git ls-files .claude | wc -l`
EXIT_CODE: 0
Output:
```
0
```

Output Summary:
- `.claude/rules/csharp.md` and `.github/agents/orchestrator.agent.md` are CURRENTLY IGNORED
  (git check-ignore prints each path; exit 0). Baseline confirms harness is untracked.
- `artifacts/orchestration/orchestrator-state.json` is CURRENTLY IGNORED (must remain ignored
  after the .gitignore edit).
- `git ls-files .claude` returns 0 tracked files (harness fully untracked pre-change).
- This is the pre-change baseline for AC-11.
