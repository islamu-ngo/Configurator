---
name: configurator-cli
type: workflow
enforcement: suggest
priority: medium
description: "Use the standalone islamu-configurator machine CLI to inspect local definitions, validate answers, explicitly generate or derive local secrets, prepare artifacts, compare configuration, or diagnose protected output; not for public policy authoring, interactive clients or target administration."
---

# Configurator machine CLI

## Authority and input selection

The compiled CLI registry and generated
[command-0.0.1 schema](../../../schemas/configurator-cli.schema.json)
own accepted syntax and machine fields. Use the [operator guide](../../../docs/public/cli.md)
for commands and the [CLI contract](../../../docs/internal/CLI_CONTRACT.md) for
limits, continuation, output delivery and failure behavior.

1. Identify the operator-selected local definition and declared artifact.
   Do not fetch a definition URL or guess a target application.
2. Inspect current root/family/leaf `--help` before choosing options. Typed
   machine help has `scope`, `commandPath`, `commands` and `options`; it needs
   no operational input and is not a compiler handshake.
3. Distinguish private answer-0.0.1 workspaces from ordinary existing configuration
   files. Answers use exact configuration names in `values` and concrete
   pointers in `omitted`, never metadata field IDs.
4. Keep private values out of arguments, logs and chat. Use only explicit bounded
   files or admitted stdin. Do not enumerate ambient environment values.

## Prepare and interpret results

For answer-based validation/preparation, use the current `artifact validate`,
`artifact create`, `artifact bundle-check` or `artifact bundle-create` route.
Repeat `--artifact` for bundle selections; do not invent aliases or CSV options.
Source-run commands below are **templates**, not complete maintained examples:

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  artifact validate --definition definition.json --artifact config \
  --input answers.json --machine

dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  artifact create --definition definition.json --artifact config \
  --input answers.json --output configuration.json --machine
```

Run from the checkout root after the [project prerequisites](../../../PROJECTS.md).
Substitute actual selected filenames and artifact IDs, or use the packaged
executable. Publication needs a new operator-selected destination. The
[quickstart](../../../docs/public/getting-started.md) supplies a runnable synthetic
example and independent byte comparisons.

Read the process exit and 0.0.1 envelope together: completion, preparation,
targetAcceptance, cleanup, diagnostics, coverage/readiness and every artifact's
writeStatus plus installation/verification facts. Empty diagnostics are not a
readiness certificate. `delivery: attempt` is not acknowledgment, and local
success never means the target accepted or authorized the result.

Restricted output never goes to stdout. Machine stdout is one result envelope;
human help and public text output use their declared transports. Unsupported
protected-save hosts refuse rather than write ordinary files. A known written
prefix survives later publication, cleanup or delivery failure; unknown never
means not-written. Retry into a fresh private destination after reviewing the
receipt, not by overwriting earlier output.

## Explicit local secret generation and derivation

Use `secret generate` or `secret derive` only for an operator-authorized local
workspace. Inspect that leaf's current `--help`, the admitted field policy and
concrete field pointer before selecting options. Product and public contracts
remain `0.0.1` during development, including private answers and definition
authoring/runtime. Public policy authoring belongs to the
[integration skill](../configurator-integrate/SKILL.md), not this private flow.

Keep derivation input in the command's bounded file/stdin transport; never place
it in arguments, environment variables, logs or chat. Never display generated
values to verify success. Validate the resulting protected workspace locally and
report only safe codes, completion and artifact write status. Local generation
does not issue provider credentials or prove that a target accepts the result.

A generated-input hash or digest needs both a fresh answer `--output` and a fresh
`--generated-password-output` destination for its original password. Select
distinct destinations before generating. The plaintext sidecar is published
first: sidecar failure must leave the hash unpublished. If the sidecar succeeds
and the answer write fails, preserve the sidecar, inspect the value-free receipt
and choose fresh destinations for a deliberate retry. Do not automatically
overwrite, delete or print the successful prefix. A missing original password
cannot be recovered from its hash. Unsupported protected-save hosts must refuse.

## Compare before deliberate adoption

`artifact compare` assesses a selected ordinary configuration file without
changing a workspace. `artifact prefill` is strict adoption into a new restricted
answer destination; an optional `--answers` supplies an existing workspace.
Neither operation guesses release history, renames or arbitrary inverses.
Use the [import contract](../../../docs/internal/IMPORT_COMPARISON.md) for
supported mappings and client/package qualification status.

Page safe details using the operation's supported `--detail` route. Follow
returned `nextOffset` and digest continuation, not the requested limit or
assumed exact totals. A stopped inventory can expose only a lower bound.
Compare completion, compatibility and autofill eligibility are different facts.

## Verification

For this guidance, check frontmatter and links. For command-schema changes,
run the native generator's `--check` and the affected CLI consumer checks in
PROJECTS. Invoking a command to verify a documentation example is not full CLI,
package, actual-host or target qualification. [STATUS](../../../docs/internal/STATUS.md)
routes those separate evidence layers.
