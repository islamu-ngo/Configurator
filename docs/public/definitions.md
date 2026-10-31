---
description: "Understand bounded definitions, exact names, answer presence, and local configuration preparation."
---

# Configuration Definitions

> **Audience:** Adopters | Integrators
> **Kind:** Guide
> **Status:** Current bounded-v2 explanation; platform and target qualification are separate
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `src/Configurator.Core/Artifacts/DefinitionArtifactComposer.cs`, `src/Configurator.Cli/SetupCliDefinitionHandlers.cs`

A definition describes configuration data, local validation, presentation, and
output mappings. Maintain one public YAML or JSON authoring source; compile it to
an admitted runtime definition before collecting private answers. Configurator
prepares candidates offline. The target application independently decides whether
those candidates are valid, authorized, and safe to apply.

This page describes the implemented **bounded-v2** profile, not full JSON Schema.
Unknown keywords and unsupported shapes reject rather than being silently
ignored. There is no v1 definition reader, migration interpreter, legacy alias,
executable plugin, network validator, or target administration capability.

Use [Integrate Your Tool](integrate-your-tool.md) for the maintainer workflow,
the [current reference](definition-reference.md) for property and capability
lookup, and the [definition standard](definition-standard.md) for portability
and conformance obligations. Complete sources live in the
[minimal example](../../examples/minimal-self-hosted/README.md) and
[Grafana example](../../examples/grafana-13.2.2/README.md); this page deliberately
does not duplicate their full configurations.

## Anatomy of a Definition

Four separate documents have different custody and version rules:

| Document | Discriminator and purpose |
|---|---|
| Public maintainer source | `$schema: urn:islamu:configurator:authoring:v2`; compact YAML/JSON with identity, schema, and filename-keyed outputs |
| Public runtime definition | `schemaVersion: 2`, `profile: bounded-v2`, computed `requiredCapabilities`; canonical admitted JSON |
| Private answer workspace | `answerContractVersion: 1`, `values`, `omitted`; exact configuration names and concrete omission pointers |
| Prepared artifact | Declared dotenv, JSON, YAML, or Markdown candidate; restricted answer-derived content |

Definition revision, application version, source contract, runtime contract,
private answer contract, and CLI machine contract are independent versions.
The [version reference](definition-reference.md#which-document-are-you-editing)
owns their current discriminators. A new command envelope does not change the
definition format.

### Mandatory and optional authoring properties

Source objects are closed. Omission invokes only the defaults listed here;
explicit `null` is not a way to omit required metadata.

| Property | Requirement and meaning |
|---|---|
| `$schema` | Required exact authoring v2 discriminator |
| `identity.familyId` | Required nonempty UUID in hyphenated `D` form |
| `identity.revision` | Required positive signed 64-bit integer |
| `project.id`, `project.name` | Required nonblank public text, each at most 256 characters |
| `project.publisher` | Optional; compiler uses `project.name` when absent |
| `project.description`, `project.definitionLicense` | Optional public text, at most 4,096 and 256 characters respectively |
| `project.repository` | Optional credential-free HTTPS URL; no query or fragment |
| `applicationVersions` | Required nonempty list of distinct exact strings; no range matching |
| `entryPoints` | Optional; compiler supplies `["setup"]` |
| `configuration` | Required non-null object schema, recursively described below |
| `outputs` | Required filename-keyed map containing 1-64 outputs |
| `presentation` | Optional sections, journeys, default journey, and navigation |
| `localization` | Optional default locale and public text translations; absence means `en` |
| `annotations` | Optional bounded inert object; not validation or execution instructions |

Version, entry-point, and locale lists are bounded to 128 distinct nonblank
strings of at most 128 characters. Public text metadata rejects control
characters and angle brackets; multiline guidance has its own rules below.

## Identity and Integrity Pinning

Preserve a family across revisions of the same definition. A family UUID or
publisher string identifies a claim; neither authenticates an author nor proves
curation, licensing, or target endorsement.

Canonicalization sorts object names ordinally, preserves array order and exact
Unicode strings, and normalizes admitted numbers and JSON serialization.
Formatting-only JSON/YAML differences can therefore produce identical canonical
runtime bytes. SHA-256 binds those bytes. Changing public metadata or output
mappings can change identity just as changing assertions can.

Different canonical content under the same family/revision is a conflict.
Application-version selection uses exact strings; a requested digest must match.
Without a version or digest, multiple available revisions are ambiguous rather
than an implicit upgrade. Session pinning and stale-operation rejection are
described in the [shared-state contract](../internal/PRESENTATION_STATE.md).
Do not replace an active workspace's admitted definition with newly fetched
content.

## Exact Names, Paths, and Value Domains

`configuration.properties` contains **target data names**, not editor field IDs.
Empty names, dots, slashes, tildes, `*`, case-distinct names, and
Unicode-normalization-distinct names can be data. Exact duplicate decoded names
reject. `A` and `a` are different; no case folding or NFC normalization occurs.
Quote unusual YAML keys to make their intended string spelling explicit.

References use `{scope, path}`:

| Pointer | Meaning |
|---|---|
| `""` | The whole selected scope |
| `/` | Its property with the empty name |
| `/service.host` | One property literally named `service.host` |
| `/service/host` | Nested `host` inside `service` |
| `/a~1b` | Property `a/b` |
| `/~0name` | Property `~name` |

Only `~0` and `~1` are escapes. There is no dotted-path expansion, wildcard,
URI-fragment decoding, percent decoding, or array append token. Escape once:
`/~01` names `~1`, not `/`. A token is an array index only while traversing an
array: canonical nonnegative decimal digits, no leading zero except `0`, within
signed 32-bit range. In an object, `"0"` remains an ordinary property name.
Pointers have at most 32 segments and 65,536 UTF-16 code units in total.

`scope: root` selects the effective configuration; `scope: item` selects the
current array item in an admitted item context, never another row. Admission
resolves references through declared object properties and array item schemas.
It does not provide general traversal through dynamic map names or branch-only
properties. Copy the whole map/branch-bearing object when a static child
reference cannot be admitted.

### Numbers and Unicode

`integer` values range from -9,223,372,036,854,775,808 through
9,223,372,036,854,775,807 (signed 64-bit). The shared scalar editor parses this
same range; there is no smaller 32-bit editor domain in the shared parser.
`number` values and numeric constraints use the bounded exact decimal domain,
not binary floating point or
unbounded JSON numbers. Canonical admission requires decimal representability,
at most 28 significant digits after trimming leading/trailing zeroes from the
coefficient, an explicit exponent between -28 and 28, and no nonzero precision
beyond scale 28 after applying that exponent. Overflow, underflow, rounding,
NaN, and infinity reject. Negative zero canonicalizes to `0`; exponent spelling
does not survive canonicalization.

UTF-8 must be valid; unpaired UTF-16 surrogates reject. String `minLength` and
`maxLength` count Unicode scalar values, so a supplementary character counts as
one. Resource and metadata character ceilings use UTF-16 code units, not grapheme
clusters. Neither length rule promises what a human perceives as one character.

### Resource ceilings

These are ceilings, not recommended source sizes. Syntax, canonicalization,
expanded references, validation, and projection each charge their relevant
budgets; a source below the byte ceiling can still reject on expansion or depth.

| Resource | Maximum |
|---|---:|
| Source/canonical document bytes | 4,194,304 |
| YAML documents | 1 |
| Parser events | 131,072 |
| Normalized/expanded nodes | 65,536 |
| Nesting depth | 32 |
| Entries per mapping or sequence | 4,096 |
| One scalar | 65,536 UTF-16 code units |
| Aggregate scalar characters | 1,048,576 |
| Declared root properties; definitions per `$defs` object | 1,024 each |
| Active local-reference chain | 32, also subject to depth/node limits |
| Discriminated branches | 2-16 |
| Scalar enum alternatives; examples | 256 each |
| Artifacts | 64 |

YAML is a bounded JSON-shaped data syntax: anchors, aliases, tags, merge/expansion
authority, and multiple documents are not a configuration language.

## Declaring Fields and Validation

Every concrete schema node has an explicit base `type`: `string`, `boolean`,
`integer`, `number`, `object`, or `array`. An `enum` is an assertion on one of
these types, not a seventh type. For nullable values, use exactly the base type
and `"null"` in a two-member type array, in either order. Null-only `type`,
arbitrary unions, boolean schemas, and arbitrary combinators are not admitted.

### Standard keyword support

| Keywords | Implemented meaning |
|---|---|
| `type` | Base type and optional null, as above |
| `enum`, `const` | Typed scalar assertions; no object/array choices or constants |
| `properties`, `required` | Recursive object members and local presence obligations |
| `additionalProperties` | Explicit `false` for a closed object, or one recursive schema for dynamic values; untyped `true` rejects |
| `items` | One required recursive schema for every array item; no tuples |
| `minimum`, `maximum` | Inclusive exact numeric bounds |
| `exclusiveMinimum`, `exclusiveMaximum` | Strict exact numeric bounds |
| `multipleOf` | Positive exact decimal divisor; no tolerance |
| `minLength`, `maxLength` | Unicode-scalar string bounds |
| `minItems`, `maxItems`, `uniqueItems` | Array cardinality and structural value uniqueness |
| `minProperties`, `maxProperties` | Effective object cardinality |
| `$defs`, `$ref` | Bounded acyclic local reuse, not external retrieval |
| `oneOf` | Restricted discriminated object branches, not general alternatives |
| `if`, `then`, `else`, `dependentRequired` | Restricted local requiredness, described below |
| `title`, `description` | Public display annotations |
| `format` | String annotation unless explicitly asserted |
| `default`, `examples` | Validated public descriptive values; never effective initialization |
| `readOnly`, `deprecated` | Annotations; not write prevention, authorization, or omission |
| `writeOnly` | Secret sensitivity floor; not an instruction to remove a value from its declared output |

Constraints reject on the wrong type, contradictory bounds, or invalid value
domains. Cardinality/length bounds are nonnegative integers up to 2,147,483,647,
still subordinate to resource limits. Enum choices must be distinct homogeneous
typed scalars; a nullable schema may also list null. If `enum` or `const` is
present, null must satisfy it too. Null otherwise bypasses non-null
string/numeric/container assertions.

There is no runtime `pattern`, `patternProperties`, `propertyNames`, tuple
`prefixItems`, `contains`, `allOf`, `anyOf`, `not`, `unevaluatedProperties`,
dynamic reference, or remote resource support. Editor meta-schemas use some of
these keywords to describe the profile itself; that does not admit them in a
definition's configuration schema.

### Objects, arrays, maps, and typed choices

Closed objects explicitly declare `additionalProperties: false`. Their
`required` array names distinct declared properties. A required nullable member
must exist but may contain null. A required object does not materialize itself;
once supplied, its required children are checked.

A typed map uses a recursive schema in `additionalProperties`. Declared
properties keep their own schemas; all other exact names use the map schema.
Fixed `properties` and a local `required` array may coexist with typed
`additionalProperties`; a map is not restricted to dynamic entries alone.
Map values may themselves be nullable, arrays, maps, or discriminated objects.
Dynamic entry names are private answer data, not public schema labels.

Arrays use `items`, including scalar arrays and recursively nested containers.
`uniqueItems: true` compares complete structural JSON values. By contrast,
`x-configurator.uniqueBy` is one nonempty pointer from an object item to a
non-null string key through required declared properties. Uniqueness is ordinal
across rows; it is not a list of candidate keys or a database constraint.

Typed scalar `enum` choices may have `x-configurator.choices`: one
`{value, label, explanation?}` entry per enum value, using the same actual JSON
type. Labels are at most 256 characters and explanations 2,048; they do not
replace the stored value.

### Discriminated branches

Use object `oneOf` only for 2-16 non-null closed object branches. The enclosing
node has no own `properties`, `required`, or `additionalProperties`. Every branch
requires the same single eligible discriminator property: a non-null scalar of
the same type with a different `const` value in each branch. Exactly one shared
property may meet that rule. Branches cannot themselves be variant wrappers or
typed maps.

A supplied object selects its branch by discriminator equality. Missing or
unknown discriminators reject; the engine does not invent a branch. Only that
branch's members are admitted. A nullable wrapper may preserve explicit null;
the configuration root itself must remain non-null. Discriminator constants
are public definition values and require a public context.

### Local definitions and references

Place reusable definitions under `configuration.$defs`. `$ref` must begin
`#/$defs/` and resolves from the configuration root, not from the current nested
node or a new `$id` resource. Percent escapes, remote URLs, filesystem lookup,
cycles, and reference amplification beyond budgets reject. Nested `$defs`
objects can be admitted, but do not establish a new reference base.

A reference node allows only `$ref`, `$defs`, `title`, `description`, `readOnly`,
`writeOnly`, `deprecated`, `examples`, `default`, and `x-configurator`.
It is not an assertion-composition escape hatch. Sibling metadata overlays the
target; a target's `writeOnly: true` survives and sensitivity is merged upward,
never downward. Expanded schemas and keywords are charged again, including
repeated uses; small textual reuse is not unlimited runtime expansion.

### Asserted formats

`format` alone is descriptive, even when its name is familiar. Enable
`x-configurator.assertFormat: true` only with these string formats:

| Format | Exact bounded check |
|---|---|
| `hostname` | ASCII alphanumeric/hyphen labels, 1-63 characters each, alphanumeric ends, total up to 253 after removing one trailing dot |
| `email-address` | One `@`, local part 1-64 and total up to 254; ASCII letters/digits and `.!#$%&'*+-/=?^_` plus backtick, braces, vertical bar, and tilde; no leading/trailing/consecutive dot; hostname rule for the domain |
| `uri` | Well-formed absolute URI; no spaces, controls, or non-ASCII characters |
| `http-url` | The URI check plus HTTP/HTTPS, nonempty host, and no user information |

These are deliberately bounded checks, not full standards validation. They do
not perform DNS, reachability, mailbox, certificate, credential, filesystem, or
target checks. Unlike public help links, asserted answer URLs are not generally
required to omit query strings or fragments.

## Presence, Defaults, and Requiredness

Effective values are resolved from a private answer snapshot, not by mutating
the public definition:

1. Inactive nodes are absent from effective values.
2. Explicit omission suppresses a node and its initialization.
3. A supplied value wins, including `false`, `0`, `""`, and permitted null.
4. An absent active node may use `x-configurator.initialValue`.
5. Children are resolved only inside present parent containers.

An absent optional object is not created merely because a child is required or
has an initial value. Supplying `{}` makes its child initialization eligible.
`default` describes the target's documented default; it does not seed an answer.
`suggestedValue` is inert until deliberately accepted as an answer. Both
initial and suggested values must be schema-valid and wholly public.

Private answers have separate `values` and `omitted` members. Omission pointers
are concrete configuration paths; overlapping omissions or a supplied value at
an omitted location are ambiguous and reject. To reset, remove the explicit
answer and its omission marker so initialization becomes eligible again.
Setting null is a supplied nullable value, not a reset.

Requiredness is a presence obligation for an active node. It does not mean
nonempty, nonblank, non-null, deliberate user entry, secret classification,
or target acceptance. Use string bounds or `x-configurator.notBlank` for those
specific string checks.

### Worked presence example

This is a **partial source excerpt** from the maintained
[neutral definition](../../examples/minimal-self-hosted/configurator.yaml);
it is not a complete definition:

```yaml
service.mode:
  type: string
  enum: [minimal, extended]
  x-configurator:
    sensitivity: public
    initialValue: minimal
```

Its property name contains a literal dot. The complete
[answer fixture](../../examples/minimal-self-hosted/answers/complete.json) is:

```json
{"answerContractVersion":1,"values":{"metrics.enabled":true,"service.credential":"synthetic-placeholder","service.mode":"extended","service.name":"sample"},"omitted":[]}
```

The explicit `extended` value wins over initialization. It activates the declared
metrics setting, whose supplied `true` value is projected as
`METRICS_ENABLED=true`; the mode projects as `SERVICE_MODE=extended`.
The complete expected files and runnable comparison commands are maintained in
the [quickstart](getting-started.md), not independently reconstructed here.

The following variations explain behavior; they are **conceptual edits** to
that fixture, not additional complete fixtures:

| Operator action | Effective outcome |
|---|---|
| Remove the supplied `service.mode`, with no omission | `minimal` becomes eligible through initialization |
| Omit `/service.mode` explicitly and remove its supplied value | The mode is absent; initialization is suppressed |
| Reset that omission | Initialization becomes eligible again |
| Supply `null` | It is present and invalid for this non-nullable string, not an omission |
| Supply `false`, `0` or `""` elsewhere | Each is present; its own schema determines validity |
| Leave an optional object absent | Child initialization cannot create the parent |

Opening a form, expanding a section or showing a suggested value is not an edit.
Those actions must not create configuration or silently accept a suggestion.

### Conditional activation and requiredness

`x-configurator.applicableWhen` controls whether a node participates. Its
predicates use `exists`, `equals`, `notEquals`, `in`, or `all`/`any`/`not` with
`operands`. Leaf predicates use a typed `{scope, path}` reference and comparison
`value` or nonempty `values`; `not` has exactly one operand.

`exists` means effective presence, including null, empty string, false, and zero.
For an absent value, `equals`, `notEquals`, and `in` all return false.
The root cannot have an applicability condition. Root references can control
nested nodes; item references are admitted only within an array-item scope.
Nested arrays establish their own current item, not a parent-row lookup.
Activation cycles reject before use.

Object `if` permits only local `required` names and `properties` tests containing
`const` or `enum`. `then` is required when `if` exists; `then` and optional `else`
contain only a nonempty local `required` array. Property tests alone pass when
that property is absent: include it in the guard's `required` array if presence
is part of the condition. `dependentRequired` adds local presence obligations
when its trigger exists. Mutual presence requirements are allowed; they are not
activation cycles.

Requirements do not activate otherwise inactive nodes. Missing, invalid, or
secret-deferred controllers can make a dependent decision **unknown** rather
than false. Uncertainty is propagated instead of choosing the `else` branch or
asserting readiness. Independent definite errors remain visible. Draft projection
with an unresolved dependency is withheld rather than guessed.

## Sensitivity and Public Metadata

`x-configurator.sensitivity` is `public`, `sensitive`, or `secret`; omission
defaults to `sensitive`. Ancestors establish a floor and protected descendants
raise a container's effective sensitivity. `writeOnly: true` imposes a secret
floor through references and descendants. A public child cannot declassify a
private ancestor.

`default`, `examples`, `const`, `initialValue`, and `suggestedValue` reject in
protected contexts; a public container with protected descendants cannot carry
one of those aggregate public values. Secret or sensitive values must be
collected privately, not embedded in public source, examples, help text,
translation strings, annotations, or projection literals. Admission's
classification rules are not a scanner that can recognize every real secret.

All answer-derived artifacts remain restricted even when every answer is marked
public. Browser secret entry requires explicit per-workspace consent; a deliberate
copy/download transfers custody without establishing native protected-save
guarantees. See [Browser](browser.md) and [native output](native-output.md).

### Guidance and credential classification

`x-configurator` also admits `detail` (`basic`, `standard`, `advanced`), `units`
(up to 128 characters), `notBlank`, `uniqueBy`, `applicableWhen`, `assertFormat`,
and the value/choice metadata above. Detail controls disclosure, not validation.

`guidance.instructions` is inert text up to 4,096 characters; it permits CR, LF,
and tab but rejects other controls, blank text, and `${`, `{{`, `}}` template
markers. `guidance.links` permits up to four `{title, url}` entries: title up to
128, URL up to 2,048 characters. Links must be absolute credential-free HTTPS
without whitespace or query strings; fragments are allowed. `searchTerms`
permits up to 16 distinct nonblank public terms of at most 64 characters.
Rendering and search support depend on the client, not just schema admission.

`credential` is valid only for a string in a secret context. It declares a task,
not a generated value, script, password-manager guarantee, or issuer login.

| `kind` | Allowed `acquisition` |
|---|---|
| `password` | `existing`, `create`, `issuer` |
| `opaque-token` | `existing`, `issuer` |
| `symmetric-key` | `existing`, `create` |
| `private-key` | `existing`, `issuer` |
| `connection-string` | `existing`, `issuer` |

## Presentation and Guided Journeys

`presentation.sections` contains 1-64 sections with unique `id`, `title`,
optional `description`, and `pointers` to declared configuration locations.
`presentation.journeys` contains 1-16 journeys with unique `id`, `title`,
optional `description`, and a nonempty list of existing section IDs.
`defaultJourney` names an existing journey. Global `navigation` is `wizard` or
`sections`; omission selects `sections`.

Section/journey IDs are bounded to 64 characters, titles to 128, descriptions to
2,048. Duplicate pointers within one section reject. This is layout metadata:
it neither makes a property required nor grants it target authority.
There are no `fields`, `experiences`, `defaultExperience`, `initialDisclosure`,
or per-journey validation aliases in the v2 source.

### Localization

`localization` contains `defaultLocale` and `translations`. Each translation is
keyed by locale and may carry `direction: ltr|rtl` plus `overrides`. Override
keys are pointers into the **public runtime definition document**, not private
answers. They must resolve to existing string members named `title`,
`description`, `instructions`, `label`, `explanation`, or `units`.
Replacement text is bounded to 4,096 characters. Without an override, the
admitted base text remains available.

Runtime `locales` is the ordinally sorted union of default locale and translation
keys; the source compiler computes it. Translation does not alter data names,
enum values, predicates, assertions, initialization, links, output mappings, or
identity selection. Admission of public translations is not evidence of
translation quality, accessibility, or actual operator usability.

## Output Artifact Mappings

Source `outputs` is keyed by safe single-component filenames. Each entry
requires `format`: `dotenv`, `json`, `yaml`, or `markdown`. Optional `id` overrides
the computed artifact ID. `targetUpdateMode` is `unspecified`, `create`, `merge`,
`replace`, `import`, `reconcile`, or `generationOnly`; `restartRequirement` is
`unspecified`, `none`, `process`, or `deployment`. Both default to `unspecified`.
These are target advice, not an apply engine.

An output chooses either `bindings` or `projection`, never both:

- JSON/YAML binding targets are concrete pointers. The compiler builds nested
  object/copy projections, rejects overlapping targets, and does not infer arrays
  from numeric object names. A sole empty target pointer copies the selected root.
- Dotenv binding targets are literal environment keys, not pointers.
- With neither mapping, JSON/YAML copy the effective root. Dotenv and Markdown
  require an explicit mapping.

Runtime artifacts always carry `id`, `fileName`, `format`, `targetUpdateMode`,
`restartRequirement`, and `projection`. Source-only sugar is gone.
Filenames and artifact IDs are at most 128 characters with bounded ASCII
letters/digits/dot/hyphen/underscore rules; filenames reject path separators,
trailing dot/space, reserved device names, and case-insensitive duplicates.

Projection operators are `literal`, `copy`, `omit`, `object`, `list`, `repeat`,
and `index`. `repeat` takes a non-nullable array reference and projects its current
item; `index` is zero-based within that context. Optional `when` uses the same
bounded predicates. There are no loops with external effects, arbitrary
expressions, scripts, or value-bearing network requests.

Dotenv requires a flat object of non-null scalar values. Markdown requires its
bounded string-list representation and escapes active Markdown syntax; it is
not an arbitrary template engine. See the [output and import reference](definition-reference.md#outputs-and-import)
and [integration workflow](integrate-your-tool.md) for format-specific authoring.

## Validation, Preparation, Drafts, and Import

These operations establish different facts:

| Operation | What it establishes; what it does not |
|---|---|
| Editor schema check | Structural help; not semantic admission, privacy, budget, or target proof |
| Definition admission | Closed grammar, compatible constraints, references, sensitivity, capabilities, and budgets before answers |
| Answer validation | Effective values and active local obligations for this immutable snapshot |
| Complete preparation | Validated declared projection and format encoding; not target acceptance or successful save |
| Secret-deferred draft | Explicitly incomplete output with completion locations, never complete readiness |
| Existing-file import | Bounded reversible prefill, not arbitrary configuration migration |
| Target validation/apply | Independently owned by the target, outside Configurator authority |

Complete preparation rejects validation diagnostics. Draft preparation may defer
missing active secret leaves, including leaves in present recursive containers;
missing non-secret obligations and invalid supplied values still fail.
String placeholders are empty strings; non-string JSON/YAML secret placeholders
can be null. Completion metadata identifies the unfinished work; dynamic map
names are represented by entry ordinals, not echoed as public correction paths.
Unknown control dependencies produce `draft-dependency-unresolved` rather than
a guessed output. A draft can contain already supplied private values and remains
restricted; it is not a sanitized public export.

### Existing-file import

Generation does not imply a reversible mapping. The current
[import and comparison guide](import-and-compare.md) distinguishes supported
inspection from strict autofill, with capability boundaries in the
[technical contract](../internal/IMPORT_COMPARISON.md). Proved direct copies,
structural literals, fixed lists and complete one-to-one repeats can participate;
arbitrary transforms, ambiguous mappings and filtered/hidden values cannot be
guessed. Whole typed-map copies preserve declared dynamic entries, while dotenv
recovers its declared non-null scalar types.

Comparison can report unaccounted input without adopting it. Strict autofill
rejects unaccounted content and unsafe conflicts atomically. Do not treat a
completed comparison as successful import or as complete readiness.

The importer proposes a new answer snapshot, checks current-answer conflicts
and omissions, and invokes the same validator. Existing values require
provenance-owned replacement permission; explicit omissions are not silently
undone. Imported members must survive effective resolution. Remaining required
work can still exist after successful prefill, so `import-ready` is not complete
readiness. A failed assessment does not partially mutate the existing snapshot.

### Scaffolding and inference

An example environment file supplies candidate **names**, not inferred types,
defaults, requiredness, or real credentials. Scaffolding creates string nodes,
marks name-based secret candidates secret and other leaves sensitive, and
discards assignment values. Review the resulting contract rather than treating
heuristics as application knowledge.

JSON Schema scaffolding is a separate bounded import into this profile. It uses
only supplied documents/local references, never network or arbitrary filesystem
resolution. Unsupported assertions reject. Transformation records distinguish
`converted`, `advisory`, `discarded`, and `unsupported` with stable value-free
codes, public-schema locations, and document ordinals. Source defaults/examples
are discarded rather than converted to `initialValue`; `format` stays advisory.
Numeric bounded-domain and nullable-variant differences require explicit
`--accept-bounded-semantics`; acknowledgment cannot authorize unsupported
assertions. See [JSON Schema import](definition-reference.md#json-schema-import).

## Diagnostics and Recovery

Use codes and bounded correction locations, not private values, to explain a
failure. Definition failures can include a public schema pointer and source
line/column span. Answer corrections bind to the input revision and distinguish
declared properties, array rows, and private map-entry ordinals. Revalidate after
an edit; an old ordinal/revision is not a durable identifier for a new workspace.

| Code or family | Meaning and next action |
|---|---|
| `definition-version`, `source-discriminator` | Choose exactly authoring v2 or runtime v2; reauthor unsupported contracts |
| `member-unsupported`, `type-unsupported`, `constraint-context` | Remove unsupported semantics or move a constraint to its owning type |
| `schema-reference`, `schema-discriminator`, `activation-cycle` | Repair the bounded reference, branch, or dependency structure |
| `public-value-protected`, `public-value-invalid` | Remove embedded protected/invalid values; collect answers privately |
| `value-required`, `value-invalid`, `value-variant`, `value-duplicate` | Correct the identified active answer or branch |
| `document-limit`, `schema-limit`, `validation-limit`, `projection-limit` | Reduce source/expansion/value complexity; do not assume a larger budget |
| `draft-dependency-unresolved` | Complete the controller or prepare an independent artifact |
| `answer-conflict`, `inverse-unsupported`, `inverse-ambiguous` | Resolve the answer/omission or use a supported explicit inverse |
| `revision-conflict`, `digest-mismatch` | Select the intended canonical revision; do not silently replace pinned content |

Generic refusals such as `definition-invalid` may be less specific when parsing
cannot establish a safe location. See [troubleshooting](troubleshooting.md) for
the wider error catalog.

## Implementation Limits and Evidence

The following source-observed limits matter when authoring:

- The codec admits metadata on the configuration root, but root-level
  `initialValue` is not used to seed the answer workspace; resolution starts at
  root members. Root variants also need a supplied discriminator before member
  initialization. Put effective initialization on eligible member nodes.
- Static references resolve declared properties/items, not arbitrary dynamic-map
  entries or branch-only properties. Whole-container copying and recursive
  validation have a wider shape domain than static child references.
- Public-value admission validates recursive types and ordinary assertions but
  does not settle `if`/`then`/`else` or `dependentRequired` relationships inside
  those values. Later answer validation can therefore reject an admitted
  initial/suggested value on a relationship obligation.
- Mixed fixed-property/typed-map objects use separate declared-member and
  dynamic-entry editors in Desktop. Declared members keep their own schemas,
  requiredness and public titles; only undeclared names use the additional-value
  schema. The Linux X11 guard covers required fixed-member correction alongside
  typed dynamic entries. This does not qualify another native host.

These are implementation limits, not promises of future behavior. The
[internal conformance profile](../internal/DEFINITION_CONFORMANCE_PROFILE.md)
records the owning mechanisms and evidence gaps. This reference is grounded in
current source and named regression scenarios; it does not certify final physical
packages, every native host, any target application's acceptance, translations,
or measured operator usability. Use the [packaging guide](packaging.md) and
client capability references for separately recorded qualification. I-VSD
findings are not closed by publishing this page.
