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
[`islamu-configurator-command/v2` schema](../../../schemas/islamu-configurator-command-v2.schema.json).
The complete operator contract is [CLI usage](../../../docs/public/cli.md);
the technical anchor is [CLI contract](../../../docs/internal/CLI_CONTRACT.md).
Commands prepare local data. They never contact, authorize or apply to a target.

## Execute

1. Identify the operator-selected local definition and declared artifact ID.
   Do not select an application implicitly or fetch a definition URL.
2. Inspect `--help` and the generated command registry before choosing options.
   Use the `catalogue`, `env`, `artifact` or `doctor` families; no legacy aliases.
3. Keep answer values in bounded input files or explicit stdin. Never put them
   in command arguments, ambient environment variables, logs or chat output.
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
Authoring/runtime schemas remain v1; command v2 does not upgrade them.

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
