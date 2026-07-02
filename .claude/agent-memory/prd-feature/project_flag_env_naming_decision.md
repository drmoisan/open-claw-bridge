---
name: flag-env-naming-decision
description: Master ENABLE_* flag names are semantic-only; realized as OpenClaw:AgentPolicy config keys (OpenClaw__AgentPolicy__* env form), no custom alias layer (issue #109)
metadata:
  type: project
---

Decision recorded in the issue #109 spec (2026-07-02): the master doc's env-style flag names (`ENABLE_ORGANIZER_RESCHEDULE`, `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`) are canonical **semantic** names only. The repo realizes them as `AgentPolicyOptions` properties bound from `OpenClaw:AgentPolicy` (env form `OpenClaw__AgentPolicy__EnableOrganizerReschedule` etc.). No custom env-alias mapping exists and none should be invented. Composition helper: static pure `CalendarWritePolicy` class (options POCO stays plain; repo projects derived logic via separate classes like `TriagePolicy.FromOptions`).

**Why:** Specs for F18/F19 (Stage 2 write paths) must document operator flag-setting through the real binding path; inventing an `ENABLE_*` env layer would describe infrastructure that does not exist.

**How to apply:** When writing F18/F19 (or any flag-related) specs, cite the `OpenClaw__AgentPolicy__*` mechanism and gate write paths through `CalendarWritePolicy` helpers, not inline boolean logic. Also note docker-compose forwards an explicit env list, so container enablement requires adding the variable to `environment:`. See [[issue-body-staleness]] for the general verify-against-code rule.
