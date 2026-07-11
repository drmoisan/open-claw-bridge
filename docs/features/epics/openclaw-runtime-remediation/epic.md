---
epic: openclaw-runtime-remediation
integration_branch: epic/openclaw-runtime-remediation-integration
created_at: 2026-07-11T00:00:00Z
intent:
  epic_type: enabler
  business_outcome_hypothesis: >-
    Remediating the three defect classes found in the 2026-07-10 OpenClaw runtime
    diagnosis restores a working end-to-end scheduling path: messages link to events,
    the installer ships matching Control UI and gateway images, and gateway admin
    access is provisioned without undocumented manual steps.
  leading_indicators:
    - Scheduling pipeline returns a linked event for a genuinely linked message and a
      clean null (never a 400) for an unlinked message.
    - Installer staging validation rejects mismatched Control UI and gateway image
      versions before an install proceeds.
    - Gateway admin access (token delivery, device-token rotation, web_search
      provisioning) completes from automation, with human-held-secret steps covered by
      a committed runbook.
  nfrs:
    - Preserve the graceful-degradation contract of HostAdapterSchedulingService.
    - Keep installer image-alignment behavior consistent with the #142 and #144 fixes.
features:
  # issue_num values are placeholders authored before promotion and are back-filled
  # from each child's promotion receipt during fan-in. depends_on uses stable
  # feature_folder basenames.
  - feature_folder: message-to-event-linkage
    issue_num: 901
    depends_on: []
  - feature_folder: installer-image-version-alignment
    issue_num: 902
    depends_on: []
  - feature_folder: admin-access-automation
    issue_num: 903
    depends_on: [installer-image-version-alignment]
---

# Epic: OpenClaw Runtime Remediation

## Goal

Remediate the defects surfaced by the 2026-07-10 OpenClaw runtime diagnosis so the
scheduling pipeline, installer, and gateway admin-access flow return to a working
end-to-end state. The epic groups three independently mergeable child features that
share the OpenClaw runtime surface but touch disjoint code paths.

## Scope

- **A — message-to-event linkage (feature, C#):** Implement real message-to-event
  linkage in the scheduling pipeline. Introduce the MailBridge RPC and HostAdapter
  route needed for a Core consumer to resolve the event linked to a message, and
  preserve the graceful-degradation contract of `HostAdapterSchedulingService`.
- **B — installer image-version alignment (bug, PowerShell):** Fix the installer
  shipping mismatched Control UI and gateway container images. The fix is confined to
  the installer staging/validation surface (`Install.ps1`, the
  `OpenClawContainerValidation` module, and tests under `tests/scripts`) and must stay
  consistent with the #142 and #144 fixes.
- **C — admin-access automation (feature, PowerShell):** Automate gateway admin
  access — gateway-token delivery to the Control UI (`#token=` fragment), device-token
  rotation/reissue, and `web_search` provider provisioning — with a committed runbook
  for the human-held-secret steps.

## Non-Goals

- Token generation itself: the onboarding script already generates gateway tokens;
  feature C provisions delivery, rotation, and provider setup only.
- Any change to classifier or model-affecting logic.
- Changes outside the OpenClaw runtime, installer, and admin-access surfaces.

## Shared Design

All three features operate on the OpenClaw runtime surface but touch disjoint trees:
feature A is C# under the MailBridge/HostAdapter/Core projects; features B and C are
PowerShell under the installer and admin-access tooling. The only cross-feature
ordering constraint is that admin-access automation (C) consumes the installer's
version-aligned staging behavior established by feature B, so C depends on B.

## Decomposition Rationale

The objective decomposes into three child features, each independently mergeable with
its own issue, feature folder, and PR:

| feature_folder | wave | complexity | type | rationale |
| --- | --- | --- | --- | --- |
| message-to-event-linkage | 0 | C3 | feature | New cross-module wire contract (MailBridge RPC + HostAdapter route + Core consumer); `cross_module_contract_change` floor-forces C3. |
| installer-image-version-alignment | 0 | C2 | bug | Localized installer staging/validation fix; no floor-forcing signal; assessed C2. |
| admin-access-automation | 1 | C3 | feature | Token delivery and device-token rotation are token-handling paths; `auth_or_token_handling` floor-forces C3. |

### Dependency Graph and Waves

`depends_on` records only real upstream/downstream contracts. The single edge is
`admin-access-automation -> installer-image-version-alignment` (C consumes B's
version-aligned staging). The graph is cycle-free.

Wave assignment by longest-path layering (`wave(f) = 0` if `depends_on` empty, else
`1 + max(wave(d))`):

- **Wave 0:** message-to-event-linkage, installer-image-version-alignment
- **Wave 1:** admin-access-automation

## Execution Note

This manifest was authored by `epic-planner`. Every child feature is prepared to
preflight clearance on the integration branch `epic/openclaw-runtime-remediation-integration`.
Execution has not started and begins only when the user runs
`/epic-run openclaw-runtime-remediation` or replays the committed kickoff prompt.
