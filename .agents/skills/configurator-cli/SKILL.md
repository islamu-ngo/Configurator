---
name: configurator-cli
type: workflow
enforcement: suggest
priority: medium
description: "Use the standalone islamu-configurator machine CLI to inspect local definitions, validate answer workspaces, prepare declared artifacts, compare projections, or diagnose protected output; not for interactive clients or target administration."
---

# Configurator Machine CLI

## Authority

Use the compiled CLI registry and its generated
[`islamu-configurator-command/v3` schema](../../../schemas/islamu-configurator-command-v3.schema.json).
The complete operator contract is [CLI usage](../../../docs/public/cli.md);
the technical anchor is [CLI contract](../../../docs/internal/CLI_CONTRACT.md).
Commands prepare local data. They never contact, authorize or apply to a target.

## Execute

1. Identify the operator-selected local definition and declared artifact ID.
   Do not select an application implicitly or fetch a definition URL.
2. Inspect `--help` and the generated command registry before choosing options.
   Use public `catalogue`, `env`, `artifact`, `definition` or `doctor` routes;
   no legacy aliases or framework `cli`/`export` routes.
3. Keep answer values in bounded input files or explicit stdin. Never put them
   in command arguments, logs or chat output. Ambient environment names/values
   are not enumerated or accepted as answer inputs. Ordinary filenames and IDs
   are not banned for secret-word substrings; never use actual private values.
4. Use `--machine` for one JSON result envelope. Check both the process exit
   code and completion, preparation, targetAcceptance, cleanup, diagnostics,
   coverage/readiness counts and every artifact's writeStatus. `delivery: attempt`
   is not acknowledgment. Local success never means the target accepts it.
5. For publication, select an explicit new destination. All answer-derived
   output is restricted and create-only. Linux owner-only publication is the
   supported boundary; unsupported hosts refuse without a plain-file fallback.

Run from the repository root:

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact validate --definition definition.json --artifact config \
  --input answers.json --machine

dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact create --definition definition.json --artifact config \
  --input answers.json --output configuration.json --machine
```

These names illustrate operator-owned local files and a definition-owned
artifact ID; substitute actual declared selections. `--dry-run` validates and
composes without publication. `--format` asserts the declared format and cannot
change the projection.

Strict Spectre grammar accepts `--option value` and `--option=value`, ordinary
stdin `-` and the `--` terminator. Command/option case is strict; singleton options
cannot repeat. Root, family and leaf `--machine --help` need no operational inputs
and perform no reads. Their typed `help` has `scope`, `commandPath`, `commands`
and `options` (`name`, `requiresValue`, `repeatable`), not human-help text.
Root results identify `cli`/`help`, branches family/`help`, and leaves their actual
route. Help is not a compiler handshake or evidence of successful check/build.

For bundles repeat `--artifact config --artifact environment` (at most 64 IDs,
each at most 128 characters); CSV `--artifacts` is removed without an alias.
Invocations allow 256 tokens, 32,768 UTF-8 bytes including separators and 4,096
characters per argument. The receipt is at most 65,536 UTF-8 bytes including LF.
Known written outputs survive later publication, cleanup or delivery failure;
unknown does not mean not-written. Inspect destinations before retrying a write
after exit 74 or incomplete stdout; no fallback receipt repairs partial delivery.

For omitted diagnostics or coverage, use `--detail diagnostics|coverage` only on
read-only `definition check`, `artifact validate`, `artifact check-existing` or
`artifact bundle-check`. Start at offset 0, limit 1–32; advance by entries actually
returned and supply the returned digest as `--expected-detail-digest` on every
noninitial page (offset at most 2,048). Digest mismatch rejects continuation.
The digest binds a sanitized structural view, never private answer bytes.
Authoring/runtime schemas are v2 and private answers are v1; command v3 is a separate axis.

## Projection And Import Boundaries

`--input` normally contains a JSON answer workspace keyed by field IDs.
To inspect an existing projected artifact, supply its answer-workspace proof
through `--answers`; the engine recomposes and rejects a mismatch. Do not
invent missing answers or assume arbitrary host artifacts are reversible.

Import-semantic artifacts require `artifact import`, an explicit
`--expected-revision` and the declared `--revision-field`. The revision must
survive in the top-level projection. This creates an import-request file, not
an authenticated target request.

`--input -`, `--answers -` or `--baseline -` may consume stdin, but only one per
invocation. Restricted artifact bytes never go to stdout, including dry runs.
Machine stdout remains reserved for the result envelope. Public catalogue
metadata may use `--output -` only in text mode.

Human help goes to stdout; failures and status accompanying public-artifact stdout
go to stderr. No backward-compatibility adapter is required for the removed
ambient/substring filtering. Public authoring custody, discovery/repair, skill
lifecycle and installed adoption evidence remain deferred. The requested .NET 10
Linux glibc/musl x64/arm64, macOS x64/arm64 and Windows 10/11 x64 expansion is not
yet implemented or qualified; Windows 7 is unsupported. Protected saves remain
Linux-only, with no claim of non-Linux writers.

## Validation

Validate this skill's machine-consumed frontmatter:

```bash
dotnet run --file eng/configurator/ValidateConfiguratorSkill.cs -- \
  .agents/skills/configurator-cli/SKILL.md
```

Check schema freshness through the native generator:

```bash
dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj -- --check
```
