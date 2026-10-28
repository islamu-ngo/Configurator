---
description: "Draft v2 bounded configuration profile, explicit answer states, mappings, and client obligations."
---

# Definition Standard v2

> **Audience:** Adopters | Integrators | Contributors
> **Status:** Draft; bounded-v2 base implemented, broader scenarios and client qualification incomplete
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `docs/internal/DEFINITION_PROFILE.md`, `src/Configurator.Core/Definitions/`

This Draft records the v2 standard and its selected later design. The current
product implements the bounded-v2 base; use the
[current reference](definition-reference.md) for executable authoring and precise
admission limits. Base implementation does not qualify every scenario, client,
package or target. Sections marked later design are not admitted support.

The base includes recursive closed objects/array items, typed enums and nullable
types, explicit value/omission states, scoped references, root applicability and
root presence-only relationships, typed maps, discriminated variants, exact
numeric assertions, local references and explicitly asserted formats.
Broader/nested conditions, source maps, localization policy expansion and
machine v3 remain later phases. No v1 fallback is provided.

A definition describes public configuration requirements and output mappings.
It never contains private answers, executes code, fetches references, authenticates
to a target, or grants permission to apply prepared files.

## Documents and versions

| Document | Discriminator | Responsibility |
|---|---|---|
| Public YAML/JSON source | `$schema: urn:islamu:configurator:authoring:v2` | Maintained requirements |
| Public compiled JSON | `schemaVersion: 2`, `profile: bounded-v2`, computed sorted `requiredCapabilities` | Canonical admitted runtime |
| Private answer JSON | `answerContractVersion: 1` | Values and explicit omissions |
| CLI machine envelope | Command contract v2 | Bounded operation result; v3 is later work |
| Editor meta-schema | Packaged JSON Schema | Structural assistance, not semantic admission |

Profile, definition revision, application version, locale, and command version
are distinct. There is no v1 fallback or implicit migration. Application versions
match exact declared strings, never guessed semantic-version ranges.
The current authoring/runtime compiler handshake is 2/2; it does not change the
command envelope or private answer version.

## Source envelope

The root is a closed object. Unknown semantic members reject. Required means
the member must occur; explicit null never substitutes for omission.

| Member | Requirement | Meaning when omitted |
|---|---|---|
| `$schema` | Required exact discriminator | Reject |
| `identity` | Required `familyId` UUID, positive integer `revision` | Reject; initialize through native tooling |
| `project` | Required nonempty `id` and `name` | Reject |
| `applicationVersions` | Required nonempty distinct string array | Reject |
| `configuration` | Required object schema | Reject |
| `outputs` | Required nonempty filename-keyed object | Reject |
| `entryPoints` | Optional distinct string array | `["setup"]` |
| `presentation` | Optional closed descriptor | Neutral reachable journey |
| `localization` | Optional closed descriptor | Base text, locale `en` |
| `annotations` | Optional bounded inert JSON object | No annotations |

Project metadata may additionally declare `publisher`, `description`,
`repository`, and `definitionLicense`. Publisher defaults to project name;
other omissions make no claim. Repository addresses are credential-free public
HTTPS. Store admission is separate from local use. Output keys are sanitized
basename hints, not paths or filesystem authority.

## Recursive schema grammar

The implemented recursive grammar applies to configuration roots, properties and
array items, map values and closed variant branches, without item-only aliases.
The configuration root itself must be a non-null object.

| Member | Allowed context and rule |
|---|---|
| `type` | One of `string`, `boolean`, `integer`, `number`, `object`, `array`; or an array containing exactly one base type and `null` |
| `title`, `description` | Optional bounded inert text |
| `enum` | Nonempty distinct homogeneous scalar values compatible with the base type; nullable types may additionally include null |
| `const` | One compatible scalar; conflicts with enum or assertions reject |
| `properties` | Object; exact-name keyed schema map, empty permitted |
| `required` | Object; distinct locally declared property names; omission means none |
| `additionalProperties` | Objects explicitly choose `false` or a recursive typed value schema; untyped `true` rejects |
| `items` | Required for array; one recursive schema |
| `minimum`, `maximum` | Current inclusive numeric bounds |
| `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf` | Numeric exact bounds and positive exact multiple |
| `minLength`, `maxLength` | Strings only; nonnegative Unicode-scalar counts |
| `minItems`, `maxItems` | Current array cardinality bounds |
| `uniqueItems` | Boolean structural uniqueness assertion |
| `minProperties`, `maxProperties` | Objects only; nonnegative bounds |
| `dependentRequired` | Current configuration-root map of local names to distinct nonempty local required-name arrays; nested form later |
| `$defs`, `$ref` | Acyclic local schema references rooted at configuration, starting `#/$defs/`; supplied offline documents are additionally supported by the importer |
| `if`, `then` | Current configuration-root presence-only `{required: [...]}` assertions |
| Broader `if`/`then`, `else` | Later design; general/nested predicates and conditional assertions |
| `oneOf` | 2-16 closed object variants with exactly one shared eligible required scalar discriminator and distinct const values |
| `format` | String annotation; no assertion by itself |
| `default` | Descriptive value only; never inserted |
| `readOnly`, `writeOnly`, `deprecated`, `examples` | Inert annotations except writeOnly raises the secret floor; public values must meet privacy rules |
| `x-configurator` | Optional closed extension object below |

Missing bounds impose only the existing global budget. Reversed bounds,
unknown members, assertions on incompatible types, invalid literal values, and
unrepresentable numbers reject at admission. Closed objects reject undeclared
answer members unless a typed additional-properties schema validates each
dynamic member. Discriminator selection never guesses a branch from labels;
nullable provider values remain null outside branch selection.

Unsupported constructs include tuples, general unions/combinators, arbitrary
regular expressions, dynamic/remote/recursive references, executable validators,
templates, and unrestricted schema vocabularies. Acknowledgment cannot waive an
unsupported assertion.

### Configurator extension

| Member | Meaning |
|---|---|
| `sensitivity` | `public`, `sensitive`, or `secret`; omission is conservatively sensitive |
| `initialValue` | Public valid value inserted only at eligible never-supplied locations |
| `suggestedValue` | Public valid value adopted only by deliberate operator action |
| `notBlank` | String-only Boolean assertion rejecting empty/whitespace text when true |
| `assertFormat` | String-only Boolean enabling a supported format assertion when true |
| `uniqueBy` | Array-of-object uniqueness by an admitted concrete property pointer |
| `applicableWhen` | Current direct configuration-property predicate using root-scoped references; nested/item predicates later |
| `detail` | `basic`, `standard`, `advanced`; presentation only, default standard |
| `guidance` | Closed instructions, static documentation links, and search terms |
| `credential` | Closed kind/acquisition descriptor; requires effective secret context |
| `choices` | Typed `value`, human `label`, optional explanation for enum alternatives |
| `units` | Inert display text; never conversion or numeric rounding |

An ancestor or descendant cannot be relabeled public to admit private initial,
suggested, default, example, or const metadata. Public definitions and caches must
not contain actual credentials. Credential intent never lowers sensitivity.
All answer-derived artifacts remain restricted regardless of field labels.

### Numeric and text domains

Integer values use signed Int64. Number values use exact decimal representation:
at most 28 significant digits after redundant zero removal, scale at most 28,
and explicit exponents in the range -28 through 28. Parsing must reject overflow,
underflow, rounding, non-finite tokens, and values outside the admitted domain.
Constraints and values use the same domain; multipleOf uses exact decimal
remainder rather than rounding.

Strings preserve exact Unicode scalar values. Unpaired surrogates, invalid UTF-8,
and exact duplicate object members reject. Names are ordinal and case-sensitive:
`A`, `a`, composed/decomposed Unicode, `_internal`, and names containing dots are
distinct. Strings are not normalized. String length counts Unicode scalars,
not UTF-16 code units or grapheme clusters.

### Asserted format registry

`format` alone is annotation. `x-configurator.assertFormat: true` enables the
following bounded lexical checks; no check performs network I/O or proves target
acceptance. False leaves the annotation inert.

| Name | Supported lexical domain |
|---|---|
| `hostname` | ASCII DNS labels, letters/digits/hyphens, no edge hyphens; optional trailing root dot; labels 1-63 bytes, complete hostname at most 253 bytes excluding root dot |
| `email-address` | ASCII dot-atom local part and hostname domain; no display names, comments, quoted local parts, IP literals, or internationalized addresses |
| `uri` | ASCII absolute URI with a valid scheme and well-formed escapes; no whitespace/control characters |
| `http-url` | Absolute HTTP(S) URL with host and no user information; this lexical assertion does not grant retrieval permission |

Both HTTP and HTTPS are lexical values for `http-url`, while definition retrieval
and guidance still require HTTPS. URI strings
in answers never create outbound authority. International hostname/mailbox
extensions require a separately qualified profile change.

## Paths, references, and safe locations

Use concrete RFC 6901 JSON Pointer strings. Empty string selects the root;
`/` selects an empty property name. Encode `~` as `~0` and `/` as `~1` in
tokens. Decode escapes once, rejecting malformed escapes. Array indices use
canonical nonnegative decimal notation without leading zeroes; `-` is not an
existing item. Dots and asterisks are literal key characters, not path operators.

A reference is exactly `{scope: root|item, path: <pointer>}`. Root selects the
effective configuration; item selects the current admitted repeat/row context.
Item scope outside that context rejects. Reference expansion and traversal
consume the same bounded work budget as validation.

`SetupFieldDefinition.Name` is the exact root data key; bounded `Id` is metadata,
not answer identity. Representable names retain their ID except reserved `f-`;
other names, including literal `f-` names, use `f-` plus a domain-separated full
SHA-256 of the escaped exact root pointer. Names are not normalized.

**Later map/source-map design:** Dynamic map keys are private answer data.
Public diagnostics must identify declared
schema positions and bounded row/entry indices, never raw dynamic keys or values.
The local workspace would resolve correction identities; edits would retire
old correction tokens. Source maps are not delivered. Authoring errors may
eventually include a public schema pointer,
numeric line/column, stable code, and bounded supplied-document ordinal, but no
raw scalar, exception, arbitrary unknown key, or private filesystem path.

## Effective values and answer states

The private envelope contains exactly `answerContractVersion`, `values` (an object
configuration tree), and `omitted` (distinct concrete pointers). A value cannot
be both supplied and explicitly suppressed. Malformed or overlapping suppression
that conflicts with supplied content rejects atomically.

| State at an applicable location | Effective result |
|---|---|
| Explicit supplied value | Preserve and validate it, including false, zero and empty containers |
| Explicit supplied null | Preserve only if the schema is nullable |
| Explicit omission | Exclude the location; suppress initialValue |
| Never supplied, eligible initialValue | Use the admitted initial value |
| Never supplied, no initialValue | Missing |
| Reset | Remove supplied override and suppression; initialization becomes eligible |
| Suggested value | No change until deliberately adopted |
| Descriptive default | No inserted value |
| Absent container with initialized child | Keep the container absent |
| Present container with never-supplied child | Resolve eligible child initialization recursively |
| Inapplicable location | Exclude from validation/output; changing layout never changes applicability |

Required means property presence, not nonblank text. An empty required string is
valid unless a separate assertion rejects it. Null satisfies presence only under
a nullable schema. Required descendants do not instantiate an absent optional
container. One Core routine resolves values for validation, drafts, and output;
interactive clients must not seed a different configuration.

### Applicability and relationships

Predicates are closed records with `op`. `exists` takes `reference`; `equals`
and `notEquals` add a typed `value`; `in` adds a nonempty typed `values` array.
`all`/`any` take nonempty `operands`; `not` takes exactly one operand.
Extra members reject.

| Controller state | exists | equals/notEquals/in |
|---|---|---|
| Valid present value, including null | true | Typed comparison |
| Missing | false | false |
| Invalid or secret-deferred | unknown | unknown |

`not unknown` is unknown. For all, false dominates, then unknown, then true.
For any, true dominates, then unknown, then false. Unknown never becomes a
guessed active branch. Applicability dependency cycles reject. Current
applicability occurs only on direct configuration properties with root-scoped
references; those may select declared nested controllers. Item scope is current
for admitted repeat projections, not nested/item applicability.

Current `dependentRequired` and presence-only root `if.required`/`then.required`
validate relationships; they do not toggle visibility or form activation edges.
Their arrays name locally declared root properties. Mutual requirements are
permitted: neither username nor password can be supplied alone, while both or
neither follow ordinary requiredness. Broader predicates, nested relationships
and `else` are later design, not implemented assertions.

## Outputs and inverse import

Every output requires format `dotenv`, `json`, `yaml`, or `markdown`. Optional
`targetUpdateMode` is `unspecified`, `create`, `merge`, `replace`, `import`,
`reconcile`, or `generationOnly`; optional `restartRequirement` is `unspecified`,
`none`, `process`, or `deployment`. Both default to unspecified. Advice never
grants apply, administration, or native-save authority.

JSON/YAML without explicit mappings copy the effective configuration tree.
Explicit `bindings` map concrete target pointers to scoped source references;
dotenv targets instead use literal environment keys. Dots never imply nesting.
Choose bindings or a projection, never both.

Projection operations remain `literal`, `copy`, `omit`, `index`, `object`, `list`,
and `repeat`; references are typed root/item references. Repeats may nest only
within existing expansion/work limits. Optional predicates cannot become string
expressions. Current admission checks references, contexts and dotenv's non-null
scalar member shape. Markdown requires a string list at composition; an editor or
definition check is not proof that every output will compose. General projected
shape/omission/nullability inference remains later qualification, not a blanket
pre-answer rejection guarantee.

Inverse import remains narrower than generation; do not infer that every current
root/nested scoped copy has a qualified inverse. Only supported unconditional
unambiguous direct-copy mappings are candidates. Transforms, conditional projections, repeated ambiguity, unknown
input members, and nonreplaceable conflicts reject atomically. Failed import
preserves the entire current workspace. No undeclared passthrough is retained.

Drafts defer genuinely missing secret leaves only. Deliberately supplied malformed
values still fail. Unknown secret-dependent outputs are withheld, completion
locations stay value/key-free, and partial preparation never claims complete
readiness or target acceptance.

## Presentation, localization, and capabilities

Presentation optionally supplies ordered `sections`, `journeys`,
`defaultJourney`, and `navigation` (`wizard` or `sections`). Sections have stable
IDs, titles, and concrete schema pointers; journeys have stable IDs, titles, and
ordered section IDs. Without metadata derive a neutral journey. Unlisted fields
join a deterministic fallback section. Detail, journey, and section changes
must leave all applicable required/error work reachable and cannot alter answers.

Base localization declares `defaultLocale` and `translations` keyed by locale;
translations contain optional `direction` (`ltr`/`rtl`) and pointer-keyed text
overrides. Current overrides target existing public string metadata ending in
title, description, instructions, label, explanation or units. Expanded
localization policy, diagnostic-message translation and broader coverage are
later work, never authority to override identities or typed values.
An absent translation falls back to base text. Direction is metadata, not inferred
from private text. Language changes cannot alter numeric serialization or output.

Guidance allows bounded multiline plain text and technical punctuation, never an
HTML/Markdown execution engine. Links require static credential-free HTTPS,
allow fragments, and disallow query strings. No interpolation, prefetch, automatic
fetch, opener transfer, or referrer transfer. Essential instructions have a plain
text path in every supported interactive client.

Required semantic capabilities are computed from admitted features, not author
assertions. Consumers must refuse unsupported mandatory editing before answers.
Optional layout hints degrade to a reachable neutral form. Capability metadata
is public, sorted and part of canonical identity; it grants no target authority.
Core computes the current subset; actual full CLI/Browser/Desktop/Terminal
scenario support remains separately qualified, not established by this Draft.

## Identity, budgets, and conformance

Family/revision binds canonical admitted content. Different content reusing an
admitted revision rejects even after registry removal. Workspaces retain their
pinned digest. Publisher labels and family IDs are not authentication.
Translations and inert annotations participate in the digest.

The custom canonical profile uses ordinal object-key ordering, preserved array
order, compact UTF-8 JSON, exact normalized decimals, and validated Unicode.
Zero, including negative zero, serializes as `0`; redundant decimal zeroes and
exponent spelling do not change a number's canonical meaning. This is not JCS.
Independent byte/digest vectors, not serializer defaults alone, are required
before declaring portable identity support.

Admission preserves existing limits: 4 MiB aggregate source, depth 32,
4096 entries per container, 65536 nodes, 65536 scalar characters, and 1 MiB
aggregate scalar characters. Existing stricter reference/import/output budgets
remain authoritative. Charge expansion and work before materialization; exceeding
a limit rejects without partial admitted state, output, or value-bearing errors.

Qualification must separately record Core conformance, actual machine/client
behavior, generated schema freshness, published package closure, and any target's
own acceptance. A build, editor pass, or synthetic projection does not prove
usability, assistive-technology compatibility, target acceptance, or a new host.
See the [implementation and conformance map](../internal/DEFINITION_PROFILE.md).

The maintained examples now have fresh v2 families at revision 1; exact source
identities are recorded in the [integration guide](integrate-your-tool.md#complete-minimal-configuratoryaml-example).
The parent reports native source/runtime agreement **3/3**. This documentation
unit did not rerun it, and no target acceptance follows from it. Retaining Draft
status preserves the distinction between implemented base semantics, later
standard design and unqualified end-to-end scenarios.
