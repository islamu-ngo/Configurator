# Configurator JSON Schemas

This directory contains generated structural JSON Schemas for definition authoring, runtime documents, and machine CLI envelopes. Core admission remains the authority for semantic and resource checks; it does not become a general JSON Schema validator merely because these editor schemas exist.

---

## Schema Overview

| Schema File | Purpose | Primary Consumer | Authoring Format |
|---|---|---|---|
| [`configurator-authoring-v2.schema.json`](configurator-authoring-v2.schema.json) | Describes current compact maintainer source (`configurator.yaml` or `configurator.json`). | Tool authors, IDE validation, compiler preflight. | YAML / JSON |
| [`setup-definition-v2.schema.json`](setup-definition-v2.schema.json) | Describes canonical runtime schemaVersion 2/profile bounded-v2 definitions. | `Configurator.Core`, definition store, runtime clients. | Canonical JSON |
| [`configurator-answers-v1.schema.json`](configurator-answers-v1.schema.json) | Describes the private answerContractVersion 1 envelope with values and explicit omitted pointers. | Local answer tooling; never a public definition/cache payload. | Private JSON |
| [`islamu-configurator-command-v4.schema.json`](islamu-configurator-command-v4.schema.json) | Defines machine receipts, independent existing-file assessments and advancing bounded finding pages. | CI/CD pipelines, automated scripts, AI coding agents. | JSON |
| [`definition-store-curation-v1.schema.json`](definition-store-curation-v1.schema.json) | Validates catalog metadata and project curation records (`curation.json`) for the Definition Store. | Store builders, catalog contributors. | JSON |

---

## Setting Up IDE Schema Validation

### 1. Visual Studio Code

To associate the structural schemas with your YAML and JSON files, add the following mapping to your workspace `.vscode/settings.json`. Adjust paths to your local schema copies. Editor validation is not a substitute for `definition check`: references, inherited sensitivity, cycles, and resource limits also require semantic admission.

```json
{
  "yaml.schemas": {
    "./schemas/configurator-authoring-v2.schema.json": [
      "**/configurator.yaml",
      "**/configurator.yml",
      "**/*-configurator.yaml"
    ]
  },
  "json.schemas": [
    {
      "fileMatch": [
        "**/configurator.json"
      ],
      "url": "./schemas/configurator-authoring-v2.schema.json"
    },
    {
      "fileMatch": [
        "**/setup-definition.json",
        "**/*-definition.json"
      ],
      "url": "./schemas/setup-definition-v2.schema.json"
    },
    {
      "fileMatch": [
        "**/curation.json"
      ],
      "url": "./schemas/definition-store-curation-v1.schema.json"
    }
  ]
}
```

> [!TIP]
> Ensure the [YAML extension by Red Hat](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) is installed in VS Code to enable schema validation for `.yaml` files.

---

### 2. Authoring Discriminator vs. Editor Association

Compact authoring requires the exact `$schema` discriminator below. Its URN identifies the source contract; use the editor association above rather than assuming that the URN supplies a downloadable schema. This fragment omits required fields and outputs. Allocate your own family through native `definition init` or `init --fork-from`; the illustrative UUID is not an identity to reuse:

**In YAML (`configurator.yaml`):**
```yaml
$schema: urn:islamu:configurator:authoring:v2
identity:
  familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
  revision: 1
project:
  id: my-service
  name: My Application
  publisher: org-name
# ... configuration and outputs
```

Compiled runtime definitions instead use `schemaVersion`. Do not add `$schema` to them: the runtime contract is closed, and combining `$schema` with `schemaVersion` rejects. Associate the runtime schema through editor settings. Runtime identity fragment, not a complete definition:
```json
{
  "schemaVersion": 2,
  "profile": "bounded-v2",
  "familyId": "69cdb122-41d2-4fab-9b82-7929a889c726",
  "revision": 1
}
```

This fragment also omits required runtime members, including the sorted computed
`requiredCapabilities`. Compile source rather than hand-inventing that set:
runtime admission recomputes and checks it. The configuration tree uses exact
property names, closed objects (`additionalProperties: false`) and recursive
array `items`; typed enums and nullable base types are admitted. References are
explicit root/item scoped RFC6901 pointers, not dotted-key expressions.

Private answers instead contain exactly `answerContractVersion: 1`, object
`values` and distinct concrete `omitted` pointers. Values use exact data names,
not bounded metadata IDs. Standard `default` never inserts answers;
`x-configurator.initialValue` initializes eligible never-supplied locations and
`suggestedValue` requires adoption. Explicit omission suppresses initialization;
reset removes override and suppression. No initialized child creates an absent
container.

Command results use envelope v4; successful authoring check/build reports
authoringContractVersion 2/runtimeSchemaVersion 2. Answer v1 and store v1 are
independent contracts, not compatibility fallbacks for definition v1.
Command v3 is removed without an alias. Assessment inventory retains up to 4,096
findings; only a complete inventory has an exact `total`. Detail pages expose
`returnedCount`, `nextOffset` and `terminal`, so byte-shortened pages still
advance. `artifact compare` reports preparation `not-evaluated` and target
acceptance `unassessed`. See the [CLI contract](../docs/internal/CLI_CONTRACT.md).

The schemas in this directory describe Configurator contracts, not the target application's schema. An application's own public JSON Schema is a different input: it can scaffold a compact source only through the bounded [JSON Schema importer](../docs/public/definition-reference.md#json-schema-import). Its transformation ledger records retained, converted, advisory, discarded and unsupported concepts; numeric-domain/nullable-branch acknowledgment never bypasses unsupported assertions. Defaults/examples are not private initialization and `writeOnly` ancestry remains secret. Generated schema validity does not establish target-application acceptance.

## Portable use and capability boundaries

Copy these public schemas into your own project for editor assistance, or obtain
built-in schema documents with native tooling:

```bash
islamu-configurator definition schema --kind authoring --text
islamu-configurator definition schema --kind runtime --text
islamu-configurator definition schema --kind answers --text
islamu-configurator definition schema --kind command --machine
```

Schema discovery does not inspect private answers or fetch arbitrary references.
There is no Store/GitHub dependency for local authoring, admission or preparation.
Use [portable adoption](../docs/public/integrating.md) and the
[cookbook](../docs/public/definition-cookbook.md) rather than manually deriving
runtime capabilities or copying full raw configurations into documentation.

Objects explicitly declare closure or a typed `additionalProperties` schema;
arrays declare recursive `items`. Closed discriminator branches and bounded
configuration-root local references are supported, not general unions or remote
schema fetch. `format` remains descriptive unless the admitted
`x-configurator.assertFormat: true` opts into hostname, email-address, uri or
http-url lexical checks. Editor acceptance does not prove contextual reference,
privacy, capability or resource admission. Consumers must refuse unsupported
mandatory semantics before answer collection.

CLI, Terminal, Desktop and Browser share Core admission; their actual editor,
custody and package evidence remains separate. Public schemas contain no
answers or executable plugins, and native filename hints confer no path
authority. Browser public caches exclude private answer and artifact payloads.

---

## Schema Regeneration & Freshness

The schema files in this directory are generated and verified automatically by build tools located under `eng/configurator/`:
- `Configurator.DefinitionSchemaGenerator`
- `Configurator.CommandSchemaGenerator`

> [!IMPORTANT]
> Never hand-edit these generated `.schema.json` files. If an underlying model or contract changes, update the owning C# records in `src/Configurator.Core` or `src/Configurator.Cli` and run the generator projects to update the schemas.

From the repository root, the native definition generator supports:

```bash
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --write
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --check
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --write-examples
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --renew-example-families
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --check-examples
```

`--write` generates the authoring-v2, runtime-v2 and answers-v1 schemas and removes
retired authoring/runtime v1 files. `--check` checks those files and retirement
without writing. With no arguments the generator prints the runtime schema.
These commands do not regenerate the independently owned command/store schemas.

`--write-examples` compiles the three maintained sources into their canonical
runtime files. `--renew-example-families` first forks each source through the
native identity editor to a fresh family/revision 1, then writes source/runtime;
it is an intentional example identity replacement, not routine freshness repair.
`--check-examples` compares canonical bytes for minimal-self-hosted,
grafana-13.2.2 and islamu-event without writing.

The [maintained example index](../examples/README.md) names the three pairs.
`--check-examples` reports canonical source/runtime agreement only; this
documentation unit did not execute it or renew identities. Any current pass
claim belongs to parent-owned verification, not this catalog. Agreement is not
target acceptance, final physical/package completion or I-VSD closure.

The source-backed profile includes root/row applicability and local
presence/const/enum guards with `then.required`, optional `else.required` and
`dependentRequired`, not arbitrary conditional subschemas. Recursive typed maps,
closed discriminated variants, bounded local refs, exact numeric assertions,
unique items and opt-in format assertions share the same Core admission.
Definition-only source maps and command v4 diagnostics are implemented;
localization overrides address existing public text and do not change answers.
Structural schemas can still describe shapes whose contextual semantics reject
in Core. Consult the [reference](../docs/public/definition-reference.md),
[standard](../docs/public/definition-standard.md) and
[Core mechanism map](../docs/internal/CORE_DEFINITIONS.md), not editor validity
alone. Target validators and operator apply/restart authority remain outside
schema generation.
