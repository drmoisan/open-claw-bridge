---
name: full-bug-single-artifact
description: full-bug work mode expects spec.md only; user-story.md must be intentionally absent despite the prd-feature dual-artifact output contract
metadata:
  type: project
---

For `- Work Mode: full-bug` features, `spec.md` is the sole acceptance-criteria source and `user-story.md` should be absent unless requirements explicitly justify it (`.claude/skills/feature-promotion-lifecycle/SKILL.md`, ~line 105; mirrored in `acceptance-criteria-tracking`).

**Why:** The prd-feature agent definition (`.claude/agents/prd-feature.md`) and its SubagentStop hook nominally require both `spec-path` and `user-story-path` artifacts, but that contract was written for `full-feature`. Creating a user-story for a bug fix contradicts the persisted work mode and can be flagged downstream as an integrity issue.

**How to apply:** On `full-bug` runs (e.g., issue #142, 2026-07-10), write and report `spec.md` only, and state explicitly in the final report that `user-story.md` is intentionally absent per full-bug mode. Do not fabricate a user-story to satisfy the hook regex.
