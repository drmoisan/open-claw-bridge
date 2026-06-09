# Code Review: [Feature or Scope Name] ([#Issue or Scope Reference])

> **Template Usage Instructions:**
>
> This template is for writing feature-level or staged-diff code reviews that accompany policy audits and feature audits.
>
> **How to use:**
> 1. Copy this template to the target feature folder with an ISO-8601 timestamped filename: `code-review.yyyy-MM-ddTHH-mm.md`.
> 2. Replace all placeholders with concrete branch, file, test, and evidence details.
> 3. Keep the sections that apply to the reviewed scope and delete inapplicable language subsections.
> 4. Base all findings on verified evidence: diff inspection, toolchain output, coverage data, and feature-folder artifacts.
> 5. Use severity labels consistently: `Blocker`, `Major`, `Minor`, `Nit`, `Info`.
> 6. End with a clear readiness recommendation that matches the actual findings.
>
> **When to use:**
> - Feature branch review relative to a base branch
> - Post-remediation re-review
> - Staged-diff review before commit
> - Any repository workflow that requires `code-review.<timestamp>.md`
>
> **Delete this instruction block before finalizing the review.**

---

**Review Date:** [YYYY-MM-DD]
**Reviewer:** [Agent or reviewer name]
**Feature Folder:** `[docs/features/active/or/archive/path]`
**Feature Folder Selection Rule:** [How this feature folder was selected, or delete if obvious]
**Base Branch:** `[branch-name]`
**Head Branch:** `[branch-name or staged working tree]`
**Review Type:** [Initial review / Post-remediation re-review / Staged-diff review]

---

## Executive Summary

[Summarize the reviewed change in 1-2 paragraphs. State what changed, how large the scope is, what evidence was reviewed, and the overall implementation quality.]

**What changed:**
[Describe the implementation delta relative to the base branch or staged diff. Keep this concrete and file-aware.]

**Top 3 risks:**
1. [Most important remaining risk or uncertainty]
2. [Second most important remaining risk or uncertainty]
3. [Third most important remaining risk or uncertainty]

**PR readiness recommendation:** **[Go / Conditional Go / Needs Revision / Blocked]** — [One-sentence rationale grounded in the evidence below.]

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| [Blocker/Major/Minor/Nit/Info] | `[path]` | `[line range / section / n/a]` | [Concrete issue] | [Concrete corrective action or note] | [Why it matters] | [Command output, artifact path, or inspected file reference] |
| [Blocker/Major/Minor/Nit/Info] | `[path]` | `[line range / section / n/a]` | [Concrete issue] | [Concrete corrective action or note] | [Why it matters] | [Command output, artifact path, or inspected file reference] |

[If applicable, add a short line such as `No Blockers or Major findings.`]

---

## Implementation Audit

**Instructions:** Keep only the language subsections that apply to the reviewed diff. Delete unused subsections.

### Python implementation audit (if applicable)

#### What changed well

- [Summarize the strongest implementation decisions in the Python scope.]

#### Typing and API notes

- [Describe type annotation quality, protocol/dataclass usage, public API clarity, or note `No new public Python API surface was added.`]

#### Error handling and logging

- [Describe exception handling, logging choices, and any relevant safeguards.]

### TypeScript implementation audit (if applicable)

#### What changed well

- [Summarize the strongest implementation decisions in the TypeScript scope.]

#### Type safety and maintainability

- [Describe exported type precision, generic use, suppression status, maintainability, and any notable gaps.]

#### Error handling and logging

- [Describe boundary validation, failure behavior, and logging patterns.]

### PowerShell implementation audit (if applicable)

#### What changed well

- [Summarize the strongest implementation decisions in the PowerShell scope.]

#### API and safety notes

- [Describe advanced-function usage, parameter validation, ShouldProcess behavior, naming, and analyzer hygiene.]

#### Error handling and logging

- [Describe failure behavior, error surfacing, and logging choices.]

### C# implementation audit (if applicable)

#### What changed well

- [Summarize the strongest implementation decisions in the C# scope.]

#### Type safety and API notes

- [Describe nullable safety, public contracts, analyzer cleanliness, and API clarity.]

#### Error handling and logging

- [Describe exception handling, logging patterns, and resource/lifecycle safety.]

---

## Test Quality Audit

[Summarize the automated and manual verification evidence reviewed. Note whether coverage, regression, or end-to-end evidence is present and what gaps remain.]

### Reviewed test and QA artifacts

- `[test-or-artifact-path]` — [What it verifies, execution quality, and any notable gap]
- `[test-or-artifact-path]` — [What it verifies, execution quality, and any notable gap]
- `[evidence/artifact-path]` — [What it proves about the change]

### Quality assessment prompts

- **Determinism:** [How the tests avoid flaky dependencies]
- **Isolation:** [Whether each test targets a clear behavior]
- **Speed:** [Observed runtime quality or evidence source]
- **Diagnostics:** [How clearly failures would identify the problem]

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | [✅ PASS / ⚠️ PARTIAL / ❌ FAIL / N/A] | [Concrete inspection result] |
| No unsafe subprocess or command construction | [✅ PASS / ⚠️ PARTIAL / ❌ FAIL / N/A] | [Concrete inspection result] |
| Input validation at boundaries | [✅ PASS / ⚠️ PARTIAL / ❌ FAIL / N/A] | [Concrete inspection result] |
| Error handling remains explicit | [✅ PASS / ⚠️ PARTIAL / ❌ FAIL / N/A] | [Concrete inspection result] |
| Configuration / path handling is safe | [✅ PASS / ⚠️ PARTIAL / ❌ FAIL / N/A] | [Concrete inspection result] |

[Add or delete rows to match the reviewed technology and risk profile.]

---

## Research Log

[State whether external research was required. If none, say so directly. If research was required, list the sources and why they mattered.]

---

## Verdict

[Provide the final review conclusion in 1-2 paragraphs. State whether the change is ready for normal PR flow, ready after a specific follow-up, or blocked. Ensure this conclusion is consistent with the Findings Table and PR readiness recommendation above.]
