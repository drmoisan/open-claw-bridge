---
name: harness-gitignore-scan
description: The .claude/ and .github/agents/* harness files are gitignored, so default ripgrep scans miss them; use --no-ignore when auditing harness markers
metadata:
  type: feedback
---

When auditing the agent harness (`.claude/`, `.github/agents/*`), the harness files are gitignored in
this repository. A default `rg` (ripgrep) scan respects `.gitignore` and silently skips them.

**Why:** During the Issue #66 harness-migration correction, the canonical AC scan
(`rg -n "<markers>" .claude .github AGENTS.md`, no flags) reported far fewer matches than the true
state. A `--no-ignore` rerun revealed additional unqualified markers in gitignored files (for example
`.github/agents/csharp-typed-engineer.agent.md`). A grep-based "zero markers" claim against the default
scan can therefore be a false negative for harness content.

**How to apply:** When verifying a harness/policy change by marker scan, run both the canonical
(default-ignore) scan that the spec/plan specifies AND a `--no-ignore` diagnostic scan. Report the
default-scan result as the AC gate (matching the spec's command), but classify every `--no-ignore`-only
hit as qualified, out-of-scope, or a follow-up. A qualified statement ("X is not present", "Do not
introduce X") still matches a literal regex, so reword to avoid the literal token only when the AC's
verification command is a strict "no matches" check. See [[remediation-loop-strict-handoff]].
