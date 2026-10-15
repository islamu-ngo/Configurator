---
name: setup-assistant-cli
description: "Load when invoking Configurator standalone machine CLI for offline definition validation and artifact preparation; not for terminal, desktop, browser, Event backend or host administration."
type: workflow
enforcement: suggest
priority: high
---

# Configurator Native CLI

## Rules

- The executable identity is `islamu-configurator`; no `event-setup` alias.
- The machine schema identity is `islamu-configurator-command/v2`.
- Resolve grammar from the current executable and generated command schema; do not invent flags from historical Event commands.
- Definitions are local bounded data. Preparation never authenticates to a target, provisions it or certifies host acceptance.
- Native answer-derived artifacts are restricted. Never send private output to stdout, public previews or ordinary file writes.
- Argument binding failure emits one value-safe machine result and the documented usage exit code, not framework diagnostic content.
- Automate through the single machine JSON result, not human-rendered help or results. Branch on the actual schema's status and diagnostic fields and the process exit code; an empty diagnostic list alone does not prove success.
- Never place passwords, tokens, credentials, private keys, API keys, or connection strings in arguments, input paths, environment names, logs, or retained command output. Do not echo rejected values; bounded diagnostic codes are the failure detail.
- Use only input/output transports admitted by the current command schema. Do not infer stdin, stdout, dry-run flags, aliases, or exit-code meanings from historical Event commands.

## Workflow

1. Confirm the current executable and generated machine contract through the CLI owner's documented help/schema. Resolve command grammar and exit codes before invocation.
2. Inspect only public definition metadata using an admitted read-only operation; never expose native answers in browser or public output.
3. Validate bounded local inputs before artifact preparation. Select only capabilities and providers actually admitted by the definition and command schema.
4. Prepare restricted answer-derived artifacts through the protected native output owner, not stdout or an ordinary file-write fallback. Treat a planning result as unwritten unless the actual contract proves a successful protected write.
5. Read the machine result and process exit code together. Successful local validation or preparation is not host acceptance, authentication, provisioning, or permission to mutate a server.

## Verification

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- --help
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release
```

Run only when the CLI owner has made the product ready. Tooling-only work does not run this product suite. See [PROJECTS.md](../../../PROJECTS.md) for ownership and readiness.
