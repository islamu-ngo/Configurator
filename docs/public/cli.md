# Command-line configurator

`islamu-configurator` is an offline preparation tool. It never contacts or applies
changes to a target application. Supply a local admitted definition explicitly;
there is no built-in Event catalogue or old command alias.

```bash
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- --help
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj -- \
  artifact create --definition definition.json --artifact config \
  --input answers.json --output service.json --machine
```

Definitions accept the compact [maintainer source](integrate-your-tool.md) with
`$schema: urn:islamu:configurator:authoring:v1` or compiled `schemaVersion: 1`
runtime data. `.yaml` and `.yml` files use bounded YAML; other extensions use JSON.
Answers are a JSON object whose property names are definition field IDs, for
example `{"service.name":"my-service"}`. Answers are never command-line values.
For ordinary artifact operations, the input is an answer workspace. Generic
projections are not necessarily reversible. Existing host manifests, identity
exports and legal drafts must be represented as declared object/list fields by
their target-owned definition. The target remains responsible for integrity and
full semantic acceptance of its exported/imported formats.

Ordinary `artifact` and `env` operations also accept `--answers PATH`. With that
option, `--input` is an existing projected artifact and `--answers` is its answer
workspace proof. The CLI recomposes using the admitted definition and rejects a
projection mismatch. JSON/YAML compare bounded parsed documents, dotenv compares
parsed key/value entries, and Markdown compares exact bytes. Formatting then
publishes the composer's canonical rendering. YAML proof uses Core's closed
mapping-document normalizer; non-mapping YAML proof is rejected. Such projections
can still be prepared directly from an answer workspace. Without this proof the CLI cannot
infer omitted/transformed answers or claim host acceptance of arbitrary artifacts.

```bash
islamu-configurator artifact open --definition definition.json --artifact config \
  --input service.json --answers answers.json --machine
```

## Complete usage

All commands accept `--machine`, `--text` or `--help`. Machine and text are mutually
exclusive. Every value option occurs at most once. Commands are case sensitive.

| Command | Required options | Optional options |
| --- | --- | --- |
| `catalogue list` | `--definition PATH`, `--output PATH` | `--dry-run` |
| `catalogue show` | `--definition PATH`, `--key ID`, `--output PATH` | `--dry-run` |
| `catalogue describe` | `--definition PATH`, `--key ID`, `--output PATH` | `--dry-run` |
| `env render` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `env validate` | `--definition PATH`, `--artifact ID`, `--input PATH` | `--input-revision NUMBER` |
| `artifact create` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `artifact open` | `--definition PATH`, `--artifact ID`, `--input PATH` | `--input-revision NUMBER` |
| `artifact validate` | `--definition PATH`, `--artifact ID`, `--input PATH` | `--input-revision NUMBER` |
| `artifact format` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `artifact diff` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--baseline PATH` | `--input-revision NUMBER` |
| `artifact coverage` | `--definition PATH`, `--artifact ID`, `--input PATH` | `--input-revision NUMBER` |
| `artifact export` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `artifact import` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH`, `--expected-revision REVISION`, `--revision-field ID` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `artifact check-existing` | `--definition PATH`, `--artifact ID`, `--input PATH` | `--answers PATH`, `--input-revision NUMBER` |
| `artifact prefill` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--answers PATH`, `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `artifact bundle-check` | `--definition PATH`, `--artifacts IDS`, `--input PATH` | `--input-revision NUMBER` |
| `artifact bundle-create` | `--definition PATH`, `--artifacts IDS`, `--input PATH`, `--output DIRECTORY` | `--input-revision NUMBER`, `--dry-run` |
| `artifact preview` | `--definition PATH`, `--artifact ID`, `--input PATH`, `--output PATH` | `--input-revision NUMBER`, `--format FORMAT`, `--dry-run` |
| `doctor` | none | none |
| `definition init` | `--project-id ID`, `--name NAME`, `--application-version VERSION` | `--root DIRECTORY`, `--output PATH`, `--format yaml\|json`, `--from-env-example PATH`, `--from-schema PATH`, `--accept-bounded-semantics` |
| `definition init` (fork) | `--fork-from PATH`, `--output PATH` | `--format yaml\|json` (must match source) |
| `definition check` | none | `--source PATH`, `--root DIRECTORY`, `--against PATH` |
| `definition build` | none | `--source PATH`, `--root DIRECTORY`, `--against PATH`, `--output PATH`, `--id-map PATH` |
| `definition revise` | none | `--source PATH`, `--root DIRECTORY`, `--against PATH` |

`--dry-run` replaces the output requirement. It validates and composes but
publishes no file. `--input-revision` is a nonnegative Int64 workspace revision
(default `0`), distinct from the definition revision and target expected revision.
`--format` asserts the selected artifact's declared format, one of `json`,
`yaml`, `dotenv`, `markdown`; it cannot override a projection's format.
Select another declared artifact ID for another format. `env` requires an
artifact declared as `dotenv`.

### Maintainer definitions

`definition init` creates a public YAML source by default, generates its family
identity once and starts revision 1. JSON offers the same capabilities. The exact
target application version is mandatory; it is independent of the definition
revision. An empty source is a starting point: add the application's real fields
and outputs before distributing it. `--from-env-example` reads only declared keys
into string fields, with conservative sensitivity and no copied defaults or values.
Review types, privacy and output behavior. `--from-schema` uses the
[bounded schema importer](integrate-your-tool.md#reuse-a-public-json-schema).

Without `--source`, check/build/revise discover exactly one `configurator.yaml`,
`configurator.yml` or `configurator.json` in `--root` (default current directory).
Multiple candidates reject. Explicit `--source` selects that file alone. Check is
read-only and compilation does not allocate identities. Build creates canonical
public JSON at `dist/configurator/revisions/<revision>.json` relative to the source
directory, or `--output`; identical existing bytes are accepted, conflicting bytes
reject. Generated output cannot replace a source or use a discovery filename.
`--id-map` optionally creates a public logical-field-key to runtime-ID map.

```bash
islamu-configurator definition init --project-id example.service \
  --name "Example Service" --application-version 1.0.0 --machine
islamu-configurator definition check --machine
islamu-configurator definition build --machine
```

Once a canonical revision exists at the default anchor, check and build reject
changed content under that revision. After editing the source, `definition revise`
compares with that anchor or `--against` and increments once only if canonical
content changed. The anchor must have the same family and current revision;
missing/mismatched anchors and overflow reject. Revision changes preserve source
comments, Unicode, BOM and line endings. Build remains a separate operation.
For a separately located release, use `check --against PATH` or `build --against PATH`
to bind that explicit anchor; an explicitly named missing anchor rejects.
An explicit anchor cannot replace conflicting history already present at the
default recorded revision path, including during `revise`.
`init --fork-from source.yaml --output fork/configurator.yaml` explicitly creates
a new family at revision 1 and leaves the original unchanged.

Source edits use a create-only sibling `.configurator.lock`, a same-directory
temporary file and an immediate original-digest check before replacement. Existing
locks, symlinks and changed originals reject; stale locks are never automatically
removed. External editors can still race the final replacement window. These are
public source operations, separate from the protected answer writer below.

`open` admits an answer workspace and prepares its selected projection without
publishing. `validate` checks field rules and whether the projection can be
rendered. `coverage` reports active provided fields and missing required fields.
`diff` composes candidate and baseline answer workspaces and compares canonical
artifact bytes: the selected artifact ID appears in `coveredKeys` when identical
or `missingKeys` when different. Difference is not a validation failure.
`create`, `format` and `export` publish the definition's canonical rendering.
`preview` uses exactly that rendering and protected output, not terminal display.
These are preparation operations, not target commands.

`import` prepares an import-request artifact, never an API request. The artifact
must declare `applySemantics: "import"`. `--expected-revision` is `absent` or a
lowercase 64-character SHA-256 revision. `--revision-field` must name a declared
string field copied unconditionally into a top-level object member of the selected
projection. It must remain active, provided and valid. The option is injected into
that field; a conflicting input value rejects. Definitions that cannot preserve
this explicit revision reject with `revision-projection-required`; no invented
host wrapper, implicit revision or integrity claim is added.
Import-semantic artifacts cannot be published through `create`, `format`,
`export`, `preview` or `env render`; those routes reject with
`import-command-required` rather than bypassing explicit revision binding.

`check-existing` assesses a local dotenv, JSON or YAML file through a selected
definition projection. `prefill` performs the same check and writes a restricted
JSON answer workspace. Only present top-level output keys with unconditional
`copy` mappings can be read back; unknown, duplicate, conditional, transformed,
inactive, invalid or conflicting values reject the whole import. Optional
`--answers` supplies current answers for conflict detection; without it the
workspace starts empty. `--input-revision` labels that current workspace and a
successful prefill advances it by one. Missing fields remain unanswered, so a
successful check or prefill does not mean the configuration is ready for a host.
These operations are separate from revision-bound `artifact import` and the
`--answers` recomposition proof on ordinary operations.

```bash
islamu-configurator artifact check-existing --definition definition.json \
  --artifact config --input service.json --machine
islamu-configurator artifact prefill --definition definition.json \
  --artifact config --input service.json --output prefilled-answers.json --machine
```

`bundle-check` composes multiple declared artifacts from one answer workspace
without writing them. `bundle-create` prepares all selected destinations in the
output directory before writing any, then creates them in definition order.
Pass artifact IDs as a comma-separated `--artifacts` value, for example
`config,environment`. Existing files and unsupported protected-save hosts block
the write. A later race or I/O failure may leave an earlier file written; the
machine result reports each artifact as `written`, `conflict`, `blocked`,
`failed` or `not-attempted` and exits unsuccessfully. Inspect those statuses
before retrying. Neither command accepts an artifact with import semantics;
use revision-bound `artifact import` for that artifact. `--dry-run` checks
composition only and does not reserve or inspect destinations.

```bash
islamu-configurator artifact bundle-check --definition definition.json \
  --artifacts config,environment --input answers.json --machine
islamu-configurator artifact bundle-create --definition definition.json \
  --artifacts config,environment --input answers.json --output ./prepared --machine
```

Catalogue output is structural metadata only: ID, type, sensitivity, required
flag, disclosure, and (for `describe`) presence of activation, required-condition,
default and recommendation metadata. It excludes answer values, titles, help,
defaults and choices. `doctor` checks native protected-writer availability and
the compiled machine-schema authority; it does not certify filesystem targets
or a host application.

## Input, output and confidentiality

`--definition` must be a local file, never stdin or a URL fetch. `--input -`,
`--baseline -` and `--answers -` explicitly read stdin; only one may consume stdin
in an invocation.
Answer output is create-only. On Linux, native publication proves owner-only file
protection; unsupported platforms or unsafe targets fail closed. There is no
plain file fallback. An existing output remains unchanged.

All answer-derived bytes are **restricted**, even when every field is declared
public. They never reach stdout. `--output -` is allowed only for public catalogue
metadata in text mode; human status goes to stderr in that case. In machine mode
stdout is reserved for one result envelope. Dry runs cannot authorize restricted
stdout. Diagnostics contain fixed codes and safe paths, not input values, raw
exceptions or filesystem paths. Arguments and ambient environment names that
look like secret-bearing process surfaces are rejected without reading their values.
File inputs retain the 4 MiB byte limit and depth 32.

## Machine contract and exit codes

`--machine` emits exactly one UTF-8 JSON object, one trailing LF, no BOM or ANSI,
and no additional framework diagnostics. `schemaVersion` is
`islamu-configurator-command/v1`. The authoritative schema is generated from the
compiled registry by `Configurator.CommandSchemaGenerator`; it is available at
`schemas/islamu-configurator-command-v1.schema.json`.

The closed envelope contains `schemaVersion`, `invocation` (commandFamily,
operation, mode), `status`, `exitCategory`, `exitCode`, `dryRun`, `diagnostics`,
`artifacts`, `coverage`, `readiness`. Diagnostic objects contain code/path/severity.
Artifact objects contain kind/mediaType/digest/sensitivity/coverage/readiness/
pathIntent/writeStatus. No artifact body is included. Kinds are `catalogue`,
`definition-artifact`, `answer-snapshot`, `doctor-report`, `authoring-source`,
`compiled-definition`, `logical-id-map`; sensitivities are `public`, `restricted`.
Successful `definition check` and `definition build` additionally report
`authoringContractVersion: 1` and `runtimeSchemaVersion: 1`. A downstream compiler
handoff must verify those versions, the envelope version, successful exit and the
compiled file's digest before installation. The chosen output path is supplied by
the caller and is not echoed in the machine envelope.
Bundle results use one `definition-artifact` entry per selected output, each
with a value-free `writeStatus`. A successful local check does not claim the
target application accepted the configuration.
Coverage lists are unique and ordinally sorted. Successful local preparation
reports `host-validation-required` readiness, not host acceptance.

| Exit | Category | Meaning |
| --- | --- | --- |
| 0 | success | Local operation completed; host acceptance still required |
| 2 | validation | Declared field/projection validation failed |
| 3 | incomplete | Required answer values missing |
| 4 | blocked | Privacy, revision or unsupported protected-host boundary |
| 64 | usage | Invalid arguments, missing options or incompatible format |
| 65 | data | Malformed definition/answer document or admission failure |
| 70 | internal | Unexpected invariant/framework failure |
| 74 | io | Bounded read, protected publication or output-stream failure |

Old `manifest`, `tenant-package`, `portability`, `legal`, `catalog` and `tui`
families are not aliases. Event workflows use Event-owned definitions and
artifact IDs; Event's backend still owns acceptance and authorization.
