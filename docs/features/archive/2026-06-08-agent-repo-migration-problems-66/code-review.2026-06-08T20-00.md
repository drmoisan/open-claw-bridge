# Code Review: Agent Harness Migration Correction (#66)

**Review Date:** 2026-06-08
**Reviewer:** feature-review agent
**Feature Folder:** `docs/features/active/2026-06-08-agent-repo-migration-problems-66`
**Feature Folder Selection Rule:** Folder suffix `-66` matches the branch issue number (`bug/agent-repo-migration-problems-66`) and is the sole active folder with material scoping-doc changes (`spec.md`).
**Base Branch:** `development` (merge-base `72d11879918bab20652abf2965eea42f17ab67d1`)
**Head Branch:** `bug/agent-repo-migration-problems-66` @ `3ed46efaa28c43fefc946413bb3ba64866ca8d29`
**Review Type:** Initial review

---

## Executive Summary

This change corrects the agent harness that was copied from the "drm-copilot" / "TaskMaster" repository without per-file adaptation, and brings the previously-gitignored harness (`.claude/`, `.github/{agents,instructions,prompts,skills}`) under version control. The change is documentation, policy, and configuration only: 179 Markdown files, 15 PowerShell hooks (additions via version-control inclusion, content unchanged), 2 JSON, 1 YAML (`quality-tiers.yml`), and `.gitignore`. No product source, test source, build config, CI workflow, benchmark, or action file is in the diff (confirmed by `git diff --name-only ... -- 'src/' 'tests/' '*.csproj' '*.sln' '.github/workflows/'` returning empty).

**What changed:**
- `.gitignore` un-ignores `.claude/` and `.github/{agents,instructions,prompts,skills}` while keeping `artifacts/` ignored and adding `.claude/settings.local.json` to ignore.
- Residual cross-repo markers (`No-COM`, `TaskMaster`, `xUnit`, `NSubstitute`, `Office.js`, `taskpane`, `dependency-cruiser`, `Directory.Build.props`, `vstest.console`, `msbuild TaskMaster`) removed or qualified across `.claude/rules/*`, `.github/instructions/*`, and `AGENTS.md`.
- Tool names reconciled to MSTest/Moq/FluentAssertions, global `csharpier`, `dotnet build`/`dotnet test` against `OpenClaw.MailBridge.sln`; coverage thresholds unified at line >= 85% / branch >= 75%.
- Python and TypeScript ecosystem files (rules, agents, skills, prompts, hooks) deleted; orchestrator delegation references to removed workers stripped.
- New supporting files created: `quality-tiers.yml` (9 solution projects classified), `docs/ci.research.md` (tier system source of truth), `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`.

**Top 3 risks:**
1. PowerShell coverage is 0% on the `.claude/hooks/*.ps1` files that entered the branch diff; the harness hooks have no in-repo Pester suite. This is a blocking coverage gate failure for a language with changed files.
2. Hand-edited `AGENTS.md` can drift from its source instructions until the absent generator script (`scripts/dev-tools/sync-agents-from-instructions.ps1`) is authored. Mitigated for this change by editing both together and verifying with AC-06; recorded as a follow-up.
3. `.github/agents/*` REVIEW-classified personas (beast-mode, `hlbpa`, `mentor`, `commentary-remediation`) are now tracked but intentionally untouched, pending a separate human decision; they may still contain cross-repo assumptions.

**PR readiness recommendation:** **Needs Revision** — the harness correction is complete and all 15 acceptance criteria verify PASS, but the PowerShell coverage FAIL on the newly-tracked `.claude/hooks/*.ps1` files is a blocking item that must be routed through remediation.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Blocker | `.claude/hooks/*.ps1` (15 files) | branch diff additions | PowerShell has 15 changed `.ps1` files in the branch diff; the coverage artifact reports 0% line coverage on 5 measured hooks and omits the other 10. Uniform tier rule requires line >= 85%. | Add Pester coverage for the harness hooks, or scope them out of coverage at the repo policy level (not at review time). Until then this is a coverage FAIL. | A language with changed files in the branch diff must meet the coverage threshold; the Scope Invariant forbids excusing it as out-of-scope. | `artifacts/pester/powershell-coverage.xml` (report total LINE missed=284 covered=0); `git diff --name-only ... -- '*.ps1'` = 15 files |
| Info | `AGENTS.md` | header / whole file | `AGENTS.md` is hand-edited because its generator script is absent; risk of drift from `.github/instructions/*` sources. | Author `scripts/dev-tools/sync-agents-from-instructions.ps1` as a follow-up so the header is truthful. | Documented follow-up; not blocking for this change. | `spec.md` "Out of scope / follow-ups" item 1 |
| Info | `.github/agents/csharp-typed-engineer.agent.md` | L173-175 (prior state) | Previously contained `msbuild TaskMaster.sln` / `vstest.console.exe`; corrected by the scope extension. | None — corrected. | Confirms the previously-gitignored residual is now fixed. | `rg "msbuild TaskMaster|vstest\.console" .github/agents/csharp-typed-engineer.agent.md` = no match |
| Nit | `.claude/rules/benchmark-baselines.md` | L29 | References `scripts/benchmarks/Test-BaselineProvenance.ps1`, qualified as not-yet-present. | None — qualification is explicit and policy-permitted. | AC-02 permits qualified-absent references. | `rg "Test-BaselineProvenance.ps1" .claude/rules/benchmark-baselines.md` shows "not yet present" |

No Major findings beyond the Blocker above.

---

## Implementation Audit

### PowerShell implementation audit

#### What changed well

- The `.gitignore` edit correctly preserves `artifacts/` as ignored while un-ignoring the harness, verified by `git check-ignore artifacts/orchestration/orchestrator-state.json` (still matches) and `git check-ignore .claude/rules/csharp.md .github/agents/orchestrator.agent.md` (no match).
- `.claude/settings.json` remains valid JSON after the agent/skill/hook removals (`Get-Content ... | ConvertFrom-Json` succeeds), and every remaining hook `command` path resolves on disk (11/11 verified).

#### API and safety notes

- The 15 `.claude/hooks/*.ps1` files are pre-existing harness infrastructure whose content is unchanged by this branch; they entered the diff only because `.gitignore` was edited. No new advanced-function or parameter-validation surface is introduced by this change.
- PSScriptAnalyzer and Invoke-Formatter were not re-run against the hooks because their content is unchanged; these gates are UNVERIFIED rather than FAIL for the code-change axis. The coverage axis is separately FAIL (see Findings Table).

#### Error handling and logging

- Not re-audited line-by-line: the hooks' content is unchanged by this branch. No error-handling regression can be introduced by a pure version-control inclusion with no content delta.

---

## Test Quality Audit

This change adds no product code and no new unit tests. The verification evidence consists of the executor's marker scans, path-existence checks, and a `dotnet build`/`dotnet test` command-validity smoke.

### Reviewed test and QA artifacts

- `evidence/qa-gates/ac10-command-smoke.md` — Confirms `dotnet build OpenClaw.MailBridge.sln` and `dotnet test ... --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` resolve and run green (298 passed, 0 failed, 3 skipped). Validates the corrected command strings; not a coverage gate for this documentation change.
- `evidence/qa-gates/ac12-residual-scan.md` — Per-hit disposition table classifying every residual-marker match as a permitted exception. Independently reproduced during this review.
- `evidence/qa-gates/ac14-settings-json.md` — Confirms `.claude/settings.json` validity and hook-path resolution. Independently reproduced.
- `artifacts/pester/powershell-coverage.xml` — JaCoCo Pester coverage report (dated 06/06/2026) showing 0% line coverage on the measured `.claude/hooks/*.ps1` files. This is the evidence behind the Blocker finding.

### Quality assessment prompts

- **Determinism:** The verification is grep/Test-Path based and deterministic; the marker scans reproduce identically.
- **Isolation:** Each AC maps to a focused check command.
- **Speed:** All review checks are fast (seconds).
- **Diagnostics:** The per-hit disposition tables in the evidence make any future marker regression easy to localize.

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | Diff is documentation/policy/config; `.claude/settings.local.json` added to `.gitignore` to keep local settings out of version control. |
| No unsafe subprocess or command construction | N/A | No new executable code authored; hook content unchanged. |
| Input validation at boundaries | N/A | No new code paths authored. |
| Error handling remains explicit | N/A | No code content changed. |
| Configuration / path handling is safe | ✅ PASS | All referenced filesystem paths exist or are explicitly qualified not-present (AC-02); `quality-tiers.yml` lists only real solution projects (AC-03). |

---

## Research Log

No external research was required. All findings are grounded in the branch diff, the live working tree, the PR-context artifacts (`artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt`), the feature folder evidence, and the research audit (`artifacts/research/2026-06-08-issue-66-harness-migration-audit.md`).

---

## Verdict

The harness correction is well-scoped and internally consistent: residual cross-repo markers are removed or properly qualified, the Python/TypeScript ecosystem is fully excised with no dangling references, the three supporting files exist and resolve their citations, and all 15 acceptance criteria verify PASS on independent re-scan. The change touches no product runtime, test, build, or CI surface.

The change is not ready for normal PR flow as-is because of one blocking item: PowerShell has 15 changed `.ps1` files in the branch diff and the available coverage artifact reports 0% line coverage with 10 of those files unmeasured, failing the uniform line >= 85% threshold and the repo-wide < 80% trigger. This must be addressed through the remediation handoff (`remediation-inputs.2026-06-08T20-00.md`) — either by adding harness-hook coverage or by establishing a repo-level coverage-scoping policy for runtime-executed harness hooks. The recommendation is **Needs Revision** pending resolution of the coverage gate.
