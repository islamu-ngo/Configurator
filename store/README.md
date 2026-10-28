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

## Definition v2 generation

The pre-release v2 cutover starts fresh families for the two checked examples.
The former v1 authoring sources and neutral revisions 2/3 and Event revisions
5/6 remain historical evidence in Git, not active store inputs. Their old
family/revision identities were not assigned new content. There is no v1
runtime interpreter or automatic answer migration.

`Configurator.DefinitionSchemaGenerator --renew-store-families` performs this
one-time cutover from the qualified v2 examples. It validates the named v1
snapshots before changing files, carries forward public redistribution metadata,
allocates identities through the native identity editor, admits both candidates
before writing, and creates each new revision 1 snapshot without overwriting an
existing release. It rejects a second cutover of an already-v2 source.

The current neutral family is `289f3d1c-21ed-4888-9de3-3897d2d3fc1d`; the
current Event family is `d532b75d-5b62-4276-a03b-2bec28155d3d`. Both start at
revision 1. Subsequent v2 revisions retain their history under the ordinary
rules above. Old pinned v1 inventories are not silently accepted as a promoted
v2 history baseline; local generation does not establish publication authority.

Event definitions remain MIT-transferred Configurator material. Event's
application and third-party components retain their own terms.

See the [contribution guide](../CONTRIBUTING.md),
[public store guide](../docs/public/definition-store.md) and
[engineering contract](../docs/internal/DEFINITION_STORE.md).
