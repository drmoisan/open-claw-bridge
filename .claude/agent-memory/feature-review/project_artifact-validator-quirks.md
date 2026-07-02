---
name: artifact-validator-quirks
description: Exact heading and line-format rules enforced by validate_orchestration_artifacts for policy-audit, code-review, and feature-audit artifacts
metadata:
  type: project
---

The `mcp__drm-copilot__validate_orchestration_artifacts` tool enforces strict structural checks
that differ from the bundled MCP templates. Building artifacts straight from the template text
will fail validation until these are fixed.

**Why:** the validator parses headings and specific lines literally; the templates use prose
variants that the parser rejects.

**How to apply (verified 2026-06-13 on issue #74 review):**
- policy-audit requires these exact headings present: `## 5. Test Coverage Detail`,
  `## 7. Code Quality Checks`, `## Appendix A: Test Inventory` (in addition to others).
- policy-audit Coverage Evidence Checklist must contain literal lines for BOTH TypeScript and
  PowerShell baseline + post-change artifacts even when out of scope (use `N/A - out of scope`).
- policy-audit per-language comparison line (Section 1.2.1) must be a SINGLE line per language
  containing all of: `Baseline: ...`, `-> Post-change: ...`, `Change: ...`,
  `New/changed-code coverage: ...`, `Disposition: ...`, `Evidence: ...`. Wrapping it across
  multiple physical lines breaks the parser ("comparison line missing ... for <lang>"). The
  three numeric tokens must use the literal labels with colons: `Baseline: N%`,
  `Post-change: N%`, and `Change: +/-N%` — prose like "Baseline 88.96% -> Post-change 89.95%"
  WITHOUT a `Change:` token fails with "missing explicit change text" (verified 2026-06-16
  env-driven-publish-versioning minor-audit).
- feature-audit requires the heading spelled exactly `## Acceptance Criteria Check-off`
  (lowercase "off"); the template ships it as `## Acceptance Criteria Check-Off` which fails.
- policy-audit per-language-comparison parser is anchored off `**bold**` table-row labels and
  will FALSELY flag the LAST bold row (e.g. `**No new analyzer/nullable suppressions**`) as
  "missing numeric baseline/post-change/new-code coverage + per-language comparison line" when
  that row's cell text contains glob/`*` characters (verified 2026-06-16 #75 re-audit: a cell
  with `` `src/**/*.cs` `` and `'^+'`/`#nullable` tokens broke it). Fix: keep `*`-glob patterns
  and coverage-like tokens out of the bold suppression-row cell; the fix re-validated cleanly.

Validate each artifact immediately after writing per the [[feature-review-workflow]] contract so
these are caught one at a time.
