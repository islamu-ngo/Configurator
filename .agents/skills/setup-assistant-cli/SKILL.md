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
- The machine schema identity is `islamu-configurator-command/v1`.
- Resolve grammar from the current executable and generated command schema; do not invent flags from historical Event commands.
- Definitions are local bounded data. Preparation never authenticates to a target, provisions it or certifies host acceptance.
- Native answer-derived artifacts are restricted. Never send private output to stdout, public previews or ordinary file writes.
- Argument binding failure emits one value-safe machine result and the documented usage exit code, not framework diagnostic content.

## Verification

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- --help
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release
```

Run only when the CLI owner has made the product ready. Tooling-only work does not run this product suite. See [PROJECTS.md](../../../PROJECTS.md) for ownership and readiness.
