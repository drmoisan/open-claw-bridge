---
name: epic-plan
description: Scope and prepare a multi-feature epic end-to-end before execution for the epic-planner agent - epic-worthiness gate, decomposition, dependency-wave design, complexity assessment, preparation-mode child orchestrator delegations, integration-branch fan-in, and the epic-orchestrator kickoff prompt artifact.
argument-hint: "[epic objective or epic-manifest-path]"
context: fork
agent: epic-planner
---

# Epic Plan Skill

A user invocation (`/epic-plan <objective>`) forks the `epic-planner` agent with this procedure
in context. The epic objective (or an existing epic-manifest path) for this run is:

$ARGUMENTS

This skill frames work for the `epic-planner` agent, parallel to how
`.claude/skills/epic-orchestrate/SKILL.md` frames work for `epic-orchestrator`. It documents the
epic-worthiness gate, decomposition and wave design, per-feature complexity assessment, the
preparation-mode child delegation contract, integration-branch fan-in, the kickoff-prompt
artifact, and planning-checkpoint handling so the procedure is not re-derived ad hoc on each
planning run. Planning ends at preflight clearance; no atomic execution, PR authoring, or CI
monitoring occurs under this skill.

## Prerequisites

Before proceeding, `epic-planner` must:

1. Read `CLAUDE.md` for repository tone policy and architectural context.
2. Read applicable `.claude/rules/` files for the languages in scope.
3. Read the policy files listed in the compliance reading order section of `CLAUDE.md`.

## Epic-Worthiness Gate

The first planning step is a deterministic assessment of whether the objective warrants an epic:

- Decompose the objective into candidate child features, each independently mergeable with its
  own issue, feature folder, and PR.
- The objective warrants an epic only when BOTH hold:
  1. The decomposition yields two or more child features, and
  2. At least one candidate feature exceeds — or the combined scope clearly exceeds — a single
     large-path feature's practical change budget.

When the gate fails, `epic-planner` reports to the user that the complexity does not warrant an
epic, states the rationale (feature count and estimated change budget), and offers to delegate
the work directly to a single `Agent(orchestrator)` run as one feature. It records the verdict
under `epic_worthiness` in the planning checkpoint and proceeds with epic scaffolding only when
the gate passes or the user explicitly directs it to.

## Decomposition and Wave Design

For an epic-worthy objective:

1. Define the epic slug and epic home `docs/features/epics/<epic-slug>/`.
2. Author `docs/features/epics/<epic-slug>/epic.md` using the manifest frontmatter schema
   defined in the `epic-orchestrate` skill (that skill is the single schema authority; do not
   redefine it here). The Markdown body carries the epic narrative: goal, scope, non-goals,
   shared design, and decomposition rationale.
3. Derive `depends_on` edges from real upstream/downstream contracts only; do not add ordering
   edges for stylistic reasons, because every edge reduces execution parallelism.
4. Compute wave assignment with the longest-path layering formula from the `epic-orchestrate`
   skill (`scripts/dev_tools/epic_wave_computation.py` is the tested reference implementation)
   and reject cycles or unresolved references before any preparation is delegated.
5. Record the planned waves in the planning checkpoint and in the epic narrative.

At manifest-authoring time child issues do not exist yet, so `issue_num` values are recorded as
placeholders and back-filled from each child's promotion receipt as preparation completes. The
manifest is committed in final, resolved form before the kickoff artifact is written.

## Complexity Assessment

Assess each child feature's complexity band (`C1`-`C4`) using the `model_policy` scale and
signals in `config/orchestration-routing.json`, and record the band with a short rationale in
the planning checkpoint's `features[]` entries and in the epic narrative. The bands serve two
purposes: they feed the epic-worthiness rationale, and they give each child orchestrator's own
model-selection step a reviewed starting assessment.

## Integration Branch Lifecycle

1. Before any preparation delegation, create the integration branch off the tip of `main` if it
   does not already exist: `git fetch origin main`,
   `git checkout -b epic/<epic-slug>-integration origin/main`,
   `git push -u origin epic/<epic-slug>-integration`.
2. Commit the epic home (`epic.md`) to the integration branch before delegating preparation.
3. All prepared child outputs fan in to the integration branch (see Fan-In below), so the
   branch's final state is the complete, execution-ready epic plan.

## Preparation-Mode Child Delegation

Delegate one `Agent(orchestrator)` run per child feature. Because preparation produces documents
and plans rather than code, dependency edges impose no build-order constraint: launch ALL child
preparations concurrently (one message, N `Agent` calls, each `isolation: "worktree"` and
`run_in_background: true`), branching each worktree from `origin/epic/<epic-slug>-integration`.
For a dependent feature, include the upstream features' planned scope (spec/plan references or
manifest excerpts) as context lines so its spec and plan cite the upstream contracts they will
consume.

Each delegation prompt includes the literal preparation-mode kickoff line:

> `Preparation mode: true. route_id: preparation. epic_feature_folder: <epic-slug>. integration_branch: epic/<epic-slug>-integration. Perform promotion, research, feature documents (spec.md, user-story.md), atomic planning, and preflight clearance only. Atomic execution, PR authoring, and CI monitoring are out of scope for this run and are executed later by epic-orchestrator. After the atomic-executor preflight returns PREFLIGHT: ALL CLEAR, commit the feature folder and plan to the current branch, set out-of-scope step statuses to not-applicable, set next_step to S5_atomic_execution, and stop, reporting the plan-path and preflight status.`

The prompt must also reference the child's target feature folder path once promotion assigns it,
or the promotion inputs (potential entry path, short name) when it does not exist yet. The
preparation-mode kickoff line deliberately omits the epic-mode marker (`Epic mode: true`) so the
`enforce-epic-wave-barrier.ps1` deterrent, which gates execution-phase delegations, does not
apply to preparation.

### Child run contract (route_id: preparation)

A preparation-mode `orchestrator` run:

- Selects `route_id: preparation` (defined in `config/orchestration-routing.json`), whose
  required receipts are `task-researcher`, `prd-feature`, `atomic-planner`, and
  `atomic-executor` (preflight-only), the skills `orchestrate`,
  `feature-promotion-lifecycle`, and `atomic-plan-contract`, and the promotion plus validator
  MCP tools.
- Runs promotion via the MCP surface, research, feature documents, atomic planning, and the
  atomic-executor preflight (precondition validation only, per the orchestrate skill's R2
  semantics), iterating plan revisions until `PREFLIGHT: ALL CLEAR`.
- Terminates with `completed_steps` containing `S3_promotion` and `S4_atomic_planning`,
  `next_step: "S5_atomic_execution"`, out-of-scope step statuses `not-applicable`, and
  `blocked_reason: "none"`. The route's `requires_ci_gate: false` means the completion validator
  demands no `ci_gate`/`pr_gate` evidence; the run must NOT assert `next_step: "complete"`.

## Fan-In to the Integration Branch

As each child preparation completes:

1. Fetch the child worktree's branch and merge it into `epic/<epic-slug>-integration`. Prepared
   outputs live in disjoint `docs/features/active/<feature>/` trees, so conflicts indicate a
   decomposition defect; on conflict, halt fan-in and record blocked state rather than resolving
   ad hoc.
2. Back-fill the child's `issue_num` (and resolved `feature_folder`) into the epic manifest.
3. Update the planning checkpoint's `features[]` entry (`preparation_status`, `plan_path`,
   `preflight_status`).
4. Remove the child worktree once its branch is merged.

After the final fan-in, push the integration branch.

## Kickoff Prompt Artifact

After all features are prepared and committed, write the epic-orchestrator kickoff prompt to
`artifacts/orchestration/epic-kickoff-<epic-slug>.md`, and commit a durable copy to
`docs/features/epics/<epic-slug>/epic-kickoff.md` (the `artifacts/` tree is gitignored; the
committed copy travels with the integration branch). The artifact contains:

```markdown
# Epic Kickoff: <epic-slug>

Planned by epic-planner on <iso8601>. All child features are prepared: issues promoted, active
folders created, research complete, spec/user-story written, atomic plans approved, preflight
ALL CLEAR. Planning state: artifacts/orchestration/epic-planner-state.json (branch:
epic/<epic-slug>-integration).

## Invocation Prompt

Run `/epic-run <epic-slug>` to execute this epic, or paste the prompt below.

Use the epic-orchestrator subagent to execute the prepared epic at
docs/features/epics/<epic-slug>/epic.md. The integration branch
epic/<epic-slug>-integration already contains every prepared feature folder and approved atomic
plan; child features resume at atomic execution from their committed plan-path rather than
re-planning. Execute per the epic-orchestrate skill: wave-scheduled child orchestrator runs in
isolated worktrees, merge-on-green fan-in to the integration branch, and the final
integration-to-main PR.

## Feature Summary

| issue_num | feature_folder | wave | complexity | plan-path |
| --- | --- | --- | --- | --- |
| ... | ... | ... | ... | ... |
```

The `## Invocation Prompt` section is the exact text the user replays (from the main session,
never from an `orchestrator` agent) to launch execution.

## Checkpoint Handling

Persist `artifacts/orchestration/epic-planner-state.json` after every completed step with the
fields listed in `.claude/agents/epic-planner.md` (`## Checkpoint Persistence`). On resume,
re-derive durable ground truth from `git branch`/`git worktree list --porcelain`, the epic
manifest, and the prepared feature folders; treat the checkpoint's `next_step` as the resume
pointer, not as a substitute for on-disk state.

## Completion Report

The final report to the user must include: the epic manifest path, one `plan-path:` line plus
preflight status per feature, the integration branch name, and the kickoff artifact paths. End
with the statement that execution has NOT started and will begin only when the user runs
`/epic-run <epic-slug>` or replays the kickoff prompt.
