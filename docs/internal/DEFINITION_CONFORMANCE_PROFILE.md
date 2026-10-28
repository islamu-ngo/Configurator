# Implemented Definition Conformance Profile

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented bounded-v2 source reference; physical and target qualification remain separate
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/DefinitionProfile.cs`, `src/Configurator.Core/Definitions/DefinitionNode.cs`, `src/Configurator.Core/Definitions/SetupDefinitionCodec.cs`, `src/Configurator.Core/Definitions/DefinitionValidator.cs`, `src/Configurator.Core/Definitions/DefinitionVocabulary.cs`, `src/Configurator.Core/Definitions/DefinitionSchema.cs`, `tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs`

## Purpose and evidence boundary

This is the engineering reference for what current code admits and evaluates,
not a claim of full JSON Schema conformance or completion of platform
qualification. The public twin, [Configuration Definitions](../public/definitions.md),
explains authoring choices. [Core definition contract](CORE_DEFINITION_CONTRACT.md)
owns surrounding Core integration; [maintainer authoring](MAINTAINER_AUTHORING.md)
owns source compilation and lifecycle; [Presentation state](PRESENTATION_STATE.md)
owns workspace generations and custody. The companion
[definition standard](../public/definition-standard.md) and
[current reference](../public/definition-reference.md) own adopter property,
shape, and capability navigation.

Qualification on 2026-10-28 includes a clean Release solution build, Core 217,
Desktop 229, Terminal 106 and actual-package Architecture 73 passing tests.
The real Linux x64 X11 host passed all 19 guards in English and Arabic.
Published AOT Browser checks covered desktop/mobile layouts, RTL, typed editing,
offline operation, consent revocation, caches and service-worker version pinning.
Standalone CLI and Terminal protected outputs matched the Browser's synthetic
complete artifact. See [Operations](OPERATIONS.md) for custody and evidence
limits. No target validator, stakeholder study, translation review or other-host
qualification is implied. This prose increment does not rerun product suites.

## Admission and control flow

1. `SetupDefinitionSourceCodec` selects an explicit content contract after the
   caller selects JSON or YAML syntax. Exact authoring `$schema` selects
   `DefinitionAuthoringCodec` and `DefinitionAuthoringCompiler`; otherwise
   `schemaVersion` selects runtime admission. Mixed discriminators reject.
2. `DefinitionProfile` canonicalizes bounded syntax with
   `CompositionKeyPolicy.Exact`. `DefinitionJson` is the shared canonical
   byte/decimal owner. Syntax parsing does not itself admit a usable definition.
3. The source compiler lowers source-only envelope/output conveniences, then
   calls `SetupDefinitionCodec.AdmitCompiled`. That method computes profile and
   capabilities, then re-enters public runtime `ParseJson`.
4. `SetupDefinitionCodec.Admit` checks the closed envelope and recursively
   constructs `DefinitionNode` through `ReadNode`. It validates references,
   constraints, privacy, applicability, output projection, presentation, and
   localization before returning `SetupDefinition`.
5. `DefinitionValidator` resolves one immutable `SetupAnswerSnapshot` into
   effective values, diagnostics, field/node states, and uncertainty/deferred
   locations. `DefinitionArtifactComposer.ValidatedComposition` owns validation
   for exactly one definition, snapshot, and complete/draft mode. Callers cannot
   substitute an unrelated validation result.
6. Declared projections serialize restricted candidates. Protected native
   publication and browser export are distinct owners; neither changes target
   semantic acceptance or apply authority.

There is one runtime evaluator, no v1 migration layer, no source v1 fallback,
no executable definition plugin, and no runtime schema fetch. Ordinary standard
keyword spelling does not import an external schema implementation.

## Versions, identity, and canonical content

| Contract | Current identity |
|---|---|
| Profile | `DefinitionProfile.Id = bounded-v2` |
| Public authoring | `urn:islamu:configurator:authoring:v2` |
| Runtime | `schemaVersion: 2`; generated schema ID `urn:islamu:configurator:setup-definition:v2` |
| Private answers | `answerContractVersion: 1`; generated schema ID `urn:islamu:configurator:answers:v1` |
| CLI machine envelope | v3, independently owned by [CLI contract](CLI_CONTRACT.md) |

Runtime JSON has no `$schema`. Its required members are `schemaVersion`,
`profile`, `familyId`, `revision`, `project`, `applicationVersions`,
`entryPoints`, `locales`, `configuration`, `artifacts`, and
`requiredCapabilities`; `presentation`, `localization`, and `annotations` are
optional. Unknown members reject. `configuration` must admit as a non-null
object. `familyId` is a nonempty `Guid` in exact `D` form; `revision` is a
positive `Int64`. Application versions are distinct exact ordinal strings,
not semantic-version ranges.

`SetupDefinition` snapshots canonical bytes and computes `ArtifactDigest` over
them. `CanonicalBytes` returns a copy. `SetupDefinitionRegistry` rejects
different digests for one family/revision within its inputs, deduplicates equal
digests, requires exact application-version membership, and applies requested
digest selection. No version/digest plus multiple compatible candidates is
ambiguous. Registry capacity is 4,096. Persistent admitted-history conflict
memory and active-session pinning belong to Presentation, not this array-backed
registry. Publisher and family labels are not authentication or promotion
authority.

`DefinitionJson` orders object properties with `StringComparer.Ordinal`,
preserves array order, validates Unicode, writes exact canonical decimal
spelling, and rejects duplicate decoded exact keys. JSON/YAML syntax differences
that produce the same canonical tree need not change the runtime digest.
Metadata is content too; canonical identity is not just a hash of assertions.

## Exact paths, numbers, Unicode, and budgets

`DefinitionPath` is an immutable bounded RFC6901-style concrete pointer with
ordinal equality. The empty pointer selects the scope; `/` is one empty property
name. Only `~0` and `~1` escape. No dotted path, wildcard, percent decoding,
fragment decoding, or append token exists. Case and Unicode normalization
differences remain distinct. `FromSegments` snapshots and escapes once;
`ToString` deliberately hides potentially private tokens.

`TryGetArrayIndex` accepts canonical ASCII nonnegative decimal `Int32` tokens,
without a leading zero except `0`. The same token in an object is a declared
property name, not an index. Source schema references, answer locations, public
schema pointers, output targets, and safe correction locations have different
roles; do not substitute one for another.

`DefinitionProfile.FieldId` separates UI identity from target names. A
1-128-character ASCII identifier beginning with a letter and containing
letters/digits/dot/hyphen/underscore is retained unless it starts with reserved
`f-`. Other exact names derive `f-` plus lowercase SHA-256 of UTF-8
`field:` followed by their one-segment pointer. That generated ID is not a
configuration key.

`DefinitionJson.Number` strips coefficient punctuation/sign and leading/trailing
zeroes for its 28-digit guard, accepts explicit exponent only in [-28, 28],
requires `decimal.TryParse`, and rejects nonzero effective scale above 28.
It does not accept arbitrary JSON precision or round into the domain.
Canonical exact mode uses fixed decimal spelling with at most 28 fractional
positions; all zero spellings become `0`. `integer` answer compatibility
additionally requires `TryGetInt64`; `number` requires `TryGetDecimal`.
Numeric bounds and `multipleOf` use exact decimal comparisons/remainder.

The integer answer domain is exactly [-9,223,372,036,854,775,808,
9,223,372,036,854,775,807]. `DefinitionValidator.ValueValid` delegates to
`LocalValueValid`, whose WholeNumber arm uses `TryGetInt64`
(`src/Configurator.Core/Definitions/DefinitionValidator.cs:514`,
`src/Configurator.Core/Definitions/DefinitionValidator.cs:557`).
Predicate expected-value compatibility uses the same signed range at
`src/Configurator.Core/Definitions/SetupDefinitionCodec.cs:625`.
The shared scalar editor uses `long.TryParse` with invariant
`NumberStyles.Integer`, not Int32
(`src/Configurator.Presentation/Presentation/DefinitionScalarEditing.cs:23`).
Empty numeric editor text is a draft/absence state, not a zero or null integer.

Strict UTF-8 decoding and scalar re-encoding reject invalid byte sequences and
unpaired surrogates. No Unicode normalization occurs. String assertions count
`EnumerateRunes()`, whereas resource/metadata lengths use .NET string length.

`SetupCompositionLimits.Default` supplies these relevant limits:

| Domain | Bound |
|---|---:|
| Aggregate source/canonical bytes | 4,194,304 |
| YAML documents / parser events | 1 / 131,072 |
| Normalized nodes / nesting depth | 65,536 / 32 |
| Mapping entries / sequence entries | 4,096 / 4,096 |
| Scalar / aggregate scalar UTF-16 units | 65,536 / 1,048,576 |
| Pointer segments | 32 |
| Configuration root declared properties | 1,024 |
| `$defs` entries per object / reference stack | 1,024 / 32 |
| Enum choices / examples / branches | 256 / 256 / 2-16 |
| Runtime artifacts | 1-64 |

`DefinitionNode` snapshots ordinal properties and sorted required names,
clones canonical keywords, and snapshots variants. It accounts expanded child
nodes, depth, and property-name characters. Admission's `CompositionBudget`
also charges local keywords and each expanded reference; immutable construction
alone is not admission. Small source files can exceed expansion budgets.
Validation charges concrete answer traversal, including private map keys;
pointer overflow becomes value-free `validation-limit`.

## Closed recursive grammar

Concrete `type` is one of `string`, `boolean`, `integer`, `number`, `object`,
`array`. Nullable type is exactly that base plus `"null"` in a two-member array.
The runtime does not admit boolean schemas, null-only types, arbitrary unions,
or implicit type inference.

| Role | Keywords and restrictions |
|---|---|
| Shape | `type`, `properties`, `required`, `additionalProperties`, `items` |
| Scalar assertions | Typed `enum` and `const`; no object/array alternatives |
| Numeric assertions | Inclusive/exclusive min/max and positive `multipleOf` |
| String assertions | Unicode-scalar `minLength`/`maxLength`; extension `notBlank` |
| Container assertions | `minItems`/`maxItems`, `minProperties`/`maxProperties`, `uniqueItems`, extension `uniqueBy` |
| Local reuse | `$defs`, bounded `$ref` |
| Alternatives | Discriminator-restricted object `oneOf` |
| Relationships | Restricted `if`/`then`/`else`, `dependentRequired` |
| Annotations | `title`, `description`, `format`, `default`, `examples`, `readOnly`, `deprecated` |
| Privacy annotation with enforced effect | `writeOnly` imposes a secret floor |
| Product extension | Closed `x-configurator` object described below |

Unknown keywords reject via `Only`. Unsupported examples include `pattern`,
`patternProperties`, `propertyNames`, `allOf`, `anyOf`, `not`, `contains`,
`prefixItems`, `unevaluatedProperties`, `$id`, dynamic references, and external
resource scopes. A generated editor meta-schema may use such keywords to
describe the grammar; this does not extend the admitted node vocabulary.

`DefinitionSchema.Generate` exports a structural schema, not the runtime
evaluator. Cross-keyword compatibility, exact numbers/Unicode, expansion,
discriminator uniqueness, sensitivity and capability equality require admission.
`GenerateAnswers` describes the separate closed private envelope.

### Objects, sequences, typed maps, and variants

An ordinary object requires explicit `additionalProperties: false` or a typed
recursive schema. Untyped `true` rejects. Declared properties use their own
nodes; other names use the additional-property node. Required names must be
distinct declared properties, not arbitrary dynamic entries. Arrays require one
recursive `items` schema and do not admit tuple schemas.

Fixed properties, fixed-member requiredness, and a typed additional-property
schema may coexist. `ReadNodeCore` admits them independently
(`src/Configurator.Core/Definitions/SetupDefinitionCodec.cs:264`,
`src/Configurator.Core/Definitions/SetupDefinitionCodec.cs:270`,
`src/Configurator.Core/Definitions/SetupDefinitionCodec.cs:277`);
`ValueValid` validates fixed properties separately and applies the map schema
only to undeclared names
(`src/Configurator.Core/Definitions/DefinitionValidator.cs:522`).
`DefinitionNode` does not prohibit that combination. Do not infer a pure-map-only
contract from an adapter's editor choice.

`uniqueItems` uses canonical fingerprints as buckets plus `JsonElement.DeepEquals`
to establish actual duplicate values. `uniqueBy` requires an object item and
a nonempty pointer through required declared properties ending at a non-null
string. Its comparison is ordinal and its duplicate correction identifies the
later row and declared key path, not the key value.

`DefinitionNode` discovers exactly one shared eligible discriminator across
2-16 variants. Each branch must be a non-null closed mapping, not another
variant wrapper or typed map. The discriminator is a required non-null scalar
of the same type in every branch with distinct `const` values. The wrapper has
no own properties/required/map. `DefinitionValidator.SelectVariant` selects by
supplied discriminator equality; no implicit branch is synthesized. Root
variants select before resolving root fields. Nullable nested wrappers preserve
null without selecting a branch; the configuration root cannot be nullable.

### Local reference expansion

All references resolve against the original configuration schema root and must
begin `#/$defs/`, contain no percent escape, and remain acyclic/bounded.
Traversal uses exact object names and canonical array ordinals. Nested `$defs`
are validated but do not create a new base. No URL, filesystem, dynamic resource,
or executable resolver exists.

Reference siblings are restricted to `$defs`, title/description,
readOnly/writeOnly/deprecated, examples/default, and `x-configurator`.
`ReadNodeCore` clones the target and overlays siblings. Existing true writeOnly
cannot be unset; sensitivity merges by the maximum of secret, sensitive, public,
with omitted sensitivity treated as sensitive. Other extension members overlay
by name and then undergo ordinary admission. Shared source text is charged for
every expansion, including annotations/keywords; cycle checks are not the sole
amplification defense.

## Assertions versus annotation and effective values

`DefinitionValidator.LocalValueValid` checks base type, const/enum, string
length/notBlank/opt-in format, exact numeric bounds/multiple, uniqueness and
container cardinality. Null must satisfy const/enum but bypasses non-null
constraints. `ValueValid` recursively checks properties/items/maps/selected
branches and unconditional requiredness; it is used during public-value admission.
The full snapshot resolver additionally handles activation, initialization,
omission, relationships, uncertainty and diagnostics.

`format` alone never asserts. `assertFormat: true` admits only:

- `hostname`: ASCII alphanumeric/hyphen labels, alphanumeric ends, 1-63 units
  each, total 1-253 after stripping one trailing dot.
- `email-address`: exactly one `@`, local part 1-64, total at most 254,
  a bounded ASCII atom-character set, no leading/trailing/repeated dot,
  and the hostname check for the domain.
- `uri`: absolute, `IsWellFormedOriginalString`, no character at or below space
  or at/above DEL.
- `http-url`: URI check plus HTTP/HTTPS, nonempty host and empty user information.

These checks are not full RFC implementations, IDN/mailbox guarantees,
reachability tests, trust, credential validation, or public help-link policy.

`readOnly` and `deprecated` do not freeze answers or remove projected data.
`default` and `examples` are descriptive but schema-checked public values.
`suggestedValue` does not enter effective values; explicit acceptance must create
an answer. `initialValue` applies to an absent active node unless explicitly
omitted. False/zero/empty string/nullable null are supplied values, not absence.

The resolver starts at root fields. Nested resolution proceeds only inside
present containers, so required children/child initial values never materialize
an absent optional parent. Omission masks the selected subtree.
`SetupAnswerSnapshot` and its codec reject overlapping omission paths and
supplied/omitted ambiguity; reset removes the explicit value and suppression
marker, restoring initialization eligibility. No codec silently migrates old
answer layouts.

## Requiredness, activation scopes, and uncertainty

Requiredness is active presence, not nonblankness, secrecy, user intent, target
authorization, or completeness of every optional setting.

Object-local `if` admits only `required` plus property tests with `const`/`enum`;
`then` is mandatory and optional `else` is allowed. Consequences contain only
nonempty local `required` sets. Tests refer to declared properties and typed
expected values. Absent properties satisfy a property-only guard, so require the
controller explicitly when presence is intended. `dependentRequired` triggers
on effective presence. Mutual requirements are valid and distinct from cycles.

`applicableWhen` is a closed predicate tree: leaf `exists`, `equals`,
`notEquals`, `in`; composite `all`, `any`, `not`. References carry `scope` and
nonempty pointer for predicates. Root applicability rejects. Root references can
control nested nodes; item references require the current array-item context.
Nested item scopes do not mean arbitrary ancestor/sibling-row access.
`ValidateApplicability` checks static paths and dependency cycles in root/item
property scopes; `SchemaReferences` collects dependencies before resolution.

`exists` includes explicit null. Absent comparisons, including notEquals, are
false. Invalid/deferred/unresolved controllers produce nullable Boolean unknown.
`all`/`any` preserve decisive false/true respectively while propagating remaining
unknown; `not` preserves unknown. Full validation resolves controllers before
dependents, applies local relationships inside present objects, then settles
root obligations and uncertainty to a bounded fixed point. Inactive targets do
not become active merely because a relationship names them.

`DefinitionNodeState` distinguishes provided, required, ready, missing,
obligation and obligation-ready. A required object with child obligations avoids
double-counting its container as another actionable obligation. Node correction
locations bind to the answer revision. Definite missing work in one row remains
visible even if a different row has invalid control; uncertainty must not hide
independent errors.

## Sensitivity and public metadata

`SetupFieldDefinition.SensitivityOf` maps writeOnly to secret; otherwise
explicit public/secret or default sensitive. `EffectiveSensitivity` computes the
maximum across ancestry and descendants, including maps/items/variants.
`ReadNodeCore` carries `publicAncestors` and `secretAncestors`, while `Protected`
examines descendants. This prevents a public wrapper or reference sibling from
laundering private embedded values.

`default`, `const`, `examples`, `initialValue`, and `suggestedValue` require
wholly public context and compatible values. A protected descendant prevents
an aggregate public default/initial/example. This is classification enforcement,
not secret detection in arbitrary public strings, enum labels, annotations or
literal projections. Definitions must contain only public synthetic data.
Every `DefinitionArtifact` is restricted regardless of declared answer sensitivity.

The extension vocabulary is sensitivity, initialValue, suggestedValue, notBlank,
detail, guidance, credential, choices, units, uniqueBy, applicableWhen,
assertFormat. Detail is basic/standard/advanced. Choice labels match every typed
enum value exactly once. Credential metadata requires a string in secret
context; create is only password/symmetric-key, issuer excludes symmetric-key,
and existing is available to all admitted credential kinds.

`ValidateGuidance` admits inert instructions up to 4,096, allowing CR/LF/tab but
no other controls or template markers `${`, `{{`, `}}`. It admits up to four
links and 16 distinct search terms. Public links require credential-free HTTPS,
no query, whitespace or template markers; fragments are allowed for guidance,
not project repository. Ordinary metadata text rejects controls and angle
brackets. Do not conflate the instruction-text rule with the ordinary `Text`
rule or claim admission strips arbitrary markup.

`ValidatePresentation` admits sections (1-64), journeys (1-16), existing section
references, defaultJourney and global wizard/sections navigation. Section
pointers use static root schema references; duplicate pointers within a section
reject. Presentation does not change validation or require every property to be
in exactly one section.

`ValidateLocalization` computes the ordinal locale set and permits overrides of
existing public string members ending in title, description, instructions,
label, explanation, or units. Pointers traverse the runtime document, not the
answer tree. Translation entries may declare ltr/rtl. `DefinitionLocalization`
falls back to base text when no override exists and direction to ltr when absent.
No translation changes identity keys, assertion values, output policy, or links.

## Source-only lowering and projection boundaries

`DefinitionAuthoringCompiler` accepts `$schema`, identity, project,
applicationVersions, entryPoints, configuration, outputs, presentation,
localization, annotations. It defaults publisher to project name and entryPoints
to setup, derives locales from localization (otherwise en), sorts output
filenames ordinally, and delegates the recursive schema unchanged to admission.
Source wrappers are not a second schema semantics.

Filename-keyed outputs become artifacts. IDs default to `out-<fileName>` if that
is an eligible identifier, otherwise `o-` plus SHA-256 of `output:<fileName>`.
Update/restart advice defaults to unspecified. JSON/YAML bindings lower concrete
target pointers to object/copy projections; prefix-overlapping targets reject.
A sole empty target is a root copy. Numeric tokens create object properties,
not inferred arrays. Dotenv bindings use literal validated environment keys.
Without bindings/projection, only JSON/YAML receive an effective-root copy.
Markdown bindings reject. Projection and bindings together reject.

Projection admission is closed over literal/copy/omit/index/object/list/repeat,
plus optional when. Repeat requires a nonnullable array reference and establishes
item scope; index requires item context. Static references traverse declared
properties and items, not arbitrary dynamic-map keys or branch-only members.
Dotenv and Markdown have additional projection result-shape checks. Public
literal projection content is bounded data, never a template/plugin.

TargetUpdateMode values are unspecified/create/merge/replace/import/reconcile/
generationOnly; restartRequirement is unspecified/none/process/deployment.
`DefinitionVocabulary` is authoritative. These describe target intent, not an
implementation of reconciliation, authentication, restart, or apply.

### Preparation and secret-deferred drafts

Complete composition rejects validation diagnostics, then projects effective
values with the same bounded predicate/reference machinery and exact
serialization. It stamps artifact definition digest/revision and input revision.
Byte access returns copies; disposal clears the retained buffer, not copies
already given to callers.

Draft validation can defer absent active secret leaves, not invalid supplied
values or missing non-secret obligations. CopyField emits empty string
placeholders for strings and dotenv/Markdown; non-string JSON/YAML placeholders
can be null. Completion metadata retains declared property paths and array
ordinals but replaces private dynamic map names with `@entry` ordinals.
Nullable supplied null is copied, not treated as missing. Unresolved predicate
or copy dependency fails the affected projection with
`draft-dependency-unresolved`; it is not resolved by guessing false.
Draft bundle results distinguish surviving independent output from withheld
artifacts and diagnostics. Draft success never establishes complete readiness.

### Existing-file inverse import

`DefinitionImport.TryMappings` permits unconditional object/copy trees and root
copies for JSON/YAML, plus nonnullable scalar copies in flat dotenv. It rejects
when, literals, repeat/index, unsupported formats, and overlapping source
locations; overlap is reported as inverse-ambiguous. Whole typed maps can copy
their recursively validated contents. It does not infer arbitrary inverses.

`Assess` parses bounded input, collects only known target mappings, proposes
assignments without mutating the current snapshot, and checks conflicting
values/omissions. Replacement requires the caller's provenance-owned
replaceable-default set. The candidate increments input revision, invokes the
sole validator, checks imported active fields and non-required errors, and
requires imported members to survive effective resolution. Remaining required
work can coexist with import-ready. Failed assessment returns no partial answer
update. This is prefill, not target migration or target acceptance.

### Schema and environment scaffolding

`DefinitionSchemaImporter` accepts one public schema plus explicitly supplied
local documents: at most 64 documents total and 4 MiB aggregate. It resolves only
its supplied-reference domain, rejects `$id` resource scope and unsupported
assertions, and compiles its result through ordinary admission. It is not a
runtime resolver or an alternative validator.

Transformation records are immutable classification/code/public location/
document ordinal values. Converted/advisory/discarded/unsupported do not echo
raw values or private filesystem names. Numeric bounded-domain and
nullable-variant conversions require acceptBoundedSemantics; CLI exposes
`--accept-bounded-semantics`. Acknowledgment never bypasses unsupported assertions.
Defaults/examples are discarded; format/readOnly/deprecated remain advisory;
writeOnly ancestry remains secret through nested shapes and references.
No imported default becomes initialValue.

`SetupCliDefinitionHandlers.EnvironmentKeys` extracts valid environment names,
creates string nodes, classifies name-based secret candidates versus sensitive
leaves, and drops assignment values. It does not infer requiredness, defaults,
typed constraints, semantic validity or issuer authority.

## Diagnostics and capability handshake

`SetupDefinitionException` contains a stable code plus optional public SchemaPath
and SourceSpan. `ReadNode` preserves the nearest known schema location;
source-map lookup can fall back to an ancestor. Generic parser/shape failures may
lack a precise span. Messages must remain codes, never raw private input.

`DefinitionDiagnostic` carries a public field ID and a revision-bound
`DefinitionCorrectionLocation`. Segment kinds distinguish declared Property,
ArrayRow and MapEntry; an object property named `"0"` is not an array row.
Map entry names are represented by ordinal, not serialized private keys.
Do not promote these locations to unversioned durable answer identities.

The public twin's [diagnostic table](../public/definitions.md#diagnostics-and-recovery)
maps major code families to recovery. Syntax/admission failure, answer invalidity,
inverse support, draft uncertainty, publication outcome, and target rejection
must remain separate result concepts.

Capabilities are derived by admission, not requested permissions. Runtime
requiredCapabilities must equal the computed sorted set exactly. The closed
registry in `DefinitionProfile.Capabilities` contains:

- predicate.item, predicate.root;
- projection.copy, projection.index, projection.list, projection.literal,
  projection.object, projection.omit, projection.repeat;
- relationship.presence;
- schema.array, schema.boolean, schema.bounds, schema.const, schema.enum,
  schema.integer, schema.notBlank, schema.nullable, schema.number, schema.object,
  schema.string, schema.uniqueBy, schema.map, schema.variant, schema.localRef,
  schema.multipleOf, schema.uniqueItems, schema.assertFormat;
- values.initial, values.suggested.

A declared capability cannot admit unknown semantics. Core admission also does
not prove a particular client has an editor for every admitted shape; use
separate [client capability evidence](../public/definition-reference.md#client-capabilities).

## Source-observed limits requiring explicit treatment

These are not silently corrected by documentation:

1. **Root initialization:** ReadNode admits root initialValue, but
   ValidateCore starts with answers and resolves root fields, never root
   initialValue. Root variant selection likewise reads the supplied discriminator
   before member initialization. Do not promise root seeding or branch selection
   from wrapper/child initialization
   (`src/Configurator.Core/Definitions/DefinitionValidator.cs:115`).
2. **Public-value relationship validation:** ValueValid does not evaluate local
   if/then/else, dependentRequired, or activation. It validates recursive ordinary
   assertions/unconditional requiredness. An admitted initial/suggested aggregate
   can still fail snapshot relationship validation. Authoring references must not
   equate public-value admission with all effective-value semantics
   (`src/Configurator.Core/Definitions/DefinitionValidator.cs:514`).
3. **Static reference domain:** SetupDefinitionCodec.Reference does not traverse
   additional-property schemas or variants. Whole-container validation/copy has a
   broader supported shape domain than per-child presentation/predicate/copy
   references. Do not infer dynamic-key or branch-member references from support
   for typed maps/variants
   (`src/Configurator.Core/Definitions/SetupDefinitionCodec.cs:678`).
4. **Editor schema is not identical semantic admission:** DefinitionSchema's
   structural schema checks wrapper alternatives and helper forms, but the codec
   owns actual cross-keyword/context/privacy/budget behavior. Passing a generic
   editor validator is insufficient; do not describe its generated use of full
   schema combinators as runtime support.
5. **Desktop mixed-map editor:** MapFieldViewModel owns stable declared children
   with their actual schemas separately from additional entries. The existing
   templates render both collections; declared keys cannot be added as dynamic
   entries and all supplied members count toward maxProperties. FieldViewModel
   distinguishes declared public titles from dynamic literal-key labels.
   The typed Desktop regression and actual Linux X11 D19 guard cover fixed-member
   correction, types and output alongside dynamic entries. Core's mixed-object
   support is not narrowed by an adapter limitation, and another native host is
   not implicitly qualified.

## Narrow conformance evidence map

The following source tests encode independent behavioral expectations. This map
names inspected evidence; it is not a newly executed pass receipt.

| `DefinitionProfileConformanceTests` scenario | Contract guarded |
|---|---|
| ConcretePointersEscapeOnceAndKeepOrdinalIdentity; PathsRejectMalformedUnicodeEscapesAndResourceOverflow; ArrayIndicesAreCanonicalOnlyWhenExplicitlyRequested | Exact pointers, Unicode identity, bounds, array-token distinction |
| ExactJsonAndYamlShareIndependentCanonicalBytes; ExactAdmissionRejectsDuplicateInvalidUnicodeAndNumbersWithoutValues | Canonical golden bytes, syntax parity, duplicate/number/Unicode refusal |
| V2ResolvesInitializationOnlyInsidePresentContainers; V2OmissionSuppressesInitializationAndResetRestoresEligibility | Effective initialization, no implicit parent, suppression/reset |
| PrivateAnswerEnvelopesRejectAmbiguityAndUnknownContracts; CutoverRejectsPriorAndUnknownSemanticSourceContracts | Separate answer v1, definition v2 cutover, no fallback |
| TypedMapsValidateRecursiveItemsWithoutPublishingPrivateEntryNames; NullableConstrainedItemsPreserveNullAndEnforceExactAssertions | Recursive maps, typed/null choices and exact assertions |
| DiscriminatedProvidersSelectOneClosedBranchAndRetainNullablePresence; LocalReferencesExpandUnderOneConfigurationRootAndCyclesReject | Restricted branches and root-local references |
| AssertedFormatsAreOptInAndNeverGrantNetworkOrCredentialAuthority | Annotation/assertion distinction |
| ItemApplicabilityUsesOnlyItsRowAndInvalidControllersRemainUnknown; RowRelationshipsAllowMutualRequirementsAndConditionalElse | Item scope and local presence relationships |
| DefiniteMissingWorkInOneRowSurvivesInvalidControlInAnother; NestedRootControllersAreResolvedBeforeRowApplicability | Dependency order and independent error visibility |
| CorrectionsDistinguishPrivateMapEntriesRowsAndNumericDeclaredNames; DynamicEntryPointerOverflowRejectsWithBoundedValueFreeDiagnostics | Safe revision-bound correction and resource failure |
| RecursivePrivacyAndReferenceExpansionCannotLaunderPublicValues; ReferenceAmplificationIsBoundedBeforeAnExpandedSchemaEscapesAdmission; DeferredMapSecretsNeverExposePrivateKeysThroughCompletionMetadata | Privacy floors, expansion budgets, private map completion metadata |

For adoption and release, retain separate evidence for synthetic conformance,
actual client interaction, native package/save host, target-owned validation,
and stakeholder/usability review. None can be inferred from a source table,
build count, schema export, or another platform's passing run.
