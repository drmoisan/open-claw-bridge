# Example Human-Exception Runbook — Grant Tenant-Wide Admin Consent for an Entra Application

This is a self-contained, contract-conformant example runbook per `.claude/skills/human-exception-runbook/SKILL.md`. It demonstrates the required five sections and the dated-citation requirement. It does not reference any other feature folder. The values below (tenant, application name) are illustrative placeholders.

## Cue

Act on this runbook when the orchestrator records an `exception` response for the requirement "tenant-wide admin consent for the Entra application." Admin consent for delegated Microsoft Graph permissions that require administrator approval cannot be granted unattended without a Global-Administrator service principal in CI (declined per the autonomous-execution mandate's scope decisions), so it is resolved as a permitted exception and this runbook is the human follow-up.

## Prerequisites

- An account with the **Global Administrator** or **Privileged Role Administrator** role in the target Microsoft Entra tenant.
- The application's **Application (client) ID** and the tenant's display name.
- Access to the Microsoft Entra admin center (https://entra.microsoft.com).
- The set of delegated permissions the application requests is already declared on the app registration (this runbook grants consent for them; it does not add them).

## Step-by-step Instructions

1. Sign in to the Microsoft Entra admin center at https://entra.microsoft.com with the Global Administrator account.
2. In the left navigation, select **Identity** > **Applications** > **App registrations**.
3. Select **All applications**, then open the application by its Application (client) ID.
4. In the application's left menu, select **API permissions**.
5. Review the listed permissions and confirm the requested delegated Microsoft Graph permissions are present with status "Not granted for <tenant>".
6. Select **Grant admin consent for <tenant>** at the top of the **Configured permissions** list.
7. In the confirmation dialog, select **Yes** to grant tenant-wide admin consent.

## Verification

- After step 7, each affected permission row shows the status **Granted for <tenant>** with a green check mark.
- Re-open **API permissions** and confirm no permission remains in the "Not granted" state.
- Optionally, confirm programmatically that the consent exists by querying the service principal's OAuth2 permission grants with the Microsoft Graph CLI or `az ad`.

## Source and Citation

- Step source (third-party UI navigation, sourced MCP-first): Microsoft Learn — "Grant tenant-wide admin consent to an application." Source URL: https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent — updated_at: 2026-06-01.
- API permissions UI reference (web-second corroboration): Microsoft Learn — "Configure a client application to access a web API." Source URL: https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-configure-app-access-web-apis — updated_at: 2026-06-01.
- Verification reference (CLI corroboration for the optional programmatic check): Microsoft Learn — "az ad app permission" command reference. Source URL: https://learn.microsoft.com/en-us/cli/azure/ad/app/permission — updated_at: 2026-06-01.
