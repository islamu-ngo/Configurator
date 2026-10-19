Universal Self-Hosting Configuration Assistant: Deep Research and Senior CTO Review

## Executive verdict

**I would green-light this as a standalone ISLAMU project.** The opportunity is real, but I would define the product more narrowly—and more strategically—than “a universal configuration-manifest generator.”

The product I would build is a **portable setup-definition runtime for self-hosted software**:

> A self-hosted application publishes a declarative, non-executable description of what an operator must decide, which values are sensitive, which questions depend on earlier choices, how those answers map into one or more native configuration artifacts, and which application versions have been tested. ISLAMU renders that same definition through CLI, Terminal.Gui, Avalonia desktop, and Avalonia browser, with an optional curated catalog for discovery and trust.

That distinction matters.

Your competitive advantage is **not** rendering forms from a schema. Home Assistant, Backstage, Portainer, RunTipi, CapRover, and many generic JSON-Schema form systems already demonstrate pieces of that model. Home Assistant's blueprint selectors turn declarative input metadata into appropriate UI controls, while Backstage builds multi-step forms from JSON Schema, adds UI hints, collects secrets separately, and shows a review stage before executing a software template. citeturn5search0turn5search3turn13search0turn13search1

Your defensible product would be the combination of:

**portable application definitions + native-format generation + deterministic CLI + excellent human UX + secret-aware handling + version/conformance evidence + an optional open catalog + direct application deep links + an AI-safe machine interface.**

In the primary-source products reviewed, I did **not** find one system combining all of those properties while remaining independent of a particular orchestrator or target application. RunTipi, Umbrel, CasaOS, CapRover, Portainer, and YunoHost bind their template ecosystems substantially to their respective deployment platforms. Keycloak, authentik, Jenkins, Grafana, and Home Assistant have powerful configuration-description systems, but they are application-specific. Backstage is generic, but its templates ultimately invoke executable scaffolding actions and target software-development workflows rather than being a self-hosting configuration interoperability layer. citeturn18search0turn18search1turn19search1turn20search0turn20search1turn13search0

Your existing I-VSD strategy review independently reached essentially the architectural conclusion I would recommend after this external research: schema first, offline/local import as a first-class capability, headless deterministic core, Keycloak as an early external proof, progressive disclosure, strict separation from application-specific live authority, and an optional governed catalog rather than a mandatory store. fileciteturn0file0

### The architectural change I would make to your proposal

I agree with making this its **own ISLAMU project**, but for an enterprise-grade design I would **not keep the runtime source code and the public community store in the same repository indefinitely**.

I would create:

| Repository                     | Responsibility                                                                                              |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| `islamu/setup`                 | Specification, JSON meta-schema, .NET core engine, CLI, Terminal.Gui/Avalonia adapters, conformance tooling |
| `islamu/setup-catalog`         | Definitions only, review metadata, translations, golden fixtures, generated static catalog index            |
| Publisher/private repositories | Optional definitions that never need to enter the ISLAMU catalog                                            |

The reason is trust separation, not aesthetics. Hundreds of configuration PRs should not share the same contribution surface, CODEOWNERS rules, CI privileges, release cadence, or reviewer workflow as the engine capable of processing secrets and writing files.

The catalog should remain **optional forever**. That is not merely philosophically attractive: CapRover explicitly allows multiple third-party one-click repositories, RunTipi supports custom/community stores, Portainer supports alternate template sources, and CasaOS/ZimaOS documents a static third-party-store model. citeturn18search0turn19search1turn19search5turn19search0turn20search5turn20search11

The public catalog can therefore become the **curated discovery and evidence layer**, rather than the proprietary container that gives the file format value.

### The most important product boundary

There are actually several things commonly called “configuration,” and your product must refuse to blur them:

| Configuration class               | Examples                                                                   | ISLAMU responsibility                                                               |
| --------------------------------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **Bootstrap configuration**       | `.env`, ports, database URL, SMTP host, HCL server config                  | First-class                                                                         |
| **Declarative application state** | Keycloak realms, authentik blueprints, Grafana provisioning, Jenkins JCasC | First-class, but version/profile bounded                                            |
| **Secrets and credentials**       | Passwords, client secrets, private keys, API keys                          | Collect or generate locally when necessary; never part of portable definition/store |
| **Runtime mutable state**         | Users, sessions, workflows, uploaded content, job history                  | Normally out of scope                                                               |
| **Backup/restore state**          | Databases, volumes, snapshots, full instance exports                       | Explicitly out of scope unless a separately designed product capability exists      |

Keycloak illustrates why this taxonomy is essential. Its JSON export/import mechanism can include realms and users depending on export mode, but Keycloak explicitly warns that export/import is not a general backup/restore mechanism; sessions, events and other state are omitted, and different import paths have different overwrite behavior. citeturn15search0 MinIO provides another excellent counterexample: its server configuration export excludes users, policies, bucket metadata and environment-provided settings, contains credentials in plaintext, and an import **replaces the whole managed configuration** rather than merging it. citeturn16search2

So I would make this a hard rule:

> **ISLAMU Setup generates and understands configuration. It does not pretend configuration equals instance state or backup state.**

That single decision will save enormous architectural pain later.

## Market and adjacent-product landscape

The research shows a surprisingly consistent pattern: many successful self-hosting ecosystems have independently reinvented pieces of exactly what you are describing.

The opportunity is that each stopped at a particular product boundary.

### The closest store and guided-configuration precedents

| Project                          | What it already does                                                                                                                                                                                                                                                                                                                                                                                                            | What ISLAMU should learn                                                                                                                                                                              | Where your idea differs                                                                                                                                                                                                                  |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **RunTipi**                      | Separate application-store repository; individual app directories; `config.json`; Docker configuration; user-facing `form_fields`; random secret generation; custom/community stores. Its current public store lists hundreds of apps. The Immich entry, for example, declares random `DB_PASSWORD`, `JWT_SECRET`, and other generated fields mapped to environment variables. citeturn19search1turn19search3turn19search5 | This is probably the **closest precedent** for schema → UI → environment configuration plus an open store.                                                                                            | RunTipi definitions ultimately belong to the RunTipi deployment/orchestration model. ISLAMU can remain deployment-neutral and generate native artifacts for software running anywhere.                                                   |
| **Umbrel**                       | One app per directory, Docker Compose plus an `umbrel-app.yml` manifest, PR submission to the official App Store, image-digest pinning and dependency metadata. Umbrel's public project advertises more than 300 apps. citeturn18search1turn18search2                                                                                                                                                                       | Strong evidence that **Git PRs + per-app directories + automated review** can support a real ecosystem. Pinning exact image digests is particularly good.                                             | Umbrel is an application packaging/install platform. Its newer manifest can also enable executable lifecycle hooks, which is precisely a trust boundary I recommend you _not_ copy into portable setup definitions. citeturn18search1 |
| **CapRover One-Click Apps**      | Adds metadata to Compose-like definitions for variables, labels, defaults, descriptions, regex validation and generated random values; supports multiple repositories and third-party repositories. citeturn18search0                                                                                                                                                                                                        | Very close precedent for turning configuration parameters into a guided install form. External/private catalog support is especially relevant.                                                        | Its input model is relatively flat and ultimately uses substitution into the deployment configuration. You can provide much stronger typing, dependency logic, version evidence and multi-artifact output.                               |
| **CasaOS / ZimaOS App Store**    | Each app is backed by Docker Compose with a top-level `x-casaos` metadata contract. Store sources are built into static artifacts that can be hosted from ordinary HTTPS static hosting. citeturn20search0turn20search11                                                                                                                                                                                                    | Excellent architectural precedent for **not reinventing an underlying native format**: metadata augments Compose rather than replacing Compose semantics. Also validates a static-index architecture. | You are not trying to describe containers themselves; you should similarly delegate native artifact semantics to `.env`, JSON, YAML, etc.                                                                                                |
| **Portainer App Templates**      | Maintains an official JSON template repository, can expose environmental inputs, and supports alternate template sources. citeturn19search0turn2search2turn2search3                                                                                                                                                                                                                                                        | Confirms demand for preconfigured container/application experiences and replaceable catalogs.                                                                                                         | Templates are primarily deployment abstractions rather than application-native configuration guidance.                                                                                                                                   |
| **YunoHost**                     | App packages combine a manifest, installation questions/resources and lifecycle scripts; the catalog uses PRs, automated CI quality levels, app status and explicit not-maintained markers. citeturn20search1turn2search5turn2search6                                                                                                                                                                                      | The **maintenance-status and automated-quality model** is extremely relevant to your catalog governance.                                                                                              | YunoHost packages actually install and operate applications; that requires executable scripts. Your portable definition should stop before that authority boundary.                                                                      |
| **Backstage Software Templates** | A template can define multi-step JSON-Schema-based parameter forms, UI-specific presentation hints, required secrets, review UX, and then run scaffolder actions. citeturn13search0turn13search1                                                                                                                                                                                                                            | It is one of the best enterprise precedents for schema-driven progressive data collection and review.                                                                                                 | Execution actions make Backstage fundamentally more powerful—and more dangerous—than the data-only core I recommend for ISLAMU.                                                                                                          |
| **Home Assistant Blueprints**    | Blueprints can declare inputs and selectors that tell the UI what semantic control to render; Home Assistant has progressively moved ordinary configuration into guided UI rather than forcing every user to hand-edit YAML. citeturn5search0turn5search2turn5search3                                                                                                                                                      | This is an important UX lesson: **the schema is not an alternative syntax for users to learn; it is a source of semantics from which appropriate interfaces are generated.**                          | Home Assistant controls its own application domain; your renderer must preserve the same semantics across unrelated software and CLI/TUI/GUI surfaces.                                                                                   |

### What this competitive landscape tells me

There is substantial validation for almost every constituent idea you have:

A Git-backed catalog works. Per-application folders work. Community PRs work. Private/third-party catalogs work. Schema-driven forms work. Generated random values work. Review-before-execution works. Static catalog distribution works. Exact image pinning works. Application-native provisioning manifests work. citeturn18search0turn18search1turn19search1turn20search0turn13search1

What has **not** become a strong general interoperability layer is the boundary between:

> “This application has 68 environment variables, three meaningful deployment modes, seven values a new operator actually needs to understand, an optional JSON bootstrap manifest and four sensitive values”

and

> “Give me a safe, progressively disclosed experience that produces exactly the artifacts this specific release expects.”

That is the gap I believe is worth attacking.

### The strongest differentiation is not the store

The store is useful, but it should not be the moat.

The moat is the **quality of the definitions and their compatibility evidence**.

A competitor can clone a JSON schema and render a form quickly. It is much harder to maintain:

* exact application-version compatibility;

* meaningful dependency/relevance rules;

* security-sensitive defaults;

* correct `.env` quoting;

* nested native JSON/YAML output;

* import semantics;

* secret classification;

* generated-secret behavior;

* useful explanatory text;

* deterministic output;

* cross-version semantic diffing;

* disposable integration tests;

* trusted publisher attribution;

* current definitions for hundreds of changing upstream applications.

That maintenance and evidence network is the durable asset.

I would consequently position the project technically as a **configuration interoperability protocol/runtime**, not as a “self-hosting app store” and not as another Docker launcher.

## Best manifest-bearing self-hosted targets beyond Keycloak

There is no reliable global installation count that makes “most adopted self-hosted software” strictly rankable across these ecosystems. GitHub stars, Docker pulls and vendor/user counts measure different things. They are useful public popularity signals, but they should not be presented as equivalent installed-base statistics.

For example, repositories such as n8n, Immich, Home Assistant and Grafana have extremely large GitHub communities, while Jenkins, Vault, Argo CD and authentik also have substantial self-hosted/operator communities. Jenkins additionally publishes actual JCasC plugin penetration: the current Configuration as Code plugin page reports installation on 14.5% of Jenkins controllers reporting plugin data. citeturn7search0turn4search1turn4search3turn4search0turn7search2turn7search3turn7search1turn17search15turn16search4

For your product, however, the best early targets are **not simply the projects with the highest star counts**. You need targets that force the definition language to prove different capabilities.

### Authentik should be immediately after Keycloak

**authentik Blueprints are the strongest next external target I found.**

Blueprints are YAML configuration packages used to template, automate and distribute authentik configuration. They can configure flows and other application objects, can be uploaded locally, stored in the database, or loaded from OCI registries, and blueprint applications are processed in one atomic database transaction. Authentik explicitly warns that imported blueprints should come from trusted sources and be reviewed first. citeturn17search0turn17search1

It also exposes a published blueprint schema, including version-specific schema locations. citeturn17search7

Even more usefully for your design, authentik has an export path for converting existing configuration into blueprints, while deliberately omitting write-only values such as an OAuth provider's secret key. citeturn17search10

Why this is an excellent ISLAMU proof:

* nested declarative application objects;

* references between objects;

* collections;

* YAML output rather than JSON;

* imports and exports;

* sensitive security configuration;

* version-specific schema;

* OCI distribution;

* both one-shot import and reconciled blueprint-instance behavior.

It also demonstrates what you should **not** copy. Authentik explicitly regards blueprint imports as privileged: its security documentation notes that imports can arbitrarily modify application objects, and that blueprint capabilities including filesystem access are intentional aspects of its privileged model. citeturn17search2

That is precisely why your portable schema should remain less powerful.

**Recommendation: Keycloak + authentik should prove that ISLAMU can describe security-sensitive application configuration without becoming an execution engine.**

### Grafana is probably your best third proof

Grafana has an excellent configuration-as-code surface for your purposes.

Grafana provisioning can manage data sources from YAML and dashboards using provisioning configuration plus dashboard JSON. Provisioning can update existing objects, and some resources support deletion/pruning behavior. citeturn15search2turn15search3

It also offers the exact secret-reference behavior that your architecture should support: for example, a provisioned MySQL data source can place its password under `secureJsonData` while sourcing the actual value through an environment placeholder. citeturn15search9

Grafana therefore forces you to handle:

* YAML;

* JSON;

* directories/multiple artifacts;

* references to secrets rather than necessarily embedding them;

* restart/reconciliation semantics;

* desired-state configuration;

* application-specific nested structures.

It also gives you a major, widely recognized project rather than another identity platform.

I would put **Grafana in the first public catalog launch**.

### Jenkins Configuration as Code is a very high-value enterprise test

Jenkins Configuration as Code, or JCasC, represents most Jenkins controller configuration as YAML. Jenkins can show/export the effective JCasC configuration, reload configuration, manage plugin configuration, and keep the YAML in source control for reproducibility and rollback. Jenkins says most plugins can be supported without plugin-specific glue, with adapters available when required. citeturn16search0turn16search3

It is especially valuable because its configuration space is **open-ended through plugins**.

That will tell you whether your schema architecture has accidentally assumed that every application's configuration universe is closed and centrally known.

It is also a superb secret-safety test. Jenkins documentation explicitly calls out a `credentials` section in exported configuration and recommends considering removing it; the JCasC plugin has historically had security advisories involving secret values, variable evaluation and configuration export. citeturn16search0turn16search4

I would therefore support Jenkins only after your secret model is already solid.

### Argo CD is an enterprise-grade complexity test

Argo CD can define Applications, AppProjects and system settings declaratively through Kubernetes manifests. Its declarative setup includes ordinary ConfigMaps as well as Kubernetes Secrets containing repository credentials, signing material and related sensitive configuration. citeturn16search11

This is valuable because it will force you to distinguish:

**“generate a manifest” from “own Kubernetes deployment semantics.”**

ISLAMU should be able to guide an operator through creating appropriate manifest fragments without trying to become Helm, Kubernetes, Terraform or Argo CD itself.

That is a recurring scope rule I would enforce:

> **Integrate with existing infrastructure-as-code ecosystems; never try to replace them.**

Later outputs can include things such as Helm values fragments, Kubernetes objects or `tfvars.json`, but ISLAMU should remain the authoring and guidance layer.

### MinIO is a fantastic destructive-semantics test

MinIO/AIStor exposes a configuration export/import workflow, but its documentation makes two unusual properties explicit: exports contain credentials in plaintext, and import is a **whole-configuration replacement**—omitted values return to defaults and omitted named targets can disappear. citeturn16search2

That makes MinIO one of the best test cases for a feature many products get wrong: **an artifact's syntax is not enough to describe its operational meaning**.

A generated file should therefore carry informational semantics such as:

```text
generationOnly
createOnly
merge
replace
reconcile
startupImport
```

Those semantics do not authorize ISLAMU to apply anything. They let the UX truthfully explain:

> “Importing this artifact replaces the target configuration. Review the diff and keep a backup.”

That is far more valuable than merely saying “Valid configuration file.”

### Home Assistant is more valuable as a UX reference than as a full-instance manifest target

Home Assistant Blueprints do not represent its entire instance in the same sense as a Keycloak realm. Their real importance to you is the input model.

Blueprint selectors can say semantically “this is an entity,” “this is a boolean,” “this is a number,” and so forth, allowing Home Assistant to choose the suitable UI rather than embedding web-widget instructions into the data model. Blueprint input sections can also organize and progressively expose related inputs. citeturn5search3turn5search4

I would study this heavily for your field model.

A definition should say:

```text
semantic type = hostname
sensitivity = secret
required when = externalDatabase == true
meaning = database password
```

not:

```text
AvaloniaControl = PasswordBox
TerminalControl = TextField
WebComponent = PasswordInput
```

Your UI adapters should choose controls.

That is how you preserve the same definition across Avalonia WebAssembly, desktop, Terminal.Gui, CLI and future frontends.

### High-adoption environment-heavy targets still matter

Projects such as **n8n and Immich** are particularly good targets even when their primary self-hosted onboarding surface is more environment/Compose-oriented than realm-like configuration. Their large communities make them high-value demonstrations of the core onboarding problem you are trying to solve. The current RunTipi Immich definition is itself revealing: it has to manufacture several random secrets before deployment, illustrating how existing app launchers compensate for upstream environment-variable friction. citeturn7search0turn4search1turn19search3

I would therefore divide catalog targets into two categories rather than pretending everything is “a manifest”:

| Candidate          | Native configuration shape         | What it proves                                                                       | My priority      |
| ------------------ | ---------------------------------- | ------------------------------------------------------------------------------------ | ---------------- |
| **ISLAMU Event**   | Your own `.env` + native manifest  | Full control/reference implementation                                                | P0               |
| **Keycloak**       | Realm JSON / RealmRepresentation   | Nested JSON, collections, identity/security configuration, multiple import semantics | P0               |
| **authentik**      | Blueprint YAML                     | Nested YAML, object references, imports/exports, security-sensitive config           | P1               |
| **Grafana**        | Provisioning YAML + dashboard JSON | Multi-artifact bundles, secret references, provisioning/reconciliation               | P1               |
| **Immich or n8n**  | Environment-heavy deployment       | Extremely approachable `.env` onboarding and generated secrets                       | P1               |
| **Jenkins**        | JCasC YAML                         | Large extensible configuration surface and plugins                                   | P2               |
| **Argo CD**        | Kubernetes YAML                    | Enterprise declarative infrastructure integration and secret references              | P2               |
| **MinIO**          | Full configuration export/import   | Replacement semantics and secret-bearing export danger                               | P2               |
| **Home Assistant** | Blueprint YAML/input selectors     | Progressive-disclosure and semantic-control UX                                       | Design reference |
| **Vault**          | HCL/JSON server configuration      | High-security bootstrap configuration                                                | Later            |

### Keycloak itself gives you one particularly important warning

Keycloak does **not** have one universal “realm import behavior.”

Its command-line importer can overwrite existing realms, with `--override` defaulting to true; startup `--import-realm` skips a realm that already exists; the Operator's `KeycloakRealmImport` resource is create-oriented and does not reconcile later changes; Admin Console partial import has fail/skip/overwrite choices. citeturn15search0turn15search1

That single project proves why I would not expose a generic:

```text
supportsImport: true
```

boolean.

Instead, each output profile should document an **application/version-specific application contract**.

Generation and application are different concerns.

## Recommended product and schema architecture

The strongest architecture is one in which your UI applications are almost boring.

All intelligence should live below them.

```text
                       Optional Catalog
                              │
                    signed / pinned definition
                              │
                              ▼
┌────────────────────────────────────────────────────┐
│                  SetupDefinition                   │
│ fields · conditions · help · validation · outputs │
└────────────────────────┬───────────────────────────┘
                         │
                         ▼
               ┌────────────────────┐
               │ Headless Core      │
               │ parse / validate   │
               │ relevance          │
               │ readiness          │
               │ derivation / diff  │
               └─────────┬──────────┘
                         │
                    SetupSession
                         │
           ┌─────────────┼───────────────┐
           ▼             ▼               ▼
          CLI       Terminal.Gui      Avalonia
           │             │            Desktop/Web
           └─────────────┬───────────────┘
                         ▼
                    Render Plan
                         │
           ┌─────────────┼───────────────┐
           ▼             ▼               ▼
         .env          JSON/YAML       future adapters
                         │
                         ▼
              operator-controlled files
```

### Keep five contracts separate

I would formalize at least these concepts.

`SetupDefinition` is portable, public and permanently non-secret. It describes what can be configured.

`SetupSession` contains the current operator's answers. It is ephemeral by default and may contain secrets. It is never catalog content.

`RenderPlan` is a deterministic description of which artifacts will be generated, which mappings will occur and which artifacts contain sensitive fields. It should support review before bytes are written.

`CatalogEntry` describes discoverability, publisher identity, curation evidence, last validation and compatibility. It is deliberately separate from the publisher's `SetupDefinition`.

`SetupLock` records the non-secret provenance required to reproduce the _configuration process_: definition digest, engine version, target software version/profile and renderer versions. It should not become a shadow copy of answers.

That separation is one of the most important enterprise decisions you can make.

### Use JSON Schema, but do not make “arbitrary JSON Schema” your product format

JSON Schema 2020-12 is a strong choice for validating the **structure of your SetupDefinition** and for tooling/editor integration. JSON Schema provides structural validation, annotations and conditional mechanisms. Its own guidance also recognizes that processing untrusted schemas can consume significant resources, reinforcing the need for implementation limits. citeturn9search0turn9search1turn9search5

I would nevertheless define a **closed ISLAMU domain model** and validate that model with JSON Schema.

In other words:

```text
JSON Schema
    validates
        ↓
ISLAMU SetupDefinition
    describes
        ↓
Self-hosted application configuration
```

Do **not** expose every JSON Schema feature and then attempt to infer product semantics from it.

Things such as:

* sensitivity;

* restart consequences;

* progressive-disclosure level;

* generated-secret policy;

* artifact destination;

* environment-variable mapping;

* “why this field is relevant”;

* target-application compatibility;

* import/application semantics;

* enterprise policy impact;

are domain concepts, not generic JSON-schema concepts.

### I would make canonical distribution JSON

For the public catalog, I would strongly prefer **canonical JSON** over YAML.

YAML is pleasant for hand-authoring but has more parser and implicit-type behavior. Jenkins' own JCasC documentation has to warn users about YAML's case sensitivity, indentation and implicit conversion of values such as booleans and numbers. citeturn16search0

A good compromise is:

> Authors may optionally write YAML locally, but the CLI compiles it into canonical JSON. Catalog artifacts, hashes and signatures operate only over normalized JSON.

RFC 8785 defines the JSON Canonicalization Scheme specifically to produce invariant JSON representations suitable for hashing and signing. citeturn9search3

I would use something conceptually like:

```json
{
  "apiVersion": "setup.islamu.org/v1alpha1",
  "kind": "SetupDefinition",
  "metadata": {
    "familyId": "a303c61d-2cf5-4ae4-bfbd-57a175740523",
    "revision": "7",
    "publisher": "keycloak",
    "application": {
      "id": "org.keycloak.keycloak"
    }
  },
  "spec": {
    "compatibility": [],
    "fields": [],
    "constraints": [],
    "artifacts": [],
    "guidance": {}
  }
}
```

`familyId` would identify the logical definition lineage. A SHA-256 content digest would identify the **exact immutable revision**.

Do not use the GUID itself as integrity or security evidence.

### The field model should be semantic

Every configurable concept should have a stable field ID independent of output syntax:

```json
{
  "id": "database.password",
  "type": "string",
  "format": "password",
  "sensitivity": "secret",
  "requiredWhen": {
    "equals": ["database.mode", "external"]
  },
  "disclosure": "guided",
  "help": "database.password"
}
```

I would include at least:

* stable field identity;

* semantic data type/format;

* sensitivity;

* requiredness and conditional requiredness;

* category/group;

* progressive-disclosure level;

* safe/default/recommended distinction;

* validation;

* consequences/restart implications;

* documentation source;

* optional generation policy;

* value provenance.

### Separate defaults from recommendations

This is subtle but very important.

There are at least three meanings of “default”:

**Native default:** what the application itself will do when the value is absent.

**Publisher recommendation:** what the definition maintainer believes most operators should choose.

**ISLAMU/organization policy:** a constraint or recommendation from your system or an enterprise.

Those must never be collapsed into one property.

For example:

```text
Application default: authentication disabled
Publisher recommendation: authentication enabled
Enterprise policy: authentication MUST be enabled
Operator selection: enabled
```

A guided UI that silently turns the publisher's preference into “the default” can become deceptive or unsafe.

Your uploaded strategy review raises essentially the same issue around safe defaults, source of recommendations, security/privacy consequences and progressive disclosure. fileciteturn0file0

### Progressive disclosure should be a semantic invariant

I strongly agree with your idea.

I would define exactly three built-in views initially:

| Mode             | Contract                                                                                                                  |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------- |
| **Guided**       | Everything necessary for a successful/reasonable setup, plus every security/privacy/destructive decision and all blockers |
| **Advanced**     | Every currently relevant declared field                                                                                   |
| **All declared** | Every field in the definition; inactive items shown read-only with an explanation of why they are inactive                |

The key principle is:

> **Guided must hide complexity, never consequences.**

A required field cannot disappear because it is “advanced.” A TLS-off choice cannot be hidden because the form is trying to look simple. A destructive import consequence cannot be relegated to an advanced panel.

I would also make the engine capable of answering:

```text
Why is this field visible?
Why is this field hidden?
What activates it?
What will this value affect?
Why is setup not ready?
```

Those explanations can power the GUI, TUI, CLI and AI skill equally.

### Conditions need a tiny closed DSL

I would reject JavaScript, CEL, shell expressions, Nunjucks, Jinja, C# snippets or generic evaluation in portable definitions.

Something like this is sufficient:

```json
{
  "all": [
    { "equals": ["database.mode", "external"] },
    { "not": { "equals": ["deployment.topology", "single-container"] } }
  ]
}
```

Possible primitives:

```text
all
any
not
equals
notEquals
in
present
```

That is enough for a very large proportion of setup branching.

More advanced operations can be added only when actual definitions demonstrate that they are needed.

Backstage is instructive here: its form description begins declaratively with JSON Schema, but its scaffolding system eventually introduces template expressions and executable actions. citeturn13search0 Your goal is to stop before that boundary in the portable layer.

### Derived values also need a closed model

You will quickly encounter:

```text
PUBLIC_URL = "https://" + DOMAIN
database URL = host + port + database
random JWT secret
UUID client ID
slug derived from tenant name
```

Do not respond by adding a general template language.

Offer audited built-ins such as:

```text
concat
url
lower
upper
slug
uuid
randomBytes
base64url
```

A secret generator should use the engine's cryptographically secure random source. The definition may request **greater** entropy than your security policy, but it should not be allowed to lower an engine-enforced security floor.

### Think in artifact bundles, not one output file

Most serious applications will eventually need several outputs.

A single setup session may generate:

```text
.env
docker-compose.override.yml
keycloak-realm.json
grafana/provisioning/datasources/main.yaml
some-app.yaml
```

So the primary abstraction should be a **bundle of named artifacts**.

Each artifact should carry:

```text
artifact ID
renderer kind
suggested filename
target profile
apply semantics
restart consequence
secret-bearing status (computed by the engine)
```

The definition should never be trusted to specify arbitrary absolute paths such as:

```text
/etc/shadow
../../some-file
C:\Windows\...
```

At most it provides a sanitized filename/destination hint. The trusted host application owns actual filesystem selection.

### Make renderers typed rather than using arbitrary text templates

For an enterprise v1 I would support:

**Dotenv renderer**

```text
field → environment key
```

**Structured JSON renderer**

```text
field → JSON Pointer/path
```

**Structured YAML renderer**

Same logical document model, serialized to YAML.

Then add TOML/INI/HCL only when real targets justify them.

For a Keycloak realm, you will need static application-defined structure plus dynamic field mappings and repeatable collection mappings. I would solve that with a **typed document projection model**, not string substitution.

For example, conceptually:

```text
base realm object
+
/realm                 ← realm.name
/enabled               ← realm.enabled
/clients/*/clientId     ← clients[].id
/clients/*/redirectUris ← clients[].redirectUris
```

This guarantees that a user-entered string remains a JSON string rather than becoming injected JSON syntax.

### Import deserves its own subsystem

Do not assume that because you can render a configuration, you can automatically import it losslessly.

I would classify importers:

| Import class     | Meaning                                                                                        |
| ---------------- | ---------------------------------------------------------------------------------------------- |
| **Lossless**     | Can parse and later reproduce all meaningful data                                              |
| **Semantic**     | Maps known fields while preserving unknown material separately                                 |
| **Extract-only** | Reads recognized values to prefill a session; regeneration may not reproduce the original file |

An `.env` importer may be relatively straightforward but still has comments, duplicate keys and quoting semantics to consider.

A Keycloak realm importer is significantly harder because a realm representation can contain far more state than the safe bootstrap subset you may want to generate. Keycloak's export options themselves distinguish users and other material, while Admin Console exports mask sensitive values. citeturn15search0

Therefore:

> **Never promise “universal import/export.” Every importer is an explicit, versioned capability with a declared fidelity level.**

That is enterprise-grade honesty.

## Enterprise security, catalog, deep links, and AI boundary

This is where I would be most opinionated.

A universal configuration system sits immediately next to passwords, identity systems, databases, SMTP credentials, API keys, private endpoints and privileged deployment configuration.

It must be architected as a security product even if its primary feature appears to be UX.

### Definitions must remain completely non-executable

I would make this a permanent architectural principle, not merely a v1 limitation.

A catalog `SetupDefinition` should never be able to:

* execute a command;

* invoke PowerShell/Bash;

* load a .NET assembly;

* load a WASM module;

* run JavaScript;

* dynamically install a plugin;

* read environment variables;

* read arbitrary files;

* contact arbitrary hosts;

* fetch arbitrary `$ref` schemas;

* call a secret manager;

* perform HTTP health checks;

* apply its generated output.

These can exist as **separately installed trusted host capabilities** later, but catalog content can never grant itself those powers.

This is exactly where the design diverges from systems such as Umbrel hooks, YunoHost packaging scripts and Backstage actions: those systems intentionally cross into application orchestration/execution. citeturn18search1turn20search1turn13search0

### Treat every definition as hostile input

Even data-only definitions can attack you.

A malicious definition could attempt:

* enormous nesting or field counts;

* cyclic relevance dependencies;

* pathological regular expressions;

* path traversal through output names;

* misleading security descriptions;

* invisible Unicode/control characters;

* unsafe suggested defaults;

* thousands of generated outputs;

* secret-looking literal values;

* external tracking links/images;

* recursive structures;

* expensive validation.

JSON Schema's own security guidance is one reason to impose explicit evaluation limits when handling untrusted schemas. citeturn9search5

I would therefore:

* use closed objects and reject unknown members in catalog definitions;

* limit document size, nesting, counts and string lengths;

* reject dependency cycles;

* prefer built-in validator IDs over arbitrary regex;

* when regex is necessary, use bounded/non-backtracking evaluation;

* sanitize filenames;

* prohibit remote schema resolution;

* prohibit raw HTML;

* make external links explicit user actions;

* never background-fetch a URL embedded by a definition.

### The secret architecture should have four hard zones

```text
Definition / Catalog        NEVER SECRET
        ↓
Session                     MAY CONTAIN SECRET
        ↓
Rendered Artifact           MAY INTENTIONALLY CONTAIN SECRET
        ↓
Logs / telemetry / lock     NEVER SECRET
```

The distinction between the definition and the generated artifact is essential.

A definition can say:

```text
SMTP_PASSWORD is a secret.
Ask locally.
Do not show it after entry.
Map it to SMTP_PASSWORD if dotenv output is selected.
```

The store never contains the password.

The UI/session can temporarily contain it.

A user explicitly choosing `.env` output may cause it to become plaintext on disk.

Your UX should state that clearly.

### `.env` should be supported, but never treated as “secure secrets storage”

Docker's own current documentation recommends using Docker secrets for passwords, certificates and API keys rather than placing those values directly in environment variables when possible, because environment variables can be unintentionally exposed. Compose supports secrets sourced from files or host environment values and grants them explicitly to services. citeturn13search4turn13search7turn13search12

That leads to a very good future design:

A field marked `secret` can have several **target-supported delivery modes**:

```text
inline dotenv
secret file
Docker Compose secret
environment reference
external secret reference
```

The application definition declares which mechanisms the application actually supports. The operator chooses.

For compatibility, `.env` remains completely valid; your product simply tells the truth about its properties.

Keycloak itself supports environment placeholders inside realm configuration JSON and warns that sensitive environment values need careful handling. citeturn15search0 That makes it an excellent early case for emitting a **secret reference rather than the secret itself** when the target supports it.

### Secret collection must be agent-safe

Your existing CLI + agentic-skill Markdown is actually one of the strongest parts of this architecture.

I would make the CLI the **canonical machine API**, with GUI and TUI remaining human adapters over the same core.

But I would explicitly design the agent protocol so the model never needs to see secret values.

For example:

```text
$ islamu-setup plan
DATABASE_PASSWORD: required, secret, not provided

$ islamu-setup secret provide database.password
Enter value: ********
Secret accepted.

$ islamu-setup plan --format json
{
  "field": "database.password",
  "sensitivity": "secret",
  "provided": true
}
```

The AI sees:

```text
provided: true
```

not the password.

I would prohibit secrets as command-line arguments because command arguments may become visible through shell history or process inspection. Secret-taking commands should use a non-echoing terminal, standard input under an explicitly safe mode, an OS secret source, or another protected side channel.

The machine API should expose things such as:

```text
definition list
definition inspect
session create
answer set
secret provide
explain
plan
validate
render
diff
import
doctor
```

and **never return secret values** from ordinary JSON output.

This makes your Markdown skill unusually powerful: an AI can completely guide configuration while delegating sensitive entry to a trusted local human interface.

### AI should be an accelerator, never a correctness dependency

The deterministic schema/core should remain capable of producing a correct configuration with AI entirely disabled.

AI can add:

* natural-language explanations;

* “what does this setting mean?”;

* interpretation of upstream documentation;

* suggested topology based on operator goals;

* troubleshooting from redacted diagnostics;

* definition-author assistance.

But an AI should not silently add undocumented environment variables or change security-critical answers.

A useful rule is:

> AI may **propose** configuration. Only the deterministic core determines whether the proposal is represented, relevant, valid and ready.

That will age very well.

### The browser deep-link idea is very good

I would absolutely implement the ISLAMU Event → configuration experience handoff.

But the deep link should carry an **identity**, not configuration state.

For example:

```text
https://setup.islamu.org/launch/
  a303c61d-2cf5-4ae4-bfbd-57a175740523
  ?appVersion=2026.9.0
  &entry=initial-setup
```

Conceptually:

```text
family GUID
+ application version
+ optional entry point
```

The assistant resolves that to:

```text
definition revision
+ immutable content digest
+ publisher identity
+ compatibility evidence
```

and **pins that exact digest for the session**.

If the catalog updates five seconds later, an active setup does not silently mutate.

For maximum reproducibility an application can embed a known definition digest and launch that directly.

### A GUID must never be a trust token

This is critical.

A GUID tells you **which logical definition** somebody intended.

It does not tell you:

* who wrote it;

* whether it has changed;

* whether it is safe;

* whether it matches the application version;

* whether ISLAMU reviewed it.

Those are separate questions.

That is why you need both:

```text
stable family ID   = identity/discovery
SHA-256 digest     = immutable content identity
publisher identity = authorship evidence
curation status    = ISLAMU review evidence
conformance status = target compatibility evidence
```

### Never put secrets in the deep-link URL

URLs are routinely retained in browser history, logs, bookmarks and intermediaries. RFC 3986 explicitly warns against secrets in URIs because URIs are frequently displayed and logged, while CWE-598 documents the broader problem of sensitive information in HTTP query strings. citeturn14search1turn14search2

So these are fine:

```text
definitionId
applicationVersion
entryPoint
locale
```

These are not:

```text
password
API key
access token
SMTP secret
database URL containing credentials
private target URL
secret callback token
```

I would also **not accept an arbitrary remote schema URL in a launch link**.

This:

```text
/setup?schema=https://whatever.example/schema.json
```

looks flexible but creates a nasty fetching/trust problem.

Instead:

```text
/setup/launch/<known-id>
```

resolves through a configured catalog/trust source.

Users can still explicitly import local files or explicitly add a third-party catalog.

### Avoid arbitrary return URLs

An application may eventually want:

```text
ISLAMU Event
  → Setup Assistant
  → return to ISLAMU Event
```

Do not solve that with:

```text
?returnUrl=https://anything...
```

That creates an open-redirect and trust surface.

Use a declared integration/callback identity or a locally controlled navigation mechanism. The portable setup definition does not get to choose where the assistant sends the browser afterward.

### Browser secret mode deserves different trust treatment

A WebAssembly client can calculate and render configuration locally, but code delivered by a web origin is still code controlled by that serving origin.

For high-assurance secrets, I agree with the position in your existing strategy review: native desktop/TUI/CLI should remain the stronger trust path, and hosted browser secret entry should not quietly be treated as equivalent merely because the computation occurs client-side. fileciteturn0file0

I would make browser modes explicit:

```text
Non-secret setup
    fully supported

Secret-bearing setup
    explicit trust warning / policy-controlled

High-assurance mode
    "Continue in desktop / terminal"
```

This is enterprise-grade rather than inconvenient.

### Build the catalog as a trust system, not merely a folder of JSON files

The original Git-folder/PR concept is sound. Umbrel, RunTipi, CasaOS and YunoHost all provide evidence that Git-based application catalogs are operationally viable. citeturn18search1turn19search1turn20search5turn20search1

I would structure a catalog entry approximately like:

```text
definitions/
  keycloak/
    metadata.json
    definitions/
      2026-09-01/
        definition.json
        i18n/
          en-US.json
          fr-FR.json
        fixtures/
          basic.answers.json
          basic.realm.expected.json
```

But the **review information should not live inside the publisher-authored definition**.

A publisher should not be able to submit:

```json
{
  "isVerified": true,
  "isSafe": true,
  "isOfficial": true
}
```

Trust metadata is owned by the catalog.

### Use precise trust labels

I recommend four orthogonal labels:

**Publisher verified**

> We have evidence that this definition came from the stated upstream/publisher.

**Signed**

> This exact byte-equivalent definition was signed by the identified key/identity.

**ISLAMU curated**

> ISLAMU reviewed specific properties of this definition.

**Conformance tested**

> Output from this exact definition revision was successfully tested against explicitly named application release(s)/image digest(s).

None of those means:

> “Guaranteed secure.”

Signatures prove identity and integrity, not quality.

### Sign immutable catalog artifacts

For public publisher identities, Sigstore/Cosign is particularly attractive because Cosign can sign arbitrary blobs and OCI artifacts and can bind keyless signatures to OIDC-backed identities, while transparency-log support provides additional evidence around signing events. citeturn10search0turn10search4turn10search8

There is also an elegant distribution evolution available:

```text
Git repository
    ↓ human review
canonical SetupDefinition
    ↓
OCI artifact
    ↓
Cosign signature
    ↓
static ISLAMU catalog index
```

Authentik already demonstrates that configuration blueprints can be distributed through OCI registries using a blueprint-specific media type. citeturn17search0

That would give enterprises:

* standard registry mirroring;

* immutable digests;

* private registries;

* offline caches;

* ordinary OCI tooling;

* signatures.

I would **not** start there, though.

Start with static Git-generated artifacts.

Add OCI when users need mirroring/distribution at scale.

### Design now for rollback/freeze attacks even if you do not implement TUF immediately

The Update Framework exists specifically to defend software-update systems against classes of attacks including rollback, freeze, mix-and-match and compromised signing keys. citeturn11search0turn11search2

You probably do not need a full TUF deployment in your first release.

But adopt the architectural concepts:

* immutable targets;

* signed root/trust material;

* expiring index metadata;

* version numbers;

* key rotation;

* rollback detection;

* revocation;

* separation between root and ordinary publisher keys.

Once the client begins automatically refreshing definitions from a remote catalog, those concerns become much more important.

### Your catalog CI should be unusually strict

A definition PR should at least prove:

```text
meta-schema validity
closed identifiers
no duplicate IDs
no activation cycles
no unknown validator
no unbounded collection
no unsafe output filename
no literal secret
no hidden required blocker
no unsupported renderer
deterministic render
golden fixture stability
compatibility metadata present
license/source metadata present
localization consistency
```

For high-impact values, lint things such as:

```text
authentication disabled
TLS verification disabled
public/0.0.0.0 exposure
anonymous admin
default passwords
telemetry unexpectedly enabled
destructive prune/replace behavior
```

Some of those may be perfectly legitimate application configurations. The point is that they deserve review, not that they must always be prohibited.

### Do not execute contributor-provided test scripts in privileged CI

This deserves a hard rule.

The definition repository should contain **data and golden fixtures**, not executable test programs contributed by arbitrary PR authors.

Otherwise the supposedly non-executable schema becomes executable through its test ecosystem.

PR validation can run your trusted conformance engine against contributor data.

Actual target integration testing—starting Keycloak, Jenkins, etc.—should run inside a controlled pipeline after the artifact/image choices have themselves passed review and without production secrets.

Umbrel's emphasis on testing exact image digests is a useful precedent for binding evidence to the exact software artifact actually tested. citeturn18search1

### Enterprise private catalogs could become one of your strongest commercial capabilities

Without damaging the open ecosystem, enterprises could configure:

```text
ISLAMU public catalog
vendor catalog
company-internal catalog
air-gapped mirror
local definitions
```

An enterprise internal catalog can contain:

* internal applications;

* approved versions;

* organization-specific configuration guidance;

* private documentation links;

* policy constraints;

* certified golden configurations.

This is exactly where a sustainable enterprise offering can exist without paywalling the open file format.

A future `SetupPolicy` could let an organization constrain upstream definitions without forking them:

```text
TLS must be enabled
database must be external
allowed regions = [...]
public exposure prohibited
secret delivery must use file/provider
minimum password entropy = ...
```

I would keep policy as a separate signed document. A publisher describes its application; an enterprise describes its own operational policy.

Those are different authorities.

## Delivery roadmap and CTO decisions

The project is technically credible. The main threat is not feasibility; it is **scope explosion**.

You could accidentally turn this into:

```text
JSON Schema form generator
+ app store
+ package manager
+ secret manager
+ deployment orchestrator
+ Terraform competitor
+ backup system
+ browser IDE
+ application API client
+ AI agent platform
```

and never finish the valuable part.

I would therefore sequence it aggressively.

### The first architecture milestone

Extract a headless package with no Avalonia, Terminal.Gui, filesystem or network dependency.

The acceptance test should be:

```text
definition + answers
       ↓
deterministic readiness state
       ↓
deterministic render plan
       ↓
deterministic artifact bytes
```

The exact same vectors must pass through every UI adapter.

Your current CLI and agent skill are strategically valuable here because the CLI can become the executable specification of engine behavior. Your existing project review similarly recommends the headless/conformance boundary rather than coupling the universal model to the current ISLAMU-specific control plane. fileciteturn0file0

### Keep the format explicitly alpha while you prove generality

Because you explicitly do not mind breaking changes, take advantage of that.

Use:

```text
setup.islamu.org/v1alpha1
```

and make breaking revisions quickly.

Do **not** freeze the first schema merely because ISLAMU Event fits it.

I would require these before `v1beta1`:

**ISLAMU Event** proves your internal requirements.

**Keycloak** proves complex JSON/native application state.

**authentik or Grafana** proves you did not accidentally build “a Keycloak schema generator.”

If supporting the third project requires:

```csharp
if (applicationId == "grafana")
```

inside the universal engine, stop and fix the abstraction.

That is the key conformance criterion.

### Ship a multi-artifact MVP, not just `.env`

I would make your first output kinds:

```text
dotenv
JSON
YAML
```

That is enough to prove most of the important architecture.

Supporting only `.env` first risks teaching the schema model that every application is a flat key/value collection, which Keycloak immediately disproves.

### The first public catalog I would ship

I would deliberately keep it small and extremely high quality:

```text
ISLAMU Event
Keycloak
authentik
Grafana
Immich or n8n
```

That collection exercises:

```text
internal manifest
nested JSON
nested YAML
provisioning + secret references
simple high-value dotenv onboarding
```

It is a much better validation portfolio than trying to launch with fifty mediocre applications.

### Build the author experience almost as carefully as the end-user experience

Maintainers will determine whether this becomes an ecosystem.

I would want:

```text
islamu-setup definition init
islamu-setup definition validate
islamu-setup definition lint
islamu-setup definition preview
islamu-setup definition render --fixture basic
islamu-setup definition diff old.json new.json
islamu-setup definition pack
```

Then create accelerators.

For example:

```text
islamu-setup scaffold dotenv .env.example
```

could parse an upstream `.env.example` and create a **draft** definition containing keys and comments.

It would still require a human to define:

```text
meaning
requiredness
sensitivity
conditional relevance
recommendations
validation
compatibility
```

Similarly, a Compose parser could discover declared environment keys and create a draft.

AI can be extremely effective in this _definition-authoring_ workflow—extracting configuration documentation, suggesting field descriptions and finding likely conditions—while your CI and maintainers retain authority over what enters the store.

### Add semantic definition diffing early

This will become much more important than ordinary Git diffs.

Suppose a Keycloak definition revision changes:

```text
database.password
  sensitivity: private → secret

proxy.mode
  default: edge → passthrough

smtp.enabled
  requiredWhen changed

realm JSON
  new client role
```

Those deserve different prominence.

Your diff engine should understand changes such as:

```text
field added
field removed
requiredness changed
sensitivity changed
default changed
recommendation changed
validator narrowed/widened
condition changed
artifact mapping changed
literal output changed
compatibility changed
secret exposure changed
```

Then definition updates can show a human review screen before changing the pinned version.

This is an important enterprise differentiator.

### Add a setup provenance file

I would produce a small secret-free file alongside configurations, perhaps:

```text
setup.lock.json
```

containing only things such as:

```json
{
  "definition": {
    "familyId": "...",
    "revision": "7",
    "digest": "sha256:..."
  },
  "engineVersion": "...",
  "target": {
    "application": "keycloak",
    "version": "..."
  },
  "renderers": {
    "realm": "json/v1",
    "environment": "dotenv/v1"
  }
}
```

This tells support staff:

> Which configuration definition created this installation?

without giving them:

```text
DB_PASSWORD
SMTP_PASSWORD
OIDC_CLIENT_SECRET
```

I would be cautious even about publishing hashes of secret-bearing artifacts in such a file, because low-entropy values can make naive hashes more informative than operators expect. Provenance does not require secret-content fingerprints.

### Conformance should have separate evidence levels

This could become one of the best features of your public catalog.

Do **not** display a single green check mark.

Display:

| Evidence             | Meaning                                                                |
| -------------------- | ---------------------------------------------------------------------- |
| **Schema valid**     | Definition conforms to ISLAMU meta-schema                              |
| **Semantic valid**   | IDs, dependencies, mappings and progressive-disclosure invariants pass |
| **Render tested**    | Golden fixtures render deterministically                               |
| **Publisher signed** | Identity/integrity evidence exists                                     |
| **ISLAMU curated**   | Human/project review occurred                                          |
| **Target tested**    | Generated output was exercised against exact application version/image |
| **Last verified**    | Date of latest compatibility run                                       |

That turns the catalog from “random community YAML” into genuine operational infrastructure.

### Target tests should pin exact releases

A definition might claim:

```text
Keycloak >= 26 < 27
```

but the evidence should say:

```text
tested:
  26.x.y image sha256:...
  26.x.z image sha256:...
```

The range is publisher intent.

The exact versions are evidence.

Umbrel's insistence on Docker image digests for tested packages demonstrates the value of connecting compatibility claims to immutable runtime artifacts. citeturn18search1

Keycloak is particularly suitable for disposable target testing: generate realm JSON, start the specified release, import it, validate expected realm objects, and destroy the instance. The official Keycloak tooling supports realm imports through CLI, startup import and Operator workflows, giving you several distinct conformance scenarios. citeturn15search0turn15search1turn15search12

### Deep links should be in the first serious release

Your ISLAMU Event idea is too valuable to postpone.

The ideal experience is:

```text
User opens ISLAMU Event setup page
             ↓
"Configure this deployment"
             ↓
https://setup.islamu.org/launch/<family-guid>?appVersion=...
             ↓
correct definition opens immediately
             ↓
Guided mode
             ↓
Review
             ↓
.env + manifest
```

No:

```text
Open Setup Assistant
Search store
Type ISLAMU
Pick product
Pick version
Pick deployment type
```

The software already knows what it is.

Removing those redundant selection steps is exactly the kind of friction reduction this project should exist to provide.

For native applications, you can later support a registered scheme or app-link mechanism:

```text
islamu-setup://launch/<guid>
```

but I would make the **HTTPS launch URL canonical** first because it naturally works with the browser experience.

### Consider application-advertised setup metadata later

One especially interesting future extension would let software advertise something like:

```json
{
  "setupDefinition": {
    "familyId": "...",
    "digest": "sha256:...",
    "applicationVersion": "2026.9.0"
  }
}
```

through a known endpoint or included installation metadata.

Then an application would not merely link to your store; it could declare:

> “This exact SetupDefinition describes me.”

That starts turning the project from a catalog into a real interoperability standard.

I would not make it mandatory for v1.

### Do not add direct application control until much later

Eventually you will naturally want:

```text
Generate Keycloak realm
→ Import directly into Keycloak
```

or:

```text
Generate Grafana provisioning
→ Apply to Grafana
```

That is useful—but it is a **different trust product**.

Generating a JSON file needs no target credential.

Applying it needs:

```text
target URL
authentication
authorization
TLS validation
version discovery
dry run
idempotency
failure recovery
audit trail
possibly destructive permission
```

Keycloak alone demonstrates how different import pathways have different create/overwrite semantics. citeturn15search0turn15search1

So a future connector should be trusted installed code:

```text
ISLAMU Setup Core
       │
       ├── portable SetupDefinition   ← cannot access target
       │
       └── optional Keycloak Adapter ← trusted code
                 │
                 ▼
             Keycloak API
```

Never add:

```json
"script": "curl ..."
```

to the definition language to get there faster.

### Protect file writes as a privileged boundary

The core should render bytes in memory.

The host application decides whether and where to write them.

For desktop/TUI:

```text
render
→ redacted preview
→ explicit destination
→ path/symlink validation
→ protected temporary file
→ atomic replacement
→ restrictive permissions where supported
```

Browser mode should normally download rather than pretend it can guarantee operating-system file permissions.

Your existing review already identifies this separation between rendering and protected writing as part of the desired boundary. fileciteturn0file0

### Make offline operation a release requirement

This is self-hosting software.

A user who has:

```text
ISLAMU Setup executable
+ a local SetupDefinition
```

should not need:

```text
setup.islamu.org
GitHub
an account
the catalog service
telemetry
an API
```

to generate their configuration.

That gives you an extremely strong architectural test:

> Turn networking off. Does local import, guided setup, validation and rendering still work?

If yes, the catalog really is optional.

If no, the product has silently become SaaS-dependent.

### Make third-party catalogs first-class rather than a hack

The precedents are strong: CapRover supports third-party repositories, RunTipi explicitly promotes community/custom stores, and CasaOS/ZimaOS describes independent static-store hosting. citeturn18search0turn19search1turn20search11

I would make catalog sources explicit:

```text
ISLAMU Official
Acme Internal
Vendor X
Local Directory
```

Each source has its own trust root/policy.

That makes the product very attractive for:

```text
homelabs
air-gapped deployments
regulated organizations
private software
sovereign infrastructure
enterprise platform teams
```

without forcing those users to fork your public catalog.

### The metrics I would watch

Do not optimize for “number of definitions in the store.”

That metric incentivizes low-quality catalog growth.

The useful metrics are:

```text
time to first successful application start
configuration validation success
target conformance success
definition staleness
median time to correct a broken definition
support requests per definition/version
percentage of guided users who encounter advanced mode
secret-handling incidents
accessibility defects
percentage of outputs reproducible from pinned definitions
```

For a privacy-respecting self-hosted product, this does not require always-on telemetry. Opt-in research, CI evidence, issue data and support outcomes can provide substantial evidence; your existing strategy review likewise recommends no default remote telemetry and emphasizes outcome rather than catalog-size metrics. fileciteturn0file0

### My final CTO architecture decision

I would approve the project with these non-negotiable principles:

| Decision                                     | CTO recommendation                                     |
| -------------------------------------------- | ------------------------------------------------------ |
| Standalone ISLAMU project                    | **Yes**                                                |
| Independent open specification               | **Yes**                                                |
| Same core for CLI/TUI/Avalonia               | **Yes; mandatory**                                     |
| JSON Schema                                  | **Yes, as meta-schema—not as the whole product model** |
| Canonical definition format                  | **JSON**                                               |
| YAML authoring                               | Optional normalization convenience                     |
| Public Git catalog                           | **Yes**                                                |
| Catalog required                             | **No, permanently optional**                           |
| Local definitions                            | **First-class**                                        |
| Third-party/private catalogs                 | **First-class**                                        |
| Community PRs                                | **Yes, data only**                                     |
| Engine + catalog same repository             | **I recommend separating them**                        |
| `.env` generation                            | **Yes**                                                |
| JSON/YAML manifests                          | **Yes from early releases**                            |
| Arbitrary text templates                     | **No**                                                 |
| Arbitrary scripts in definitions             | **Never**                                              |
| Definition plugins                           | **Never in the portable trust boundary**               |
| Secret values in definitions/store           | **Never**                                              |
| Secret local entry/generation                | **Yes**                                                |
| Docker/file/external secret references       | **Yes where targets support them**                     |
| Progressive disclosure                       | **Core semantic feature**                              |
| Direct GUID launch/deep link                 | **Yes**                                                |
| GUID as trust proof                          | **No**                                                 |
| Definition content digest                    | **Yes**                                                |
| Publisher signatures                         | **Yes after basic catalog works**                      |
| OCI distribution                             | **Excellent later evolution**                          |
| Semantic update diff                         | **High priority**                                      |
| Exact target conformance tests               | **High priority**                                      |
| AI via CLI skill                             | **Strong differentiator**                              |
| AI receiving secrets                         | **Architecturally avoid**                              |
| Direct target imports/API calls              | **Separate trusted adapters later**                    |
| Universal backup/restore                     | **No**                                                 |
| Competing with Docker/Helm/Terraform/Ansible | **No; generate inputs for them**                       |

The highest-value product you can create is therefore **not another self-hosting installer**.

It is a layer that can sit in front of almost all of them:

```text
                 ISLAMU SetupDefinition

           ┌────────────┼────────────┐
           │            │            │
        Human         Automation      AI
      GUI / TUI          CLI         skill
           │            │            │
           └────────────┼────────────┘
                        │
                  deterministic
                   render engine
                        │
      ┌─────────────────┼───────────────────┐
      │                 │                   │
     .env             native             IaC input
                     manifests
      │                 │                   │
   Immich             Keycloak           Kubernetes
   n8n                authentik          Helm
   GitLab             Grafana            Terraform
   Vault              Jenkins            Compose
                       ...
```

The catalog then becomes the place where the ecosystem answers:

> **“What does a human actually need to decide to configure version X of this self-hosted application correctly?”**

That is substantially more valuable than a collection of environment-variable templates.

It would let upstream applications keep their own native formats, operators keep control of their secrets and files, enterprises maintain private policy/catalog layers, advanced users automate everything through the CLI, novices get a carefully guided experience, and applications such as ISLAMU Event can bypass discovery entirely by launching the exact definition they need.

Most importantly, it has a credible path from the implementation you already have. Your Avalonia surfaces, Terminal.Gui work, CLI and agentic skill are not separate products under this architecture; they become **different trust-appropriate views over one deterministic configuration protocol**. That is the point at which this stops looking like a convenient setup wizard and starts looking like infrastructure worth making its own ISLAMU project.
