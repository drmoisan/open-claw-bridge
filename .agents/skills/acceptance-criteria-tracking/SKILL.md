---
name: acceptance-criteria-tracking
description: 'Track and check off acceptance criteria in requirement source files such as issue.md, spec.md, and user-story.md as work is delivered. Use when executing plans, reviewing features, or validating delivery.'
---

# Acceptance Criteria Tracking

Shared protocol for identifying, tracking, and checking off acceptance criteria in their authoritative source files.

## When to Use This Skill

Use this skill when:
- executing an atomic plan that satisfies acceptance criteria,
- reviewing a feature branch and validating delivered behavior,
- reconciling task completion with requirement documents.

## AC Source Resolution

Resolve authoritative acceptance-criteria sources from the persisted work-mode marker in `issue.md`:

| Work Mode | AC Source File(s) |
|---|---|
| `minor-audit` | `issue.md` only |
| `full-feature` | `spec.md` and `user-story.md` |
| `full-bug` | `spec.md` only |
| legacy `full` | normalize to `full-feature` |
| missing / malformed | fail closed to `full-feature` |

When multiple source files apply, track checkboxes in each applicable file independently.

## Check-Off Rules

1. Only mark an AC item complete after implementation and verification.
2. Check off each item individually.
3. Preserve the criterion text exactly; change only `- [ ]` to `- [x]`.
4. Leave unmet items unchecked and document the gap elsewhere.
5. Do not invent or add new acceptance criteria.

## Completion Summary

At the end of plan execution or feature review, report:

```text
### Acceptance Criteria Status
- Source: <file path(s)>
- Total AC items: <N>
- Checked off (delivered): <M>
- Remaining (unchecked): <N - M>
- Items remaining: <list of unchecked criterion texts, if any>
```
