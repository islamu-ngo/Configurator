---
description: "Practical bounded-v0.0.1 configuration patterns with exact answer states and explicit authority limits."
---

# Definition cookbook

> **Audience:** Adopters | Integrators
> **Status:** Current bounded-v0.0.1 patterns; snippets are illustrative and do not qualify a host
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs`, `docs/internal/DEFINITION_CONFORMANCE_PROFILE.md`

Use these patterns inside a native-initialized authoring-0.0.1 source. Each YAML
block is a **partial schema fragment**, not a complete source or a CLI input.
Keep identity, project, application versions and outputs from your own source.
For complete maintained documents, use the [example index](../../examples/README.md);
do not assemble a new runtime envelope or copy example identities.

The [reference](definition-reference.md) owns exact vocabulary and limits.
These independently authored fragments use that vocabulary; they were not
executed in this documentation unit. Named fixture methods below are source
anchors for the mechanism, not a new claim that their suites passed.

## Legal documents with editable language rows

The Event example groups each legal document in a closed object. Its
`jurisdictionAssumptions` and `localizations` members are bounded arrays, so
operators can add or remove jurisdictions and document languages. Each language
row requires its own language tag, title, summary and Markdown content; values
in another row cannot satisfy that requirement. The artifact projection repeats
the current rows and keeps Event's document kind fixed. Tenant display name and
brand display name remain distinct inputs. These are draft inputs for Event
review, not legal approval or publication. See the
[Event source and independent output fixtures](../../examples/islamu-event/README.md).

## Typed null, absence, omission, reset and suggestion

A property schema for an optional public worker count:

```yaml
type: [integer, "null"]
minimum: 1
maximum: 32
default: 4
x-configurator:
  sensitivity: public
  initialValue: 4
  suggestedValue: 8
```

| Operator intent | Result |
|---|---|
| Never supply the property | Eligible initialization resolves to integer `4` |
| Supply integer `8` | Preserve and validate typed `8` |
| Supply null | Present null, not absence; allowed by this nullable type |
| Omit | Missing and initialization suppressed |
| Reset | Remove override and omission; initialization becomes eligible again |
| Adopt suggestion | Explicitly supply typed `8`; merely displaying it changes nothing |

`default` alone does not fill answers. Required means presence; permitted null
can satisfy presence, but nonnullable null fails. False, zero, empty string and
empty containers are supplied values, not missing values. Use length or
`notBlank` separately when the target requires text. Initializing a child never
creates an absent parent object. Keep private literals out of default, initial,
suggested, example and const metadata.

Source anchors:
[`DefinitionTypedEditingTests`](../../tests/Configurator.Presentation.Tests/DefinitionTypedEditingTests.cs)
and [`DefinitionSemanticTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionSemanticTests.cs).
Private answer files represent omission in `omitted`, not by converting it to
JSON null. Reset and suggestion adoption are deliberate editing operations,
not additional private-envelope members.

## Output choices without application-specific UI

Declare one public, nonnullable, unconditionally editable root string enum,
then bind its exact values to artifact IDs. This partial authoring fragment
illustrates two outputs; filenames and technical answer values remain distinct
from human labels:

```yaml
configuration:
  type: object
  additionalProperties: false
  required: [task]
  x-configurator: {sensitivity: public}
  properties:
    task:
      title: File to prepare
      type: string
      enum: [environment, manifest]
      x-configurator:
        sensitivity: public
        initialValue: environment
        guidance:
          instructions: Choose a file, review its settings, then export deliberately.
outputs:
  service.env:
    id: environment
    format: dotenv
    projection:
      op: object
      members: {}
      when:
        op: equals
        reference: {scope: root, path: /task}
        value: environment
  manifest.json:
    id: manifest
    format: json
    projection:
      op: object
      members: {}
      when:
        op: equals
        reference: {scope: root, path: /task}
        value: manifest
presentation:
  defaultJourney: configure
  navigation: sections
  outputSelector:
    pointer: /task
    options:
      - value: environment
        title: Environment
        artifactIds: [environment]
      - value: manifest
        title: Configuration manifest
        artifactIds: [manifest]
  sections:
    - id: basics
      title: Basics
      pointers: [/task]
  journeys:
    - id: configure
      title: Prepare configuration
      sections: [basics]
```

The empty projections illustrate the binding only; supply actual target
members in a real integration. Every enum value needs exactly one option.
Each option has a human title and nonempty artifact IDs; each referenced
artifact must have a root `equals` activation guard on the same pointer and
value. Conditional, nullable, non-public and read-only selector fields reject.
The selector edits the ordinary shared answer; it is not a second evaluator
or permission to apply output.

Initialization applies only when no explicit value or omission exists. An
offline inclusion boolean may initialize to true to expose useful settings,
but must not be confused with enabling a target service, granting consent,
inventing an endpoint or supplying a credential. Keep inclusion controls next
to their settings and retain target keys, types and activation conditions.

The maintained [Event source](../../examples/islamu-event/configurator.json)
contains all five output choices and complete capability sections.
Its [reference guide](../../examples/islamu-event/README.md) explains the
independent optional-store family and native revision workflow. English base
text and Arabic overrides use exact public schema paths, for example
`/presentation/outputSelector/options/0/title`; translations change display
text, never enum values, artifact IDs or answer keys. Changing this canonical
metadata requires a native revision and fresh generated runtime, not a manual
identity increment or a rewrite of historical releases.

Source anchors:
[`DefinitionPresentationAdmissionTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionPresentationAdmissionTests.cs)
and [`EventDefinitionFormTests`](../../tests/Configurator.Presentation.Tests/EventDefinitionFormTests.cs).
The documentation fragment is illustrative; these anchors do not assert that
verification has run for a particular release.

## Exact literal keys and pointers

These are members of a configuration schema, with unrelated properties omitted:

```yaml
type: object
additionalProperties: false
properties:
  service.port:
    type: integer
    minimum: 1
    maximum: 65535
  "a/b~c":
    type: string
  "":
    type: string
```

`service.port` is one key, addressed by `/service.port`, not `/service/port`.
`a/b~c` uses `/a~1b~0c`. `/` selects the empty key, while the empty pointer
selects the context root. Dots and asterisks have no expression meaning.
Names are ordinal and case-sensitive; Unicode normalization is not applied.
Quote YAML keys that would resolve as booleans, null or numbers.

Use `{scope: root, path: /service.port}` in a source reference. Metadata IDs
are not data keys: unrepresentable root names and reserved `f-` names receive
derived bounded IDs, but answers retain exact names. Dynamic map keys remain
private and are not exposed in diagnostic paths.

The [minimal maintained source](../../examples/minimal-self-hosted/configurator.yaml)
demonstrates literal dotted names. The source owners are
[`DefinitionPath`](../../src/Configurator.Core/Definitions/DefinitionPath.cs)
and [`DefinitionProfile.FieldId`](../../src/Configurator.Core/Definitions/DefinitionProfile.cs).

## Arrays with row-local conditions and stable identity

A property schema for endpoint rows:

```yaml
type: array
maxItems: 16
x-configurator:
  sensitivity: public
  uniqueBy: /name
items:
  type: object
  additionalProperties: false
  required: [name, mode, endpoint]
  x-configurator:
    sensitivity: public
  properties:
    name:
      type: string
      minLength: 1
      x-configurator:
        sensitivity: public
    mode:
      type: string
      enum: [local, remote]
      x-configurator:
        sensitivity: public
    endpoint:
      type: string
      minLength: 1
      x-configurator:
        sensitivity: public
        applicableWhen:
          op: equals
          reference: {scope: item, path: /mode}
          value: remote
```

Each row's mode controls only that row's endpoint. Inapplicable values are
excluded from effective output. Invalid or secret-deferred controllers remain
unknown, not a guessed active branch. A valid present null exists; missing does
not. Root scope can instead select a declared configuration controller.

`uniqueBy` is an item-relative pointer through required object properties to
a non-null string. It checks exact identity, not labels or case-folded names.
`uniqueItems: true` instead checks whole item equality. Array order is retained
in canonical content and output; `index` in a repeat means the current index,
not a permanent record ID. Presentation row tokens preserve editor identity
through supported mutations, but are not serialized application IDs. Declare
an application-owned key when persistent identity matters; do not sort rows
implicitly or derive identity from their current position.

For conditional presence without hiding fields, an object may declare:

```yaml
dependentRequired:
  username: [password]
  password: [username]
if:
  required: [mode]
  properties:
    mode: {const: remote}
then:
  required: [username]
else:
  required: [mode]
```

This fragment requires locally declared `username`, `password` and `mode`
properties in its containing object. Property assertions without guard
`required` are vacuously true when absent. Consequences are required-name sets,
not arbitrary conditional subschemas. Mutual presence requirements are legal;
applicability cycles are not.

See `ItemApplicabilityUsesOnlyItsRowAndInvalidControllersRemainUnknown` and
`RowRelationshipsAllowMutualRequirementsAndConditionalElse` in
[`DefinitionProfileConformanceTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs),
and the maintained [Event tenant example](../../examples/islamu-event/README.md).

## Bounded typed maps

A property schema for named retry budgets:

```yaml
type: object
maxProperties: 16
x-configurator:
  sensitivity: public
additionalProperties:
  type: integer
  minimum: 0
  maximum: 10
  x-configurator:
    sensitivity: public
```

This is a typed map, not `additionalProperties: true` or undeclared passthrough.
Values use the same recursive schema and validation as declared properties.
Entry keys remain exact literals, including slashes and case differences.
Declare cardinality for the target; shared depth/node/work budgets still apply.
Private entry names never become diagnostic identifiers: correction coordinates
use entry ordinals tied to the current input revision. Typed map editors operate
on the declared value type without an opaque unrestricted JSON editor.

See `TypedMapsValidateRecursiveItemsWithoutPublishingPrivateEntryNames` and
`ConfigurationRootMapsAndBoundsUseTheSameRecursiveOwner` in
[`DefinitionProfileConformanceTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs).

## Closed discriminator branches

A property schema for selecting a transport:

```yaml
type: object
x-configurator:
  sensitivity: public
oneOf:
  - type: object
    additionalProperties: false
    required: [kind, directory]
    x-configurator:
      sensitivity: public
    properties:
      kind:
        type: string
        const: local
        x-configurator: {sensitivity: public}
      directory:
        type: string
        minLength: 1
  - type: object
    additionalProperties: false
    required: [kind, address]
    x-configurator:
      sensitivity: public
    properties:
      kind:
        type: string
        const: remote
        x-configurator: {sensitivity: public}
      address:
        type: string
        minLength: 1
```

The selected profile admits 2-16 closed object branches with exactly one
eligible common required scalar const selector. This is not general `oneOf`.
Unknown selectors, ambiguous selectors and extra branch members reject.
Selecting a new branch in the shared editor supplies its discriminator and
removes the old branch payload; Core resolves eligible child initialization.
If nullable, null is handled outside branch selection.

See `DiscriminatedProvidersSelectOneClosedBranchAndRetainNullablePresence` in
[`DefinitionProfileConformanceTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs).

## Safe reusable local schemas

This fragment belongs at `configuration`:

```yaml
type: object
additionalProperties: false
$defs:
  port:
    type: integer
    minimum: 1
    maximum: 65535
properties:
  listenerPort:
    $ref: "#/$defs/port"
  upstreamPorts:
    type: array
    maxItems: 16
    items:
      $ref: "#/$defs/port"
```

References resolve under the configuration root, not the filesystem, Internet
or the nearest row. Only the bounded local fragment domain is admitted.
Cycles, remote/dynamic references and percent-encoded reference forms reject;
unused declarations are checked too. Expansion consumes shared budgets.
Annotations at reference sites cannot lower inherited sensitivity or `writeOnly`.
Do not use YAML anchors/aliases, merge keys, tags or executable plugins.

See `LocalReferencesExpandUnderOneConfigurationRootAndCyclesReject` in
[`DefinitionProfileConformanceTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs).
Public application-schema scaffolding is a separate bounded
[import workflow](integrating.md#scaffold-a-public-schema-deliberately).

## Opt in to format assertions

A property schema for a public documentation address:

```yaml
type: string
format: http-url
x-configurator:
  sensitivity: public
  assertFormat: true
```

Without `assertFormat: true`, `format` is descriptive metadata. The asserted
registry is exactly `hostname`, `email-address`, `uri` and `http-url`; unsupported
asserted names reject. Checks are bounded lexical rules, not DNS, email delivery,
network reachability, credentials or target-validator equivalence.
`readOnly`, `deprecated`, examples and ordinary formats are annotations, not
authorization, initialization or an automatic disabled-control policy.

See `AssertedFormatsAreOptInAndNeverGrantNetworkOrCredentialAuthority` in
[`DefinitionProfileConformanceTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs)
and the [format registry](definitions.md#asserted-formats).

## Localized public choices, help and links

A property schema named `mode`:

```yaml
type: string
title: Operating mode
enum: [quiet, detailed]
x-configurator:
  sensitivity: public
  choices:
    - {value: quiet, label: Quiet}
    - {value: detailed, label: Detailed}
  guidance:
    instructions: Review the target log policy before selecting detailed output.
    searchTerms: [logging, verbosity]
    links:
      - title: Operator documentation
        url: https://example.invalid/operator#logging
```

At source root, a matching partial localization descriptor:

```yaml
localization:
  defaultLocale: en
  translations:
    en-GB:
      direction: ltr
      overrides:
        /configuration/properties/mode/title: Operating mode
        /configuration/properties/mode/x-configurator/choices/0/label: Quiet output
```

Overrides address existing public string metadata, not typed enum values,
schema assertions, identity or answers. Equal labels can still denote distinct
typed values. Missing translations fall back to base text; switching locale is
a view change and does not change numeric serialization or prepared output.
This synthetic descriptor demonstrates pointer placement, not translation
quality or a complete locale coverage claim.

Help is inert plain text. Links are deliberate static credential-free HTTPS
destinations without queries; fragments are allowed. No interpolation,
prefetch, automatic fetch or answer transfer is permitted. Instructions cannot
fetch credentials or grant issuer authority. See the maintained
[minimal source](../../examples/minimal-self-hosted/configurator.yaml),
[`DefinitionTypedEditingTests`](../../tests/Configurator.Presentation.Tests/DefinitionTypedEditingTests.cs)
and [`DefinitionPresentation`](../../src/Configurator.Core/Definitions/DefinitionPresentation.cs).

## Secret policy and target format

Declare `x-configurator.secret` from the target application's public contract.
Use the [definition reference](definition-reference.md) and generated authoring
schema for its exact closed syntax. The policy establishes secret sensitivity
even without a separate `sensitivity` declaration. Explicit public metadata or a
local reference cannot lower that floor. Never place private defaults, examples,
initial values or suggested values in the definition.

Choose a password or opaque-token policy for locally generated random text;
choose HMAC or AES key material only when the target specifies that use and
encoding. Password length counts Unicode scalars, while decoded key size and
UTF-8 input limits count bytes. Encoded character count is not key size. Standard
string constraints and secret constraints both apply to entered/generated values.
A 32-byte token occupies 43 characters in unpadded Base64url; author the decoded
byte requirement and any string length constraint in their respective units.

A password hash must match the target's exact algorithm, serialization and cost
policy. SHA-2 digests are a separate recipe and must not substitute for password
storage. Hash format checks do not prove knowledge of the original password.
Likewise, a PEM or private JWK must match the target's RSA/EC parameters; local
key consistency does not prove target acceptance. A connection string can be
classified secret, but Configurator does not issue its credentials or connect to
the target. See [cryptographic formats and limits](../internal/SECRET_CRYPTOGRAPHY.md).

Generation remains a deliberate local action. Browser entry/generation requires
workspace consent; native output uses protected create-only publication. A hash
created from a generated password has a separate original-password result: save
or copy it deliberately before clearing the workspace, because the hash cannot
recover it. Copying transfers custody to the clipboard and its history. Workspace
clearing cannot retract copies or promise complete erasure of immutable strings.

## Complete preparation, incomplete drafts and inverse import

Complete preparation validates the effective snapshot and emits declared active
outputs. A secret-deferred draft can defer genuinely missing secret leaves,
not invalid supplied values or missing ordinary required work. Unknown
secret-dependent outputs are withheld; completion coordinates identify work
still to do. Draft dotenv uses blank assignments and a warning, while JSON/YAML
placeholders remain draft-only. Never rename a draft as complete or treat
placeholders as acceptable target secrets.

Browser secret entry starts disabled and requires workspace consent. Revocation
clears secret leaves and output. Public sensitivity labels do not make
answer-derived artifacts public. Native save protection and Browser deliberate
copy/download are distinct custody boundaries.

Generation is broader than inverse import. Use the
[import/comparison guide](import-and-compare.md) and
[current inverse contract](../internal/IMPORT_COMPARISON.md), rather than
assuming either that every projection is reversible or that all repeats are
unsupported. Exact copies and proved structural/list/repeat shapes are bounded;
ambiguous, filtered or hidden values cannot be inferred. Comparison can report
unaccounted content without adoption; strict autofill rejects unsafe conflicts
or unknown input atomically. Import is not an arbitrary file editor or migration engine.
Successful prefill is a new immutable answer candidate, not automatic workspace
mutation or readiness; replacing default-origin values does not permit resetting
explicit omissions.

Use [`DefinitionDraftArtifactTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionDraftArtifactTests.cs)
and [`DefinitionImportTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionImportTests.cs)
for maintained behavioral fixtures. Have the target validate the final files
and authorize apply/restart separately. No pattern here proves target startup,
qualification on other hosts or closure of stakeholder and target I-VSD findings.
