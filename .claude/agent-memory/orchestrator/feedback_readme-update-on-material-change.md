---
name: readme-update-on-material-change
description: README.md must always be updated when a PR materially changes functionality.
metadata:
  type: feedback
---

README.md MUST always be updated when a PR materially changes functionality. This is not conditional on whether README currently documents the affected surface.

**Why:** The user explicitly corrected the assumption that a "(if applicable)" Definition-of-Done docs item lets README be skipped. Their standing rule: a material functionality change (e.g., adding fields to a public DTO contract and populating them, new endpoints, new behavior/modes) always warrants a README update so the top-level docs reflect current capability. Stated on issue #72 after I shipped PR #83 without touching README.

**How to apply:**
- During planning and the PR-creation gate, treat "README updated" as a required deliverable whenever the change materially alters functionality (new/changed contract fields, methods, modes, install/run behavior). Do not mark it "not applicable" merely because README lacks a matching section — add an appropriate section or note instead.
- Include the README edit in the feature commit before `feature-review`, so the audit and PR cover it.
- Docs-only README edits still re-trigger CI on an open PR; re-run the S9 CI-green gate after pushing.
- Keep the edit factual and in the existing README style/tone (see [[checkpoint-validator-contract]] for harness-gate context).
