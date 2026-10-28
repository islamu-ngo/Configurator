# Event-owned definition reference

These public fixtures derive from first-party
[ISLAMU Event](https://github.com/ISLAMU/Event) configuration metadata, not
compiled target code or authority to apply configuration. The reauthored v2
source uses fresh family `c0d02619-0134-411b-b495-ef302f592669`, revision 1,
allocated through Configurator's native identity editor. The prior Event family
and its publication facts are historical, not authority for this new family.

`configurator.json` is the reauthored neutral v2 source, preserving 427 declared
root settings and five output mappings. Configurator's compiler produces the sole canonical
`definition.json`, admitted by Configurator Core's bounded parser. `answers/` and
`expected/` carry environment, manifest, tenant package, operator-identity and
legal-draft vectors. Configurator's actual CLI composes these answer workspaces,
against independent expected bytes. Prior Event host-parser evidence does not
certify this v2 reauthoring or its new family. The Event exporter has no
Configurator assembly dependency. `catalogue.json` is value-safe environment metadata and
`.env.example` is the target's curated template. `release.json` retains the
**historical v1 receipt**, including its old command,
authoring and runtime contract versions, executable SHA-256, exact source and
canonical definition digests, revision and host-acceptance limits. These public
reproduction facts supply no signing, promotion or authentication authority.
It is not a current compiler pin, digest, launch fragment or v2 acceptance
receipt. It remains unchanged rather than fabricating a new upstream release.

The browser embeds `definition.json` directly through a resource link; there are
no separately maintained native/public copies.
Use `/launch/c0d02619-0134-411b-b495-ef302f592669` on a Configurator origin
built with this fixture. Its canonical runtime digest is
`234b97da3612024910846742c0e2cf7c8d3837e794c805eb445582c025fa3f21`.
The historical receipt's upstream URL does not serve this new family.
Earlier store snapshots retain their bytes and are not silently revised.
The complete browser workspace accepts typed memory-only answers, with explicit
consent for secret entry and draft output for deferred secrets.
No supplied answers, private defaults or private literals are embedded.

Regenerate the local v2 runtime from the repository root:

```bash
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --write-examples
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --check-examples
```

The synthetic answer fixtures use the private version 1 answer envelope.
Regeneration does not contact Event or establish target acceptance.

## Historical Event exporter workflow

The original upstream v1 source was produced in the sibling Event checkout.
These commands describe that provenance; they do not regenerate the current
v2 source without a corresponding upstream exporter change:

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
