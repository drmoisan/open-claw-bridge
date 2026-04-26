Please do comprehensive research on how to integrate the strategy and code in this prompt into this repo. Please provide detailed instructions on what parts can be used exactly as is. For parts that need to be adapted, please research the proper adaptation for this repo.

```I still could not read the private `drmoisan/open-claw-bridge` repo tree directly from the connector tool layer, so the bridge-specific names below are grounded in your uploaded API reference. But the current official OpenClaw docs are enough to design the real deployment: run OpenClaw as the actual Gateway service, configure a dedicated `admin-assistant` agent/workspace, and integrate your Windows MailBridge through a locked-down `exec` path. The bridge artifacts you already exposed are real runtime names: `OpenClaw.MailBridge.exe`, `OpenClaw.MailBridge.Client.exe`, and the `openclaw_mailbridge_v1` named pipe. 

The key architectural choice is this: although OpenClaw generally recommends WSL2 on Windows, your current MailBridge is explicitly a Windows named-pipe + Windows executable integration for Outlook. Native Windows is supported for core CLI and Gateway usage, so for **this** assistant, the correct deployment is **OpenClaw natively on Windows**, under the dedicated `openclaw-svc` account, with the MailBridge running in the primary user’s interactive session. That keeps the OpenClaw runtime and the Outlook bridge on the same OS and avoids cross-boundary pipe shims. ([OpenClaw][1]) 

## What the actual deployable system is

Use these three pieces:

1. `OpenClaw` Gateway under `openclaw-svc`.
2. `OpenClaw.MailBridge.exe` under the **primary interactive Windows user**.
3. A dedicated OpenClaw agent named `admin-assistant` with its own workspace, its own tool policy, and one workspace skill that teaches it how to call `OpenClaw.MailBridge.Client.exe`. OpenClaw supports isolated agents via `openclaw agents add`, agent-specific workspaces, per-agent tool policy, and per-agent skill visibility. Skills are first-class in OpenClaw: each skill is a folder containing `SKILL.md`, and per-agent skills live under that agent’s workspace. ([OpenClaw][2])

The important current-state limitation is that your MailBridge API is **read-only**. The documented surface is `get_status`, `list_recent_messages`, `list_recent_meeting_requests`, `get_message`, `list_calendar_window`, and `get_event`, exposed either through the CLI client or directly over the named pipe. There are no documented send/reply/create/update endpoints in the current API reference. So the **actual administrative assistant you can deploy today** is a **triage / scheduling / draft assistant**, not a fully autonomous send-and-reschedule assistant. 

## How to deploy OpenClaw itself

Install OpenClaw using the official installer or from source. The official install flow supports Windows, and the recommended managed-startup path is `openclaw onboard --install-daemon`; on native Windows, that uses a Scheduled Task first and falls back to a Startup-folder login item if task creation is denied. OpenClaw’s config lives at `~/.openclaw/openclaw.json`, and the Gateway will not start in local mode unless `gateway.mode="local"` is set. ([OpenClaw][3])

Use this install path while logged in as `openclaw-svc`:

```powershell
# install the official CLI
& ([scriptblock]::Create((iwr -useb https://openclaw.ai/install.ps1))) -NoOnboard

# then run onboarding + managed startup
openclaw onboard --mode local --install-daemon
```

Or, if you want the true open-source-from-repo path, OpenClaw’s current docs say to clone the public repo, then run `pnpm install`, `pnpm ui:build`, `pnpm build`, and `pnpm link --global`. ([OpenClaw][4])

After install, verify the Gateway with:

```powershell
openclaw --version
openclaw doctor
openclaw gateway status
openclaw logs --follow
```

Those are the official verification commands and the Gateway runbook’s baseline health checks. ([OpenClaw][3])

## The OpenClaw config you should actually use

Start with a dedicated local-gateway config, loopback-only bind, token auth, DM isolation, and a per-agent restricted tool policy. OpenClaw’s current config model supports `gateway.mode`, `gateway.bind`, `gateway.auth`, global and per-agent `tools.*`, and per-agent workspaces. Tool profiles are real: `minimal` exposes only `session_status`, and you can add `exec` back explicitly for one agent. Per-agent tool overrides are supported under `agents.list[].tools`. ([OpenClaw][5])

Use this as the initial `~/.openclaw/openclaw.json` for `openclaw-svc`:

```json5
{
  gateway: {
    mode: "local",
    port: 18789,
    bind: "loopback",
    auth: {
      mode: "token",
      // simplest path: let onboarding generate/store the token
      // stricter path: replace with a SecretRef-backed env token later
    }
  },

  session: {
    dmScope: "per-channel-peer"
  },

  tools: {
    profile: "minimal",
    exec: {
      host: "gateway",
      security: "allowlist",
      ask: "on-miss",
      strictInlineEval: true
    }
  },

  agents: {
    defaults: {
      heartbeat: { every: "0m" }
    },
    list: [
      {
        id: "admin-assistant",
        workspace: "C:/Users/openclaw-svc/.openclaw/workspace-admin-assistant",
        tools: {
          profile: "minimal",
          allow: ["exec"],
          deny: ["group:fs", "group:web", "browser", "process", "cron"]
        },
        skills: ["mailbridge_admin"]
      }
    ]
  }
}
```

Why this shape:

* `gateway.mode: "local"` is required for a local Gateway. ([OpenClaw][6])
* `bind: "loopback"` and token auth match the Gateway’s current network/auth model. Auth is required by default, even on loopback. ([OpenClaw][7])
* `session.dmScope: "per-channel-peer"` is the secure DM pattern when more than one person can message the assistant. ([OpenClaw][8])
* `tools.profile: "minimal"` keeps the default surface tiny. ([OpenClaw][9])
* The `admin-assistant` agent gets only `exec`, because the bridge client is an external executable. Everything else dangerous is denied at tool policy level. OpenClaw explicitly supports per-agent tool overrides. ([OpenClaw][10])

## How to lock down the one command path the agent needs

Because the bridge client is an external executable, the real guardrail is OpenClaw’s exec policy plus host approvals. The exec tool supports `security=allowlist` and `ask=on-miss`, and approvals live in `~/.openclaw/exec-approvals.json`. The approvals CLI can add a per-agent allowlist entry for a specific binary path. ([OpenClaw][11])

So after you install OpenClaw as `openclaw-svc`, add exactly one allowlist entry:

```powershell
openclaw approvals allowlist add --gateway --agent admin-assistant "C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe"
```

That is the crucial step that turns this from “agent with shell access” into “agent allowed to call one local bridge client.” OpenClaw’s own docs explicitly say host exec defaults are permissive unless you tighten both tool policy and host approvals, so do not skip this. ([OpenClaw][11])

## How to deploy the MailBridge in the actual system

The MailBridge side is straightforward from your uploaded API file:

* `OpenClaw.MailBridge.exe` must already be running before calls are made.
* `OpenClaw.MailBridge.Client.exe` is the supported local client.
* The named pipe is `openclaw_mailbridge_v1`.
* The pipe is ACL-restricted to the current interactive user, Administrators, LocalSystem, and `openclaw-svc`.
* The bridge config lives at `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`.
* The bridge has `safe` and `enhanced` modes. In `safe`, sender/body details are redacted. In `enhanced`, they are present. 

That means the actual Windows deployment should be:

* Primary user logs in and starts `OpenClaw.MailBridge.exe` at login.
* `openclaw-svc` runs OpenClaw Gateway.
* The `admin-assistant` agent shells out to `OpenClaw.MailBridge.Client.exe` through the allowlisted exec path.
* The bridge pipe ACL already anticipates the `openclaw-svc` caller. 

Keep the bridge in `safe` mode first. Switch to `enhanced` only after the assistant is behaving correctly, because `enhanced` exposes sender/body/organizer details to the agent. 

## How to create the actual administrative assistant agent

Create the agent with OpenClaw’s real multi-agent command surface:

```powershell
openclaw agents add admin-assistant --workspace C:\Users\openclaw-svc\.openclaw\workspace-admin-assistant --non-interactive
```

That creates a real isolated agent with its own workspace. OpenClaw’s workspace model uses files like `AGENTS.md`, `SOUL.md`, `TOOLS.md`, `IDENTITY.md`, `USER.md`, and optional memory files as the agent’s operating instructions and long-term memory. ([OpenClaw][2])

Create this workspace tree:

```text
C:\Users\openclaw-svc\.openclaw\workspace-admin-assistant\
  AGENTS.md
  SOUL.md
  TOOLS.md
  IDENTITY.md
  USER.md
  skills\
    mailbridge_admin\
      SKILL.md
```

### `IDENTITY.md`

```md
# Identity

Name: Admin Assistant
Role: Administrative assistant for <USER>
Theme: Calm, precise, conservative
```

### `SOUL.md`

```md
You are a conservative administrative assistant.

Your priorities, in order:
1. Avoid false claims.
2. Protect private calendar/email details.
3. Surface urgent items and scheduling conflicts.
4. Draft concise proposed replies and decisions.
5. Never claim you sent an email or changed a calendar event unless a future write plane explicitly confirms success.

If data is redacted or missing, say so plainly.
```

### `TOOLS.md`

```md
Use the local MailBridge client for Outlook data.

Always start with:
- OpenClaw.MailBridge.Client.exe status

Read operations:
- OpenClaw.MailBridge.Client.exe list-messages --since "<UTC ISO-8601>" --limit <n>
- OpenClaw.MailBridge.Client.exe list-meeting-requests --since "<UTC ISO-8601>" --limit <n>
- OpenClaw.MailBridge.Client.exe list-calendar --start "<UTC ISO-8601>" --end "<UTC ISO-8601>" --limit <n>
- OpenClaw.MailBridge.Client.exe get-message --id "<bridgeId>"
- OpenClaw.MailBridge.Client.exe get-event --id "<bridgeId>"

Rules:
- Use UTC timestamps.
- Call status first.
- If bridge state is not ready, stop and report.
- If isRedacted=true or mode=safe, do not infer sender, body, attendees, or organizer details.
- The current MailBridge is read-only. Never claim that email/calendar writes occurred.
```

### `AGENTS.md`

```md
# Administrative Assistant

At session start:
1. Read SOUL.md, USER.md, and TOOLS.md.
2. Check MailBridge status.
3. Pull:
   - meeting requests from the last 7 days
   - recent messages from the last 24 hours
   - calendar events for the next 14 days
4. Expand only the items that matter with get-message / get-event.

Primary jobs:
- triage meeting requests
- summarize urgent inbox items
- identify conflicts and unanswered scheduling items
- propose reply drafts and scheduling recommendations

Use these decision labels:
- IGNORE
- PRIVATE_BUSY_ONLY
- PROTECTED_MEETING
- HUMAN_APPROVAL
- AUTO_COORDINATE

Interpret AUTO_COORDINATE as "safe to recommend a coordination action" only.
Do not interpret it as permission to send or reschedule, because the current bridge does not expose write methods.

Output format:
1. Executive summary
2. Items needing action
3. Proposed drafts / next steps
4. Unknowns / missing data
```

### `skills\mailbridge_admin\SKILL.md`

OpenClaw’s skill system supports exactly this: a skill folder with a `SKILL.md` containing YAML frontmatter plus instructions. ([OpenClaw][12])

```md
---
name: mailbridge_admin
description: Read Outlook inbox, meeting requests, and calendar from the local OpenClaw MailBridge client.
metadata:
  openclaw:
    os: ["windows"]
---

# MailBridge Admin

Use this skill whenever the user asks about inbox, scheduling, calendar conflicts, meeting requests, or drafting an admin response.

## Required workflow

1. Run `OpenClaw.MailBridge.Client.exe status`.
2. If the bridge is not `ready`, stop and report the bridge state.
3. Use list methods first.
4. Use get methods only for items already identified as relevant.
5. Respect redaction:
   - if `isRedacted=true`, explicitly say details are unavailable
   - never fabricate sender/body/attendee details
6. The bridge is read-only:
   - do not claim you replied, sent, created, updated, accepted, declined, or rescheduled anything

## Typical command patterns

- `OpenClaw.MailBridge.Client.exe list-meeting-requests --since "<utc>" --limit 20`
- `OpenClaw.MailBridge.Client.exe list-calendar --start "<utc>" --end "<utc>" --limit 100`
- `OpenClaw.MailBridge.Client.exe list-messages --since "<utc>" --limit 50`
```

Then start a new session or restart the gateway so OpenClaw picks up the skill. The official skills docs say new skills are loaded on a new session, and `openclaw skills list` is the verification command. ([OpenClaw][12])

## How to make the assistant reachable

Start with the Control UI and local CLI, not an external chat channel. OpenClaw serves the Control UI from the local Gateway port, and the current local pattern is loopback + token auth. That gives you a safe place to test the agent before exposing it through WhatsApp/Telegram/Discord. ([OpenClaw][7])

Once the assistant is stable, add a dedicated channel and bind it to `admin-assistant`. OpenClaw’s current personal-assistant guidance recommends a dedicated WhatsApp number and an allowlist, never an open-to-the-world bot. Bindings are first-class: `openclaw agents bind --agent <id> --bind <channel[:accountId]>`. ([OpenClaw][13])

Example:

```powershell
# after adding and logging in the channel account
openclaw agents bind --agent admin-assistant --bind whatsapp:assistant
```

And in config:

```json5
{
  channels: {
    whatsapp: {
      dmPolicy: "allowlist",
      allowFrom: ["+15551234567"]
    }
  }
}
```

If multiple people can message the assistant, keep `session.dmScope: "per-channel-peer"` so they do not share one DM context. OpenClaw explicitly warns about this and its security audit checks for it. ([OpenClaw][8])

## What this assistant actually does on day 1

With the current bridge, the real deployable assistant behavior is:

* check bridge health
* scan recent inbox items
* scan recent meeting requests
* scan calendar windows
* identify conflicts, pending RSVPs, and likely scheduling actions
* produce summaries
* draft suggested replies or proposed reschedule language
* explicitly mark anything that needs a human decision

What it does **not** do yet:

* send mail
* accept/decline meetings
* create events
* move calendar items
* mutate Outlook state

That is not an OpenClaw limitation; it is the current shape of your MailBridge API. 

## The clean “native OpenClaw” next step

The deploy-now path above uses a skill + allowlisted `exec` call to `OpenClaw.MailBridge.Client.exe`. That works today, but it is not the cleanest long-term integration. OpenClaw’s current plugin system supports **plugin-provided typed agent tools**, and per-agent tool policy can expose those tools directly. The better long-term integration is a small OpenClaw plugin that registers typed tools such as `mailbridge_status`, `mailbridge_list_messages`, `mailbridge_list_calendar`, `mailbridge_get_message`, and `mailbridge_get_event`, backed internally by the same MailBridge client or named pipe. ([OpenClaw][10])

That would give you the actual end-state administrative assistant stack:

* official OpenClaw Gateway
* dedicated `admin-assistant` agent
* typed local MailBridge tools
* later, a second approved write plane for reply/create/update with human approval

Right now, though, the **actual deployable assistant** is the read/triage/draft version above, and that is the correct thing to ship first because it matches both the real OpenClaw runtime model and the real MailBridge surface you already have. ([OpenClaw][7]) 

[1]: https://docs.openclaw.ai/platforms/windows "https://docs.openclaw.ai/platforms/windows"
[2]: https://docs.openclaw.ai/cli/agents "https://docs.openclaw.ai/cli/agents"
[3]: https://docs.openclaw.ai/install "https://docs.openclaw.ai/install"
[4]: https://docs.openclaw.ai/install/index "https://docs.openclaw.ai/install/index"
[5]: https://docs.openclaw.ai/gateway/configuration-reference "https://docs.openclaw.ai/gateway/configuration-reference"
[6]: https://docs.openclaw.ai/cli/gateway "https://docs.openclaw.ai/cli/gateway"
[7]: https://docs.openclaw.ai/gateway "https://docs.openclaw.ai/gateway"
[8]: https://docs.openclaw.ai/sessions "https://docs.openclaw.ai/sessions"
[9]: https://docs.openclaw.ai/tools "https://docs.openclaw.ai/tools"
[10]: https://docs.openclaw.ai/plugins/agent-tools "https://docs.openclaw.ai/plugins/agent-tools"
[11]: https://docs.openclaw.ai/tools/exec "https://docs.openclaw.ai/tools/exec"
[12]: https://docs.openclaw.ai/tools/creating-skills "https://docs.openclaw.ai/tools/creating-skills"
[13]: https://docs.openclaw.ai/channels/whatsapp "https://docs.openclaw.ai/channels/whatsapp"

```