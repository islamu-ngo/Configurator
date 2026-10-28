# Core definition contract

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented pre-release bounded-v2 base; broader scenarios remain unqualified
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `src/Configurator.Core/Artifacts/`, `src/Configurator.Core/Definitions/Authoring/`

The [public reference](../public/definition-reference.md) projects current
admission. The [Definition Standard v2](../public/definition-standard.md) remains
Draft and includes later design; neither that design nor the historical
[standard audit](reports/DEFINITION_STANDARD_AUDIT_2026-10-28.md) widens the
implemented vocabulary.

## Public API

`SetupDefinitionCodec.ParseJson/ParseYaml(ReadOnlyMemory<byte>)` admits closed,
bounded runtime definitions or throws `SetupDefinitionException` with a value-free
`Code` and, when available, public `SchemaPath`/numeric `SourceSpan`.
`SetupDefinitionSourceCodec` separately selects exact authoring-v2 source
or schemaVersion-2 runtime with no fallback. Admission snapshots data and owns
canonical bytes; digest is computed, never supplied within the definition.

`SetupDefinition` exposes `FamilyId`, `Revision`, `Digest`, `CanonicalBytes`,
`Configuration`, `RequiredCapabilities`, `Title`, `Publisher`, `ApplicationId`,
`ApplicationVersions`, `EntryPoints`, `Locales`, `Fields`, `Artifacts` and optional
`Presentation` and immutable `SourceMap`. `Configuration` is the immutable recursive `DefinitionNode` tree.
Fields are root presentation projections, not a second schema authority.

`SetupFieldDefinition.Name` preserves the exact root data key. `Id` is bounded
metadata: a representable name retains its ID except reserved prefix `f-`;
all other names use `f-` plus full SHA-256 of `"field:"` and the escaped exact
root pointer. Literal `f-` names also hash. Names are not normalized or
case-folded. Private answer lookup uses Name, never Id. `Default` exposes
descriptive metadata, while `InitialValue` and `SuggestedValue` are distinct.
Typed `Choices`, recursive `Items` and `Properties` derive from the admitted node.

`SetupDefinitionRegistry.Resolve(familyId, appVersion, digest)` uses exact
declared application versions and canonical identity. With explicit version it
selects the highest compatible revision; unknown family, incompatible version
or digest mismatch rejects. Omitting version and digest requires one registered
revision, never guessed newest selection. Conflicting content under an admitted
family/revision rejects even after registry removal. Publisher/family labels
do not authenticate or curate a source. Active sessions remain pinned.

`SetupAnswerSnapshot(inputRevision, values, omitted)` clones an exact-name
configuration tree and snapshots concrete `DefinitionPath` omissions.
`SetupAnswerSnapshotCodec` admits/serializes the private version-1 envelope.
Its bytes are restricted and are not public definition/cache material.

`DefinitionValidator.Validate` returns immutable `DefinitionValidationResult`:
`IsReady`, root `Fields`, value-free `Diagnostics`, and literal child/row `Nodes`.
Fields expose active/required/provided/error facts. Nodes additionally expose
obligation, missing and readiness facts for progress. Diagnostic root `FieldId`
is metadata; relative segments identify declared properties/rows without echoing
answer values. `TryGetEffectiveValue` is an explicit private-value read seam,
not serialized validation telemetry. Presentation owns disclosure, not validity.

## Definition JSON

Runtime is a closed object with `schemaVersion: 2`, `profile: bounded-v2`,
`familyId`, `revision`, `project`, `applicationVersions`, `entryPoints`, `locales`,
`configuration`, `artifacts`, and sorted `requiredCapabilities`. Optional roots
are `presentation`, `localization`, `annotations`. It does not admit `$schema`,
the old flat project metadata or a root `fields` array.

Authoring has `$schema: urn:islamu:configurator:authoring:v2`, nested identity,
project, configuration and filename-keyed outputs. The compiler supplies publisher
when absent, default entry point, derived locales, artifacts and computed
capabilities, then re-enters runtime admission. Public runtime parsing recomputes
the capability subset and rejects missing, altered or unsorted declarations.
The closed registry is in `DefinitionProfile.Capabilities`; declaring a name
never activates an unsupported feature.

Definition revision, target version, private answer version and command version
are independent. The CLI envelope remains v2; the authoring/runtime handshake is
2/2. Machine v3 is not delivered by this base.

### Recursive configuration grammar

Configuration is a non-null object. Objects declare
`additionalProperties: false` or a recursive typed value schema; arrays declare recursive `items`. Properties use
exact names; required sets refer to distinct locally declared names. Scalars are
string, boolean, integer and number; container types are object and array.
Nullable form contains exactly one base type plus `"null"`.

Enums are distinct homogeneous scalar values compatible with the base type;
nullable types may additionally enumerate null. Optional choice labels preserve
typed identity. Scalar const is admitted. Bounds include inclusive/exclusive
minimum/maximum, positive exact multipleOf, minLength/maxLength, minItems/maxItems,
uniqueItems and minProperties/maxProperties in their type contexts. String
length counts Unicode scalars. Format is annotation unless assertFormat is true.
`readOnly`, `deprecated`, `examples` are annotations subject to privacy/budgets;
writeOnly true raises secret sensitivity.

Closed `x-configurator` admits sensitivity, initialValue, suggestedValue,
notBlank, detail, units, guidance, credential, choices, uniqueBy, applicableWhen and assertFormat.
Missing sensitivity is sensitive. `uniqueBy` follows a concrete item-relative
pointer through required object properties to a non-null string. Effective
container sensitivity includes inherited floors and protected descendants.
Public literal metadata cannot launder private initial/suggested/default/example/
const content through ancestor or child labels.

Typed map entries use the same resolver and constraints as declared children.
Diagnostics/completion metadata use bounded entry ordinals, not private keys.
Discriminated oneOf admits 2-16 closed object branches with exactly one eligible
common required scalar const property; ambiguous/duplicate selectors reject.
Nullability is handled outside branch selection. Configuration-root maps,
variants and property bounds use the same owner, not client-side special cases.

Local `$defs`/`$ref` resolve from the configuration root under existing node,
depth, scalar and reference-stack budgets. Fragments begin `#/$defs/`; percent
encoding, remote/dynamic/cyclic references reject. Declarations are checked even
when unused; reference expansion cannot hide unsupported assertions or amplify
metadata beyond the work budget. Reference annotation merging preserves the
strongest sensitivity and writeOnly floor. Arbitrary regex and general
combinators remain outside the selected profile.

Asserted formats are hostname, email-address, uri and http-url. They perform
bounded lexical checks only; their exact ASCII domains are in the
[public registry](../public/definition-standard.md#asserted-format-registry).
Uniqueness uses canonical bytes and hash buckets with structural confirmation,
clearing temporary canonical byte buffers; hashes are not value-equality proof.

### Paths, conditions and presence relationships

`DefinitionPath` uses RFC6901 concrete pointers, ordinal segments and structural
equality. Empty string selects root; `/` selects an empty-name property. Escape
tilde/slash with `~0`/`~1`, decoded once. Dots and asterisks are literal. Existing
array indices use canonical nonnegative decimal tokens with no leading zeroes;
`-` is not an item. Invalid escapes and Unicode reject.

Scoped references are exactly `{scope: root|item, path: <pointer>}`. Root selects
the effective configuration; item requires an admitted repeat context. Data
names never become expressions, paths inferred from dots or global searches.

Applicability on declared properties uses root- or row-item-scoped `exists`,
`equals`, `notEquals`, `in`, `all`, `any`, `not`.
Leaf predicates use `reference`, plus typed `value` or nonempty `values`;
compounds use nonempty `operands`, exactly one for not. Root-scoped controllers
may select declared nested locations. Unknown references and activation cycles
reject. Item scope exists only in an admitted array-row context; applicability
on the configuration root rejects. Each row retains its own invalid/deferred
controller set, so one row cannot activate another row's fields.

Missing leaf controllers evaluate false. Present null exists when valid under a
nullable schema. Invalid/secret-deferred controllers propagate unknown; not
preserves unknown, all settles on false, any on true, otherwise unknown remains.
No unresolved secret dependency becomes guessed branch authorization.

Local object `if` guards support required presence and property const/enum
assertions; consequences are `then.required` and optional `else.required`.
`dependentRequired` names locally declared properties. Guards lower into the
same predicate kernel; property assertions without guard requiredness are
vacuously true for absent values. Relationship edges do not order activation,
so mutual requirements are legal. Definite missing work survives an invalid
controller elsewhere in the same container.

Runtime validation diagnostics carry an immutable `DefinitionCorrectionLocation`
bound to the snapshot's `InputRevision`. Segment kinds distinguish declared
`Property` names (including numeric names), `ArrayRow` indices and `MapEntry`
indices. Dynamic keys never enter this model. `RelativePath` projects the existing
safe coordinates; map entries remain `@entry` plus ordinal. This Core coordinate
does not grant stale client callbacks correction authority; client adoption and
generation fencing are verified separately.

`DefinitionSourceMap` is immutable definition-only metadata, separate from
canonical bytes and digest. The existing JSON token reader and YAML events
capture document ordinal 0 and one-based numeric line/column spans (exclusive
end). The bounded projection maps known schema positions and declaration names,
not unknown member names or literal/default/example data trees. Runtime admission
attaches its known enclosing schema position; authoring compilation rebinds that
position to the original source span. Unmapped positions fall back to a known
public ancestor, or a code-only error. No filesystem path or reference value is
published.

## Effective values and known semantic limits

The private envelope contains exactly `answerContractVersion: 1`, object `values`
and distinct concrete `omitted` pointers. Snapshots reject supplied/suppressed
conflicts, duplicate and overlapping omissions; definition validation checks
omission paths against declared shapes. Values retain exact root names/nesting.

Core owns effective-value resolution for validation, drafts and output:

| Input state | Resolution |
|---|---|
| Supplied value, including false, zero, empty string/container | Preserve and validate |
| Supplied null | Present only if nullable |
| Explicit omission | Missing; suppress initialValue |
| Never supplied with eligible initialValue | Use admitted initialValue |
| Never supplied otherwise | Missing |
| Reset | Remove override and omission, making initialization eligible |
| suggestedValue | Inert until deliberately adopted |
| Standard default | Descriptive, never inserted |
| Absent container with initialized children | Keep container absent |
| Present container with never-supplied children | Resolve eligible initialization recursively |
| Inapplicable root property | Exclude from effective validation/output |

Required means presence, not whitespace rejection; notBlank/length assertions are
separate. Required descendants do not create absent optional containers. Clients
must not seed required containers or maintain a second effective-value policy.

Admission/editor validation does not prove every runtime output composable, a
target validator equivalent, or every client qualified. Broader/nested conditions,
source maps, expanded
localization policy and machine v3 retain their later-phase status.

## Artifact preparation and inverse import

`DefinitionArtifactComposer.Compose(definition, artifactId, answers)` yields
`DefinitionArtifactResult` with success, artifact and diagnostics. Artifact bytes,
digest, definition/input revisions, artifact ID, filename, media type and
sensitivity bind the result to admitted content and one answer snapshot.
All answer-derived artifacts are restricted. Managed `ApplySemantics`/`Restart`
properties currently read wire `targetUpdateMode`/`restartRequirement`; those
property names are not old wire aliases.

Source targetUpdateMode is unspecified/create/merge/replace/import/reconcile/
generationOnly; restartRequirement is unspecified/none/process/deployment.
Both default to unspecified. A recommendation never grants apply, administration,
native I/O or target acceptance. Filename hints do not select authorized paths.

JSON/YAML bindings use concrete target pointers; dotenv bindings use literal
environment names. Source values are scoped references. Overlapping target paths
reject. The compiler expands bindings into object/copy projections; absent
JSON/YAML mappings copy the effective root. Dotenv/Markdown require mappings or
an explicit projection.

Projection operations are literal, copy, omit, index, object, list and repeat,
with root/item references and bounded expansion. Item references require repeat;
index emits the current index. Optional `when` uses admitted root predicates.
Missing/inapplicable copies omit rather than invent null. Dotenv requires
non-null scalar members; Markdown requires string lists and escapes substituted
formatting/link-sensitive text. Output remains data, not executable templates.

`DefinitionDraftArtifactComposer.Compose/ComposeBundle` returns disposable draft
artifacts, completions and value-free diagnostics. Only genuinely missing secret
leaves defer; malformed supplied values and ordinary missing values block.
Unresolved secret-dependent outputs are withheld by artifact ID. Completion
locations identify remaining work, not proof of complete readiness. Draft dotenv
uses blank assignments and a warning; JSON/YAML placeholders remain draft-only.
Fatal bundle failure clears owned outputs; disposal invalidates reads/clears bytes.

`DefinitionValidator.ValidateDraft` is the shared draft seam.
`DefinitionBundleComposer.ComposeActive` selects active complete outputs from one
snapshot, skips intentional artifact omission and rejects an empty active set.
Explicit single-artifact composition remains strict about omitted selection.
Bundle composition owns one internal `ValidatedComposition`; it validates once
and composes each emitted artifact once. Callers cannot supply trusted validation.
Fatal/aggregate-budget failures dispose staged output. Cancellation is observed at
validation/projection boundaries and before output transfer.

The local `Configurator.Core.Preparation` diagnostic listener exposes public
artifact IDs and a draft-mode Boolean, never answers or retained measurements.
See [single-pass ownership](adr/ADR-single-pass-preparation-ownership.md).

`DefinitionImport.Assess`/`CanImport` share a narrower inverse admission for
unconditional unambiguous direct copies in JSON/YAML/dotenv. Unknown input,
transform/conditional/repeat ambiguity, inactive mappings and nonreplaceable
conflicts reject atomically. Successful immutable prefill does not mutate current
workspace or imply readiness. Provenance-owned replaceable defaults are distinct
from permission to overwrite arbitrary answers. Rejection returns no answer
snapshot and no private-value diagnostic authority.

## Presentation, guidance and credentials

`SetupDefinition.Presentation` snapshots admitted sections/journeys and their
ordered pointers/section IDs. Wire uses `defaultJourney`, sections, journeys and
optional wizard/sections navigation. Section/journey IDs are bounded lowercase
kebab-case; titles 128, descriptions 2048; counts 64/16. No field-group selectors,
old experience aliases or presets are inferred. Detail maps basic/standard/
advanced to presentation disclosure without changing requiredness.

Guidance has optional instructions (4096), four links, sixteen distinct search
terms (64 each). Multiline instructions permit technical plain text, not template
expansion. Links have bounded titles/URLs, static public credential-free HTTPS,
no query and optional fragment. Consumers render inert text and open deliberately
without automatic fetching, interpolation, opener/referrer or answer transfer.
YAML comments never become runtime help.

`DefinitionInputPurpose` describes credential kind/acquisition in effective secret
string context. Kinds: password, opaque-token, symmetric-key, private-key,
connection-string. Acquisition: existing, create, issuer; create only for password/
symmetric-key, issuer not for symmetric-key. Intent grants no authentication or
generation, and never lowers inherited sensitivity.

Localization base admits defaultLocale, translations, ltr/rtl direction and
overrides pointing to existing public string metadata ending in title, description,
instructions, label, explanation or units. Runtime locales must match the derived
sorted set. Broader localization policy and actual client rendering remain separate
later qualification. Metadata participates in canonical digest; locale switches
must not alter typed answers or numeric output.

## Canonicalization and bounds

`DefinitionProfile` is the exact-key syntax boundary; `DefinitionJson` remains the
canonical byte/decimal authority. Ordinal object ordering, preserved arrays,
compact UTF-8 without BOM and normalized exact decimals bind identity. Negative
zero is zero. This custom bounded-v2 profile is not an RFC8785/JCS claim.
Integers are Int64; decimals admit at most 28 significant digits after redundant
zero removal, scale at most 28 and explicit exponents -28 through 28. No rounding,
overflow or underflow fallback admits an out-of-domain value.

Names preserve Unicode scalar values, case and normalization form. Invalid UTF-8,
unpaired surrogates and exact duplicates reject. Case/NFC-distinct keys do not
collide in definition/answer exact mode. Generic composition retains its own key
policy; this profile does not silently change unrelated composition contracts.

Limits remain 4 MiB aggregate source, depth 32, 4096 entries per container,
65536 nodes, 65536 scalar characters and 1 MiB aggregate scalar characters.
Roots cap at 1024, artifacts at 64 and repeated entries at 4096. Stricter importer/
output budgets remain authoritative. Expansion charges work before materialization.

YAML admits one bounded JSON-shaped document with lowercase booleans/null,
exact numbers, quoted/literal/folded strings and comments. Empty plain scalars,
non-string resolved keys, exact duplicates, merge keys even quoted, anchors,
aliases, tags and directives reject. Quote typed-looking keys. Distinct case/
Unicode-composition keys remain distinct.

## Examples and qualification evidence

Native `Configurator.DefinitionSchemaGenerator` owns --write/--check schemas and
--write-examples/--renew-example-families/--check-examples. Maintained source/runtime
pairs are minimal-self-hosted, grafana-13.2.2 and islamu-event.
Fresh v2 families/revision 1 are recorded from source in the
[integration guide](../public/integrate-your-tool.md#complete-minimal-configuratoryaml-example).
The parent reports source/runtime agreement 3/3. This documentation unit did not
rerun checks; target acceptance, package/host closure and complete interactive
qualification are unproven by that result.

Synthetic answer/output fixtures are not production answers and are never Browser
assets. Grafana format projections do not establish startup, provisioning or
connectivity. Event target semantics remain Event's authority.

## Event coverage handoff

Historical source/test evidence remains under
`tests/Configurator.Core.Tests/EventHandoff/Source` and `EventHandoff/Tests` as
`.cs.txt`, not compiled compatibility shims or passing Configurator tests.
The extraction origin is `configurator-extraction-r2`; the v2 base does not
erase outstanding target-owned obligations.

| Original owner | Event obligation | Configurator boundary |
|---|---|---|
| Platform environment catalogue/invariants | Export Event metadata, activation and documentation vectors | Generic catalogue and neutral invariants |
| Identity correlation environment | Indexed issuer keys, normalization and duplicate rejection | Generic arrays, repeat/index; no issuer special cases |
| Reserved fingerprints | Event privacy-erasure classifications/startup | Restricted outputs independent of field labels |
| Optional email setup | Event dispatch/local-capture topology | No deployment orchestration |
| Offline portability contracts/workflow | Event manifest/package semantics, legal/state vectors | Bounded validation/projection/workflow, not host acceptance |
| Operator identity manifest | Event identity validation/revision/digest | Generic family/revision/digest and data projections |
| Offline legal draft | Scope, provenance and approved-template policy | Declared string/list data, no compliance authority |
| Mixed dotenv contracts | Event provider/bootstrap/identity/email requirements | Neutral parser/codec round trips, bounds and ambiguity |
| Composition scale evidence | Event host/Wire acceptance measurements | Host-supplied evidence, never transferred certification |
| Core architecture Wire vocabulary | Event SetupLive availability | No Event/Explore assembly dependency |

`DotenvEntry`, `DotenvDocument`, `DotenvCodec`, `ArtifactDigest` and setup artifact
policy retain their independent writer role. Event manifest/tenant/legal DTO and
platform catalogue ownership does not move into Core. `SetupCompositionCompiler`
is a bounded offline normalizer, not an Event DTO builder.
`LocalSecretGenerator` consumes declared environment metadata, not Event keys;
declared secret eligibility, entropy floor and disposal remain separate from
credential-intent metadata. Host-supplied scale-profile evidence cannot transfer
Event benchmark hashes or target-acceptance claims.

Archived `SetupCompositionInvariantTests` inventory checks are not parser-attack
proof. Production boundary tests and actual target acceptance must be recorded
separately; parent owns current verification.
