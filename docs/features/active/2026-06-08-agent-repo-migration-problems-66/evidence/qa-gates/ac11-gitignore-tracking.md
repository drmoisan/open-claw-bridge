# AC-11 — Git-ignore / Tracking Verification

Timestamp: 2026-06-08T11-25

Command: `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md`
EXIT_CODE: 1
Result: no output (no ignore match) — both harness paths are NO LONGER ignored. PASS.

Command: `git ls-files .claude | wc -l`
EXIT_CODE: 0
Result: 78 — non-empty; `.claude/` harness is tracked. PASS.

Command: `git ls-files .github/agents .github/instructions .github/prompts .github/skills | wc -l`
EXIT_CODE: 0
Result: 83 — non-empty; the four `.github` harness subtrees are tracked. PASS.

Command: `git check-ignore .claude/settings.local.json`
EXIT_CODE: 0
Result: `.claude/settings.local.json` printed (matches) — remains ignored. PASS.

Command: `git check-ignore artifacts/orchestration/orchestrator-state.json`
EXIT_CODE: 0
Result: path printed (matches) — `artifacts/` remains ignored. PASS.

Supporting: `git ls-files artifacts | wc -l` = 0 (artifacts not tracked);
`git ls-files .claude/settings.local.json | wc -l` = 0 (local settings not tracked).

Output Summary: The harness (`.claude/` and `.github/{agents,instructions,prompts,skills}`) is now
tracked (check-ignore returns no match; ls-files non-empty after staging). `artifacts/` and
`.claude/settings.local.json` remain ignored. AC-11 PASS.
