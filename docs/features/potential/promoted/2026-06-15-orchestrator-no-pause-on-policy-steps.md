# orchestrator-no-pause-on-policy-steps (Issue #88)

- Date captured: 2026-06-15
- Author: drmoisan
- Status: Promoted -> docs/features/active/orchestrator-no-pause-on-policy-steps/ (Issue #88)

- Issue: #88
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/88
- Last Updated: 2026-06-15
## Problem / Why

The orchestrator policy prescribes a set of autonomous workflow steps that must run without operator approval: committing audit/evidence artifacts, opening the PR after a clean exit gate, post-PR CI monitoring, and entering/running the remediation loop. During Issue #73 orchestration, the orchestrator paused at the policy-defined "open PR" step to request operator confirmation. The operator confirmed this is a defect: "This should always happen without needing my feedback. It is described in the policy."

The root cause is an architecture gap: the orchestrator prompt (`.claude/agents/` orchestrator definition) describes post-exit-gate behavior (PR open, CI monitoring) as required steps but does not explicitly state that these steps are autonomous and must not solicit operator confirmation. The boundary between "novel/irreversible fork requiring operator confirmation" and "policy-defined step that runs autonomously" is implicit, so it can be misapplied — biasing toward an unnecessary pause.

## Proposed Behavior

Make the autonomous-vs-confirmation boundary explicit and enforceable in the harness so policy-defined steps never trigger a stoppage:

1. Add an explicit "Autonomous Steps (No Operator Confirmation)" subsection to the orchestrator policy enumerating the policy-defined steps that must proceed without confirmation: audit/evidence-artifact commits, branch push, PR open after the exit gate passes, post-PR CI monitoring, and remediation-loop entry/execution.
2. State the inverse rule: operator confirmation is reserved for forks that are BOTH novel (not prescribed by policy) AND consequential/irreversible (harness structure, version-control policy, coverage/CI gate surface).
3. Optional enforcement: extend `.claude/hooks/validate-orchestrator-output.ps1` (or a sibling check) to flag a checkpoint that records a `blocked_reason` of `user_requested_stop` (or an equivalent pause marker) on a step that the policy classifies as autonomous, so a regression surfaces in review rather than silently.

## Acceptance Criteria (early draft)

- [ ] The orchestrator policy contains an explicit enumeration of autonomous, no-confirmation steps (commit, push, PR open, CI monitoring, remediation loop).
- [ ] The orchestrator policy states that confirmation is reserved for novel AND consequential forks only, with the Issue #73 PR-open case cited as a negative example.
- [ ] A validation gate flags a stoppage recorded against a policy-classified autonomous step.

## Constraints & Risks

- Must not weaken the legitimate confirmation path for genuinely novel/irreversible forks (Issue #66/PR #68 harness-tracking, scope-removal, and coverage-exclusion forks were correctly surfaced).
- Harness changes are version-controlled (`.claude/rules/*` canonical) and must keep `AGENTS.md` and `.github/instructions` in sync.
- Any hook change under `.claude/hooks/**` is T4 scaffolding and excluded from the application coverage surface.

## Test Conditions to Consider

- [ ] Validation hook: a checkpoint pausing on `open_pull_request` after a clean exit gate is flagged.
- [ ] Validation hook: a checkpoint pausing on a genuinely novel fork is NOT flagged.
- [ ] Policy text review: autonomous-step enumeration present and consistent across orchestrator definition, `AGENTS.md`, and `.github/instructions`.

## Next Step

- [ ] Promote to GitHub issue (feature request / refactor template)
- [ ] Create `docs/features/active/orchestrator-no-pause-on-policy-steps/` folder from the template