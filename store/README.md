# Public definition store

Anyone can propose a project folder through a pull request. Store inclusion is
reviewed public distribution; it does not authenticate a publisher, certify
compatibility or grant permission to apply configuration. Configurator also
opens local definitions and independent public HTTPS links without this store.

Each folder contains one `configurator.yaml`, `configurator.yml` or
`configurator.json`. Start with the [minimal example](minimal-self-hosted/configurator.yaml).
Its project declares the name, description, public repository and the license
applicable to distributing the definition. The shared authoring compiler derives
runtime fields, outputs, identity and discovery metadata from that source.
Independent maintainers use the same source without joining the store.

The ordinary folder slug is the project ID in lowercase with dots, underscores
and hyphens separated by single hyphens. Optional store-owned `curation.json`
adds tags; a `slug` exception requires a meaningful `slugReason` and must match
the folder. See the generated [curation schema](../schemas/definition-store-curation-v1.schema.json).
Neither curation nor a contributor file can select the history authority.

Submit only material you may distribute under its stated terms. Keep private
answers, credentials, populated secret choices, connection strings and private
explanatory text out of every file. Definitions may declare secret fields for
later local entry. Review includes public-data and rights checks; passing the
builder cannot establish ownership or recognize every private value in prose.
No hooks, install commands, plugins, scripts, images or answer workspaces belong
in the store. Paths and links are bounded and symbolic links are rejected.

Validate from the repository root with SDK 10.0.302:

```bash
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --check
```

`--write` generates the curation schema, static index, canonical snapshots and
missing current `releases/<revision>.json`. Do not hand-edit generated output.
`--check` verifies byte-for-byte freshness and the complete output inventory.
Retain every historical release. Edit the source, revise against the previous
canonical release, then build a new revision; existing family/revision content
cannot be replaced. CI supplies `--baseline <pinned-target-inventory>
--require-baseline` from a separate target-commit checkout, so deleting or
rewriting contribution files cannot erase previously admitted history.

The historical neutral revision 2 and Event revision 5 releases are unchanged
copies of existing Configurator public resources. The neutral copy retains its
original formatting; publication canonicalizes it without changing its digest.
The Event snapshot remains MIT-transferred Configurator material; Event's
application and third-party components retain their own terms.

See the [contribution guide](../CONTRIBUTING.md),
[public store guide](../docs/public/definition-store.md) and
[engineering contract](../docs/internal/DEFINITION_STORE.md).
