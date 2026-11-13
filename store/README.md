# Public definition store

Anyone can propose a project folder through a pull request. Store inclusion is
reviewed public distribution; it does not authenticate a publisher, certify
compatibility or grant permission to apply configuration. Configurator also
opens local definitions and independent public HTTPS links without this store.

Each folder contains one `configurator.yaml`, `configurator.yml` or
`configurator.json`. Start with the [minimal example](minimal-self-hosted/configurator.json).
Its project declares the name, description, public repository and the license
applicable to distributing the definition. The shared authoring compiler derives
runtime fields, outputs, identity and discovery metadata from that source.
Independent maintainers use the same source without joining the store.

The ordinary folder slug is the project ID in lowercase with dots, underscores
and hyphens separated by single hyphens. Optional store-owned `curation.json`
adds tags; a `slug` exception requires a meaningful `slugReason` and must match
the folder. See the generated [curation schema](../schemas/definition-store-curation.schema.json).
Neither curation nor a contributor file can select the history authority.

Submit only material you may distribute under its stated terms. Keep private
answers, credentials, populated secret choices, connection strings and private
explanatory text out of every file. Definitions may declare secret fields for
later local entry. Review includes public-data and rights checks; passing the
builder cannot establish ownership or recognize every private value in prose.
No hooks, install commands, plugins, scripts, images or answer workspaces belong
in the store. Paths and links are bounded and symbolic links are rejected.

Validate from the repository root with SDK 10.0.404:

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

## Development baseline

Product and public contract versions remain `0.0.1` throughout development.
The Store catalog and curation discriminator is `schemaVersion: "0.0.1"`;
definitions use the current authoring/runtime contract and stable schema paths.
Only the latest format is supported. Old development snapshots may remain in
ignored local archives, not active Store inputs or compatibility readers.

Content identity is separate from this fixed version. A deliberate fork uses
`definition init --fork-from` with a fresh output source, then `definition build`;
the native identity editor allocates a new family at revision 1. Normal changes
within a family require a new immutable revision. Inspect each maintained
`configurator.json` for its current family instead of copying an old documented
GUID. No existing family/revision is repinned, and local generation does not
establish publication authority.

Event definitions remain MIT-transferred Configurator material. Event's
application and third-party components retain their own terms.

See the [contribution guide](../CONTRIBUTING.md),
[public store guide](../docs/public/definition-store.md) and
[engineering contract](../docs/internal/DEFINITION_STORE.md).
