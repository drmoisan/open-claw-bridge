---
agent: 'status_updater_agent'
description: 'Synchronize epic/feature status: check off delivered plan tasks, reconcile docs vs issues, and document acceptance-criteria evidence. Derives all paths from EpicRootFolder using the same epic directory rules as epic review.'
---

# Update Status Prompt

## Goal

Synchronize epic and feature status across:
- atomic plans (`plan.<timestamp>.md`) where items are unchecked despite delivery
- documentation (`issue.md`, `spec.md`, `user-story.md`)
- GitHub Issues (read-only by default; optional mutation via gh CLI)

If ALL acceptance criteria in `spec.md` and `user-story.md` are delivered, document evidence inside those files.

Do not ask clarifying questions. Make best-effort inferences, record assumptions, and only mark items complete when objective evidence exists.

## Inputs (required)

- **Epic root folder (required):** `${input:EpicRootFolder}`
  - Example: `docs/features/active/2026-02-02-some-epic-47`

## Optional inputs

- **Allow GitHub mutations (optional):** `${input:AllowGitHubMutations:true|false}`
  - Default behavior is **false** (no remote changes).
  - If true, the agent may use `gh` CLI to update remote issue state/content only if authenticated and safe.

## What must be derived from the epic root

From the epic root folder contents:
- Read `initiative.md`, `issue.md`, `orchestration.md` (if present)
- Enumerate each feature subfolder
- For each feature:
  - If version folders exist (`v1`, `v2`, ...), select highest `vN` as current
  - Otherwise treat the feature root as current
  - Select latest `plan.<timestamp>.md` in the current scope (max ISO timestamp `yyyy-MM-ddTHH-mm`)
  - Read `issue.md`, `spec.md`, `user-story.md`, and the selected plan (best-effort)

## Outputs

Write outputs to the epic root folder `${input:EpicRootFolder}`.

All filenames must include a timestamp in ISO-8601 format `yyyy-MM-ddTHH-mm`.

### Required deliverable

1) `status-sync.<timestamp>.md`
- Run metadata and summary
- Plans updated (which files, which tasks checked, evidence notes)
- Docs updated (issue/spec/user-story changes)
- Issue/doc synchronization outcomes
- Remote issue actions (if enabled) OR recommended gh commands
- Blockers/gaps where evidence was insufficient

### In-place updates (evidence-driven)

- Update relevant `plan.<timestamp>.md` files:
  - check off `[ ]` -> `[x]` only when evidence supports it
  - add compact evidence notes under checked items

- Update `issue.md` files:
  - add/refresh “Sync Summary (as of <timestamp>)” + preserve prior notes

- Update `spec.md` and `user-story.md`:
  - if ALL acceptance criteria are evidenced, append “Acceptance Criteria Evidence (as of <timestamp>)”
  - otherwise (optional) append partial evidence + open criteria list without claiming delivery
