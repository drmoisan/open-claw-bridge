# Feature Audit: [Feature or Scope Name] ([#Issue or Scope Reference])

> **Template Usage Instructions:**
>
> This template is for validating delivered behavior against the authoritative acceptance-criteria source files for a feature, bug, or minor-audit workflow.
>
> **How to use:**
> 1. Copy this template to the target feature folder with an ISO-8601 timestamped filename: `feature-audit.yyyy-MM-ddTHH-mm.md`.
> 2. Replace all placeholders with verified branch, evidence, requirements-source, and command details.
> 3. Resolve the authoritative acceptance-criteria source files from `issue.md` work mode before evaluating criteria.
> 4. Mark each criterion `PASS`, `PARTIAL`, `FAIL`, or `UNVERIFIED` based only on evidence you actually inspected.
> 5. Preserve the acceptance-criteria wording from the source files; do not invent or rewrite criteria.
> 6. End with an explicit overall readiness verdict and an AC status summary.
>
> **When to use:**
> - Feature branch acceptance review relative to a base branch
> - Post-remediation acceptance re-review
> - Minor-audit acceptance validation
> - Any repository workflow that requires `feature-audit.<timestamp>.md`
>
> **Delete this instruction block before finalizing the audit.**

---

**Audit Date:** [YYYY-MM-DD]
**Feature Folder:** `[docs/features/active/or/archive/path]`
**Base Branch:** `[branch-name]`
**Head Branch:** `[branch-name or working-tree scope]`
**Work Mode:** `[minor-audit / full-feature / full-bug / legacy full]`
**Audit Type:** [Initial acceptance review / Post-remediation acceptance verification / Staged acceptance check]

---

## Scope and Baseline

- **Base branch:** `[branch-name]` (commit `[sha]` if known)
- **Head branch/commit:** `[branch-name or scope]` (commit `[sha]` if known)
- **Merge base:** `[sha or N/A]`
- **Evidence sources:**
  - Primary: `[artifact or command output path]`
  - Secondary baseline diff: `[artifact path]`
  - Feature evidence: `[feature-folder/evidence/** or equivalent]`
  - Additional evidence: `[optional additional source]`
- **Feature folder used:** `[feature folder path]`
- **Requirements source:** `[issue.md / spec.md / user-story.md / multiple files]`
- **Work mode resolution note:** [Explain how work mode was determined from `issue.md`, or state that it was explicit.]
- **Scope note:** [Document any special baseline assumptions such as working-tree-only validation, regenerated PR context, or versioned feature scope.]

---

## Acceptance Criteria Inventory

**Instructions:** List the authoritative acceptance criteria exactly as they appear in the source files when they are checkbox-based. If the source uses prose or numbered requirements, transcribe them faithfully and note that no checkbox source exists for direct check-off.

**Authoritative AC source files for this run:**
- `[path]` — [primary / secondary / only source]
- `[path]` — [primary / secondary / optional source]

### Acceptance criteria

1. [Criterion text copied or faithfully transcribed from the source file]
2. [Criterion text copied or faithfully transcribed from the source file]
3. [Criterion text copied or faithfully transcribed from the source file]

[Add grouped subsections such as `### From user-story.md` and `### From spec.md` when multiple authoritative files are in scope.]

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | [Criterion text or abbreviated label] | [PASS / PARTIAL / FAIL / UNVERIFIED] | [Concrete file, artifact, test, or diff evidence] | `[exact command]` | [Clarifying note] |
| 2 | [Criterion text or abbreviated label] | [PASS / PARTIAL / FAIL / UNVERIFIED] | [Concrete file, artifact, test, or diff evidence] | `[exact command]` | [Clarifying note] |
| 3 | [Criterion text or abbreviated label] | [PASS / PARTIAL / FAIL / UNVERIFIED] | [Concrete file, artifact, test, or diff evidence] | `[exact command]` | [Clarifying note] |

[Add rows as needed.]

---

## Summary

**Overall Feature Readiness:** [PASS / NEEDS REVISION / BLOCKED]

**Criteria summary:**
- **PASS:** [N] criteria
- **PARTIAL:** [N] criteria
- **UNVERIFIED:** [N] criteria
- **FAIL:** [N] criteria

**Top gaps preventing PASS:**

1. [Primary blocking or revision gap, or `None.`]
2. [Secondary gap, or delete if not needed]
3. [Tertiary gap, or delete if not needed]

**Recommended follow-up verification steps:**

1. [Concrete next verification step]
2. [Concrete next verification step]

---

## Acceptance Criteria Check-Off

Per the acceptance-criteria tracking rules:
- Criteria evaluated as **PASS** may be checked off in the authoritative source file(s) if they are represented as markdown checkboxes and are not already checked.
- Criteria evaluated as **PARTIAL**, **FAIL**, or **UNVERIFIED** must remain unchecked.
- If the source uses prose or numbered requirements instead of checkbox items, do not rewrite the source file; record status only in this audit.

### AC Status Summary

- Source: `[file path(s)]`
- Total AC items: [N]
- Checked off (delivered): [N]
- Remaining (unchecked): [N]
- Items remaining: [List unchecked criterion text, or `None.`]

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `[path]` | [N] | [N] | [N] | [Checkbox-backed / prose-only / not authoritative] |
| `[path]` | [N] | [N] | [N] | [Checkbox-backed / prose-only / not authoritative] |

[If no source-file checkbox change was made, state that explicitly and explain why.]
