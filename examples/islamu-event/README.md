# Event-owned definition reference

These public fixtures derive from first-party
[ISLAMU Event](https://github.com/ISLAMU/Event) configuration metadata, not
compiled target code or authority to apply configuration. The reauthored v2
source uses family `c0d02619-0134-411b-b495-ef302f592669`,
allocated through Configurator's native identity editor. The admitted runtime
owns its current revision and canonical digest. The prior Event family
and its publication facts are historical, not authority for this new family.

`configurator.json` is the reauthored neutral v2 source, preserving five output
mappings. Configurator's compiler produces the sole canonical
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
built with this fixture. Obtain the current canonical digest by admitting
`definition.json` rather than reusing a historical pin.
The historical receipt's upstream URL does not serve this new family.
Earlier store snapshots retain their bytes and are not silently revised.
The complete browser workspace accepts typed memory-only answers, with explicit
consent for secret entry and draft output for deferred secrets.
No supplied answers, private defaults or private literals are embedded.

## Choose an offline task

The definition binds its public `/operation` enum to five output choices through
`presentation.outputSelector`. All five artifact IDs and target mappings remain
unchanged:

| Choice | Artifact ID | File |
|---|---|---|
| Environment | `environment` | `event.env` |
| Configuration manifest | `configuration-manifest` | `configuration-manifest.json` |
| Tenant package | `tenant-package` | `tenant-package.json` |
| Operator identity | `operator-identity` | `operator-identity.json` |
| Legal drafts | `legal-drafts` | `legal-drafts.json` |

A fresh workspace starts at Environment through `initialValue`, without storing
a supplied answer. Essentials includes platform settings by initializing
`select.feature.platform-config` to true. This only makes the existing offline
fields applicable; it does not enable authentication, payments, MCP, telemetry
or another target integration, supply addresses or credentials, or grant secret
consent. Public API/site addresses and the authentication provider remain unset
until supplied. Explicit false, another output choice and explicit omissions
take precedence; reset makes initialization eligible again.

Essentials includes the existing required setup secret and its explicit secret-entry
consent path. The [provider contract inventory](provider-contract-inventory.md)
records all 361 baseline environment fields against the reviewed Event revision,
including canonical choice values and target-owned provider requirements.
Dropdowns preserve exact wire values across locale changes. Target parser aliases
are not additional Configurator choices; unknown values fail shared validation.
The inventory is scoped to this baseline, not every field in newer Event builds.
The unsupported `AI_API_KEY` alias is removed; use the target's provider-specific
credential fields. Its audit row remains recorded rather than hiding the removal.

Authentication provider selection controls the Keycloak connection group. Keycloak
requires endpoint, realm, `KEYCLOAK_CLIENT_ID` and `KEYCLOAK_BLAZOR_CLIENT_SECRET`;
the optional `KEYCLOAK_BLAZOR_CLIENT_ID` is a separate API override. Local Keycloak
deployment remains a deliberate hosting choice, including `KEYCLOAK_HTTP_PORT`.
Selecting authentication alone does not activate that local port. Secret-provider selection controls
Infisical requirements. SQLite uses `DATABASE_NAME` for its persisted file path
and excludes server connection credentials. Inactive accepted answers remain in
transient memory for switch-back, but inactive secrets produce neither values nor
completion placeholders in complete or draft output. Revocation clears all secret
branches. Configurator does not verify connectivity or target acceptance.

Legal documents now use bounded document objects containing `jurisdictionAssumptions`
and `localizations` arrays. Each language row owns its required fields. This breaks
the former literal dotted answer keys: recreate old private legal answers using
the new document rows, or use supported target-artifact import after reviewing its
capabilities. There is no legacy answer-shape adapter. Existing target artifact
keys and independent expected bytes remain unchanged; old Store snapshots remain
immutable and must be reopened explicitly if needed.

Sections keep inclusion choices beside their hosting, database, identity,
storage, messaging, optional-service, security and operational settings.
Manifest, tenant, operator and legal tasks have their own sections; additional
platform and control-plane fields are in Advanced. Membership follows the
source's existing capability predicates, not guessed environment-key semantics.
English and Arabic public labels and guidance do not translate technical keys
or answer values. Existing string-valued switches retain their declared types:
no target-contract conversion was proved for this content update.

The optional-store source has the same authored experience in its separate
family `d532b75d-5b62-4276-a03b-2bec28155d3d`. Its prior release snapshots
remain immutable; an old pinned definition is not silently upgraded.

## Revise and regenerate

Changed canonical content requires native revision advancement before runtime
generation. From the repository root, with a current CLI build, create distinct
next-revision source candidates:

```bash
mkdir -p .tmp/browser-ux-simplification
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  definition revise --source examples/islamu-event/configurator.json \
  --against examples/islamu-event/definition.json \
  --output .tmp/browser-ux-simplification/event-example-next.json
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  definition revise --source store/islamu-event/configurator.json \
  --against store/islamu-event/releases/6.json \
  --output .tmp/browser-ux-simplification/event-store-next.json
```

These destinations are create-only. Review both native receipts and candidates,
then adopt each candidate as its corresponding handwritten source. Never
hand-increment identity or change historical snapshots. The store command above
uses the current revision-5 anchor; use the matching immutable anchor when
revising a later release.

Regenerate the local v2 runtime and append the store release through their
native owners:

```bash
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --write-examples
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --check-examples
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/browser-ux-definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/browser-ux-definition-store --check
```

The example generator also visits the minimal and Grafana examples; their
unchanged canonical bytes must remain unchanged. The store builder appends the
new release and generates its catalog; it must not rewrite the prior release.
The synthetic answer fixtures use the private version 1 answer envelope.
The deployment-only environment vector must explicitly exclude platform
settings to keep its original output and readiness: platform inclusion now
exposes the existing required `env.SETUP_SECRET`. Do not invent a setup secret,
weaken its requirement or change the independent expected bytes to mask that
newly applicable work.
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
