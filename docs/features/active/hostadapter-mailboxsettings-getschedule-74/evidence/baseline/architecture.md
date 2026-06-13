# Baseline — Architecture (ProjectReference Graph)

Timestamp: 2026-06-13T10-30
Command: grep -i "ProjectReference" on the three in-scope csproj files
EXIT_CODE: 0

Current ProjectReference edges (baseline):

- OpenClaw.HostAdapter -> OpenClaw.HostAdapter.Contracts, OpenClaw.MailBridge.Contracts
- OpenClaw.HostAdapter.Contracts -> OpenClaw.MailBridge.Contracts
- OpenClaw.Core -> OpenClaw.HostAdapter.Contracts

Output Summary: No pre-existing violation. Edges conform to .claude/rules/architecture-boundaries.md:
- Rule 4: OpenClaw.HostAdapter.Contracts depends only on OpenClaw.MailBridge.Contracts. PASS.
- Rule 5: OpenClaw.HostAdapter depends only on OpenClaw.HostAdapter.Contracts and OpenClaw.MailBridge.Contracts; does not reference OpenClaw.MailBridge (COM host) or OpenClaw.Core. PASS.
- Rule 6: OpenClaw.Core depends only on OpenClaw.HostAdapter.Contracts. PASS.
No circular references. This is the baseline the post-change verification must preserve (no new edges).
