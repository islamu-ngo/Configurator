---
description: "Current bounded-v2 definition grammar, explicit answer states, output mappings, and qualification limits."
---

# Current Definition Contract Reference

> **Audience:** Adopters | Integrators
> **Status:** Implemented pre-release v2 base; scenario and client qualification remain separate
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `docs/internal/CORE_DEFINITION_CONTRACT.md`, `docs/internal/MAINTAINER_AUTHORING.md`

Use this reference for the implemented base contract and the
[integration guide](integrate-your-tool.md) for the maintainer workflow.
The [Definition Standard v2](definition-standard.md) remains Draft: its broader
selected design is not a claim that all scenarios or clients are qualified.
There is no v1 fallback or implicit migration.

## Which document are you editing?

| Document | Current contract |
|---|---|
| Public maintainer YAML/JSON | `$schema: urn:islamu:configurator:authoring:v2` |
| Public canonical runtime JSON | `schemaVersion: 2`, `profile: bounded-v2`, computed sorted `requiredCapabilities`; no `$schema` |
| Private answer JSON | `answerContractVersion: 1`, `values` configuration tree, `omitted` concrete pointers |
| CLI machine result | Command envelope v2; authoring/runtime handshake reports `authoringContractVersion: 2` and `runtimeSchemaVersion: 2` |
| Application JSON Schema | Separate bounded scaffolding input, not a Configurator runtime definition |
| Editor meta-schema | Generated structural assistance in the [schema catalog](../../schemas/README.md), not semantic admission |

Definition revision, application version, answer version and command version are
independent. Do not add an editor `$schema` URL to runtime JSON; use editor
associations. Runtime admission recomputes capabilities and rejects a mismatching
declaration. An author cannot declare unsupported features into existence.

## Mandatory and optional source properties

Source is a closed object. Unknown members reject; explicit null is not omission.

| Property | Requirement and omission behavior |
|---|---|
| `$schema` | Required exact authoring discriminator |
| `identity` | Required nonempty family UUID and positive `revision` |
| `project` | Required `id` and `name`; `publisher` defaults to name |
| `applicationVersions` | Required nonempty exact version strings, not ranges |
| `configuration` | Required non-null object schema |
| `outputs` | Required nonempty filename-keyed map |
| `entryPoints` | Optional; defaults to `["setup"]` |
| `presentation` | Optional ordered sections/journeys |
| `localization` | Optional base locale and translations; absent means locale `en` |
| `annotations` | Optional bounded inert object |

Project may also carry `description`, credential-free public HTTPS `repository`,
and `definitionLicense`. Store admission is separate from local use.
Initialize identity with `definition init`, preserve the family across revisions,
and use `definition revise` for changed canonical content. Build normally writes
`dist/configurator/revisions/<revision>.json`.

## Field properties

The configuration tree replaces the former `fields`/`itemType` vocabulary.
`configuration.properties` holds exact root data names; object `properties` and
array `items` recursively use the same admitted schema grammar.

| Member | Implemented base rule |
|---|---|
| `type` | `string`, `boolean`, `integer`, `number`, `object`, `array`; nullable form is exactly one base type plus `"null"` |
| `title`, `description` | Optional bounded inert metadata |
| `enum` | Distinct homogeneous typed scalar alternatives; nullable types may additionally include null |
| `const` | Compatible scalar assertion |
| `properties`, `required` | Declared object children and local presence requirements |
| `additionalProperties` | Explicitly `false` or a recursive value schema; untyped `true` rejects |
| `items` | Required recursive schema for arrays |
| `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf` | Exact inclusive/exclusive numeric bounds and positive exact multiple |
| `minLength`, `maxLength` | String Unicode-scalar bounds |
| `minItems`, `maxItems`, `uniqueItems` | Array cardinality and structural value uniqueness |
| `minProperties`, `maxProperties` | Object cardinality bounds |
| `default` | Descriptive public metadata only; never inserted |
| `format` | String annotation unless explicitly enabled by `x-configurator.assertFormat`; never a network check |
| `$defs`, `$ref` | Bounded acyclic local schema references rooted at `configuration`, beginning `#/$defs/`; no remote resolution |
| `oneOf` | 2-16 closed object branches sharing exactly one eligible required scalar discriminator with distinct const values |
| `readOnly`, `writeOnly`, `deprecated`, `examples` | Bounded annotations; `writeOnly: true` raises the secret floor |
| `x-configurator` | Closed extension described below |
| `if`/`then`/`else`, `dependentRequired` | Local object relationships described under Conditions |

The extension admits `sensitivity` (`public`, `sensitive`, `secret`), `initialValue`,
`suggestedValue`, string `notBlank`, `detail` (`basic`, `standard`, `advanced`),
`guidance`, `credential`, typed enum `choices`, `units`, array-object `uniqueBy`,
property `applicableWhen`, and string-only `assertFormat`. Missing sensitivity is conservatively
sensitive; missing detail projects as standard. `choices` pairs each enum value
with a label and optional explanation. `uniqueBy` is a concrete item-relative
pointer through required object properties to a non-null string.

Unknown members, incompatible constraints and invalid public literal metadata
reject. Required property presence does not itself require nonblank text;
use `minLength` or `x-configurator.notBlank` deliberately.

### Exact names and references

`SetupFieldDefinition.Name` is the exact root data key. Its bounded metadata
`Id` is not an answer key or target path. Representable names retain their ID
except the reserved `f-` namespace; other names, including names starting `f-`,
receive `f-` plus a full SHA-256 over the domain-separated escaped root pointer.
No NFC normalization or case folding changes data identity.

References are closed `{scope: root|item, path: <RFC6901 pointer>}` objects.
Empty pointer selects the context root; `/` selects the empty-name property.
Escape `~` as `~0` and `/` as `~1`; decode once. Dots and asterisks are literal
characters. Array indices are canonical nonnegative decimal tokens without
leading zeroes; `-` is not an existing item. Item scope requires an admitted
repeat context and never means an implicit global search.

Names preserve case, empty names, Unicode composition and punctuation.
`A`, `a`, `a.b`, and a nested `/a/b` are different locations. Exact duplicate
members, invalid UTF-8, unpaired surrogates and malformed pointer escapes reject.

### Sensitivity and public metadata

Definitions, examples, titles and choices are public. Never embed private answers.
Private ancestors prevent public descendants from admitting private literal
metadata, and protected descendants raise a container's effective sensitivity.
Public `default`, `initialValue`, `suggestedValue`, examples and const metadata
must satisfy the applicable privacy and value rules. Do not lower sensitivity
to make initialization convenient. Credential intent never lowers sensitivity.

Every answer-derived artifact is restricted even when all fields are public.
Browser answers remain transient; secret entry needs per-workspace consent.
Deliberate browser export is not native protected-save certification.

## Presence, defaults, and requiredness

The private envelope has exactly `answerContractVersion: 1`, object `values`,
and array `omitted`. Values use exact data names, not metadata IDs. Omission
pointers are distinct concrete locations; supplied/suppressed conflicts and
overlapping suppressions reject atomically.

| State at an applicable location | Effective result |
|---|---|
| Supplied value, including false, zero, empty string/container | Preserve and validate |
| Supplied null | Present only under a nullable schema |
| Explicit omission | Missing; suppress initialization |
| Never supplied with eligible `initialValue` | Use admitted initialization |
| Never supplied without initialization | Missing |
| Reset | Remove override and omission; initialization becomes eligible again |
| `suggestedValue` | No change until deliberately adopted |
| Standard `default` | No inserted value |
| Absent container with an initialized child | Container stays absent |
| Present container with never-supplied children | Resolve eligible child initialization recursively |
| Inapplicable root property | Exclude from effective validation and output |

Required descendants do not instantiate an absent optional object. Null is
different from omission. Required empty strings are valid unless a separate
assertion rejects them. Core owns effective-value resolution for validation,
drafts and composition; clients must not invent required containers or answers.

Integers use signed Int64. Numbers use exact bounded decimals (at most 28
significant digits after redundant zero removal, scale at most 28, explicit
exponents -28 through 28). Out-of-domain values reject without rounding.
String length counts Unicode scalars, not UTF-16 code units.

### Conditions

`x-configurator.applicableWhen` is supported on declared properties, including
properties within array rows, not the configuration root. Predicates use `exists`,
`equals`, `notEquals`, `in`, `all`, `any`, `not`; leaf predicates use scoped
root references, including declared nested paths, or `item` references within
an array row. An item reference resolves only against its current row.
Comparisons use typed values;
`in` uses nonempty `values`. Compounds use nonempty `operands`; `not` takes one.
Unknown references and applicability cycles reject.

Missing controllers make leaf predicates false; valid present null counts as
existing. Invalid or secret-deferred controllers propagate unknown, never a
guessed active branch. Boolean composition preserves unknown unless a decisive
false (`all`) or true (`any`) settles it.

At every object, `if: {required: [controller]}` with
`then: {required: [dependent]}`, optional `else: {required: [...]}`, and
`dependentRequired` are supported. The guard may also declare
`properties: {controller: {const: value}}` or a nonempty typed `enum`.
As in JSON Schema, a guard property absent from the object does not fail its
property assertion; use guard `required` when absence must select the else branch.
Names must identify locally declared properties. These assertions
validate relationships, not visibility; mutual dependencies are permitted.
They do not add activation edges. Arbitrary conditional subschemas remain outside
this bounded subset. Recursive maps, discriminated variants, exact numeric
assertions and unique items use the same validation owner.

Definition parsing also retains bounded public schema positions with numeric
source spans. Errors identify a known enclosing schema position, not an unknown
assertion key, raw scalar, reference value or filesystem path. Literal values
and private answer trees are not indexed. Source positions are definition-only
metadata and do not change canonical identity. Client correction coordinates
separately distinguish declared properties, row indices and map-entry ordinals
and are bound to the current answer revision; client generation checks remain
necessary.

## Presentation and guidance

Presentation uses `defaultJourney`, optional `navigation` (`wizard`/`sections`),
`sections` with concrete `pointers`, and `journeys` with ordered section IDs.
IDs are bounded lowercase kebab-case; titles and optional descriptions are inert.
Layout/detail never changes validity or grants presets. Consumers must keep
applicable required/error work reachable; a metadata declaration is not proof
that every client has qualified that experience.

`x-configurator.guidance` accepts optional instructions (4096 characters), up to
four static HTTPS links and sixteen distinct search terms. Instructions support
plain-text multiline technical punctuation, not executable markup or templates.
Links are credential-free, have no query, may include fragments, and are opened
deliberately without answer interpolation or automatic fetching.

Credential descriptors require an effective secret string context:

| Kind | Acquisition |
|---|---|
| `password` | `existing`, `create`, `issuer` |
| `opaque-token` | `existing`, `issuer` |
| `symmetric-key` | `existing`, `create` |
| `private-key` | `existing`, `issuer` |
| `connection-string` | `existing`, `issuer` |

Descriptors do not generate or authenticate credentials. Localization currently
admits a `defaultLocale`, translations, direction and pointer-keyed overrides
of existing public text. Broader localization policy and cross-client behavior
remain later qualification, not implied translation coverage.

## Client capabilities

Admission computes `requiredCapabilities` from actual schema, value, predicate,
relationship and projection features. Canonical runtime JSON records the sorted
set; unsupported mandatory semantics must be refused before collecting answers.
Optional layout can degrade only to reachable neutral presentation.

The implemented Core base does not qualify every CLI, Browser, Desktop or Terminal
scenario, accessibility path, package or physical host. Keep indispensable
guidance in plain text and qualify the clients selected for your integration.
Machine command v3 is later work; command v2 remains current.

## Outputs and import

Outputs require `format`: `dotenv`, `json`, `yaml`, or `markdown`. Optional
`targetUpdateMode` is `unspecified`, `create`, `merge`, `replace`, `import`,
`reconcile`, `generationOnly`; `restartRequirement` is `unspecified`, `none`,
`process`, `deployment`. Both default to `unspecified`. They are advice, not
target acceptance, apply permission or filesystem authority.

Choose `bindings` or `projection`, never both. Dotenv binding keys are literal
environment names; values are scoped references to compatible non-null scalars.
JSON/YAML binding keys are concrete target pointers; `/server/port` constructs
that target object path, whereas `/server.port` uses one dotted key. Overlapping
target pointers reject. Without mappings, JSON/YAML copy the effective tree;
dotenv and Markdown need explicit mappings/projections.

Projection operations are `literal`, `copy`, `omit`, `index`, `object`, `list`,
`repeat`. Copies/repeats use scoped references; item scope exists only inside
repeat. Output bounds remain in force. Dotenv requires non-null scalar members;
Markdown requires a list of non-null strings. Admission checks the declared
result shapes before collecting answers: incompatible scalar/object/list
Markdown projections reject, and nullable repeat sources reject because a valid
null is not iterable. Item-scoped `when` uses the same typed predicate kernel
inside repeats. Explicit omissions remain omissions, not null values.
Successful admission is not proof of every runtime byte limit or target's own acceptance.

Existing-file import is narrower than generation: supported unconditional,
unambiguous root/nested direct-copy mappings can prefill answers. Nested object
targets reconstruct exact source object paths; whole typed-map copies retain
declared dynamic entries. Dotenv scalar text recovers its declared source type.
Transforms, conditional projections, repeated/index ambiguity, overlapping source
mappings, unknown input and nonreplaceable conflicts reject atomically.
Provenance-owned default replacement does not reset explicit omissions; reset
those deliberately before importing. Partial prefill is not complete readiness.

Drafts defer genuinely missing secret leaves, never malformed supplied values.
Unresolved secret-dependent outputs are withheld; completion locations identify
remaining work. A missing secret used by an item predicate remains unknown, not
false; independent row copies can still be prepared. These concrete deferred
locations are transient Core control metadata, never another stored answer tree.
Draft output is not a complete accepted artifact.

## JSON Schema import

Scaffolding accepts recursive constrained items, typed maps, nullable scalars,
typed enums, closed discriminated branches and the corresponding numeric,
length, cardinality and uniqueness assertions. Supplied local reference
expansion remains offline and bounded; remote/dynamic/cyclic references and
resource rescoping reject.

The result and refusal expose immutable `Transformations`, replacing difference
strings. Each entry has a classification (retained, converted, advisory,
discarded or unsupported), a stable code, public schema pointer and document
ordinal, never a filesystem path or raw scalar. Defaults/examples are discarded
and reported; format/readOnly/deprecated remain advisory annotations, not
initialization or permissions. Collected leaves remain sensitive unless secret.
Structural containers and supplied public constants are explicitly classified
public with conversion records; writeOnly ancestry never lowers, and protected
descendants still raise effective classification.

A standalone null schema becomes a nullable scalar restricted to enum `[null]`,
with a conversion record. Numeric-domain differences and the profile's nullable
provider domain require `--accept-bounded-semantics`: the profile admits null
outside branch selection, unlike a JSON Schema oneOf of object-only branches.
General unions/composition, regex and unsupported assertions reject even with
acknowledgment. Compilation is not target-validator equivalence.

## Bounds and verification limits

Source ceilings remain 4 MiB aggregate bytes, depth 32, 4096 entries per container,
65536 nodes, 65536 scalar characters and 1 MiB aggregate scalar characters.
Definitions cap root properties at 1024 and outputs at 64; presentation caps
sections at 64 and journeys at 16. Import has stricter reference/expansion limits.
YAML is bounded JSON-shaped data: tags, anchors, aliases, directives, merge keys,
multiple documents and non-string resolved keys reject.

The parent reports example source/runtime agreement **3/3** for the fresh v2
families at revision 1. This documentation unit did not rerun that check.
It proves neither target acceptance nor complete scenario/client qualification.
See the [Core contract](../internal/CORE_DEFINITION_CONTRACT.md) and
[maintainer contract](../internal/MAINTAINER_AUTHORING.md) for mechanisms.
