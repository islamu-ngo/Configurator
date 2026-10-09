# Prepare a configuration offline

> **Audience:** Adopters
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-09
> **Source Anchors:** `examples/minimal-self-hosted/`, `src/Configurator.Cli/Commands/ArtifactCommands.cs`, `src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs`

This guide uses a neutral service definition to prepare a `.env` file and JSON
and YAML manifests. Configurator runs locally; no Event checkout, account,
catalogue or running target is needed. Run the commands from the repository root
with .NET SDK 10.0.302 on Linux. See [packaging](packaging.md) to build a
standalone executable instead.

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
  --artifacts environment,configuration,configuration-yaml \
  --input examples/minimal-self-hosted/answers/complete.json --machine
```

The machine result lists three restricted artifact digests, `writeStatus: none`
and `host-validation-required` readiness, with no answer values. A successful
local check does not validate the files with a target application. The declared
filenames are `.env`, `service.json` and
`service.yaml`. The JSON and YAML files express the same example settings in
different formats; a real target chooses which format it accepts.

## 3. Save to a new private directory

On a supported Linux filesystem, create an empty owner-only directory and
publish all three files:

```bash
mkdir -m 700 prepared
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact bundle-create \
  --definition examples/minimal-self-hosted/setup-definition.json \
  --artifacts environment,configuration,configuration-yaml \
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
