# Code Review: exchange-rbac-scripts (#111)

**Review Date:** 2026-07-02
**Branch:** `feature/exchange-rbac-scripts-111` @ `0c1104a85b4b520f17a1eaab7cbb8006eb2b14aa`
**Base:** `main` @ merge-base `8d389819d50174be9610ae69c1c4b5c9da05f829` (origin/main)
**Scope:** Full branch diff — 9 production PowerShell files (the `OpenClawRbac` module + entry script), 8 mirrored Pester test files, the human-exception runbook, feature docs/evidence Markdown, and three agent-memory records (50 files, +3201/-3)

## Executive Summary

The implementation delivers the spec's D1-D5 design exactly, with no drift. The module layout follows the observed repo convention (`scripts/powershell/modules/<Name>/` with manifest + dot-sourcing root module, one file per public function plus one seams file), keeping every file well under the 500-line cap. The nine wrapper seams are uniform and disciplined: explicit named parameters covering exactly the used cmdlet subset, runtime resolution via `Get-Command -ErrorAction SilentlyContinue`, a specific actionable error naming the missing cmdlet and the remediation (`ExchangeOnlineManagement` + `Connect-ExchangeOnline`), and `$null`-on-not-found for the three `Get-*` wrappers so absence is data rather than an error. There is no parse-time Exchange dependency anywhere — pinned by a dedicated static-scan test — so the module formats, analyzes, and tests on machines without the tenant module, which the reviewer's own environment confirms. The five public functions are textbook advanced functions: `[guid]` typing and validation attributes fail malformed input at binding time, every write sits inside a `ShouldProcess` gate (`-WhatIf` produces zero write-wrapper calls, test-pinned per function and end-to-end through the entry script), and idempotency is check-before-create with informational no-ops (plus the deliberately narrow existing-ACE catch in `Set-OpenClawSendOnBehalf`, which re-throws everything else — the spec's documented trade-off with a recorded follow-up). `Test-OpenClawScopeBoundary` returns the spec's structured result object, never calls `exit` (static-scan-pinned), and computes precise, joinable failure reasons; exit-code mapping lives only in the entry script. The test suite mocks the wrapper seams exclusively — Exchange cmdlets are never Pester-mocked; the seam tests instead inject fake resolvable commands into module scope so the real wrapper bodies execute, which is why 168 of 169 new-code lines are covered by execution rather than by mocks. One Minor finding (the single uncovered line) and three Info observations; no blockers.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Minor | scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Seams.ps1 | line 112 (`Invoke-OpenClawNewManagementRoleAssignment`, `RecipientAdministrativeUnitScope` arm) | The AU-scope pass-through line is the only uncovered command in the new code. The seam contract case for this wrapper supplies `CustomResourceScope` only; the AU route is exercised solely against mocks (Grant tests mock the wrapper; the entry-script AU test mocks the public function), so a regression in this forwarding line would not fail any test. | Add one data-driven case to `OpenClawRbac.Seams.Tests.ps1` invoking the wrapper with `RecipientAdministrativeUnitScope` (and no `CustomResourceScope`) and asserting exact forwarding — the existing `-ForEach` framework accepts it as a one-entry addition. | Uncovered branch arms usually mark a real untested scenario; here it is the production forwarding path for Option B (Administrative Unit) tenants. Non-blocking: the arm is symmetric with the covered `CustomResourceScope` arm, all thresholds pass with margin, and the argument name is additionally pinned (against mocks) at the Grant level. | Reviewer per-line parse of `artifacts/pester/powershell-coverage.xml`: Seams.ps1 59/60, missed line nr=112; new-code aggregate 168/169. |
| Info | scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1 | `FunctionsToExport` (14 names) | AC-1 reads "exports exactly five public functions" while the manifest exports 14 (5 public + 9 wrappers). This is not a defect: spec D1 explicitly mandates exporting the wrappers so Pester can mock them with `-ModuleName OpenClawRbac` and verify invocation arguments, and AC-2 names all nine wrappers. The executor's `ac1-module-surface` gate validated the full 14-name set. | None required. If a future consumer should see only the five-function surface, a nested-module or `InModuleScope`-based test strategy could re-hide the wrappers — deliberately out of scope here. | The AC-1 phrase and the D1 decision are reconciled by the spec itself; recording the interpretation keeps the audit trail unambiguous. | `spec.md` D1 row 1; `evidence/qa-gates/ac1-module-surface.2026-07-02T18-38.md`; manifest lines 10-25. |
| Info | scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Seams.ps1 | all nine wrappers | The missing-cmdlet `throw` message is repeated nine times with only the cmdlet name varying. | Optional: a private `Resolve-OpenClawExchangeCommand -CmdletName <name>` helper would remove the repetition. Not recommended for this change — the flat form keeps each wrapper independently readable and the seam file is 197 lines. | Simplicity-first cuts both ways; nine similar literals are a smaller cost than an extra indirection layer in a file whose whole purpose is being trivially auditable. | Seams.ps1 lines 27-28 et al. |
| Info | scripts/Invoke-OpenClawExchangeRbacSetup.ps1 | lines 111-144 | State-changing module calls are forwarded `-WhatIf:$WhatIfPreference` explicitly rather than relying on ambient preference propagation across the script→module boundary. | None — this is the correct, explicit pattern; preference-variable propagation into imported-module advanced functions is a known PowerShell subtlety, and the explicit forwarding is test-pinned for all four calls. | Explicit forwarding makes the dry-run contract auditable at the call site. | Entry-script tests: `forwards -WhatIf to every state-changing call` (4 ParameterFilter assertions). |

## Implementation Audit

### PowerShell implementation audit

#### What changed well

- **Seam discipline (AC-2).** Every Exchange call in every public function goes through a wrapper; the reviewer verified no production function invokes an Exchange cmdlet directly, and the module-level static-scan test enforces the no-parse-time-dependency rule permanently. Wrapper parameters cover exactly the used subset (mock-signature-parity rule), and no parameter is named `Args`.
- **Idempotency model (spec D3).** `Register`/`Scope`/`Grant` are check-before-create with informational messages and the existing object returned; `Grant` is idempotent per role with a three-state summary row (`Created`/`AlreadyExists`/`WhatIf`); `Set-OpenClawSendOnBehalf` uses the additive `@{ Add = ... }` payload plus a targeted catch that matches only the documented existing-ACE message and re-throws anything else — exactly the narrow error handling the fail-fast policy requires.
- **ShouldProcess correctness.** Gates wrap only the write calls; the `Get-` checks and the unconditional direct-membership warning run under `-WhatIf` too, so a dry run still reports what exists — the behavior the user story's interrupted-run scenario needs. `Grant`'s `WhatIf` rows keep the summary contract intact during dry runs.
- **Boundary semantics (spec D5).** `Succeeded` is true only for (allowed, denied); the two failure reasons are exact strings joined with `; ` when both sides fail; raw authorization rows are surfaced for diagnosis; the function is read-only and `exit`-free by static-scan proof. The `@(...)` wrapping of wrapper output makes single-row results safely enumerable.
- **Entry script is genuinely thin.** Sequencing, parameter forwarding, and exit-code mapping only, as the spec demands; parameter sets mirror the module (`ByScopeName` default vs `ByAdministrativeUnit` skip route); `$ErrorActionPreference = 'Stop'` makes mid-sequence failures terminate rather than cascade.
- **Runbook quality (AC-3).** Five required sections in order; every step carries a dated Microsoft Learn citation (12 rows); the three master-doc warnings (Enterprise Application Object ID, direct-membership-only, no Send As) are quoted verbatim at the steps where the mistakes would happen; §12 Step 6 and Step 8 are delivered as human checklists; the propagation-latency note explains why the test cmdlet verifies immediately.
- **Scope discipline.** Zero existing files modified under `scripts/` or `tests/` — the branch is purely additive; no opportunistic refactors; no dependency added (ExchangeOnlineManagement deliberately excluded, spec non-goal).

#### Parameter and contract notes

- `[guid]` typing pushes malformed-ID failures to binding time, and `.ToString()` normalizes at the seam boundary so wrappers stay string-typed like the cmdlets they wrap.
- The SMTP `ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')` is the spec's "minimal SMTP pattern" — intentionally permissive, rejecting only obviously non-address input. Appropriate for an operator-facing tool where Exchange performs authoritative validation.
- The entry script's `-PrincipalMailbox` is `[string[]]` with `ValidateNotNullOrEmpty` but no SMTP pattern (unlike the module function, which validates each address). Harmless: each element is re-validated at the `Set-OpenClawSendOnBehalf` binding. Not raised as a finding.

#### Error handling and logging

- Wrapper resolution failures throw specific, actionable errors; no silent catch-alls anywhere; informational no-ops use `Write-Information` + `Write-Verbose` (both, so both streams work); the direct-membership limitation is an unconditional `Write-Warning` per spec.

## Test Quality Audit

### Reviewed test and QA artifacts

- All 8 test files (reviewer read in full) — 77 tests. The seam suite's fake-command-injection pattern is the standout: it exercises the real wrapper bodies (resolution, splatting, `ErrorAction` contract, exact-argument-count assertion so nothing extra leaks through) without ever Pester-mocking an Exchange cmdlet, which keeps the mocking-rules boundary clean and drives real coverage.
- Executor evidence set under `evidence/` — every artifact carries timestamp, command, and EXIT_CODE; batch gates (format/analyze/test x3) plus final full-scope gates plus targeted gates (`ac1-module-surface`, `runbook-conformance`, `coverage-comparison`, `plan-reconciliation`, `human-interaction-record`).
- Reviewer independent runs: full Pester suite 358/358; PSScriptAnalyzer zero diagnostics; Invoke-Formatter zero diffs; per-file coverage re-parse matches executor figures exactly.

### Quality assessment

- **Scenario completeness:** all seeded test conditions from the spec are delivered — binding failures, idempotency short-circuits, WhatIf dry-runs, exact wrapper arguments (role names, assignment names, scope routing, `AutoMapping $false`, `@{Add=}` payload), all four boundary-matrix cells with exact reasons, all nine missing-cmdlet errors, entry-script sequencing/skip/exit mapping. The one uncovered execution path is named in the findings table.
- **Determinism:** no clock, randomness, sleeps, network, or filesystem; fixed placeholder inputs; in-process script invocation reading `$LASTEXITCODE`.
- **No weakening:** no existing test touched; assertions are exact (counts with `-Exactly`, exact strings for reasons and names, both-direction export-set comparison).
- **Structure:** Arrange/Act/Assert comments throughout; `-Because` clauses where a bare assertion would be ambiguous; mock-parity `param()` blocks with explicit references to satisfy `PSReviewUnusedParameter` — a small amount of ceremony, correctly traded for analyzer cleanliness.

## Security / Correctness Checks

- No secrets, credentials, tokens, or `.env` content anywhere in the diff; authentication is deliberately the ambient `Connect-ExchangeOnline` session (spec non-goal), and the module never touches certificates or client secrets.
- No tenant-specific values: all GUIDs in code/tests/runbook are `00000000-...` placeholders or documented `contoso.com` examples (executor gate scanned; reviewer spot-checked).
- Least-privilege posture preserved: exactly the four minimum roles by default; `Calendars.ReadWrite` only behind the explicit switch (test-pinned that it is absent by default); Send As is never configured — enforced behaviorally (mock filter), statically (source scan test), and procedurally (runbook verbatim warning).
- Injection surface: `GroupDistinguishedName` is interpolated into the `MemberOfGroup -eq '<DN>'` filter string. The value is operator-supplied (not attacker-controlled), passes through `New-ManagementScope`'s own filter parser, and the exact filter string is test-pinned. No action needed for this threat model.
- `Invoke-Expression` absent; command invocation uses `& $command @arguments` with a `Get-Command`-resolved `CommandInfo` — the safe pattern.
- Boundary-test correctness verified against spec D5: `InScopeAllowed` requires at least one row with `InScope = $true`; `OutOfScopeDenied` requires none; all four matrix cells and the joined-reason case are asserted with exact strings.

## Research Log

- Verified all nine wrappers against the spec D2 table (names, wrapped cmdlets, parameter subsets) — exact match.
- Verified the module dot-source list covers all six sibling `.ps1` files and the manifest export list matches the on-disk function set (also test-pinned in both directions).
- Verified no production function calls an Exchange cmdlet directly (grep for the nine cmdlet names in the five function files: only wrapper bodies and help text match).
- Verified the runbook contains the five required sections in the required order (`grep -n "^## "`) and 12 dated Microsoft Learn citation rows; path matches the orchestrator-state HI-1 `runbook_path` exactly.
- Verified orchestrator-state `human_interaction` block against the three invariants in `.claude/rules/orchestrator-state.md`: requirements list present, `response: "exception"`, non-empty `runbook_path` (AC-4, orchestrator-owned, read-verified).
- Verified zero modified files under `scripts/` or `tests/` (name-status: all additions) and zero diff paths matching `.github/workflows/**`, `scripts/benchmarks/**`, `.github/actions/**` (the `modified-workflow-needs-green-run` rule does not fire).
- Independently re-parsed `artifacts/pester/powershell-coverage.xml`: per-file figures match the executor's evidence to the hundredth; identified the single missed command at Seams.ps1 line 112.
- Re-ran the full Pester suite (358/358), PSScriptAnalyzer (zero diagnostics), and an Invoke-Formatter idempotency check (zero diffs) at branch head.

## Verdict

**Approve — no blockers.** One Minor finding (seam-level AU-scope forwarding case — optional hardening, concrete one-case recommendation) and three Info observations (manifest export-count interpretation reconciled by spec D1; repeated missing-cmdlet literal — acceptable simplicity trade; explicit `-WhatIf:$WhatIfPreference` forwarding — correct pattern, noted approvingly). Code quality, test quality, determinism, scope discipline, security posture, and documentation all meet repository policy.
