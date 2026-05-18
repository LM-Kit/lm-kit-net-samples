# Agent Tool Permission Policies

Interactive console app that demonstrates three `ToolPermissionPolicy` profiles (`safeChat`, `devAssistant`, `readOnlyOps`) on the same agent. Same tool catalog, same prompt; the policy decides what is allowed, denied, or gated.

## What it shows

- `new ToolPermissionPolicy()` fluent builder.
- `.Allow(...)`, `.Deny(...)`, `.AllowCategory(...)`, `.DenyCategory(...)`.
- `.RequireApproval(...)`, `.SetMaxRiskLevel(ToolRiskLevel)`.
- Wildcard patterns: `filesystem_*`, `http_*`, `*write*`, `*delete*`.
- `Agent.CreateBuilder().WithPermissionPolicy(policy)` wiring.
- Three interactive modes from a menu:
  - **Describe**: print every policy (default action, max risk level).
  - **Compare**: run probes (built-in or custom) across every policy side-by-side.
  - **Chat**: pick one policy and chat interactively under that profile.

## Run

```bash
cd console_net/ai-agents/permissions/agent_permissions
dotnet run
```

No command-line arguments. The model loads once at startup. Every built-in tool is registered; the policy decides what the agent can actually call.

## Where this fits

A production agent must not see "you have shell exec, do whatever" as the default. Permission policies are how you ship an agent to enterprises that need an audit trail and a deny list.
