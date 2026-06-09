# Phase 0 — Policy Read Evidence (Issue #66)

> Remediation Cycle `remediation-plan.2026-06-08T20-00.md` (P0-T1).
> Re-read of the required policy set for the PowerShell hook coverage-gate remediation. Prior cycles retained below.

Timestamp: 2026-06-08T20-00

Policy Order (remediation cycle): per `.claude/skills/policy-compliance-order` —
1. `CLAUDE.md`
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/powershell.md`
5. `.claude/rules/quality-tiers.md`

Files read (remediation cycle, P0-T1 ordered set):

1. `CLAUDE.md` — NOT PRESENT at repo root. The `policy-compliance-order` baseline lists `CLAUDE.md` as "always loaded", but no standalone root `CLAUDE.md` exists in this repository (verified: `Glob **/CLAUDE.md` → no files; `Test-Path CLAUDE.md` → false). Standing instructions are injected via system context. Documented assumption; no file-level standing-instruction document overrides the rule files below.
2. `.claude/rules/general-code-change.md` — cross-language code change policy (read). 500-line cap for code/test/script; Markdown docs exempt.
3. `.claude/rules/general-unit-test.md` — cross-language unit test policy (read). Coverage section confirmed: line >= 85%, branch >= 75%; existing test-file exclusion clause present (line 28).
4. `.claude/rules/powershell.md` — PowerShell policy (read). Toolchain order: format (PoshQC) → analyze (PoshQC) → test (Pester); type-check N/A.
5. `.claude/rules/quality-tiers.md` — module rigor tier system (read). T4 = scaffolding; current T4 examples include PowerShell dev tooling under `scripts/`.

Override note (Option B): The operator decision authorizes targeted edits to `.claude/rules/general-unit-test.md` and `.claude/rules/quality-tiers.md` (coverage-scope exclusion of `.claude/hooks/**` as T4 scaffolding), as an explicit override of the `policy-compliance-order` baseline "Do NOT modify policy documents under `.claude/rules/`." No coverage threshold value is changed by this cycle.

Verification: this section lists all five policy files named by P0-T1.

---

## Scope Extension (Option 1A) cycle — retained for provenance

> Scope Extension (Option 1A) cycle, plan `plan.2026-06-08T11-00.md`.
> Re-read of the required policy set for the extension. Original (09-20) read retained below.

Timestamp: 2026-06-08T11-05

Policy Order (extension cycle): per `.claude/skills/policy-compliance-order/SKILL.md` —
1) CLAUDE.md (standing instructions; injected via system context — Glob `**/CLAUDE.md` returns no standalone file)
2) `.claude/rules/general-code-change.md`
3) `.claude/rules/general-unit-test.md`
4) language/domain rules in scope: `.claude/rules/csharp.md`, `.claude/rules/powershell.md`,
   plus `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`

Files read (extension cycle), all confirmed present except CLAUDE.md (injected):
`CLAUDE.md` (injected), `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`,
`.claude/rules/csharp.md`, `.claude/rules/powershell.md`, `.claude/rules/quality-tiers.md`,
`.claude/rules/tonality.md`.

Extension-cycle constraints confirmed:
- Documentation/policy/config only; per-task gate is content verification (rg / git check-ignore /
  git ls-files / Test-Path / ConvertFrom-Json), NOT language toolchains.
- Tonality: professional, factual, no humor/hyperbole/metaphor.
- File-size cap 500 lines for code/test/script; Markdown docs and config exempt or well under.
- Evidence written only under canonical `<FEATURE>/evidence/<kind>/`.

---

## Original (prior plan) read — retained for provenance

Timestamp: 2026-06-08T09-20

Policy Order: Per `.claude/skills/policy-compliance-order/SKILL.md`, policies were read in precedence order. This change is documentation/policy/config only (Markdown + one YAML + one docs Markdown + one memory Markdown); no C# or PowerShell production/test source is changed. The per-task gate is content verification, not language toolchains (see plan "Change Class and Gate Definition").

Files read (in order):

1. `CLAUDE.md` — standing instructions (loaded via system reminder).
2. `.claude/rules/general-code-change.md` — cross-language code change policy (500-line cap; tonality applies to authored content).
3. `.claude/rules/general-unit-test.md` — cross-language unit test policy.
4. `.claude/rules/csharp.md` — C# rules (already corrected to MSTest/Moq/FluentAssertions in a prior thread; confirmed as reference for tool-name agreement).
5. `.claude/rules/powershell.md` — PowerShell rules (carries the absent PoshQC `pester.runsettings.psd1` reference to be qualified).
6. `.claude/rules/quality-tiers.md` — tier system rules (carries wrong-repo T1–T4 examples to be replaced).
7. `.claude/rules/tonality.md` — required professional tone for all authored content.

Key constraints extracted:
- File-size limit 500 lines (Markdown documentation exempt; reusable script/policy files not exempt).
- Professional tone, no humor/hyperbole/metaphor.
- Coverage thresholds canonical: line >= 85%, branch >= 75% (uniform across tiers).
- `.claude/rules/*` is the single source of truth; `.github/instructions/*` and `AGENTS.md` reconcile to it.
