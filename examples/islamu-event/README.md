# Event-owned definition reference

These files are generated data from [ISLAMU Event](https://github.com/ISLAMU/Event),
not compiled target code or authority to apply configuration. The stable family
GUID is `a4bcfaa6-9979-43e5-b1f5-00d552618cdf`, recorded by Event in
`configurator/definitions/islamu-event/inputs/profile.json`.

`configurator.json` is Event's generated neutral authoring source. A separately
installed, explicitly pinned Configurator compiler produces the sole canonical
`definition.json`, admitted by Configurator Core's bounded parser. `answers/` and
`expected/` carry environment, manifest, tenant package, operator-identity and
legal-draft vectors. Configurator's actual CLI composes these answer workspaces,
and Event's host parsers verify the produced bytes. The Event exporter has no
Configurator assembly dependency. `catalogue.json` is value-safe environment metadata and
`.env.example` is the target's curated template. `release.json` records the command,
authoring and runtime contract versions, executable SHA-256, exact source and
canonical definition digests, revision and host-acceptance limits. These public
reproduction facts supply no signing, promotion or authentication authority.

The browser embeds `definition.json` directly through a resource link; there are
no separately maintained native/public copies.
Use `/launch/a4bcfaa6-9979-43e5-b1f5-00d552618cdf` on your deployed Configurator
origin for the bundled definition, or use `/open#` followed by the generated
`browserLaunchFragment` in `release.json` for a pinned upstream source. Native
and browser digest selectors identify the same revision and content. Revision 6
uses semantic field paths instead of traversal numbers. See the checked receipt
for the exact digests and compiler pin; earlier store snapshots retain their bytes.
The complete browser workspace accepts typed memory-only answers, with explicit
consent for secret entry and draft output for deferred secrets.
No supplied answers, private defaults or private literals are embedded.

Regenerate in the sibling Event checkout:

```bash
dotnet run --project eng/configurator/DefinitionExporter/DefinitionExporter.csproj \
  --configuration Release -- --export-source configurator/definitions/islamu-event/inputs NEW_SOURCE_DIRECTORY
dotnet run --project eng/configurator/DefinitionExporter/DefinitionExporter.csproj \
  --configuration Release -- --compile-local --compiler "$CONFIGURATOR_COMPILER" --compiler-sha256 "$CONFIGURATOR_COMPILER_SHA256" --check
```

`configuration-manifest.tenants` is a bounded object list. Add/remove tenant
rows locally without an exporter invocation; each row contains typed metadata,
display name, portable settings and declared documents. `uniqueBy` binds
`metadata/name` with ordinal comparison. Event's ceiling is 256 rows and shared
engine budgets also apply. The one-, two- and three-tenant vectors under
`answers/` and `expected/` are independent Event-owned data; all compose through
the `configuration-manifest` artifact ID. Removed rows are absent from output.

To prepare a different document vocabulary or nullability, Event's exporter accepts
`--export-source INPUT_DIRECTORY NEW_OUTPUT_DIRECTORY`; the profile supplies a new
revision when that changes the definition shape. It validates every existing
identity before writing any generated answers or receipt. Index expansion is
not wildcard authority. Identity content hashes and revision preconditions
remain host-owned; `--prepare-identity DOCUMENT NEW_MANIFEST` uses Event's
wire codec, rather than teaching the generic engine a target-specific hash.
Legal editing produces drafts requiring host review, never approval or live
publication. Event retains semantic acceptance and apply authority.

Event's ordinary CI verifies source freshness and independent host vectors with
`--check-local` and `--check-environment`, without a compiler or sibling checkout.
Only its positively selected explicit compiler lane proves canonical compilation
freshness. Local verification uses the actual standalone package. The separate
remote workflow requires an authorized public Linux executable asset of
`islamu-ngo/Configurator`; its availability and remote result are separate evidence.
