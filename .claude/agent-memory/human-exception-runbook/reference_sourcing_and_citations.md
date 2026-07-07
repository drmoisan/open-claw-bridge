---
name: reference-sourcing-and-citations
description: No MCP documentation tool exists in this repo; WebFetch against learn.microsoft.com is the working, repeatable sourcing path for runbook citations.
metadata:
  type: reference
---

This repository has no callable MCP documentation-retrieval tool wired as a dependency (confirmed by repo-wide search, referenced in the two-axis-model-selection spec's Out of Scope section and in `.claude/skills/human-exception-runbook/SKILL.md`). The skill's "MCP-first, web-second" sourcing rule is therefore satisfied in practice by `WebFetch` alone.

**Working pattern for citations:** `WebFetch` against `https://learn.microsoft.com/en-us/...` pages reliably returns YAML front matter containing an `ms.date` (author-set) and `updated_at` (site-computed last-touch) field — use `updated_at` as the citation date in the runbook's Source-and-Citation table, since it reflects the most recent content sync even when `ms.date` is stale. Useful pages found so far for CloudSync/Purview/Graph-audit runbooks: `graph/api/directoryaudit-list` (permissions + roles for `directoryAudits`), `entra/identity/role-based-access-control/permissions-reference` (built-in role descriptions), `purview/audit-log-search-script` (`Search-UnifiedAuditLog`), `purview/audit-search` (Purview portal UI navigation), `graph/api/resources/directoryaudit` (resource shape).

**How to apply:** When authoring a runbook that touches Entra/Graph/Purview live-tenant verification, start from this page set rather than re-discovering them via generic search; confirm each is still current by re-fetching (cache is 15 minutes) rather than trusting a stale memory of their content.
