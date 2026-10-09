# Event-owned definition reference

These files are generated data from [ISLAMU Event](https://github.com/ISLAMU/Event),
not compiled target code or authority to apply configuration. The stable family
GUID is `a4bcfaa6-9979-43e5-b1f5-00d552618cdf`, recorded by Event in
`configurator/definitions/islamu-event/inputs/profile.json`.

`setup-definition.json` (also exported as `definition.json` for existing example
consumers) is admitted by Configurator Core's bounded parser. `answers/` and
`expected/` carry environment, manifest, tenant package, operator-identity and
legal-draft vectors. Configurator's actual CLI composes these answer workspaces,
and Event's host parsers verify the produced bytes. The Event exporter has no
Configurator assembly dependency. `catalogue.json` is value-safe environment metadata and
`.env.example` is the target's curated template. `release.json` records native
and public digests, revision, source provenance and host-acceptance limits.

`public-definition.json` contains the exact same safe metadata bytes as the native definition,
installed as `src/Configurator.Browser/Definitions/islamu-event.public.json`.
Use `/launch/a4bcfaa6-9979-43e5-b1f5-00d552618cdf` on your deployed Configurator
origin for the bundled definition, or use `/open#` followed by the generated
`browserLaunchFragment` in `release.json` for a pinned upstream source. Native
and browser digest selectors identify the same revision and content. Revision 5
has canonical digest
`6cd8341015d119501b6ffdca358bef6e6cc74363a5cd85ab6dadc7110a48155f`.
The complete browser workspace accepts typed memory-only answers, with explicit
consent for secret entry and draft output for deferred secrets.
No supplied answers, private defaults or private literals are embedded.

Regenerate in the sibling Event checkout:

```bash
dotnet run --project eng/configurator/DefinitionExporter/DefinitionExporter.csproj \
  --configuration Release -- --write ../Configurator
dotnet run --project eng/configurator/DefinitionExporter/DefinitionExporter.csproj \
  --configuration Release -- --check ../Configurator
```

`configuration-manifest.tenants` is a bounded object list. Add/remove tenant
rows locally without an exporter invocation; each row contains typed metadata,
display name, portable settings and declared documents. `uniqueBy` binds
`metadata/name` with ordinal comparison. Event's ceiling is 256 rows and shared
engine budgets also apply. The one-, two- and three-tenant vectors under
`answers/` and `expected/` are independent Event-owned data; all compose through
the `configuration-manifest` artifact ID. Removed rows are absent from output.

To prepare a different document vocabulary or nullability, Event's exporter accepts
`--export INPUT_DIRECTORY NEW_OUTPUT_DIRECTORY`; the profile supplies a new
revision when that changes the definition shape. It validates every existing
identity before writing any generated answers or receipt. Index expansion is
not wildcard authority. Identity content hashes and revision preconditions
remain host-owned; `--prepare-identity DOCUMENT NEW_MANIFEST` uses Event's
wire codec, rather than teaching the generic engine a target-specific hash.
Legal editing produces drafts requiring host review, never approval or live
publication. Event retains semantic acceptance and apply authority.
