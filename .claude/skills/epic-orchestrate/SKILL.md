---
name: epic-orchestrate
description: Route a multi-feature epic through the deterministic wave-scheduling, integration-branch, and fan-in workflow for the epic-orchestrator agent.
argument-hint: "[epic-manifest-path]"
context: fork
agent: epic-orchestrator
---

# Epic Orchestrate Skill

A user invocation (`/epic-orchestrate <epic-manifest-path>`) forks the `epic-orchestrator`
agent with this procedure in context. The epic manifest path (or epic slug) for this run is:

$ARGUMENTS

This skill frames work for the `epic-orchestrator` agent, parallel to how
`.claude/skills/orchestrate/SKILL.md` frames work for `orchestrator`. It documents the epic
checkpoint handling, wave computation, integration-branch lifecycle, wave barrier,
merge-conflict handling, worktree cleanup, and documentation-maintenance procedures so the
procedure is not re-derived ad hoc on each epic run.

## Prerequisites

Before proceeding, `epic-orchestrator` must:

1. Read `CLAUDE.md` for repository tone policy and architectural context.
2. Read applicable `.claude/rules/` files for the languages in scope.
3. Read the policy files listed in the compliance reading order section of `CLAUDE.md`.

## Epic Dependency Manifest

The epic manifest is the YAML frontmatter of the single epic home
`docs/features/epics/<epic-slug>/epic.md`. `epic.md` is the merged source of truth: its
frontmatter carries the fields that must be parsed deterministically, and the Markdown body
below the frontmatter carries the single free-text epic narrative (goal, scope, non-goals,
shared design, decomposition) that is not machine-parsed. `epic.md` is also the source from
which the epic GitHub issue body is generated.

Frontmatter schema:

```yaml
---
epic: <epic-slug>
integration_branch: epic/<epic-slug>-integration
created_at: <iso8601>
# Optional additive SAFe-style intent block. Omit the whole block when unused; when
# present, epic_type and business_outcome_hypothesis are required and
# leading_indicators / nfrs are optional lists of strings.
intent:
  epic_type: <business | enabler>
  business_outcome_hypothesis: <measurable outcome the epic is expected to move>
  leading_indicators: [<early validation signal>, ...]
  nfrs: [<non-functional requirement>, ...]
features:
  - issue_num: <int>
    feature_folder: <resolvable-hint-basename>
    depends_on: [<upstream-issue_num>, ...]
---
```

- `issue_num` is the primary key: the stable GitHub issue number for the child feature. The
  DAG is keyed by `issue_num`, so it does not drift when a child is promoted from `active/` to
  `completed/`.
- `feature_folder` is a resolvable hint, not a stable identifier. It may resolve to a concrete
  path under `docs/features/active/<basename>` or `docs/features/completed/<basename>`; a
  lifecycle prefix is stripped to the basename during resolution.
- `depends_on` is an array of `issue_num` values (legacy manifests may still use
  `feature_folder` basenames). Each entry must resolve — via the union index of the
  `issue_num` set plus the `feature_folder` set — to another entry in `features[]`. A
  `depends_on` entry that does not resolve, or a duplicate `feature_folder` value, is a
  malformed manifest and is rejected before epic kickoff as a synthetic Blocking finding —
  `epic-orchestrator` does not guess.
- The optional `intent` block is additive and presence-gated: when present it is validated
  (`epic_type` in {business, enabler}, non-empty `business_outcome_hypothesis`, string-list
  `leading_indicators` / `nfrs`); when absent, validation is byte-identical to a manifest
  without it.

## Wave Assignment

Wave assignment is computed deterministically by longest-path layering over the dependency DAG,
not by an arbitrary valid topological order:

```
wave(f) = 0                                   if depends_on(f) is empty
wave(f) = 1 + max(wave(d) for d in depends_on(f))   otherwise
```

`scripts/dev_tools/epic_wave_computation.py` is the canonical, tested reference implementation
of this formula.

Compute this via memoized recursion with cycle detection: a `feature_folder` encountered while
still being resolved (i.e., it appears in its own dependency chain) indicates a cycle in the
manifest, which is rejected as a malformed manifest before kickoff. Within a wave, feature
ordering for emission into checkpoint arrays is lexicographic by `feature_folder`, purely for
deterministic serialization; wave *membership* itself has no ties to break since it is a pure
function of the DAG.

## Epic Integration Branch Lifecycle

1. Before wave 0's launch, `epic-orchestrator` creates the integration branch off the tip of
   `main`: `git fetch origin main`, `git checkout -b epic/<epic-slug>-integration origin/main`,
   `git push -u origin epic/<epic-slug>-integration`.
2. Before starting each wave, `epic-orchestrator` runs
   `git fetch origin epic/<epic-slug>-integration` so the wave's child worktrees branch off the
   current remote tip, not a stale local ref.
3. Each child feature's worktree/branch (created via
   `Agent(orchestrator, isolation: "worktree", run_in_background: true)`) is branched from
   `origin/<integration_branch>`, not `origin/main`. The child's own `orchestrator` instance
   honors this via the epic-mode kickoff line below.
4. Each child feature's PR base branch is the integration branch, not `main` — an explicit
   epic-mode override recorded in the checkpoint, not a reliance on `pr-base-branch-merge-base`'s
   ancestry heuristic. Non-epic (standalone) orchestration is unchanged.
5. At epic completion (every feature in the final wave has `merge_status: "merged"`),
   `epic-orchestrator` drives a final PR merging `epic/<epic-slug>-integration` into `main`,
   delegating PR authoring to `Agent(pr-author)` and refreshing context via
   `mcp__drm-copilot__collect_pr_context`. `epic-orchestrator` runs the same S9 CI-green
   procedure (`scripts/orchestration/Invoke-CiGateParser.ps1`) directly against this PR, records
   the result under the epic checkpoint's `epic_merge_pr` object, then executes
   `gh pr merge --merge` once green, gated by `enforce-epic-merge-gate.ps1`.

## Merge-on-Green Kickoff Parameter

When `epic-orchestrator` delegates a child feature to `Agent(orchestrator)`, the prompt includes
the literal epic-mode kickoff line:

> `Epic mode: true. epic_feature_folder: <epic-slug>. integration_branch: epic/<epic-slug>-integration. epic_checkpoint_path: artifacts/orchestration/epic-orchestrator-state.json. PR base branch MUST be <integration_branch>, not main; pass --base <integration_branch> to gh pr create.`

The child's own `orchestrator`, on reading this line, records `epic_mode: true` and
`epic_context: { epic_feature_folder, integration_branch, epic_checkpoint_path }` at its first
checkpoint write, and on CI-green (S9 step 6) merges its own PR into the integration branch,
recording `epic_merge: { merge_commit_sha, target_branch, merged_at }`. Standalone (non-epic)
orchestration is unchanged: `epic_mode` absent or `false` makes S9 step 6 a no-op.

## Model Selection

When `epic-orchestrator` delegates a child feature to `Agent(orchestrator)`, the prompt appends the
session model-budget kickoff marker line, following the existing kickoff-marker pattern:

> `model_budget.fable_policy: <disabled|available|preferred>.`

The child's own `orchestrator` reads this line and applies the two-axis model-selection mechanism
documented in `.claude/skills/orchestrate/SKILL.md` (`## Model Selection`): it assesses a
judgment-based `complexity_band`, records `complexity_assessments[]` and `model_routing_receipts[]`,
and resolves each delegation's model tier under the given `fable_policy`. The two canonical, tested
reference implementations are `.claude/lib/model-routing/ModelRouting.psm1`
(`Get-ComplexityFloor`) and `.claude/lib/model-routing/ModelRouting.psm1`
(`Resolve-DelegationModel`). Default `fable_policy` is `disabled` when the marker is absent.

When `epic-orchestrator` itself spawns `Agent(orchestrator)` or `Agent(pr-author)`, it applies
the same per-delegation resolution and passes `model` equal to the routing receipt's `model` on
the spawn call. It MUST NOT omit `model` (an omitted `model` falls back to the delegate's
frontmatter default — `opus` for these workers — which suppresses a `fable` resolution) and
MUST NOT hard-code `model=opus` in a way that overrides the resolved routing model, mirroring
step 5 of `## Model Selection` in `.claude/skills/orchestrate/SKILL.md`.

`route` is never an input to model selection; `route` remains file-count driven and governs only
agents, skills, and MCP tools. A skill whose frontmatter `context` field holds the value `fork`
inherits the parent model and ignores a model override, so model selection applies to agent
delegations, not to fork-routed skill invocations.

## Context Handoff to Dependent Features

When `epic-orchestrator` kicks off a feature with a non-empty `depends_on`, the delegation prompt
includes one literal citation line per dependency, appended after the epic-mode kickoff line
above:

> `Upstream context for <issue_num>: depends on <dep_issue_num> (spec: <dep_resolved_folder>/spec.md; plan: <dep_resolved_folder>/plan.<ts>.md; merged as PR #<dep_pr_number>, commit <dep_merge_commit_sha>, into <integration_branch>).`

`epic-orchestrator` resolves each dependency by its stable `issue_num` against its own
checkpoint's `features[]` records, and resolves `<dep_resolved_folder>` to the dependency's
concrete `feature_folder` path — under `docs/features/active/` or `docs/features/completed/`
depending on the dependency's current lifecycle state at emit time — before emitting the line.
Because the DAG is keyed by `issue_num`, no active→completed path-drift workaround is needed: the
concrete path is resolved from the checkpoint's current state, so the dependent feature's own
`orchestrator`/`atomic-planner` is told exactly which upstream artifacts are relevant rather than
being expected to rediscover prior design decisions from the diff alone.

## Merge-Conflict Handling (Fan-In)

The merge-conflict remediation loop runs inside the child feature's own `orchestrator` instance
(the same one that executes S9 step 6), reusing the existing R1–R5 loop
(`.claude/skills/orchestrate/SKILL.md` "Remediation Loop (R1–R5)") unmodified — no new loop is
owned by `epic-orchestrator`.

Procedure, triggered by S9 step 6's merge failure:

1. The child's `atomic-executor` runs `git fetch origin <integration_branch>`,
   `git merge --no-commit origin/<integration_branch>`, and on non-zero exit captures
   `git diff --name-only --diff-filter=U` (the conflicted file list) plus the raw conflict-marker
   (`<<<<<<<`/`=======`/`>>>>>>>`) content of each conflicted file.
2. This is written as `remediation-inputs.<timestamp>.md` in the **child feature's own active
   folder** (not the epic folder), severity `Blocking`, naming the conflicting branches, carrying
   the conflicted-file list and marker excerpts — the same shape the existing CI-failure handling
   already uses, substituting the conflict-detection output for `gh run view --log-failed`.
3. The existing R1–R5 loop processes this finding exactly as a local blocking finding:
   `atomic-planner` (R1) plans the resolution, `atomic-executor` performs preflight (R2) then
   resolves the conflict markers, stages, and commits (R3), `feature-review` re-audits (R4).
4. The child's own `remediation_pass` counter is shared with local-finding and CI-failure passes
   (cap 3), unmodified.
5. On the third conflict pass without resolution, the child's `orchestrator` records
   `step9_status: "blocked_conflict_loop_limit"` (parallel to `blocked_ci_loop_limit`), does not
   write DONE, and halts. It reports this status to `epic-orchestrator`, which mirrors it into
   the epic checkpoint's per-feature `merge_status: "blocked_conflict_loop_limit"` field.

## Wave Barrier (Two-Layer Design)

Wave-barrier enforcement is a two-layer design: no single hook mechanism can validate a whole
batch of concurrent `Agent` calls, since `PreToolUse` hooks fire per call with no
cross-call/conversation-state visibility.

- **Layer 1 — per-call deterrent:** `.claude/hooks/enforce-epic-wave-barrier.ps1`, a `PreToolUse`
  hook on the `Agent` matcher. It fires when `subagent_type == "orchestrator"` and the serialized
  prompt contains the marker `Epic mode: true`, resolves the target `feature_folder` from the
  prompt text, reads `artifacts/orchestration/epic-orchestrator-state.json`, looks up that
  feature's `depends_on`, and denies with reason `EPIC_WAVE_BARRIER_BLOCKED` unless every
  dependency's `merge_status` is `merged` or `worktree_removed`.
- **Layer 2 — retrospective backstop:** the wave-barrier ordering invariant inside
  `validate_epic_orchestrator_state_text`, enforced at `epic-orchestrator` `SubagentStop` time via
  the parameterized `validate-orchestrator-output.ps1` hook. It appends
  `EPIC_WAVE_BARRIER_VIOLATION: <f> started before dependency <d> merged` when a dependency edge's
  timing invariant is violated.

Both layers are required; neither alone closes the gap. `epic-orchestrator` does not launch wave
N+1 until every wave-N feature's dependency edges are durably confirmed merged, verified against
`git worktree list --porcelain`, `git branch`, and `gh pr view --json state,mergedAt,headRefOid`
on resume, not from in-memory completion notifications alone.

## Worktree Cleanup

After a child feature's `epic_merge.merge_commit_sha` is recorded (S9 step 6 succeeds) and
`epic-orchestrator` mirrors that into its own checkpoint's `merge_status: "merged"` and
`merge_confirmed_at`, `epic-orchestrator` (running from the main repository checkout, not any
child worktree) issues `git worktree remove <worktree_path>`, gated by
`.claude/hooks/enforce-epic-worktree-removal-gate.ps1`, which denies with reason
`EPIC_WORKTREE_REMOVAL_BLOCKED` unless the epic checkpoint's matching `features[]` record has
`merge_status` in `{merged, worktree_removed}`. On success, `epic-orchestrator` sets
`merge_status: "worktree_removed"` and `worktree_removed_at`.

## Documentation Maintenance Boundaries

`epic.md` (the merged manifest + narrative source of truth) and `epic-status.md` (a separate,
epic-orchestrator-maintained status document) are kept distinct. `epic.md`'s frontmatter is the
human-authored, largely static input; automatic epic decomposition is out of scope, so this
file is not repeatedly rewritten. `epic-status.md` is a generated projection only: it is
regenerated (never hand-authored) from the epic checkpoint and is never the source of the DAG.
`epic-orchestrator` maintains `docs/features/epics/<epic-slug>/epic-status.md`, regenerated from
the epic checkpoint at each of the following boundaries, not only at final completion:

- Epic kickoff — initial status table seeded from the manifest (one row per feature: wave,
  status `not_started`).
- Each time a feature's `merge_status` changes (`worktree_created`, `pr_open`, `ci_green`,
  `merge_conflict`, `merged`, `worktree_removed`) — the corresponding row is updated in place.
- Each wave transition (`current_wave` increments).
- Final integration PR opened, green, and merged.

Each row records: `feature_folder`, `issue_num`, `wave_number`, `merge_status`, PR link
(`pr_url`), `merge_commit_sha`, and the four lifecycle timestamps from the epic checkpoint.
`epic-status.md` is a human-readable projection of the epic checkpoint's `features[]` array; the
checkpoint JSON remains the durable, machine-authoritative source.

## Epic-Level Checkpoint

`artifacts/orchestration/epic-orchestrator-state.json` carries `objective`, `route_id: "epic"`,
`epic_feature_folder`, `epic_manifest_path` (which points at
`docs/features/epics/<epic-slug>/epic.md`), `epic_status_doc_path`, `integration_branch`,
`completed_steps`, `next_step`, `last_updated`, `current_wave`, `waves[]`, `features[]`,
`epic_merge_pr`, and the three receipt arrays (`delegation_receipts[]`, `skill_receipts[]`,
`mcp_call_receipts[]`) — the full schema is defined in `spec.md` §6 of this feature. The
`merge_status` enum is: `not_started`, `worktree_created`, `pr_open`, `ci_green`,
`merge_conflict`, `blocked_conflict_loop_limit`, `merged`, `worktree_removed`. The optional
`intent` object (projection of the `epic.md` intent block) is validated presence-gated.

Every field needed to re-derive state durably on resume (`worktree_path`, `branch_name`,
`pr_number`, `merge_status`) is re-derivable from `git worktree list --porcelain`, `git branch`,
and `gh pr view --json state,mergedAt,headRefOid` — the checkpoint is a cache of that durable
state, not the source of truth.

Validate the checkpoint via
`python -m scripts.dev_tools.validate_orchestration_artifacts epic-orchestrator-state <path> --require-complete`
(or the equivalent `mcp__drm-copilot__validate_orchestration_artifacts` call with
`artifact_type: "epic-orchestrator-state"`), implemented in
`scripts/dev_tools/validate_epic_orchestrator_state.py`.

## Completion Requirements

`epic-orchestrator` must not report completion until:

1. Every feature in the manifest has `merge_status` in `{merged, worktree_removed}`.
2. The final integration-to-`main` PR has merged and `epic_merge_pr.merge_commit_sha` is
   recorded.
3. `docs/features/epics/<epic-slug>/epic-status.md` reflects the completed state.
4. The epic checkpoint passes `validate_epic_orchestrator_state_text` with
   `require_complete=True`.
