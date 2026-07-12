# admin-access-automation (Issue #148)

- Date captured: 2026-07-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/admin-access-automation/ (Issue #148)
- Epic: openclaw-runtime-remediation (child C, wave 1)
- Depends on: installer-image-version-alignment (child B)

- Issue: #148
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/148
- Last Updated: 2026-07-11
- Work Mode: full-feature

## Problem / Why

Provisioning gateway admin access after an OpenClaw install currently requires
undocumented manual steps. The 2026-07-10 runtime diagnosis found that an operator
must hand-deliver the gateway token to the Control UI, has no supported path to
rotate or reissue the device token, and must configure the `web_search` provider by
hand. These gaps break a clean, repeatable end-to-end admin-access flow and leave
human-held-secret steps uncaptured.

Token generation itself is already solved: `scripts/Invoke-OpenClawAgentOnboarding.ps1`
generates the gateway token (`OPENCLAW_GATEWAY_TOKEN`) into the target `.env`. This
feature provisions delivery, rotation, and provider setup only; it does not generate
tokens.

## Proposed Behavior

Automate gateway admin access with three capabilities plus a committed runbook:

1. **Gateway-token delivery to the Control UI.** Deliver the gateway token to the
   Control UI via the `#token=` URL fragment so an operator does not paste it by hand.
2. **Device-token rotation/reissue.** Rotate or reissue the HostAdapter device token
   (`/run/openclaw/hostadapter.token`, used for `Authorization: Bearer <token>`).
3. **`web_search` provider provisioning.** Provision the `web_search` provider in the
   OpenClaw agent configuration.
4. **Human-held-secret runbook.** Author a committed runbook for the steps that cannot
   be automated because they require a human-held secret.

Token delivery and device-token rotation are token-handling / security-sensitive
paths and must be treated with the corresponding rigor (T1 token handling).

## Acceptance Criteria (early draft)

- [ ] Gateway token is delivered to the Control UI via the `#token=` URL fragment.
- [ ] Device token can be rotated/reissued through an automated, idempotent path.
- [ ] `web_search` provider is provisioned in the OpenClaw agent configuration.
- [ ] A committed runbook covers the human-held-secret steps that cannot be automated.
- [ ] Token generation is out of scope (reuses the existing onboarding script).

## Constraints & Risks

- PowerShell 7+; repository PowerShell rules and quality-tier gates apply.
- Security-sensitive token paths: no plaintext secret logging, no hard-coded tokens.
- Depends on installer-image-version-alignment (child B): admin-access automation runs
  against the version-aligned staging behavior B establishes, with matched Control UI
  and gateway container images. Where scope touches the installer staging path or
  container image versions, cite that B provides matched images.
- Idempotency: delivery, rotation, and provisioning must be safe to re-run.

## Test Conditions to Consider

- [ ] Unit coverage for token-delivery fragment construction and device-token rotation.
- [ ] Negative/error paths (missing token, unreadable token file, invalid provider config).
- [ ] Idempotency of rotation and provider provisioning.
- [ ] `web_search` provider config produces a valid agent configuration.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/admin-access-automation/` folder from the template
