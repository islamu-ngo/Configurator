# Prepare a configuration offline

> **Audience:** Adopters
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-16
> **Source Anchors:** `examples/minimal-self-hosted/`, `src/Configurator.Cli/Commands/ArtifactCommands.cs`, `src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs`

This guide prepares three files for a neutral example service: `.env`,
`service.json` and `service.yaml`. It uses checked-in synthetic answers and ends
with a byte-for-byte comparison against the expected files. No account,
catalogue, network service or running target is needed.

## Before you start

- Use a local checkout on Linux with .NET SDK **10.0.302**. Run the commands from
  the repository root. The first `dotnet run` may need network access to restore
  build dependencies; configuration preparation itself is offline.
- Use a filesystem and directory that support [protected native output](native-output.md).
  The save step is unavailable on macOS and Windows; it does not fall back to an
  ordinary write.
- Start without a `prepared` directory. The example deliberately refuses to
  overwrite files from a previous run.

Already have a [standalone package](packaging.md)? Replace
`dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --` in each
command with the path to `islamu-configurator` from that package. The installed
executable does not require the .NET SDK.

For sensitive configuration, prefer a reviewed local Desktop, TUI or CLI build
on a trusted machine. The hosted browser is the least safe interface choice for
high-value secrets because it adds origin/browser trust and lacks native
protected-save guarantees. Its WebAssembly processing keeps answers local;
you can also defer secrets and finish a draft locally. Review the
[browser trust choices and limits](browser.md#choose-a-client-and-trust-level)
before choosing that path. Native protected saving has actual-host evidence on
Linux x64; Arm/musl host qualification remains pending.

## 1. Review the definition and answers

Open the [definition](../../examples/minimal-self-hosted/setup-definition.json)
and [complete synthetic answers](../../examples/minimal-self-hosted/answers/complete.json).
The definition declares the output files and their field mappings. The answers
choose `extended` mode, enable metrics and provide a **synthetic placeholder**
credential. They are safe for reproducing this example, not for operating a
real service. For real use, replace the placeholder in a locally protected
answer file. Do not pass private answers on the command line. Alternatively,
use the [browser](browser.md) with transient local answers: explicitly consent
before entering secrets, or export an incomplete secret-deferred draft and
finish its completion locations locally. Browser downloads do not establish
native protected-save permissions.

## 2. Check all outputs without writing

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact bundle-check \
  --definition examples/minimal-self-hosted/setup-definition.json \
  --artifact environment --artifact configuration --artifact configuration-yaml \
  --input examples/minimal-self-hosted/answers/complete.json --machine
```

The machine result lists three restricted artifact digests, `writeStatus: none`
and `host-validation-required` readiness, with no answer values. A successful
local check does not validate the files with a target application. The declared
filenames are `.env`, `service.json` and
`service.yaml`. The JSON and YAML files express the same example settings in
different formats; a real target chooses which format it accepts.

**Checkpoint:** the command exits with `0`, returns one JSON result and creates no
output files. If it fails, stop here and use the result's diagnostic codes; do
not continue to the save step.

## 3. Save to a new private directory

On a supported Linux filesystem, create an empty owner-only directory and
publish all three files:

```bash
mkdir -m 700 prepared
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact bundle-create \
  --definition examples/minimal-self-hosted/setup-definition.json \
  --artifact environment --artifact configuration --artifact configuration-yaml \
  --input examples/minimal-self-hosted/answers/complete.json \
  --output ./prepared --machine
```

For this example, compare the result with the checked expectations; `cmp`
prints nothing when each file matches:

```bash
cmp prepared/.env examples/minimal-self-hosted/expected/environment.env
cmp prepared/service.json examples/minimal-self-hosted/expected/service.json
cmp prepared/service.yaml examples/minimal-self-hosted/expected/service.yaml
```

**Checkpoint:** the save exits with `0`, each artifact reports
`writeStatus: written`, and all three comparisons succeed. Linux files are
owner-only (`0600`); the directory created above is owner-only (`0700`). Keep
these files restricted when handing them to another tool.

Bundle output is restricted and create-only. An existing destination blocks
publication during preflight; a later race or I/O failure can leave a written
prefix. Check each machine `writeStatus` and the files before retrying, and use
a fresh directory when appropriate. Configurator does not overwrite or roll
back written files. Protected saving on Windows and macOS remains unavailable
pending actual-host proof; there is no ordinary-file fallback. See
[native output](native-output.md) for the save boundary.

## 4. Hand the files to the target

Review the target's own configuration contract and apply or restart process.
This example does not run a target, grant administrative authority, or prove a
target will accept these files. The definition's `restart: process` metadata
describes an expected consequence, not a command issued by Configurator.

For an existing local file, [CLI import guidance](cli.md#complete-usage) explains
the bounded `check-existing` and `prefill` operations. For native UI workflows,
see [Terminal](terminal.md) and [Desktop](desktop.md). Definition authors can
start with the [definition guide](definitions.md).

## If a step fails

| Symptom | What to do |
| --- | --- |
| `mkdir` says `prepared` already exists | Do not delete it blindly. Inspect the previous output and choose a new empty owner-only directory; update the save and comparison paths together. |
| Exit `2` or `3` | Review validation diagnostics or supply missing required answers, then repeat the read-only check. |
| Exit `4` | Review the privacy, revision or host limitation. Building a package for a platform does not make its protected writer available. |
| Exit `64` or `65` | Check command options, file paths and document syntax. [CLI help](cli.md#help-without-operational-inputs) works without answers. |
| Exit `70`, `74` or `130`, or missing/partial JSON | A save may already have written files. Follow [save recovery](native-output.md#recover-before-retrying); do not assume rollback. |

The [CLI reference](cli.md#machine-contract-and-exit-codes) defines every exit
category and explains bounded diagnostics. The [documentation hub](../README.md)
routes to other clients and integration guides.
