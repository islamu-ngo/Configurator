# Event-owned definition reference

These public fixtures derive from first-party
[ISLAMU Event](https://github.com/ISLAMU/Event) configuration metadata, not
compiled target code or authority to apply configuration. The current
source uses family `ea55cccf-dce5-4f88-8ea1-6dd211714edc`,
allocated through Configurator's native identity editor. The admitted runtime
owns its current revision and canonical digest. The prior Event family
and its publication facts are historical, not authority for this new family.

`configurator.json` is the maintained neutral source with declared deployment
recipes, component scopes and recipient-specific environment mappings.
Configurator's compiler produces the sole canonical
`definition.json`, admitted by Configurator Core's bounded parser. `answers/` and
`expected/` carry environment, manifest, tenant package, operator-identity and
legal-draft vectors. Configurator's actual CLI composes these answer workspaces,
against independent expected bytes. Prior Event host-parser evidence does not
certify this reauthoring or its new family. The Event exporter has no
Configurator assembly dependency. `catalogue.json` is value-safe environment metadata and
`.env.example` is the target's curated template. `release.json` retains the
**historical v1 receipt**, including its old command,
authoring and runtime contract versions, executable SHA-256, exact source and
canonical definition digests, revision and host-acceptance limits. These public
reproduction facts supply no signing, promotion or authentication authority.
It is not a current compiler pin, digest, launch fragment or current acceptance
receipt. It remains unchanged rather than fabricating a new upstream release.

The browser embeds `definition.json` directly through a resource link; there are
no separately maintained native/public copies.
Use `/launch/ea55cccf-dce5-4f88-8ea1-6dd211714edc` on a Configurator origin
built with this fixture. Obtain the current canonical digest by admitting
`definition.json` rather than reusing a historical pin.
The historical receipt's upstream URL does not serve this new family.
Earlier store snapshots retain their bytes and are not silently revised.
The complete browser workspace accepts typed memory-only answers, with explicit
consent for secret entry and draft output for deferred secrets.
No supplied answers, private defaults or private literals are embedded.

## Choose an offline task

The definition binds its public `/operation` enum to five output choices through
`presentation.outputSelector`. Environment destinations follow the selected
deployment context; the other four task mappings retain their target bytes.

| Choice | Artifact ID | File |
|---|---|---|
| Standalone process | `environment-standalone` | `event-standalone.env` |
| API process | `environment-api` | `event-api.env` |
| Blazor process | `environment-blazor` | `event-blazor.env` |
| All Compose inputs | `environment-compose` | `event-compose.env` |
| API Compose inputs | `environment-compose-api` | `event-compose-api.env` |
| Blazor Compose inputs | `environment-compose-blazor` | `event-compose-blazor.env` |
| Configuration manifest | `configuration-manifest` | `configuration-manifest.json` |
| Tenant package | `tenant-package` | `tenant-package.json` |
| Operator identity | `operator-identity` | `operator-identity.json` |
| Legal drafts | `legal-drafts` | `legal-drafts.json` |

A fresh workspace starts at Environment through `initialValue`, without storing
a supplied answer.

### Answer shape

Answers are nested objects, not flat dotted keys. Each Environment area is one
top-level object whose members are the target settings, for example
`database.host`, `messaging.smtp.port` or `security.ticketingRecovery.enabled`.
Output bindings still write the target's exact names (`DATABASE_HOST`,
`MAIL_SMTP_PORT`, …), so prepared files are unchanged.

- **Optional areas** (`hosting`, `supportingServices`, `database`, `postgresql`,
  `databaseServer`, `identity`, `localBootstrap`, `storageReconciliation`,
  `s3Storage`, `localStorage`, `messaging`, `svix`, `integrations`, `payments`,
  `security`, `cerbos`, `observability`, `agentBrowser`) are included by being
  present. Adding any member includes the area; its required members then
  apply. An absent area contributes nothing to readiness or output.
- **`platform`** is included by default through `initialValue: {}`. Omit
  `/platform` to leave its settings out. Inclusion only makes offline fields
  applicable; it does not enable authentication, payments, MCP, telemetry or
  another integration, supply addresses or credentials, or grant secret consent.
- **Always-present groups** (`deployment`, `bootstrapAdministrator`,
  `keycloak`, `secrets`, `recipientAuthorities`, `controlPlane` and the four
  document roots) open through `initialValue: {}` and carry their shared
  condition once, for example `keycloak` applies only when
  `/platform/authenticationProvider` is `keycloak`.
- `/platform/setupSecret` uses `requiredWhen`: it is required when
  `/hosting/replicaCount` is present and not 1.

Environment starts with `deployment.recipe=standalone` and
`deployment.scope=all` (answer paths `/deployment/recipe` and `/deployment/scope`). Standalone configures both API and Blazor. The
`separate-images` and `docker-compose` recipes allow `all`, `api` or `blazor`.
These are ordinary public answer keys, so CLI callers use the existing answers
input. Component scope determines obligations before validation. Choosing a
smaller export does not bypass incomplete work inside the selected scope.
Blazor-only requires its external API connection and session authority; it
does not require API administration, database or migration credentials.

Secret-provider selection is always explicit. Data Protection ring/admission
material belongs to the selected provider; its pin and generation floor are
independent process inputs. BFF session Redis authority is separately provisioned.
Infisical machine identities and token policies remain separate for API, BFF,
Combined and Migration. Process destinations map them to the target's structured
keys; Compose destinations preserve orchestration role namespaces.

Event can generate and persist its setup secret on a single-replica host with
a writable persistent `SETUP_SECRET_FILE`. Multiple replicas require one shared
deployment-owned secret. Configurator neither provisions this authority nor
proves the target filesystem or host startup.
The [provider contract inventory](provider-contract-inventory.md) records the
baseline plus every reviewed target-catalogue delta, including unsupported inputs,
canonical choice values and target-owned provider requirements.
Dropdowns preserve exact wire values across locale changes. Target parser aliases
are not additional Configurator choices; unknown values fail shared validation.
The inventory distinguishes reviewed source behavior from unqualified image behavior.
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
family `f79259e8-af23-49b0-8d8d-6e371cc96080`. Its prior release snapshots
remain immutable; an old pinned definition is not silently upgraded.

## Revise and regenerate

Changed canonical content requires native revision advancement before runtime
generation. From the repository root, with a current CLI build, create distinct
next-revision source candidates:

```bash
mkdir -p .tmp/deployment-aware-configuration
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  definition revise --source examples/islamu-event/configurator.json \
  --against examples/islamu-event/definition.json \
  --output .tmp/deployment-aware-configuration/event-example-next.json
dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release -- \
  definition revise --source store/islamu-event/configurator.json \
  --against store/islamu-event/releases/4.json \
  --output .tmp/deployment-aware-configuration/event-store-next.json
```

These destinations are create-only. Review both native receipts and candidates,
then adopt each candidate as its corresponding handwritten source. Never
hand-increment identity or change historical snapshots. The store command above
uses the current revision-4 anchor; use the matching immutable anchor when
revising a later release.

Regenerate the local current runtime and append the store release through their
native owners:

```bash
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --write-examples
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj \
  --configuration Release -- --check-examples
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/deployment-aware-configuration/store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/deployment-aware-configuration/store --check
```

The example generator also visits the minimal and Grafana examples; their
unchanged canonical bytes must remain unchanged. The store builder appends the
new release and generates its catalog; it must not rewrite the prior release.
The synthetic answer fixtures use the private 0.0.1 answer envelope and cover
Standalone, API-only, Blazor-only and full Compose inputs. Their independent
expected bytes include recipient-specific Data Protection/session material;
synthetic material cannot establish target authority or startup readiness.
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

`/configurationManifest/tenants` is a bounded object list. Add/remove tenant
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
