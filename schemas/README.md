# Configurator JSON Schemas

This directory contains generated structural JSON Schemas for definition authoring, runtime documents, and machine CLI envelopes. Core admission remains the authority for semantic and resource checks; it does not become a general JSON Schema validator merely because these editor schemas exist.

---

## Schema Overview

| Schema File | Purpose | Primary Consumer | Authoring Format |
|---|---|---|---|
| [`configurator-authoring-v2.schema.json`](configurator-authoring-v2.schema.json) | Describes current compact maintainer source (`configurator.yaml` or `configurator.json`). | Tool authors, IDE validation, compiler preflight. | YAML / JSON |
| [`setup-definition-v2.schema.json`](setup-definition-v2.schema.json) | Describes canonical runtime schemaVersion 2/profile bounded-v2 definitions. | `Configurator.Core`, definition store, runtime clients. | Canonical JSON |
| [`configurator-answers-v1.schema.json`](configurator-answers-v1.schema.json) | Describes the private answerContractVersion 1 envelope with values and explicit omitted pointers. | Local answer tooling; never a public definition/cache payload. | Private JSON |
| [`islamu-configurator-command-v3.schema.json`](islamu-configurator-command-v3.schema.json) | Defines the structured machine envelope emitted by CLI commands when `--machine` is invoked. | CI/CD pipelines, automated scripts, AI coding agents. | JSON |
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

Compact authoring requires the exact `$schema` discriminator below. Its URN identifies the source contract; use the editor association above rather than assuming that the URN supplies a downloadable schema. This fragment omits required fields and outputs:

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

Command results still use envelope v3; successful authoring check/build reports
authoringContractVersion 2/runtimeSchemaVersion 2. Answer v1 and store v1 are
independent contracts, not compatibility fallbacks for definition v1.

The schemas in this directory describe Configurator definition documents. An application's own JSON Schema is a different input: it can scaffold a compact source only through the bounded [JSON Schema importer](../docs/public/definition-reference.md#json-schema-import). Generated schema validity does not establish target-application acceptance.

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

Native checking confirmed source/runtime agreement **3/3** for the current fresh v2
families, recorded in the [integration guide](../docs/public/integrate-your-tool.md#complete-minimal-configuratoryaml-example).
The result is not target acceptance or complete client/package qualification.

Root applicability and root presence-only `if.required`/`then.required` plus
`dependentRequired` are current base support. Broader/nested conditions,
source maps, expanded localization policy and machine v3 are later work.
Recursive typed maps, closed discriminated variants, local refs, exact numeric
assertions, unique items and explicit format assertions are supported. Structural schemas can describe
shapes whose contextual semantics still reject in Core; consult the
[current reference](../docs/public/definition-reference.md), not editor validity
alone. The [public standard](../docs/public/definition-standard.md) remains Draft.
