---
name: plan-validator-conflict
description: Two conflicting plan validators (MCP em-dash vs repo-hook ASCII-hyphen) plus the MCP plan validator reads stale/wrong content; use the repo hook regex, not the MCP tool.
metadata:
  type: project
---

Three plan-structure validators disagree on phase-heading dash, and the MCP one is unreliable.

- MCP `mcp__drm-copilot__validate_orchestration_artifacts` (artifact_type plan): `PLAN_PHASE_RE = /^### Phase (\d+) — (title)$/` requires an **em-dash** (U+2014). In this environment it also reads stale/wrong-worktree content — identical task lines returned different verdicts across back-to-back runs (lesson 4). Treat its plan verdict as non-authoritative here.
- Repo hook `.claude/hooks/validate-planner-output.ps1` (`Get-PlanStructureValidationReport`): `^### Phase (\d+)\s+-\s+(title)$` requires an **ASCII hyphen** `-`. This is the version-controlled, deterministic validator (a SubagentStop hook for atomic-planner; it does NOT fire during execution).
- Executor `drm-copilot/scripts/dev_tools/atomic_executor/plan_parser.py`: `^\s*#+\s*Phase\s+(\d+)\b` is **dash-agnostic** — the real execution gate does not care about the separator.

**Why:** epic-planner commits plans with em-dash headings (matching the MCP contract), but this repo's own hook and wave-0 lesson 1 require ASCII hyphen. The two cannot both be satisfied by one file.

**How to apply:** when resuming at execution with a committed plan, normalize the phase headings from em-dash to ASCII hyphen (`sed 's/^\(### Phase [0-9]\+\) \xe2\x80\x94 /\1 - /'`), commit as a small chore, and validate against the repo hook's regexes directly (dot-source is awkward due to a Mandatory-param guard; just apply the two regexes in a standalone pwsh loop). Do not gate on the MCP plan validator. Execution is unaffected either way because the executor parser is dash-agnostic. Related: [[checkpoint-validator-contract]], [[plan-artifact-crlf-fails-validator]].
