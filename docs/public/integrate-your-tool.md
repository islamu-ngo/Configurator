---
description: "Maintain one public YAML or JSON source, compile immutable configuration definitions, and distribute independently or through the optional store."
---

# Integrate your tool

Maintain one public `configurator.yaml`, `configurator.yml` or `configurator.json`.
Configurator compiles that declaration into an admitted, immutable runtime JSON
definition. Your application remains responsible for target validation, acceptance
and administration. No store, running service or target credentials are needed.

## Integrate with your coding agent

Use the portable [configurator-integrate skill](../../skills/configurator-integrate/SKILL.md)
with an agent that can read your repository. The agent researches public
configuration declarations, schemas, validators and synthetic examples, then
authors the integration file using the supported vocabulary. It records the
source of consequential facts and asks about missing behavior rather than
guessing. Private configuration and real answers are not research inputs.

The standalone CLI provides the native feedback loop: initialize, check, revise,
build, and exercise synthetic answer workspaces against independently specified
target output. The contributor reviews the source and the executed evidence.
The skill guides this work; the CLI enforces its machine contracts.

An adopter should need one maintained integration file, not a target-specific
exporter project, compiler wrapper, downloader or copied Configurator runtime.
Keep the integration beside the target code so maintainers can review it when
configuration changes. Application startup and deployment are separate.

## Create and review the source

```bash
islamu-configurator definition init --project-id example.service \
  --name "Example Service" --application-version 1.0.0 --machine
```

The command generates the family identifier and revision once. Keep that identity
when editing; never replace it on every build. `--format json` creates equivalent
JSON. Add the meaningful facts your application owns, for example:

```yaml
$schema: urn:islamu:configurator:authoring:v1
identity:
  familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
  revision: 1
project:
  id: example.service
  name: Example Service
applicationVersions: ["1.0.0"]
fields:
  SERVICE_PORT:
    type: integer
    sensitivity: public
    default: 8080
    minimum: 1
    maximum: 65535
  SERVICE_TOKEN:
    type: string
    sensitivity: secret
    required: true
outputs:
  ".env":
    format: dotenv
    restart: process
```

Use your generated identity instead of the example's. Types and exact supported
application versions require review. Omitted sensitivity is `sensitive`; defaults
and recommendations require explicitly public classification at each applicable
leaf. Never put private configuration, tokens or actual private answers in source.
`init --from-env-example .env.example` extracts keys as string fields and discards
all values. Review their types and privacy rather than treating the scaffold as a
verified application contract.

Ordinary JSON/YAML output copies literal root keys without guessing nesting. Dotenv
requires scalar fields and valid environment keys. Optional `bindings` map output
keys to logical fields. For advanced output, use the existing closed `projection`
and condition vocabulary from the [definition guide](definitions.md); bindings and
projection cannot be mixed. Generated IDs depend on semantic keys, so inserting an
unrelated setting does not rename existing fields. `build --id-map ids.json` emits
an optional public mapping for fixture maintenance.

YAML supports comments, strings, mappings, sequences and bounded JSON-shaped
numbers. Quote typed-looking keys and intended strings. Tags, anchors, aliases,
merge keys, directives, duplicate/colliding keys and multiple documents reject.
Both syntaxes produce identical canonical bytes for equivalent declarations.

## Describe journeys and credential acquisition

Add public explanations beside the field declaration, not private values or a
human-only sidecar. For an interactive first-party secret field, author useful
`help` before accepting the integration. A YAML `#` comment does not survive into
the admitted runtime definition. For example, extend the source above with:

```yaml
presentation:
  defaultExperience: recommended
  sections:
    - id: network
      title: Network
      fields: [SERVICE_PORT]
      disclosure: guided
    - id: access
      title: Access
      fields: [SERVICE_TOKEN]
      disclosure: advanced
  experiences:
    - id: recommended
      title: Recommended
      initialDisclosure: essential
      navigation: guided
      sections: [network, access]
    - id: custom
      title: Custom
      initialDisclosure: advanced
      navigation: sections
      sections: [access, network]
```

Under the existing `fields.SERVICE_TOKEN`, add this partial field metadata:

```yaml
help: Credential issued for this service, not your hosting login.
guidance:
  instructions: Ask the service administrator to issue the credential.
  links:
    - title: Public credential instructions
      url: https://example.org/docs/service-credentials
  searchTerms: [service, access, token]
credential:
  kind: opaque-token
  acquisition: issuer
```

These are metadata additions, not another complete source. `presentation` must
cover every root exactly once across its sections, and each experience must list
all sections once. Alternatively a section's `groups: [network]` selects all roots
whose field `group` is `network`; explicit fields and group expansion must not
overlap. Nested properties stay within their root. The compiler rewrites logical
keys through the same ID map used by conditions and output mappings.

Disclosure and initial disclosure default to `guided`; navigation defaults to
`sections`. The labels Recommended and Custom are ordinary author choices, not
hard-coded modes or answer presets. Metadata admission is available independently
of client rendering. Omitting presentation, guidance or credentials remains valid
for a machine-only definition.

Use only bounded plain public text and credential-free HTTPS documentation links,
without query strings or fragments. `credential` describes an effectively secret
string leaf, including inherited secret context. `create` is permitted only for
passwords and symmetric keys; issuer acquisition cannot describe a symmetric key.
It never enables generation, bypasses consent, or implies target acceptance.
See the [complete vocabulary and limits](definitions.md#authored-journeys-and-field-guidance).
Changing any admitted metadata requires a new canonical revision just like a field
or output change.

## Check, build and revise

```bash
islamu-configurator definition check --machine
islamu-configurator definition build --machine
```

Automatic discovery requires exactly one candidate at the current project root;
select `--root DIRECTORY` or `--source PATH` explicitly when necessary. Check never
changes source identity or revision. Build creates
`dist/configurator/revisions/1.json` for revision 1; `--output` selects another
public destination. Existing identical bytes are idempotent; conflicting bytes
are never overwritten. Keep canonical releases as the prior-revision anchor.
When selecting a separate release location, `check --against prior.json` and
`build --against prior.json` explicitly bind its family, revision and digest.

After a semantic edit, `check` and `build` reject reuse of the default recorded
revision. Run `definition revise --output candidates/configurator.next.yaml --machine`,
or also supply `--against prior.json` for an explicitly selected anchor. Review
and deliberately adopt the candidate before building the new revision, or build
it using an explicit `--source`. An unchanged canonical
document, including comments/format-only edits, does not advance. Revision updates
preserve all other source text, including comments, Unicode, BOM and newlines.
The candidate must have the same source format and a new, non-source,
non-discovery destination. Existing files and symlink paths reject. The command
never replaces the input source, so concurrent editor saves remain intact.
Changed content requires `--output`; unchanged content creates no candidate.
This workflow does not claim filesystem compare-and-swap or captured-handle
authority.

Use `definition init --fork-from configurator.yaml --output other/configurator.yaml`
only for a deliberate new family. It preserves the original source. Family labels
and canonical anchors provide identity/conflict evidence, not publisher trust.

## Reuse a public JSON Schema

```bash
islamu-configurator definition init --project-id example.service \
  --name "Example Service" --application-version 1.0.0 \
  --from-schema schema.json --accept-bounded-semantics --machine
```

The importer supports Draft 2020-12 explicit string, boolean, integer and number
types, homogeneous string enums, representable length/numeric/item bounds, closed
objects with object-scoped required sets, homogeneous arrays, `$defs` and supplied
local `$ref` documents. The native adapter reads references only below the schema
directory, rejects symlinks and traversal, and never fetches a URL. Core accepts
only bounded supplied bytes and has no filesystem/network authority.

Imported defaults are discarded until you explicitly review a value and classify
its leaf public. `writeOnly: true` raises the sensitivity floor to secret. Other
fields stay sensitive. `format` is explanatory annotation in field help; it is
not promoted to a target validator.

Strict import refuses these named preparation-domain differences:

| Diagnostic | Difference requiring acknowledgment |
| --- | --- |
| `schema-required-string-whitespace` | Configurator required strings reject whitespace-only answers. |
| `schema-string-utf16-length` | Configurator measures string length in UTF-16 code units. |
| `schema-numeric-bounded-domain` | Configurator numbers use bounded exact decimal values and integers use Int64. |

`--accept-bounded-semantics` acknowledges only these differences, which remain in
the machine diagnostics. It cannot bypass unsupported constraints or privacy.
Remote/dynamic references, cycles, open maps, tuple arrays, unions, combinators,
conditionals, regex constraints and unknown asserted keywords reject. Item-level
constraints that Configurator cannot represent also reject. This is schema-assisted
scaffolding, never a claim of equivalent target-schema validation.
Nested `$id` resource scopes and root `$id` with nonfragment references reject;
the importer never silently rebases a remotely identified schema onto local files.
Descriptive annotations must fit the runtime text bounds; line breaks and tabs
become spaces in explanatory help. Reference expansion charges emitted metadata
against the aggregate budget before constructing an amplified source.

## Distribute independently

Native clients and Browser local loading accept the maintainer source. Browser
HTTPS/deep links require compiled canonical runtime JSON. Answers and prepared
outputs remain separate and restricted; public definitions never contain them.
Add a credential-free HTTPS `project.repository` and explicit
`project.definitionLicense` when redistributing. The optional
[definition store](definition-store.md) compiles the same source and retains
immutable releases; it is not required for integration.

See the [CLI contract](cli.md), [portable maintainer skill](../../skills/configurator-integrate/SKILL.md)
and [technical authoring contract](../internal/MAINTAINER_AUTHORING.md).
