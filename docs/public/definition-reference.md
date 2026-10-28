---
description: "Implemented pre-release v1 definition properties, value semantics, limits, and client capabilities."
---

# Current Definition Contract Reference

> **Audience:** Adopters | Integrators
> **Status:** Implemented pre-release v1 reference; known limitations are identified below
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `docs/internal/CORE_DEFINITION_CONTRACT.md`, `docs/internal/MAINTAINER_AUTHORING.md`, `src/Configurator.Core/Definitions/`, `src/Configurator.Presentation/Presentation/`

Use this reference to author a definition that today's Configurator understands.
The [integration guide](integrate-your-tool.md) provides the complete workflow;
the [definitions guide](definitions.md) explains examples.

This is not a promise that the pre-release format is frozen. A standards-aligned
redesign is proposed, not implemented. Names such as `items`, `initialValue`, and
`applicableWhen` from that proposal are not current authoring properties.

## Which document are you editing?

| Document | Meaning | Current discriminator |
|---|---|---|
| Maintainer source | Public YAML/JSON authored beside application code | `$schema: urn:islamu:configurator:authoring:v1` |
| Compiled runtime definition | Public canonical JSON distributed to consumers | `schemaVersion: 1`; no `$schema` member |
| Answer workspace | Private local values keyed by admitted root field ID | Neither definition discriminator |
| Application JSON Schema | A target application's existing validation schema | Separate input to bounded scaffolding, not a Configurator definition |
| Editor schema | Structural description of a Configurator source/runtime document | The generated files in the [schema catalog](../../schemas/README.md) |

Do not add an editor `$schema` URL to compiled runtime JSON. Use editor
associations instead. Structural editor validation does not enforce every
cross-reference, privacy, output, or resource rule.

## Mandatory and optional source properties

The source root is closed: unknown properties reject. Omission and explicit
`null` are not interchangeable.

| Property | Required? | Meaning or omission behavior |
|---|---|---|
| `$schema` | Yes | Exact authoring discriminator above |
| `identity` | Yes | Nonempty `familyId` UUID and positive integer `revision`; initialization generates identity |
| `project` | Yes | Requires `id` and `name`; `publisher` defaults to `name` |
| `applicationVersions` | Yes | Nonempty set of exact version strings, not version ranges |
| `fields` | Yes | Keyed map of root fields; an empty map is permitted |
| `outputs` | Yes | Nonempty keyed map of target filenames |
| `entryPoints` | No | Defaults to `["setup"]` |
| `locales` | No | Defaults to `["en"]`; declaration does not translate field text |
| `presentation` | No | Authored sections and experiences; absence permits a neutral generated presentation |

Optional project metadata is `publisher`, `description`, `repository`, and
`definitionLicense`. A repository URL is public, credential-free HTTPS.
Store/distribution requirements are separate from local admission.

Maintain source identity with `definition init` and `definition revise`; do not
copy another project's family ID. The compiled definition materializes required
runtime properties that source authors may omit. Build normally writes
`dist/configurator/revisions/<revision>.json`.

## Field properties

Source keys identify root logical fields; nested `properties` are keyed maps too.
Compilation assigns runtime IDs. Root keys that do not fit the identifier
grammar receive deterministic IDs; nested property names must already start with
an ASCII letter and contain only ASCII letters, digits, dots, hyphens, and
underscores, within 128 characters. Dots are literal characters, not paths.
Case/normalization collisions reject.

| Property | Required? | Meaning or restriction |
|---|---|---|
| `type` | Yes | `string`, `boolean`, `integer`, `number`, `enum`, `list`, or `object` |
| `title` | No | Humanized key when omitted |
| `required` | No | False when omitted; evaluated only while active |
| `sensitivity` | No | `sensitive` when omitted; other values are `public` and `secret` |
| `disclosure` | No | `guided` when omitted; other values are `essential` and `advanced` |
| `help`, `consequence` | No | Public inert explanations; neither makes a field required |
| `group` | No | Public grouping label; authoring presentation may select that label |
| `format` | No | Descriptive metadata, not an asserted validator |
| `validator` | No | One of the closed validators listed below |
| `default`, `recommended` | No | Only in allowed public contexts; must satisfy value constraints |
| `choices` | For `enum` | Nonempty distinct string values; rejected on other types |
| `itemType` | For `list` | `string`, `boolean`, `integer`, `number`, or `object` |
| `properties` | For object/object-list | Nested field definitions; rejected on other types |
| `uniqueBy` | No | Object-list path ending at a required string property |
| `minLength`, `maxLength` | No | String/enum length bounds |
| `minimum`, `maximum` | No | Integer/number inclusive bounds |
| `minItems`, `maxItems` | No | List cardinality bounds |
| `activeWhen`, `requiredWhen` | No | Root-only conditions |
| `guidance` | No | Instructions, documentation links, and search terms |
| `credential` | No | Required `kind` and `acquisition` inside an optional descriptor |

Unknown field members reject. Supplying `help: null`, for example, is not the
same as omitting help even if an editor's structural schema accepts it.

### Sensitivity and public metadata

Definitions, help, links, choices, and declared defaults are public material;
never put actual private answers in them.

- `public`: public defaults/recommendations are possible after deliberate review.
- `sensitive`: values are protected; defaults/recommendations reject.
- `secret`: values additionally require browser workspace consent; credential
  descriptors require an effective secret context.

A private ancestor prevents a public child's default. Protected descendants
raise a container's effective sensitivity. Do not lower classification merely
to admit a convenient default.

Every answer-derived artifact remains restricted, even if every participating
field is public. Browser copy/download is deliberate export, not native
protected-save certification.

## Presence, defaults, and requiredness

Requiredness, applicability, and disclosure are different concepts.
`essential` means prominent in presentation, not mandatory for validity.

| Answer state | Current meaning |
|---|---|
| Absent root, no default | Omitted; invalid only if active and required |
| Absent root with default | Core uses the declared default as the effective value |
| Root cleared after an edit | A declared root default becomes effective again; clearing cannot suppress it |
| Absent nested property | Core does not recursively insert its default during validation |
| Interactive object/row creation | Materializes declared nested defaults and required containers, not invented required scalar answers |
| Present `null` | Not a valid field value; current field types are not nullable |
| Present empty/whitespace string | Counts as present for conditions; required strings reject it |
| Present `false` or `0` | Supplied values, not missing |
| Present `[]` or `{}` | Supplied containers; item/property constraints still apply |
| Absent optional object | Descendant required properties do not independently require the absent container |
| Inactive root | Excluded from effective validation/output, even if raw workspace values remain |

Root fallback and nested creation-time initialization are different current
behaviors, not a promise of uniform recursive defaulting. `recommended` is an
advisory typed value and is not automatically substituted by Core.

Integers use signed 64-bit representation; numbers use exact bounded decimal
representation. String lengths use UTF-16 code units. These are important
differences from unrestricted application JSON Schema.

### Conditions

Operations are `present`, `equals`, `notEquals`, `in`, `all`, `any`, and `not`.
Simple conditions reference declared root fields. Compound conditions use
`operands`; `not` takes one operand and `all`/`any` at least two.

`present` means an effective non-null value exists. It includes defaults and
empty strings and does not report answer provenance. If a value is absent,
`equals`, `notEquals`, and `in` all return false; use explicit Boolean
composition when absence matters. In draft preparation, unresolved secret
dependencies remain unknown rather than becoming guessed true/false values.

Nested conditions, unknown references, and cycles reject. Both activation and
conditional-requiredness references participate in cycle rejection.

### Validators

| Name | Current check |
|---|---|
| `none` | No additional check |
| `hostname` | DNS hostname accepted by the bounded runtime check; not an IP-address validator |
| `absolute-uri` | Absolute HTTP(S) URI without embedded user information, not every URI scheme |
| `email-address` | Bounded lexical email check; no mailbox or target verification |
| `port-number` | Integer from 1 through 65535 |
| `positive-integer` | Integer greater than zero |

Choose a validator compatible with the field type. Current admission does not
reject every incompatible combination; such a definition can fail only when
answers are validated. Field `format` does not activate these checks.

## Collections and supported shapes

- Objects have declared, closed properties; unknown answer properties reject.
- Lists have one `itemType`. Scalar items cannot carry their own enum, format,
  numeric, or length constraints through a recursive item schema.
- Object-list `properties` describe each row. `uniqueBy` follows required
  object properties to a string; comparison is ordinal and diagnostics exclude
  the duplicate value.
- Enum choices are strings, without independent option labels/descriptions.
- Nullable unions, typed open maps, tuple arrays, and general alternative
  schemas are not supported field shapes.

## Presentation and guidance

Authored presentation requires `defaultExperience`, `sections`, and
`experiences`. Every root occurs once across nonempty sections; each experience
lists every section once. Section/experience identifiers use bounded lowercase
ASCII kebab-case. Sections and experiences have titles and optional descriptions.
Only roots are laid out independently.

Source sections may select logical `fields` and/or existing `groups`.
Compilation expands groups once into runtime root references. Explicit field
order is preserved; group members and unconfigured source maps have ordinal
ordering. No executable selectors or answer presets exist.

Disclosure is `essential`, `guided`, or `advanced`. Navigation is `guided` or
`sections`. Changing presentation must not change configuration validity.

Guidance accepts optional `instructions` (4096 characters), up to four `links`,
and up to sixteen distinct `searchTerms`. Metadata text rejects control
characters and angle brackets. Instructions also reject backticks, Markdown
link syntax, and template markers. Consequently, multiline YAML syntax does
not make multiline guidance metadata valid.

Links require public credential-free HTTPS and reject query/fragment
delimiters. They are inert metadata: no answer expansion, automatic fetching,
prefetching, or execution. Consumers open links deliberately.

Credential combinations:

| Kind | Acquisition |
|---|---|
| `password` | `existing`, `create`, `issuer` |
| `opaque-token` | `existing`, `issuer` |
| `symmetric-key` | `existing`, `create` |
| `private-key` | `existing`, `issuer` |
| `connection-string` | `existing`, `issuer` |

The descriptor requires a secret string context and grants no generation,
authentication, or network authority.

## Client capabilities

This table records inspected consumer behavior, not newly executed runtime QA.
Shared validity does not imply identical presentation.

| Capability | Current support boundary |
|---|---|
| Admission, readiness, activation, and preparation | Shared Core semantics |
| Authored sections, ordering, guided navigation | Browser consumes them; inspected Desktop/Terminal forms use flatter root-order projections |
| Rich guidance and deliberate documentation links | Browser renders them; ordinary help/consequence is the portable fallback |
| Apply recommendation | Explicit Desktop action; no equivalent action in the inspected Browser/Terminal field controls |
| Detailed controller/correction navigation | Browser has richer navigation; native feedback differs |
| Temporary under-minimum lists | Shared editing can permit them; Desktop disables removal at the declared minimum |
| Definition translation | `locales` alone does not translate scalar titles/help; client chrome localization is separate |
| Machine catalogue | Structural metadata only; not a complete form schema or evaluated requiredness model |

Put indispensable instructions in ordinary help until the chosen clients'
capabilities are verified. Declared choices are public metadata; masking values
does not imply that all clients hide the same choice controls.

## Outputs and import

Source outputs are keyed by sanitized filename hints and require `format`.
Optional properties are `id`, `applySemantics`, `restart`, and either `bindings`
or `projection`. Omitted apply semantics is `generationOnly`; restart defaults
to `none`, without proving hot reload or target acceptance.

Bindings map target keys to declared logical root fields. JSON/YAML shorthand
copies literal root keys, including dots; it does not infer nesting.
Dotenv bindings require compatible scalar fields and environment names.
Filenames do not grant filesystem authority.

Projection operations are `literal`, `copy`, `omit`, `index`, `object`, `list`,
and `repeat`, with optional root-condition `when`. A copy references a root;
`$item` copies the current whole repeated item and `index` emits its zero-based
index. Nested repeats and row-relative property selectors are not admitted.

| Format | Required projected shape |
|---|---|
| `dotenv` | Object of non-null scalar values |
| `json` | Bounded JSON value |
| `yaml` | Bounded YAML serialization of the projected value |
| `markdown` | List of strings, joined with formatting/link-sensitive characters escaped |

Admission does not prove every format-specific shape. Answer validation,
successful composition, native publication, and target acceptance are separate
outcomes. A complete active-output bundle skips intentionally omitted outputs;
explicit selection of an omitted output remains an error.

Existing-file import is narrower than generation: JSON/YAML/dotenv support an
unconditional root-object mapping of direct copies with no ambiguous repeated
field mapping. Other projections can be non-importable. Conflicts with existing
nonreplaceable answers reject; partial prefill does not imply complete readiness.

Secret-deferred drafts defer only missing secret leaves, retain completion
locations, and withhold unresolved secret-dependent outputs. Ordinary missing
or malformed values still block. A prepared draft is not a complete artifact.

## JSON Schema import

Import scaffolds public source from supplied bounded documents. It is not a
claim that Configurator is a general Draft 2020-12 validator.

| Input concept | Current disposition |
|---|---|
| Explicit scalar types and string enums | Supported within native numeric/string domains |
| Closed objects and local required sets | Supported; explicit `additionalProperties: false` required |
| Homogeneous arrays | Supported only where item constraints fit the current `itemType` representation |
| Inclusive numeric, length, item bounds | Supported with the stated semantic differences |
| Supplied local `$defs`/`$ref` | Bounded expansion; remote/dynamic/cyclic/resource-rescoping cases reject |
| `title`, `description` | Selected bounded text retained; line breaks/tabs normalized |
| `writeOnly` | Raises secret classification, including referenced/root annotations |
| `default` | Discarded, not automatically restored as public |
| `format` | Explanatory help, not asserted validation |
| `examples`, `readOnly`, `deprecated` | Accepted annotations do not become equivalent native interaction/validation behavior |
| Nullable unions, open maps, general combinators/conditionals, regex, unsupported item assertions | Rejected, not bypassed by acknowledgment |

The acknowledgment flag permits only encountered required-string-whitespace,
UTF-16-length, and bounded-number differences. It does not waive unsupported
assertions or privacy rules. Review generated fields before adding public
defaults or claiming target-validator equivalence.

## Bounds and verification limits

Current source ceilings include 4 MiB aggregate bytes, depth 32, 4096 map/list
entries, 65536 nodes, 65536 scalar characters, and 1 MiB aggregate scalar
characters. Definitions additionally cap roots at 1024 and outputs at 64;
presentation caps sections at 64 and experiences at 16. Schema import has its
own stricter document, reference, and expansion limits.

YAML accepts a bounded JSON-shaped data dialect. Anchors, aliases, tags,
directives, merge keys, multiple documents, and ambiguous non-string keys reject.
Quote strings that resemble typed values.

For mechanisms and exact API contracts, see the [Core definition contract](../internal/CORE_DEFINITION_CONTRACT.md)
and [maintainer authoring contract](../internal/MAINTAINER_AUTHORING.md).
Current inspection limits and proposed improvements are retained in the
[definition-standard audit](../internal/reports/DEFINITION_STANDARD_AUDIT_2026-10-28.md).
