---
epic: openclaw-vision
integration_branch: epic/openclaw-vision-integration
created_at: 2026-07-06T00:00:00Z
features:
  # ---- Epic A — Local MVP data-integrity remediation (wave 0) ----
  - feature_folder: core-response-status-roundtrip-80
    issue_num: 80
    depends_on: []
  - feature_folder: calendar-overlap-filter-19
    issue_num: 19
    depends_on: []
  - feature_folder: core-cacherepository-linecap-82
    issue_num: 82
    depends_on: []
  - feature_folder: sensitivity-redaction-18
    issue_num: 18
    depends_on: []
  # ---- Epic B — Agent runtime completion (Stage 0 read-and-reply loop) ----
  - feature_folder: 2026-07-02-wire-sendmail-runtime-99
    issue_num: 99
    depends_on: [sensitivity-redaction-18]
  - feature_folder: 2026-07-02-send-idempotency-dedupe-101
    issue_num: 101
    depends_on: [2026-07-02-wire-sendmail-runtime-99]
  - feature_folder: 2026-07-02-ordinary-mail-candidates-103
    issue_num: 103
    depends_on: [2026-07-02-wire-sendmail-runtime-99]
  - feature_folder: 2026-07-02-one-on-one-move-history-105
    issue_num: 105
    depends_on: []
  - feature_folder: 2026-07-02-outbound-audit-log-107
    issue_num: 107
    depends_on: [2026-07-02-wire-sendmail-runtime-99]
  - feature_folder: 2026-07-02-calendar-write-flags-109
    issue_num: 109
    depends_on: []
  # ---- Epic C — Stage 1 foundation (cloud auth + Exchange RBAC groundwork) ----
  - feature_folder: 2026-07-02-exchange-rbac-scripts-111
    issue_num: 111
    depends_on: []
  - feature_folder: 2026-07-02-app-only-auth-module-113
    issue_num: 113
    depends_on: []
  - feature_folder: 2026-07-02-graph-backed-adapter-115
    issue_num: 115
    depends_on: [2026-07-02-app-only-auth-module-113]
  - feature_folder: 2026-07-03-graph-subscriptions-delta-117
    issue_num: 117
    depends_on: [2026-07-02-graph-backed-adapter-115]
  - feature_folder: send-on-behalf-allowlist
    issue_num: null
    depends_on: [2026-07-02-graph-backed-adapter-115]
  - feature_folder: azure-bicep-iac
    issue_num: null
    depends_on: [2026-07-03-graph-subscriptions-delta-117]
  - feature_folder: negative-scope-smoke-test
    issue_num: null
    depends_on: [2026-07-02-exchange-rbac-scripts-111, 2026-07-02-graph-backed-adapter-115]
  # ---- Epic D — Stage 2 Final Vision (calendar writes + audit) ----
  - feature_folder: organizer-reschedule
    issue_num: null
    depends_on: [2026-07-02-calendar-write-flags-109, send-on-behalf-allowlist, azure-bicep-iac, negative-scope-smoke-test]
  - feature_folder: attendee-propose-new-time
    issue_num: null
    depends_on: [organizer-reschedule]
  - feature_folder: graph-activity-log-purview
    issue_num: null
    depends_on: [2026-07-03-graph-subscriptions-delta-117]
---

# Epic: OpenClaw Vision Delivery (Epics A–D unified)

## Goal

Deliver the full OpenClaw Administrative Assistant vision defined in
[docs/open-claw-approach.master.md](../../../open-claw-approach.master.md) across all three
delivery stages (Stage 0 Local MVP, Stage 1 Product Increment 1 cloud RBAC, Stage 2 Final Vision)
as a single dependency-scheduled epic. This manifest unifies the four epics from the roadmap
([docs/research/2026-07-01-open-claw-vision-gap-analysis.md](../../../research/2026-07-01-open-claw-vision-gap-analysis.md)
§ "Proposed Epic and Feature Breakdown") into one `epic-orchestrator`-driven program.

## Scope

Twenty features (F1–F20). Features F1–F13 have already merged to `main`; feature F14
(`#117`, Graph subscriptions/webhook/queue/delta) is in flight; F15–F20 are not started. See
[epic-status.md](epic-status.md) for the live per-feature status projection.

## Non-goals

- Automatic epic decomposition. This manifest is human-authored static input; `epic-orchestrator`
  does not rewrite it.
- Live-tenant verification. Every Stage 1/Stage 2 (Epic C/D) tenant-dependent step ships as
  mocked-Graph/mocked-cmdlet contract tests plus a human runbook (`human_interaction` exception),
  per the gap-analysis Automation Feasibility table. No Azure/Exchange credentials exist in this
  environment or CI.

## Retrofit provenance (read before resuming)

This epic was authored retrospectively on 2026-07-06 to convert an earlier single-`orchestrator`
`program_mode` run into an `epic-orchestrator`-resumable program. Key reconciliation decisions:

1. **Integration branch = current `main`.** F1–F13 merged directly to `main` under the earlier
   run (PRs #96–#116), so `epic/openclaw-vision-integration` is created off the current `main` tip
   (`e085712`, which already contains all merged work plus the PR #118 harness update). F1–F13 are
   therefore recorded as `merged` with their real `main` merge-commit SHAs; they are terminal and
   are never relaunched.
2. **F14 (#117)** branched cleanly off the same tip (merge-base `e085712`, 2 commits ahead, 0
   behind). It is recorded `worktree_created` at
   `C:/Users/DanMoisan/repos/open-claw-bridge-wt-2026-07-01-22-00`, branch
   `feature/graph-subscriptions-delta-117`, mid-remediation (cycle 1, finding B-117-01). Its
   PR base must be the integration branch, not `main`.
3. **F3 (#82)** was closed by verification with no code change and no active folder; the manifest
   basename `core-cacherepository-linecap-82` is a nominal identifier for a terminal, no-op entry.
4. **F15–F20 `issue_num: null`.** These are not yet promoted. Each child `orchestrator` promotes
   its feature (creating the GitHub issue and active folder) at launch and records the real issue
   number back into the epic checkpoint's `features[]` entry. The provisional `feature_folder`
   basenames above are the identifiers the child promotion must adopt.

## Wave assignment (longest-path layering)

Computed from the `depends_on` edges (`wave(f)=0` if no deps, else `1 + max(wave(d))`):

| Wave | Features |
|---|---|
| 0 | F1, F2, F3, F4, F8, F10, F11, F12 |
| 1 | F5, F13 |
| 2 | F6, F7, F9, F14, F15, F17 |
| 3 | F16, F20 |
| 4 | F18 |
| 5 | F19 |

Current wave: **2**. Within wave 2, F6/F7/F9 are already merged; F14 is in flight; F15 and F17
are not started. F14/F15/F17 are unblocked because their dependencies (F13, F11) are merged.

## Per-feature complexity assessment (C1–C4)

Bands are judgment-based per `config/orchestration-routing.json` `model_policy.complexity`. `floor`
is `compute_complexity_floor(signals_present)`: any floor signal
(`classifier_or_model_logic`, `auth_or_token_handling`, `concurrency_or_ordering`,
`cross_module_contract_change`) raises the floor to C3; C4 is judgment-only and never floor-forced.
`band >= floor` always holds.

| F | Feature | Band | Floor | Floor signals | Rationale |
|---|---|---|---|---|---|
| F1 | core-response-status-roundtrip | C2 | C1 | — | Localized cache round-trip bug fix (schema column + read/write). |
| F2 | calendar-overlap-filter | C2 | C1 | — | Localized overlap-window filter bug fix. |
| F3 | core-cacherepository-linecap | C1 | C1 | — | Verification only; no code change. |
| F4 | sensitivity-redaction | C3 | C3 | cross_module_contract_change | Data-layer redaction invariant composes across `BridgeMode`; alters persistence guarantee. |
| F5 | wire-sendmail-runtime | C3 | C3 | cross_module_contract_change | Activates the live send path end-to-end across Agent runtime + pipeline kill-switch. |
| F6 | send-idempotency-dedupe | C3 | C3 | concurrency_or_ordering | Retried-cycle dedupe with restart-durable state; ordering invariant. |
| F7 | ordinary-mail-candidates | C2 | C1 | — | New pure matching helper + candidate widening; no public-contract change. |
| F8 | one-on-one-move-history | C3 | C3 | concurrency_or_ordering | Move-history state-transition rule ("twice per six, never consecutive"). |
| F9 | outbound-audit-log | C3 | C3 | cross_module_contract_change | Correlation-id threading across four worker emission points. |
| F10 | calendar-write-flags | C2 | C1 | — | Config-key rename toward master naming; flags remain off, no behavior change. |
| F11 | exchange-rbac-scripts | C3 | C3 | auth_or_token_handling | RBAC role-assignment automation; security-sensitive path. |
| F12 | app-only-auth-module | C3 | C3 | auth_or_token_handling | MSAL client-credentials token acquisition. |
| F13 | graph-backed-adapter | C3 | C3 | cross_module_contract_change | New Graph-backed implementation of a cross-boundary interface (contract parity). |
| F14 | graph-subscriptions-delta | C4 | C3 | concurrency_or_ordering, cross_module_contract_change | Novel: webhook handshake + queue + `messages/delta` reconciliation + subscription lifecycle. |
| F15 | send-on-behalf-allowlist | C3 | C3 | auth_or_token_handling | Principal-mailbox representation gated by an authorization allowlist. |
| F16 | azure-bicep-iac | C2 | C1 | — | Declarative IaC authoring (Bicep text); no runtime behavior. |
| F17 | negative-scope-smoke-test | C3 | C3 | auth_or_token_handling | Asserts in/out-of-scope RBAC authorization split at startup. |
| F18 | organizer-reschedule | C3 | C3 | cross_module_contract_change | First real calendar-write RPC (`PATCH /events`) behind a flag. |
| F19 | attendee-propose-new-time | C3 | C3 | cross_module_contract_change | Calendar-write propose-new-time RPC behind a flag. |
| F20 | graph-activity-log-purview | C2 | C1 | — | Additive audit/observability integration. |

Model tiers resolve from these bands per delegation at child-`orchestrator` runtime under the
session `model_budget.fable_policy` (currently `preferred`), recorded as `complexity_assessments[]`
and `model_routing_receipts[]` in each child checkpoint. Bands here are the epic-level planning
inputs; each child re-assesses per phase and the child's recorded value is authoritative.
