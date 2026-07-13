---
name: plan-heading-dash-must-be-ascii-hyphen
description: The repo plan validator requires an ASCII hyphen in "### Phase N - <Title>", but the atomic-plan-contract skill documents an em-dash; em-dash phase headings fail every phase/task line.
metadata:
  type: project
---

The repository's planner-output validator hook `.claude/hooks/validate-planner-output.ps1` matches phase headings with `'^### Phase (?<Phase>\d+)\s+-\s+(?<Title>.+)$'` — an ASCII hyphen (`-`). The `atomic-plan-contract` skill and the bundled MCP `validate_orchestration_artifacts` error message both display the heading form with an em-dash (`—`, U+2014): "Phase headings must be `### Phase N — <Title>`". A plan whose phase headings actually use the em-dash FAILS validation: every `### Phase N` heading fails the heading regex, `$currentPhase` goes null, and every following `- [ ] [P#-T#]` task line then also reports as invalid, ending with "Plan does not contain any canonical phase headings."

**Why:** Verified 2026-07-12 on epic child #146. The committed, "preflight-cleared" plan used em-dash phase headings and failed both the MCP plan validator and the repo hook regex on ~44 lines. Hexdump confirmed `e2 80 94` (em-dash) in the headings; task lines were well-formed. Converting only the phase-heading dashes to ` - ` (ASCII) made a local replication of the repo hook logic pass with zero errors (phases 0-6 sequential, tasks sequential, path tokens present).

**How to apply:** When a plan fails the validator on every phase/task line but the bytes look correct, check the phase-heading dash character. Normalize em-dashes in `### Phase N` headings to ASCII ` - ` (leave prose em-dashes elsewhere alone): `perl -i -pe 's/^(### Phase \d+) \x{e2}\x{80}\x{94} /$1 - /' <plan>`. The repo hook (`\s+-\s+`) is the operative enforcement; trust it over the skill's documented em-dash. Distinct from the CRLF failure mode — see [[plan-artifact-crlf-fails-validator]]. Related: [[checkpoint-validator-contract]].
