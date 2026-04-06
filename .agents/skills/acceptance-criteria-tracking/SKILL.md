---
name: acceptance-criteria-tracking
description: 'Track and check off acceptance criteria in requirement source files (issue.md, spec.md, user-story.md) as they are delivered. Use when executing plans, reviewing features, or validating delivery to keep AC checkboxes current.'
---

# Acceptance Criteria Tracking

Shared protocol for identifying, tracking, and checking off acceptance criteria (AC) in their source requirement files as work is delivered and verified.

## When to Use This Skill

Use this skill when:
- Executing an atomic plan that delivers work satisfying acceptance criteria.
- Reviewing a feature branch and verifying acceptance criteria.
- Validating small-path or large-path delivery against requirement files.
- Any agent completes work that satisfies one or more acceptance criteria from `issue.md`, `spec.md`, or `user-story.md`.

## AC Source Resolution (by Work Mode)

Resolve the authoritative acceptance-criteria source file(s) using the work-mode marker from `issue.md`, per the mode rules in `feature-promotion-lifecycle` and `atomic-plan-contract`:

| Work Mode | AC Source File(s) |
|---|---|
| `minor-audit` | `issue.md` only |
| `full-feature` | `spec.md` and `user-story.md` |
| `full-bug` | `spec.md` only |
| legacy `full` | normalize to `full-feature` for compatibility |
| missing / malformed | fail closed to `full-feature` (`spec.md` + `user-story.md`) |

Deterministic mode rule: use the persisted `- Work Mode: ...` marker from `issue.md` as the single source of truth. `full-feature` always means `spec.md` **and** `user-story.md`; `full-bug` always means `spec.md` **only**; legacy `full` always normalizes to `full-feature`.

For `minor-audit`, require an explicit `## Acceptance Criteria` section in `issue.md`. Do not treat other `issue.md` checkbox sections as acceptance criteria for `minor-audit`.

When multiple AC source files exist, track checkboxes in **each** applicable file independently.

## AC Identification

Acceptance criteria are markdown checkbox items within AC source files.

Deterministic heading rule:
- For `minor-audit`, acceptance criteria MUST live under the exact heading `## Acceptance Criteria` in `issue.md`.
- For other work modes, headings such as `## Acceptance Criteria`, `### Acceptance Criteria`, or `## Done When` may still be used when they are the authoritative AC source file.

AC items use the standard markdown checkbox format:
- `- [ ] <criterion text>` (not yet delivered)
- `- [x] <criterion text>` (delivered and verified)

If the explicit `minor-audit` section is missing, fail closed instead of guessing from other issue sections. If AC items are not in checkbox format (e.g., numbered lists or prose), do NOT reformat them. Instead, note their status in the agent's own tracking artifacts (plan checklist, feature-audit, etc.) and document that the source file uses a non-checkbox format.

## Check-Off Protocol

### Rules (non-negotiable)

1. **Evidence before check-off**: Only mark an AC item `[x]` after the work satisfying it has been **implemented and verified** (e.g., tests pass, toolchain clean, code reviewed).
2. **One-at-a-time**: Check off each AC item individually as the corresponding work is verified. Do not batch-check items without individual verification.
3. **Preserve text**: When checking off, change only `- [ ]` to `- [x]`. Do not modify the criterion text.
4. **Leave unmet items unchecked**: If an AC item cannot be fully delivered or verified, leave it as `- [ ]` and document the gap in the appropriate artifact (plan checklist, remediation inputs, or feature-audit).
5. **No phantom criteria**: Do not add new AC items to source files. AC items are authored by planning/scoping agents, not by executors or reviewers.

### When Executors Check Off AC

During plan execution, after completing a task whose work satisfies an acceptance criterion:

1. Identify which AC item(s) in the resolved source file(s) are satisfied by the completed task.
2. Verify the work meets the criterion (task acceptance criteria passed, tests pass, etc.).
3. Update the AC source file(s): change `- [ ]` to `- [x]` for the satisfied criterion.
4. Report the check-off in the task's progress output (e.g., "Checked off AC: `<criterion text>` in `issue.md`").

Timing: Check off AC items as soon as the corresponding plan task passes verification — do not defer all AC updates to the end of plan execution.

### When Reviewers Check Off AC

During feature review (feature-audit phase):

1. For each AC item evaluated as **PASS** in the feature-audit evaluation table, check it off in the source file(s) if not already checked.
2. For items evaluated as **PARTIAL**, **FAIL**, or **UNVERIFIED**, leave them unchecked and document the gap.
3. Report any newly checked-off items in the feature-audit artifact.

### When Orchestrators Enforce AC Tracking

Orchestrators do not directly check off AC items. Instead:

1. Ensure delegated executors and reviewers reference this skill.
2. After executor or reviewer completion, verify that AC source files reflect delivered work (spot-check; do not re-execute verification).
3. If AC items remain unchecked after all delegated work completes, flag them in the orchestration summary as outstanding acceptance criteria.

## AC Status Summary (Required at Completion)

At the end of plan execution or feature review, report an AC summary:

```
### Acceptance Criteria Status
- Source: <file path(s)>
- Total AC items: <N>
- Checked off (delivered): <M>
- Remaining (unchecked): <N - M>
- Items remaining: <list of unchecked criterion texts, if any>
```

This summary must appear in:
- The executor's final completion report (after last plan task).
- The reviewer's feature-audit artifact (Phase F or equivalent).
