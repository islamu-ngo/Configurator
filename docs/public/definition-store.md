---
description: "Discover explicit public definition revisions or contribute a project without making the store a runtime dependency."
---

# Public definition store

The optional Store lists public configuration definitions bundled with a
Configurator browser package. Choose a project and an explicit definition
revision, then review its declared application versions, entry points, locales
and output filenames before starting. A definition revision identifies the
configuration document; it is separate from the application version it declares.
Publisher labels, repository links and store inclusion are provenance claims,
not publisher authentication or compatibility certification. The target
application still validates and authorizes any eventual application of output.

The Store loads when you visit it before working with answers. If it is missing,
empty or unavailable, Home and Open remain usable. Open accepts a local JSON
or YAML source, compiled runtime JSON or an independent public HTTPS runtime
definition link. The browser keeps answers
transient and asks before discarding an active workspace; changing the store
does not upgrade a definition already admitted into a workspace.

Browser packages may include selected public store content. Opening discovery
or reviewing a revision caches only its public bytes for later offline use.
Offline discovery is available for content already opened; unavailable optional
files do not prevent the app's local workflow from becoming ready.
Retaining a separately opened definition is a separate deliberate choice;
neither public cache contains answers or prepared artifacts. See
[browser guidance](browser.md) for secret-entry consent and deliberate export.

## Propose a project

Anyone may submit a pull request adding an ordinary project folder to
[`store/`](../../store/README.md). Maintain one `configurator.yaml`,
`configurator.yml` or `configurator.json`, declaring a public repository,
description and the license applicable to distributing the definition. The
same source works with the independent compiler described in
[integrate your tool](integrate-your-tool.md); joining the store is optional.
The builder derives canonical identity, names, digests, versions and output
metadata instead of requiring a second handwritten inventory.

Use the existing [neutral source](../../store/minimal-self-hosted/configurator.yaml).
The folder normally follows the project's lowercase ID, replacing dot,
underscore and hyphen separators with single hyphens. Optional store-owned
`curation.json` holds tags or a justified folder-slug exception. It cannot change
the project, license, revision or history authority. Its vocabulary has a
generated [curation schema](../../schemas/definition-store-curation-v1.schema.json).

Submit only public data and material you have permission to distribute. Never
include saved answers, real credentials or populated private defaults. Secret
field declarations are permitted for later local entry. Reviewers verify rights
and public-data suitability; automated validation alone cannot prove ownership
or discover every private fact in text. No account, rating or install hook is
part of this format.

Run these commands from the repository root using its pinned .NET SDK:

```bash
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --check
```

Generation admits every input before writing output and reports value-free
failure codes. Generated schema, index and snapshots are owned by the builder.
`--write` also creates any missing current `releases/<revision>.json` without
overwriting historical files; `--check` requires them to exist. Keep these
generated releases in the contribution. For edits, revise the source against
its previous canonical release and compile a new revision. Never edit published
snapshots or reuse an existing family/revision for changed content.

Pull-request validation checks every retained revision and its project binding
against an inventory generated from a separately checked-out pinned target
commit. The workflow supplies `--baseline <inventory> --require-baseline`;
contributors cannot bypass retention by deleting their release files. Local
validation without that option proves source admission and freshness, while
the trusted baseline proves preservation of previously accepted history.

Store packaging is an explicit option on the browser publishing target. An
ordinary package does not require the source folder. See
[packaging](packaging.md) and the [store source contract](../internal/DEFINITION_STORE.md).
