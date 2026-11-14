# Core definition contract

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Current bounded-v0.0.1; candidate qualification and target acceptance are separate
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `src/Configurator.Core/Artifacts/`, `src/Configurator.Core/Definitions/Authoring/`

The [public reference](../public/definition-reference.md) projects current
admission. The [Definition Standard](../public/definition-standard.md) records
the qualified bounded profile; neither follow-up design nor the historical
[standard audit](reports/DEFINITION_STANDARD_AUDIT_2026-10-28.md) widens the
implemented vocabulary.

## Public API

`DefinitionCodec.ParseJson/ParseYaml(ReadOnlyMemory<byte>)` admits closed,
bounded runtime definitions or throws `DefinitionException` with a value-free
`Code` and, when available, public `SchemaPath`/numeric `SourceSpan`.
`DefinitionSourceCodec` separately selects exact authoring-0.0.1 source
or runtime-schema runtime with no fallback. Admission snapshots data and owns
canonical bytes; digest is computed, never supplied within the definition.

`Definition` exposes `FamilyId`, `Revision`, `Digest`, `CanonicalBytes`,
`Configuration`, `RequiredCapabilities`, `Title`, `Publisher`, `ApplicationId`,
`ApplicationVersions`, `EntryPoints`, `Locales`, `Fields`, `Artifacts` and optional
`Presentation`, `Deployment` and immutable `SourceMap`. `Configuration` is the immutable recursive `DefinitionNode` tree.
Fields are root presentation projections, not a second schema authority.

The promoted [Core overview](CORE_DEFINITIONS.md) and
[bounded conformance profile](DEFINITION_CONFORMANCE_PROFILE.md) complement this
detailed source contract. Public adopters start with the
[implemented reference](../public/definitions.md) and
[supported-shape cookbook](../public/definition-cookbook.md). Successful
preparation remains distinct from target semantic acceptance and authorization.

`FieldDefinition.Name` preserves the exact root data key. `Id` is bounded
metadata: a representable name retains its ID except reserved prefix `f-`;
all other names use `f-` plus full SHA-256 of `"field:"` and the escaped exact
root pointer. Literal `f-` names also hash. Names are not normalized or
case-folded. Private answer lookup uses Name, never Id. `Default` exposes
descriptive metadata, while `InitialValue` and `SuggestedValue` are distinct.
Typed `Choices`, recursive `Items` and `Properties` derive from the admitted node.

`DefinitionRegistry.Resolve(familyId, appVersion, digest)` uses exact
declared application versions and canonical identity. With explicit version it
selects the highest compatible revision; unknown family, incompatible version
or digest mismatch rejects. Omitting version and digest requires one registered
revision, never guessed newest selection. Conflicting content under an admitted
family/revision rejects even after registry removal. Publisher/family labels
do not authenticate or curate a source. Active sessions remain pinned.

`AnswerSnapshot(inputRevision, values, omitted)` clones an exact-name
configuration tree and snapshots concrete `DefinitionPath` omissions.
`AnswerSnapshotCodec` admits/serializes the private version-1 envelope.
Its bytes are restricted and are not public definition/cache material.

`DefinitionValidator.Validate` returns immutable `DefinitionValidationResult`:
`IsReady`, root `Fields`, value-free `Diagnostics`, and literal child/row `Nodes`.
Fields expose active/required/provided/error facts. Nodes additionally expose
obligation, missing and readiness facts for progress. Diagnostic root `FieldId`
is metadata; relative segments identify declared properties/rows without echoing
answer values. `TryGetEffectiveValue` is an explicit private-value read seam,
not serialized validation telemetry. Presentation owns disclosure, not validity.

`DefinitionValidationSession` owns one traversal's effective values, diagnostics,
dependency resolution and composition budget. `DefinitionValidator` retains the
shared value and condition rules; session state is never cached between answers.

`DefinitionSecretOperations.Resolve` consumes fields and node coordinates from
that same validation, including the selected root variant and typed root-map
entry ordinals. Static `Definition.Fields` is not a prerequisite for a root-map
secret target. Read-only ancestors, omissions, local acquisition and generation
requirements still determine eligibility. Presentation adds the workspace's
consent, revision, logical-identity and operation-generation fences before
adopting generated output.

## Definition JSON

The codec decodes and canonicalizes source before `DefinitionAdmission` checks
the complete document contract. `DefinitionNodeReader` recursively admits the
configuration schema with the same depth, reference and composition budgets.
These are internal owners; source format does not bypass admission.

Runtime is a closed object with `schemaVersion: "0.0.1"`, `profile: bounded-v0.0.1`,
`familyId`, `revision`, `project`, `applicationVersions`, `entryPoints`, `locales`,
`configuration`, `artifacts`, and sorted `requiredCapabilities`. Optional roots
are `presentation`, `localization`, `annotations`, `deployment`. It does not admit `$schema`,
the old flat project metadata or a root `fields` array.

Authoring has `$schema: urn:islamu:configurator:authoring:v0.0.1`, nested identity,
project, configuration and filename-keyed outputs. The compiler supplies publisher
when absent, default entry point, derived locales, artifacts and computed
capabilities, then re-enters runtime admission. Public runtime parsing recomputes
the capability subset and rejects missing, altered or unsorted declarations.
The closed registry is in `DefinitionProfile.Capabilities`; declaring a name
never activates an unsupported feature.

Product and public contract versions remain `0.0.1` during development even when
formats change. The authoring/runtime handshake and private-answer discriminator
are strings with that value; the [CLI contract](CLI_CONTRACT.md) owns the current
machine envelope. Definition revisions and canonical digests still bind content,
and target-application versions retain their own meaning. The fixed development
baseline never permits replacing an admitted family/revision with changed bytes.

### Deployment context and destination admission

Optional `deployment` metadata binds `recipePointer` and `scopePointer` to
ordinary public finite string answers. Their explicit `initialValue` values
must select a compatible pair. Metadata declares 1–64 components, 1–16 recipes
and 1–16 scopes per recipe. Each scope contains nonempty `componentIds` and
`artifactIds`, an optional eligible `primaryArtifactId`, and optional
`prerequisitePointers` bounded to 1,024 across the deployment root. IDs and
references are admitted before collecting answers. Localized public titles
and descriptions use the existing override coordinates.

`Definition.Deployment` snapshots these declarations. A selected context is
evaluation-local: no accepted answer, readiness or active-location result is
cached on the shared definition. Eligible projection references and explicit
prerequisites seed scope closure; activation controls and relevant presence
relationships extend it. Structural ancestors do not activate every required
sibling. Global input shape, unknown-member, omission and work-limit checks
remain independent of scoped requiredness. The optional deployment `when`
predicate preserves unscoped task behavior when false.
Scope-control observations use the existing initialization, activation and
uncertainty kernel under the same operation budget. A deferred relationship
trigger blocks an unresolved closure. Excluded array rows retain their concrete
indices as null structural slots, without retaining private row values in the
effective tree; accepted answers remain available for switching back.

`DefinitionValidationResult.ConfigurationContext` publishes the current public
recipe/scope and eligible artifacts with readiness facts. Bundle preparation
uses current eligibility; an explicit artifact outside it rejects. Single,
complete and draft composition derive context from the same accepted snapshot,
so export selection cannot narrow validation or expose retained inactive secrets.

Artifact/output usage metadata is closed: optional `title`, `description`,
`consumerIds`, `surface` and `environmentDialect`. Supported surfaces are
`process-environment` and `compose-input`; dotenv dialects are `docker-run`
and `compose`. Non-dotenv dialects and inconsistent surface/dialect pairs
reject. Scope consumers must contain every recipient of each eligible artifact.
Every dotenv artifact referenced by a deployment scope explicitly declares both
surface and dialect. Generic dotenv artifacts outside deployment metadata use
the Docker run dialect when no dialect is declared.
Portable literal filenames and case-insensitive uniqueness protect aggregate
delivery without authorizing a native overwrite or target application.

Admission computes `deployment.scope` and explicitly used dialect capabilities
`environment.docker-run` / `environment.compose`; runtime parsing recomputes
the exact required subset. Schema generation projects the same closed shape.
The [decision record](adr/ADR-deployment-context-and-destinations.md) explains
ownership and the distinction between local invariant evidence and actual
destination/host qualification.

### Recursive configuration grammar

Configuration is a non-null object. Objects declare
`additionalProperties: false` or a recursive typed value schema; arrays declare recursive `items`. Properties use
exact names; required sets refer to distinct locally declared names. Scalars are
string, boolean, integer and number; container types are object and array.
Nullable form contains exactly one base type plus `"null"`.

Enums are distinct homogeneous scalar values compatible with the base type;
nullable types may additionally enumerate null. Optional, partial choice metadata preserves
typed identity. Scalar const is admitted. Bounds include inclusive/exclusive
minimum/maximum, positive exact multipleOf, minLength/maxLength, minItems/maxItems,
uniqueItems and minProperties/maxProperties in their type contexts. String
length counts Unicode scalars. The 23 registered formats are asserted automatically;
unknown format tokens remain runtime annotations so immutable releases stay admissible.
`readOnly`, `deprecated`, `examples` are annotations subject to privacy/budgets;
writeOnly true raises secret sensitivity.

Closed `x-configurator` admits sensitivity, initialValue, suggestedValue,
notBlank, detail, units, guidance, secret, choices, uniqueBy, applicableWhen, requiredWhen, propertyOrder,
order, placeholder, defaultText, deprecationMessage, widget, rows, syntax, booleanLabels,
itemTitlePointer and displayRawKey. `assertFormat` is retired and rejects.
Missing sensitivity is sensitive. `uniqueBy` follows a concrete item-relative
pointer (or up to eight pointers forming a composite key) through required object
properties to a non-null string. Effective
container sensitivity includes inherited floors and protected descendants.
Public literal metadata cannot launder private initial/suggested/default/example/
const content through ancestor or child labels.

### Display text, ordering and non-value metadata

`DefinitionDisplayText` resolves display text in one order: localized override,
explicit `title`/`label`, then deterministic humanization. Humanization turns
`SCREAMING_SNAKE_CASE`, `snake_case`, `kebab-case`, `camelCase` and `PascalCase`
identifiers into Title Case with a canonical acronym and product dictionary
(`PUBLIC_BASE_URL` becomes `Public Base URL`, `OPENAI_COMPATIBLE` becomes
`OpenAI Compatible`). It strips an `env.` prefix, renders `__` and `.` levels with
` / `, and returns symbolic literals (versions, CIDR ranges, paths, media types and
text containing spaces) unchanged. `FieldDefinition.Name`/`TechnicalName` and
`DefinitionChoice.Value`/`WireToken` always keep the exact wire value;
`displayRawKey: true` keeps the raw key as the display title.

`DefinitionNode.OptionMetadata` lists every enum member as a `DefinitionChoice`
(display label, explicit-label flag, explanation, deprecation and the index of its
`choices` entry for localization). `choices` may annotate any subset of `enum`;
`deprecationMessage` requires `deprecated: true`. `DefinitionNode.OrderedPropertyNames`
applies `propertyOrder` (with one optional `*` placing unlisted members), then
per-member `order` ranks, then ordinal names. Order is data: canonical JSON still
sorts keys, so digests are unaffected. `FieldDefinition.Properties` and
`Definition.Fields` enumerate in this order.

`DefinitionFieldPresentationMetadata` exposes `placeholder`, `defaultText`,
`deprecationMessage` (as `DeprecationGuidance`), `units`, `widget`, `rows`,
`syntax`, `booleanLabels`, `itemTitlePointer` and `displayRawKey`. Text uses the
markup-free text floor. A placeholder is an input exemplar and is admitted only on
public subtrees; `defaultText` explains target behavior on omission and never
becomes a value. Widget options must match the node: `switch` needs a boolean,
`slider` a bounded number, `select`/`radio` an enum, `rows` a multi-line, Markdown
or code widget and `syntax` the code widget. `itemTitlePointer` must reach a public
scalar inside list items. Misuse rejects with `widget-invalid`, `item-title-invalid`,
`deprecation-invalid` or `public-value-protected`.

### Asserted formats

`DefinitionFormatValidator` owns 23 formats: `hostname`, `ipv4`, `ipv6`,
`ip-address`, `cidr`, `uri`, `http-url`, `https-url`, `origin`, `email-address`,
`slug`, `env-key`, `bcp47-language-tag`, `iso3166-alpha2`, `uuid`, `semver`,
`duration` (RFC 3339 Appendix A), `byte-size`, `unix-path`, `relative-path`,
`oci-image`, `sha256-hex` and `cron-5`. Each is a hand-written single-pass scanner
without regular expressions or backtracking; validation emits value-free
`format-invalid`. Checks are lexical only and grant no reachability, ownership or
target acceptance. Asserted formats add no capability. The authoring compiler
rejects unknown format tokens (`format-unsupported`); runtime admission keeps them
as annotations for historical releases. `pattern` stays unsupported.

### Applicability across nested members

Root-scoped references are tracked by full path. A nested member may depend on a
sibling inside the same root object (`/ai/provider` on `/ai/enabled`); admission
orders siblings by the paths their subtrees read and rejects references to the
node itself, an ancestor or a descendant as `activation-cycle`. The validation
session resolves siblings in that order and evaluates such conditions against the
partially resolved root object. Output and deployment selectors may be nested when
every ancestor is a public, required, unconditional and non-nullable object.

### Optional groups, conditional requirements and undecided values

An object that is not listed in its parent's `required` is an optional group:
its members are collected and their own `required` lists apply only while the
object is present. Presence is the inclusion decision, so a synthetic boolean
toggle is unnecessary. `x-configurator.initialValue` may open a public object
that holds protected members when the value supplies none of them
(`DefinitionNodeReader.SuppliesProtected`); an opened object may omit its required
members, which become obligations (`DefinitionValidator.ValueValid(..., requireMembers: false)`).
Explicit omission of the object suppresses the initial value.

`x-configurator.requiredWhen` makes a named object member required while a
predicate holds. It shares `applicableWhen`'s grammar, reference validation,
dependency ordering and `activation-cycle` rules
(`DefinitionValidator.NodeConditions`), is decided only for collected members,
and rejects with `relationship-invalid` when the member is also in `required`
and with `condition-context` on the configuration root, list items, map values
or branches. Nested members join `childRequired` in `ResolveProperty`;
top-level members join relationship settlement beside `if`/`then` and
`dependentRequired`. An undecided `requiredWhen` is reported like an undecided
applicability condition. Use local `if`/`then`/`else` for sibling rules and
`requiredWhen` when the controlling answer lives elsewhere.

Undecided values are tracked by path. A missing nested member, a deferred nested
secret or an undecided nested condition records its location
(`DefinitionValidationResult.UnresolvedLocations`, nested `DeferredLocations`);
only top-level causes record a whole member name. A condition reference is
undecided when it overlaps an undecided location, so a missing member never hides
an answered sibling. Draft composition refuses a copy that reads or contains an
undecided location (`draft-dependency-unresolved`) and evaluates `when` against
`UndecidedRoots` and `UndecidedLocations`; editable and import projections and
deployment scope resolution use the same pair.

### Authoring lowering

The authoring compiler lowers conveniences before runtime admission, so runtime
documents contain only canonical keywords:

- `DefinitionSchemaLowering`: `choices` without `enum` synthesize `enum` in
  order; authored mapping order becomes `propertyOrder` (from source positions,
  only when no explicit order exists); generated byte secrets infer
  `minBytes`/`maxBytes` and encoded length, generated passwords a minimum length.
- `DefinitionPredicateLowering`: root `predicates` referenced as
  `{"op":"predicate","name":…}` are inlined; cycles reject `predicate-cycle`,
  unknown names `predicate-unknown`, chains deeper than 16 `predicate-limit`.
- `DefinitionLocalizationLowering`: keys such as
  `…/x-configurator/choices[value=LOCAL]/label` resolve to the choice's index, adding
  a choice entry and humanized English label when needed.
- `DefinitionOutputLowering`: bindings accept a pointer string, `{ "literal": … }`
  or an explicit reference; `includeBindings` reuses another output's bindings
  (dotenv with dotenv, JSON with YAML); `bindObject` binds each member of an object
  (`only`/`except`, optional JSON `target`); output `when` guards the artifact.

Section `detail` and `applicableWhen`, collapsible `groups` (whose pointers must
lie within the section's pointers), journey `detail`/`applicableWhen` and nested
section pointers are runtime presentation data exposed as `DefinitionSection`,
`DefinitionSectionGroup` and `DefinitionExperience`; they never change validation.

### Answer schema and quality lint

`DefinitionAnswerSchemaGenerator` turns one admitted definition into a Draft
2020-12 schema for its private answer file: identity pinned through `$id` and
`x-configurator` (`familyId`, `revision`, `definitionDigest`), typed `values` in
authored order, `markdownDescription` hover text (units, omission behavior,
choices table, deprecation) and optional `omitted` (`default: []`). Secret leaves
are `writeOnly` with bounds only, never enum, const, default or examples. It
reads no answers. `DefinitionQualityLinter` reports `raw-identifier-title`,
`verbatim-choice-label`, `jsonpath-description`, `exporter-diagnostic-description`,
`missing-deprecation-message`, `missing-section-description`,
`required-secret-missing-guidance` and `unreferenced-journey-section` without
changing admission. The schema importer drops titles that only echo their key.

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
[public registry](../public/definitions.md#asserted-formats).
Uniqueness uses canonical bytes and hash buckets with structural confirmation,
clearing temporary canonical byte buffers; hashes are not value-equality proof.

### Paths, conditions and presence relationships

`DefinitionPath` uses RFC6901 concrete pointers, ordinal segments and structural
equality. Segment construction snapshots the caller's collection and checks the
escaped pointer length without serializing it. Equality compares segments and
hashing uses their ordinal content; pointer text is produced only when requested
or retained from parsed input. Empty string selects root; `/` selects an
empty-name property. Escape
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

The private envelope contains exactly `answerContractVersion: "0.0.1"`, object `values`
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
| Inapplicable root or nested property | Exclude from effective validation/output |

Required means presence, not whitespace rejection; notBlank/length assertions are
separate. Required descendants do not create absent optional containers. Clients
must not seed required containers or maintain a second effective-value policy.

Admission/editor validation does not prove every runtime output composable, a
target validator equivalent, or every client qualified. Broader/nested conditions,
expanded localization policy retain their separate follow-up status. Source maps
and the current machine diagnostics are implemented; package and target evidence
remain separate.

## Artifact preparation and inverse import

`DefinitionArtifactComposer.Compose(definition, artifactId, answers)` yields
`DefinitionArtifactResult` with success, artifact and diagnostics. Artifact bytes,
digest, definition/input revisions, artifact ID, filename, media type and
sensitivity bind the result to admitted content and one answer snapshot.
`DefinitionArtifactSerializer` writes admitted projected values as YAML, dotenv
or Markdown. Composition retains validation, projection, size checks and owned
buffer clearing; serialization does not grant publication authority.
All answer-derived artifacts are restricted. Managed `Format`, `ApplySemantics`
and `Restart` use `DefinitionArtifactFormat`, `DefinitionTargetUpdateMode` and
`DefinitionRestartRequirement`. `DefinitionArtifactVocabulary` maps their exact
wire tokens, including `generationOnly`, and rejects undefined enum values on
output. Admission remains case-sensitive and rejects numeric, null and missing
runtime tokens. Internal enum numbering never enters canonical definition bytes.

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
index emits the current index. Optional `when` uses the sole admitted predicate
kernel with the current root/item schema context.
Missing/inapplicable copies omit rather than invent null. Dotenv requires
non-null scalar members; Markdown requires string lists and escapes substituted
formatting/link-sensitive text. Admission checks Markdown list/copy/repeat/literal
result shapes and rejects nullable repeat sources before answer collection.
An explicit omit does not acquire a null result type.
Output remains data, not executable templates.

Dotenv destinations declare `environmentDialect`: `docker-run` uses literal
`KEY=value` assignments; `compose` uses double-quoted literals with escaped
backslashes/quotes and doubled dollars. A supplied empty value remains an
assignment and never requests inheritance from the host environment. Destination
keys use ASCII letters, digits and underscores, beginning with a letter or
underscore; case-insensitive duplicates reject. Values containing controls,
newlines, invalid Unicode or values exceeding the existing byte bounds reject
without partial output. Encoded line/file limits include escaping overhead.
`DotenvCodec` accepts the explicit `EnvironmentDestinationDialect` independently
of `DotenvPurpose`: existing template-placeholder restrictions remain separate
from actual destination values. Destination parsing never evaluates shell text;
Compose interpolation that cannot be resolved literally is refused.

Docker and Compose consumers were qualified using synthetic files with an
allowlisted environment, including a negative Compose host-variable precedence
case. Direct Docker used an installed image, an explicit reachable endpoint,
disabled networking and `--pull=never`; a codec round-trip alone does not
establish consumer qualification. See
[Docker environment files](https://docs.docker.com/reference/cli/docker/container/run/#env)
and [Compose interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/).

`DefinitionDraftArtifactComposer.Compose/ComposeBundle` returns disposable draft
artifacts, completions and value-free diagnostics. Only genuinely missing active
secret leaves defer; malformed supplied values and ordinary missing values block.
Inactive provider secrets are absent from both complete and draft output, including
placeholder fields and completion obligations. The composer uses the validator's
deferred locations rather than reconstructing applicability while copying objects
or repeated rows. Accepted inactive answers may remain in the transient workspace
for switch-back; revocation clears secret leaves regardless of applicability.
Unresolved secret-dependent outputs are withheld by artifact ID. Completion
locations identify remaining work, not proof of complete readiness. Draft dotenv
uses blank assignments and a warning; JSON/YAML placeholders remain draft-only.
Fatal bundle failure clears owned outputs; disposal invalidates reads/clears bytes.

`DefinitionValidator.ValidateDraft` is the shared draft seam.
Its internal concrete deferred-location set contains paths only, not a second
answer tree. The predicate kernel intersects an item reference with those
locations to preserve unknown for a missing secret. A different nonsecret field
in that row stays independently readable; the composer does not implement a
second missing-controller interpreter.
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

`DefinitionImport.GetCapability` describes bounded inspection and strict-autofill
support separately. The inverse admits exact direct copies, objects, structural
literals, fixed lists with stable positions, complete one-to-one repeats and
ordinal checks in supported JSON/YAML/dotenv projections. One unambiguous,
schema-valid public scalar equality selector may establish artifact context;
it is not a file-observed answer. Filtered rows, overlapping source assignments,
guessed hidden values and unsupported conditions refuse. Numeric declared names
remain properties; arbitrary source array-index mappings are not inferred.

`DefinitionImport.Inspect` owns one recovered-evidence snapshot and returns a
disposable assessment with a detached immutable value-free report. It checks
structural metadata and accounts for unknown input without printing private
names or values. The sole validator's supplied-evidence mode disables
initialization and distinguishes observed value, proved absence, unobserved and
context-derived value. A whole copy proves its subtree; projected children do
not prove an entire object's completeness or cardinality.

`InspectAsync` and `AssessAutofillAsync` accept a caller-owned cooperative yield
callback. Charged traversal yields every 1,024 work units without changing
semantic counters or limits. Validation additionally yields every 256 validated
nodes because path, dependency and constraint resolution makes those visits
more expensive; these checkpoints do not charge extra work.
Input and answer-snapshot canonicalization use the
same synchronous/asynchronous canonicalizer, with separate scheduling
checkpoints every 1,024 normalized nodes; these do not charge additional semantic
work. Validator container materialization likewise yields every 1,024 JSON nodes
without changing semantic charges. It writes private UTF-8 directly, clears its
owned byte buffer on every exit, and reuses the resulting immutable element only
within the current node's checks instead of serializing that node repeatedly.
Scalar materialization retains its bounded synchronous path.
`AnswerSnapshot.CreateAsync` shares admission and immutable ownership
with its public constructor. Whole root-member copies enter that snapshot
directly instead of being serialized through an intermediate JSON tree.
After recovery completes, its private observation dictionary transfers directly
to the immutable evidence owner. No caller retains mutation authority and no
second full dictionary is constructed at the publication boundary.
Cancellation is checked after suspension; Browser answers are not transferred
to a Web Worker.
The synchronous APIs require their shared no-yield operations to be completed
before reading their results.

Unknown/invalid controllers, including objects with invalid variant selectors,
remain unknown to dependent rules. Proven absence
retains existing missing-reference and property-assertion semantics.
Missing required is reported only where applicable absence is proved;
unrepresented obligations remain unassessed. Initialization cannot hide absent
file members. Normal `Validate` and `ValidateDraft` retain workspace semantics.

Partial root copies cannot establish whole-root property-count coverage.
Unproved minimum/maximum coverage remains unassessed, while a represented-member
lower bound that already exceeds a maximum is a known blocker. Proved
missing-required facts are emitted before subsequent traversal can hit a limit.

The report separates completion, compatibility, coverage, inventory and file-level
autofill eligibility. Bounded work or retention stops preserve known blockers
and report incomplete coverage/inventory with a retained lower bound, not an
invented exact total. Safe diagnostic facts are emitted during the same
validation traversal, so a later limit cannot discard earlier findings. Paging
does not repeat validation or change assessment completeness.

`DefinitionImport.Assess` remains strict atomic prefill over these same
primitives; the owned assessment's `AssessAutofill` rechecks a current snapshot.
Unknown content, structural mismatch, invalid supplied values, inactive mappings
and nonreplaceable conflicts cannot partially apply. Provenance-owned replaceable
defaults do not grant permission to overwrite explicit answers or omissions.
Imported members must survive the existing validator's effective-tree projection;
the importer does not implement another conditional evaluator. Strict rejection
returns no candidate. Successful prefill remains private answer data, not a
readiness or target-acceptance claim.

Strict failure retains only the public root diagnostic and a separate `Schema`
address. That address preserves admitted literal property names and stops before
rows or dynamic entries. It never contains a candidate's future
`DefinitionCorrectionLocation`; successful adoption must be followed by ordinary
current-workspace validation for correction authority.

File coordinates, declared schema locations and current-workspace correction
tokens are distinct authorities. Inspection never mints workspace corrections
from imported row/map ordinals. All raw import is potentially secret-bearing;
interactive acquisition/custody and deliberate adoption remain with Presentation
and its thin adapters. See [assessment boundaries](IMPORT_COMPARISON.md) and
[the operator workflow](../public/import-and-compare.md). Actual client and
resource qualification is recorded separately from this semantic contract.

### Captured editable Environment projection

`DefinitionImport.CaptureEditableScope(definition, artifactId, answers)` returns a
value-free `DefinitionEditableProjection`, separate from whole-file
`GetCapability`, `CanImport`, `Inspect` and strict prefill. It requires a selected
dotenv artifact with an object projection; otherwise it has no editable mappings.
`IsSupported` means only that at least one captured mapping is editable, not that
the artifact has a complete inverse.

Capture uses `DefinitionValidator.ValidateDraft` for effective values, inherited
activity and unresolved/deferred/error facts. Eligible members are valid dotenv
keys with direct root-scoped copies to non-root, non-nullable scalar paths through
declared object properties, without variant traversal. The source path and all
ancestors must be public. Dynamic entries, array indices and compound values are
not editable. Artifact/member conditions must resolve active, and the validator's
source node must be active.

`Coverage` contains definition keys, optional exact `DefinitionPath` pointers and
`DefinitionEditableCoverageKind`, not answers: `Editable`, `ReadOnly`, `Protected`,
`Inactive`, `ConditionUnresolved`, `Unsupported`, `Ambiguous` or `Controller`.
Literals/omit start read-only; repeated source paths and case-insensitive key
collisions exclude ambiguous assignments. Controller references are collected from
projection conditions and applicable source/ancestor conditions, recursively
including controllers' own applicability. A mapping equal to, above or below a
controller path is excluded. Coverage classification also accounts for activity
and ambiguity; it is not a second schema or disclosure authority.

The capture binds family, definition revision/digest, artifact ID and input
revision. `IsCurrent` checks definition identity and input revision and compares
recomputed coverage for the captured artifact. The workspace owner must separately
fence lifetime and consent; the method neither mutates nor rebases answers.
`TryConvertValue` accepts only captured editable keys, reuses
`DefinitionImport.TryDotenvValue`, then checks `DefinitionValidator.ValueValid`.
Strings retain explicit emptiness when allowed; booleans require `true`/`false`;
integer/decimal text must match the inverse primitive's exact serialized spelling.
Null and composite conversion are not inferred.

Dotenv codecs and contracts belong to `Configurator.Core.Dotenv`; the separate
`Configurator.Core.Environment` namespace owns the environment catalogue and its
diagnostics. The shared codec's explicit `DotenvPurpose.EditableValues` preserves empty strings
and normalizes one leading BOM and CRLF; default `TemplatePlaceholders` behavior
and ordinary import remain strict. The
[codec and import boundary](IMPORT_COMPARISON.md#editable-value-dotenv-codec)
owns exact limits and coordinate semantics. These Core seams do not establish
Browser Developer editing or atomic Apply delivery.

## Presentation, guidance and credentials

`Definition.Presentation` snapshots admitted sections/journeys and their
ordered pointers/section IDs. Wire uses `defaultJourney`, sections, journeys and
optional wizard/sections navigation. Section/journey IDs are bounded lowercase
kebab-case; titles 128, descriptions 2048; counts 64/16. No field-group selectors,
old experience aliases or presets are inferred. Detail maps basic/standard/
advanced to presentation disclosure without changing requiredness.

Optional `presentation.outputSelector` binds one concrete root `pointer` to
ordered `options`, each with an exact string enum `value`, public `title` (128)
and nonempty distinct `artifactIds` (64 references per option). Admission limits
options to 64 and requires exhaustive unique enum coverage. The selected field
and its root must be public and editable; nullable, conditional, read-only,
write-only, nested or non-enum bindings reject. Each referenced artifact must
exist with a root `when` that equals that option's value at the bound root
pointer. This is descriptive metadata for the existing typed answer edit, not
another activation evaluator. Initialization still belongs to Core; explicit
answers and omissions retain precedence. `DefinitionOutputSelector` snapshots
the path and read-only option/artifact collections.

Guidance has optional instructions (4096), four links, sixteen distinct search
terms (64 each). Multiline instructions permit technical plain text, not template
expansion. Links have bounded titles/URLs, static public credential-free HTTPS,
no query and optional fragment. Consumers render inert text and open deliberately
without automatic fetching, interpolation, opener/referrer or answer transfer.
YAML comments never become runtime help.

`DefinitionSecretSpecification` describes the admitted secret kind/acquisition,
constraints and optional generation recipe for a string. Kinds are password,
opaque-token, symmetric-key, private-key, password-hash, digest and connection-string.
Acquisition is local/existing/issuer. Only local acquisition admits generation;
connection strings never generate. The typed policy establishes the secret floor
through `DefinitionSensitivityPolicy`; reference merges reject downgrade attempts
and conflicting policies. Old credential syntax rejects. An unavailable profile
blocks readiness with `secret-profile-unavailable` rather than accepting unchecked
values. Admission and local policy checks grant no issuer or target authority.

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
zero is zero. This custom bounded-v0.0.1 profile is not an RFC8785/JCS claim.
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
pairs are minimal-self-hosted, grafana-13.2.2, islamu-event and secret-generation.
`DefinitionSecretSchema` assembles the closed secret recipe alternatives inside
the shared schema; the native generator remains the only generated-file owner.
Fresh definition families/revision 1 are recorded from source in the
[integration guide](../public/integrate-your-tool.md#complete-minimal-configuratoryaml-example).
Changing the development contract requires regenerated current runtimes and a fresh
source/runtime agreement check. Historical results do not qualify changed payloads;
target acceptance, package/host closure and complete interactive qualification
are separate evidence boundaries.

Synthetic answer/output fixtures are not production answers and are never Browser
assets. Grafana format projections do not establish startup, provisioning or
connectivity. Event target semantics remain Event's authority.

## Event coverage handoff

The current Event example and separate Store family declare Standalone/all,
separate-image scopes and Compose component scopes through the same admitted
deployment metadata. Only full Compose includes the separate migration
recipient. Explicit per-role predicates keep API setup, migration and other
partition credentials inactive under Blazor-only scope, even when retained
hosting controls would otherwise require API setup. Process projections map
role Infisical identities into `SecretProvider__Infisical__*`; Compose preserves
its orchestration namespaces. Canonical `ERASURE_*` answers replace duplicate
`PRIVACY_ERASURE_AUTHORITY_*` answer aliases. Event projects the canonical
erasure group into its own internal sections, so each setting is emitted once.
Combined admin origin instead has two distinct consumers: API origin reporting
and BFF host classification. Its two scalar target keys share one answer;
inverse adoption requires matching presence and equal typed values. The
[maintained inventory](../../examples/islamu-event/provider-contract-inventory.md)
records committed defaults, source-only evidence and explicit unsupported
target fields. Synthetic partition material is not target publication authority.

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
platform catalogue ownership does not move into Core. `CompositionCompiler`
is a bounded offline normalizer, not an Event DTO builder.
Its ordered `IEnumerable<CompositionSource>` overload accepts existing immutable
JSON/YAML sources without paths. Input bytes, normalized nodes, scalar totals,
YAML parser events and merged mapping cardinality are aggregate ceilings;
serialized JSON must also fit the 4 MiB source/output ceiling. Each successful
YAML parser advance charges stream/document markers, keys and container endings
to the same compilation budget. JSON fragments do not reset that budget or
consume YAML events. Separate compilations, definition admission and imports
retain independent budgets. Directory traversal, source rechecks and the native
barrier belong to Artifacts, as described in the
[native acquisition decision](adr/ADR-native-directory-acquisition.md).
`SecretGenerator` consumes the admitted `DefinitionSecretSpecification`, not
Event keys or environment-specific credential hints. Shared validation,
generation eligibility, entropy floors and result disposal belong to the
[cryptography contract](SECRET_CRYPTOGRAPHY.md). Host-supplied scale-profile
evidence cannot transfer Event benchmark hashes or target-acceptance claims.
Generation requires an explicit caller action; admission and validation never
generate a value. `SecretGenerationBounds.From` exposes schema-derived bounds
(`MinLength`..`MaxLength`, `MinBytes`..`MaxBytes`, `ExactByteCount`, required
character classes, and the `128`-bit free-draw entropy floor) and `Evaluate`
validates `SecretGenerationOptions` before generation or preview. `SecretGenerator.Generate`
supports optional tightened `SecretGenerationOptions` and an injected `RandomNumberGenerator`
for uniform 32-bit rejection sampling and zeroes temporary byte/char working
buffers via `CryptographicOperations.ZeroMemory`. A 32-byte opaque token encodes to 43 unpadded Base64url
characters: decoded byte policy and ordinary string length constraints both
apply, in different units.

Secret kind, acquisition, algorithm, encoding, character set, curve and input
source are closed typed contracts. `DefinitionSecretVocabulary` maps their exact
wire spellings explicitly, including `password-hash`, `RS256`, `P-256` and
`pkcs8-pem`; CLR casing and numeric enum values do not define the format.
Undefined enum values reject at cryptographic entry points. Canonical definition
bytes remain owned by admission, so this internal typing does not alter digests.

`DefinitionSecretOperations` owns pure admitted-schema traversal and immutable
answer replacement for CLI and Presentation. It resolves ordinal data paths to
typed validation locations, rejects read-only or explicitly omitted ancestors,
and requires an active locally generatable leaf. Replacement preserves unrelated
answers and omissions. Presentation still owns consent, stable editor identity,
operation generations and the final guarded mutation; Core grants no workspace
or publication authority.

Archived `SetupCompositionInvariantTests` inventory checks are not parser-attack
proof. Production boundary tests and actual target acceptance must be recorded
separately in the owning execution records linked from [STATUS](STATUS.md).
