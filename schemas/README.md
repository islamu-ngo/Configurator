# Configurator JSON Schemas

This directory contains generated structural JSON Schemas for definition authoring, runtime documents, and machine CLI envelopes. Core admission remains the authority for semantic and resource checks; it does not become a general JSON Schema validator merely because these editor schemas exist.

---

## Schema Overview

| Schema File | Purpose | Primary Consumer | Authoring Format |
|---|---|---|---|
| [`configurator-authoring-v1.schema.json`](configurator-authoring-v1.schema.json) | Validates compact maintainer source definitions (`configurator.yaml` or `configurator.json`). | Tool authors, IDE validation, compiler preflight. | YAML / JSON |
| [`setup-definition-v1.schema.json`](setup-definition-v1.schema.json) | Validates compiled, canonical runtime definitions distributed to clients. | `Configurator.Core`, definition store, runtime clients. | Canonical JSON |
| [`islamu-configurator-command-v2.schema.json`](islamu-configurator-command-v2.schema.json) | Defines the structured machine envelope emitted by CLI commands when `--machine` is invoked. | CI/CD pipelines, automated scripts, AI coding agents. | JSON |
| [`definition-store-curation-v1.schema.json`](definition-store-curation-v1.schema.json) | Validates catalog metadata and project curation records (`curation.json`) for the Definition Store. | Store builders, catalog contributors. | JSON |

---

## Setting Up IDE Schema Validation

### 1. Visual Studio Code

To associate the structural schemas with your YAML and JSON files, add the following mapping to your workspace `.vscode/settings.json`. Adjust paths to your local schema copies. Editor validation is not a substitute for `definition check`: references, inherited sensitivity, cycles, and resource limits also require semantic admission.

```json
{
  "yaml.schemas": {
    "./schemas/configurator-authoring-v1.schema.json": [
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
      "url": "./schemas/configurator-authoring-v1.schema.json"
    },
    {
      "fileMatch": [
        "**/setup-definition.json",
        "**/*-definition.json"
      ],
      "url": "./schemas/setup-definition-v1.schema.json"
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
$schema: urn:islamu:configurator:authoring:v1
identity:
  familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
  revision: 1
project:
  id: my-service
  name: My Application
  publisher: org-name
# ... fields and outputs
```

Compiled runtime definitions instead use `schemaVersion`. Do not add `$schema` to them: the runtime contract is closed, and combining `$schema` with `schemaVersion` rejects. Associate the runtime schema through editor settings. Runtime identity fragment, not a complete definition:
```json
{
  "schemaVersion": 1,
  "familyId": "69cdb122-41d2-4fab-9b82-7929a889c726",
  "revision": 1
}
```

The schemas in this directory describe Configurator definition documents. An application's own JSON Schema is a different input: it can scaffold a compact source only through the bounded [JSON Schema importer](../docs/public/definition-reference.md#json-schema-import). Generated schema validity does not establish target-application acceptance.

---

## Schema Regeneration & Freshness

The schema files in this directory are generated and verified automatically by build tools located under `eng/configurator/`:
- `Configurator.DefinitionSchemaGenerator`
- `Configurator.CommandSchemaGenerator`

> [!IMPORTANT]
> Never hand-edit these generated `.schema.json` files. If an underlying model or contract changes, update the owning C# records in `src/Configurator.Core` or `src/Configurator.Cli` and run the generator projects to update the schemas.
