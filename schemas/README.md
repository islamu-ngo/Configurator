# Configurator JSON Schemas

This directory contains the authoritative JSON Schema definitions used for authoring definitions, validating runtime specifications, and structuring machine CLI envelopes.

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

To get real-time autocompletion, field descriptions, and validation while authoring `configurator.yaml` or `.json` files, add the following mapping to your workspace `.vscode/settings.json`:

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

### 2. Explicit `$schema` Declaration

You can also bind the schema directly inside your definition file using the `$schema` property:

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

**In JSON (`setup-definition.json`):**
```json
{
  "$schema": "../../schemas/setup-definition-v1.schema.json",
  "schemaVersion": 1,
  "familyId": "69cdb122-41d2-4fab-9b82-7929a889c726",
  "revision": 1
}
```

---

## Schema Regeneration & Freshness

The schema files in this directory are generated and verified automatically by build tools located under `eng/configurator/`:
- `Configurator.DefinitionSchemaGenerator`
- `Configurator.CommandSchemaGenerator`

> [!IMPORTANT]
> Never hand-edit these generated `.schema.json` files. If an underlying model or contract changes, update the owning C# records in `src/Configurator.Core` or `src/Configurator.Cli` and run the generator projects to update the schemas.
