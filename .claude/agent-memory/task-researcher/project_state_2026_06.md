---
name: project-state-june-2026
description: Current implementation state of OpenClaw MailBridge as of 2026-06-07 — MVP substantially met, key remaining gaps documented
metadata:
  type: project
---

As of 2026-06-07 the OpenClaw MailBridge MVP is substantially complete. All six layers of the vision (bridge, named-pipe client, HostAdapter HTTP, Core Docker container, openclaw-agent, and scripted bundle install/uninstall) are implemented and tested.

**Why:** Comprehensive gap analysis conducted against feature archive records, source code, and prior research notes (PDFs were unreadable — pdftoppm unavailable).

**Key remaining gaps:**
1. Install.ps1 Stage 8.5 bridge-ready defect (HIGH): bridge does not start from MSIX windows.startupTask at install time; bounded retry + explicit launch needed. Research: `artifacts/research/2026-04-29-install-stage8.5-bridge-ready-defect.md`.
2. quality-tiers.yml absent: no project tier classification exists; T1/T2 quality gates (mutation testing, property-based tests) cannot be applied.
3. Harness migration defects catalogued as issue #66 (2026-06-08): csharp.md and architecture-boundaries.md are already fixed; remaining problems are xUnit/NSubstitute/Directory.Build.props residue in csharp-qa-gate/SKILL.md, invoke-csharp-engineer/SKILL.md, and csharp-typed-engineer.md; msbuild TaskMaster.sln / vstest.console.exe in .github/instructions/csharp-*.instructions.md and AGENTS.md; wrong-repo examples in quality-tiers.md; Office.js/taskpane residue in general-unit-test.md; pester.runsettings.psd1 path absent in powershell.md; quality-tiers.yml absent; generator script scripts/dev-tools/sync-agents-from-instructions.ps1 absent; .claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md absent; 11 .github/agents/ files for Python/React/Next.js stacks should be removed. Full audit: artifacts/research/2026-06-08-issue-66-harness-migration-audit.md.
4. HostAdapter has no auto-start mechanism — operator must start it manually after each machine restart.
5. ToJson/CcJson on MessageDto always null (reserved for future use).
6. Feature #45 AC-9 pending manual operator verification.

**How to apply:** Use this as context for any new development work. Phases 1 (Stage 8.5 fix) and 2 (rule alignment + tier classification) should precede new feature work.
