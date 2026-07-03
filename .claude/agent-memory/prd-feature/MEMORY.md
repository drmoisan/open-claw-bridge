# prd-feature agent memory

- [Harness canonical policy](project_harness_canonical_policy.md) — `.claude/rules/*` is the single source of truth; `.github/instructions/*` and `AGENTS.md` reconcile to it (Issue #66, 2026-06-08).
- [Harness deferred follow-ups](project_harness_deferred_followups.md) — generator script, benchmark validator, and dotnet-tools manifest are deferred out of Issue #66 scope.
- [Test framework discrepancy](project_test_framework_discrepancy.md) — repo tests use MSTest + FluentAssertions + Moq despite csharp.md saying xUnit + NSubstitute; specs must cite the real stack.
- [Issue-body staleness](project_issue_body_staleness.md) — April 2026 issue bodies predate #71/#72/#73 DTO fields; re-derive field lists from BridgeContracts.cs and record the delta.
- [Flag env-naming decision](project_flag_env_naming_decision.md) — master ENABLE_* names are semantic-only; flags bind as OpenClaw__AgentPolicy__* with no alias layer (#109).
- [PoshQC settings path absent](project_poshqc_settings_path_absent.md) — powershell.md cites scripts/powershell/PoshQC/settings/ but the dir does not exist; MCP supplies settings.
- [Core namespace-partition convention](project_core_namespace_partition_convention.md) — Core-adjacent logic = new OpenClaw.Core namespace + namespace-scoped NetArchTest, not a new project (#74, #113).
- [Graph Meta.Bridge null gap](project_graph_meta_bridge_null.md) — Graph-adapter envelopes have Meta.Bridge==null; cache writers must synthesize the D2 "ready"/"graph" status (#117 D-3).
