---
name: setup-assistant-cli
description: "Reference-only historical machine-v3 setup-assistant workflow. Use configurator-cli for current Configurator commands."
type: workflow
enforcement: suggest
priority: high
---

# Configurator Native CLI

> **Status:** Reference-only historical command-v3 guidance
> **Owner:** Agent Context

This retained workflow predates the current machine contract and adoption
implementation. Use [Configurator CLI](../configurator-cli/SKILL.md) for active
instructions. Older version, field-identity, package and deferred-work statements
below are historical, not current prerequisites or support claims.

## Rules

- The executable identity is `islamu-configurator`; no `event-setup` alias.
- The machine schema identity is `islamu-configurator-command/v3`.
- Resolve grammar from the current executable and generated command schema; do not invent flags from historical Event commands.
- Definitions are local bounded data. Preparation never authenticates to a target, provisions it or certifies host acceptance.
- Native answer-derived artifacts are restricted. Never send private output to stdout, public previews or ordinary file writes.
- Argument binding failure emits one value-safe machine result and the documented usage exit code, not framework diagnostic content.
- Automate through the single machine JSON result, not human-rendered help or results. Branch on the actual schema's status and diagnostic fields and the process exit code; an empty diagnostic list alone does not prove success.
- Keep actual private values out of arguments, filenames, IDs, logs and retained command output. Use explicit bounded files or redirected stdin for answers; ambient environment names/values are neither enumerated nor accepted as inputs. Ordinary filename/ID secret-word substrings are not prohibited. Do not echo rejected values; bounded diagnostic codes are the failure detail.
- Use only input/output transports admitted by the current command schema. Do not infer stdin, stdout, dry-run flags, aliases, or exit-code meanings from historical Event commands.
- Spectre owns strict case-sensitive grammar, including `--option=value`, ordinary stdin `-`, `--` and repeated bundle `--artifact`; singleton options cannot repeat. No framework `cli`/`export` routes are exposed.
- Root, family and leaf `--machine --help` need no operational inputs or reads. Consume typed `help.scope`, `commandPath`, `commands` and `options` (`name`, `requiresValue`, `repeatable`); routes are root `cli`/`help`, branch family/`help`, or the actual leaf route. Help supplies no compiler handshake.
- Human help uses standard Spectre stdout; human failures use stderr, as does status accompanying public-artifact stdout. Machine results never include human help formatting.

## Workflow

1. Confirm the current executable and generated machine contract through the CLI owner's documented help/schema. Resolve command grammar and exit codes before invocation.
2. Inspect only public definition metadata using an admitted read-only operation; never expose native answers in browser or public output.
3. Validate bounded local inputs before artifact preparation. Select only capabilities and providers actually admitted by the definition and command schema.
4. Prepare restricted answer-derived artifacts through the protected native output owner, not stdout or an ordinary file-write fallback. Treat a planning result as unwritten unless the actual contract proves a successful protected write.
5. Read the machine result and process exit code together. Successful local validation or preparation is not host acceptance, authentication, provisioning, or permission to mutate a server.

Removing ambient/substring filtering requires no backward-compatibility adapter.
Public authoring custody, discovery/repair, portable skill lifecycle and installed
adoption evidence remain deferred. The requested .NET 10 matrix (Linux glibc/musl
x64/arm64, macOS x64/arm64, Windows 10/11 x64) is not yet implemented or qualified;
Windows 7 is unsupported. Protected-save qualification remains Linux-only, not
evidence of non-Linux writers.

## Verification

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- --help
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release
```

Run only when the CLI owner has made the product ready. Tooling-only work does not run this product suite. See [PROJECTS.md](../../../PROJECTS.md) for ownership and readiness.
