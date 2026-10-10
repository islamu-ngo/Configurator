# Optional public definition store

Status: Accepted for this implementation. Owner: Browser and definition tooling.

Users need public project discovery without depending on a catalogue to configure
their applications. The initial store lives in this repository by user choice;
moving it into another checkout later must not change runtime admission.

The original separate metadata input is superseded by
[maintainer authoring](ADR-maintainer-authoring-and-canonical-distribution.md).
Each project supplies one `configurator.yaml`, `configurator.yml` or
`configurator.json`, optional `curation.json`, and retained `releases/*.json`.
The native `BuildDefinitionStore.cs` compiles the source, derives public metadata,
and checks immutable history against an independently selected prior inventory
before generating the index and snapshots.
Generated content is included only when `DefinitionStoreSourceRoot` is supplied
to browser publication, before SDK static-asset inventory and worker generation.
Store-free publication removes stale generated inputs and output assets.

`DefinitionStoreCatalog` is a lazy browser discovery owner. Exact same-origin
asset paths and bounded streaming are separate from external public HTTPS source
admission. Explicit revision review compares canonical digest and all declared
identity/selection metadata, then admits the snapshot through the existing
registry. Core, Presentation, native clients, startup and independent Open have
no catalogue dependency. Active editors retain their admitted definition;
navigation requires discard before loading another public source.

Public store assets enter the reviewed app cache only when discovery or revision
review deliberately reads them. Their missing/corrupt bytes never participate
in mandatory core preload/readiness. Optional cache misses permit only the
exact SDK-inventoried same-origin hash, without credentials/referrer/redirects
and before answer custody; offline misses remain contained discovery failures.
Deliberate
retention of an independently imported definition remains separate consent.
Neither cache contains answers or prepared output. Appearance preferences also
remain separate bounded public presentation settings.

This avoids accounts, runtime repository crawling and a hosted catalogue service.
The cost is release-time catalogue freshness and shared repository contribution
governance. Inclusion, URLs, digests and publisher labels cannot authenticate
publishers or certify target compatibility. Maintainers still review rights and
public-data claims; automated validation cannot establish legal ownership.

See [technical store contract](../DEFINITION_STORE.md),
[public guide](../../public/definition-store.md) and
[browser custody](../LAUNCH_CONTRACT.md).
