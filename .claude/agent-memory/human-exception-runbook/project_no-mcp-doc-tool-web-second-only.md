---
name: project-no-mcp-doc-tool-web-second-only
description: This repo currently has no callable MCP documentation-retrieval tool; human-exception runbooks must be sourced via WebFetch only (web-second becomes the sole mechanism).
metadata:
  type: project
---

The `human-exception-runbook` skill's sourcing rule is "MCP-first, web-second," but as of 2026-07-07 no `mcp__*` documentation-retrieval tool is wired as a dependency anywhere in this repository (verified by a repo-wide search referenced in the agent's own system prompt for feature F16/issue #125). This is documented as an explicit limitation in the two-axis-model-selection spec's Out of Scope section and is not resolved by the runbook-authoring agent — it is a standing repo condition, not specific to one feature.

**Why:** Until an MCP documentation tool is added, "MCP-first" is aspirational; `WebFetch` against `learn.microsoft.com` (or the relevant vendor docs) is the only available sourcing mechanism for third-party UI/CLI citations in runbooks.

**How to apply:** When authoring any future `*.runbook.md` in this repo, do not search for or expect an MCP doc tool to be available — go straight to `WebFetch` for every citation, and still record a dated `updated_at` per citation using either the fetched page's own `updated_at`/`ms.date` metadata or the capture date if the source page has no such metadata. Re-check this memory occasionally: if an MCP documentation tool is later added and wired as a dependency, this memory is stale and the skill's MCP-first order should be followed literally.
