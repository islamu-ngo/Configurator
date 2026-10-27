# Core definition contract

Status: implementation contract for `configurator-extraction-r2`.

## Public API

`Configurator.Core.Definitions.SetupDefinitionCodec.ParseJson(ReadOnlyMemory<byte>)`
and `ParseYaml(ReadOnlyMemory<byte>)` admit a closed, bounded definition or throw
`SetupDefinitionException` with a value-free `Code`. `SetupDefinition` exposes
`FamilyId`, `Revision`, `Digest`, `CanonicalBytes`, `Title`, `Publisher`,
`ApplicationId`, `ApplicationVersions`, `EntryPoints`, `Locales`, `Fields`, and
`Artifacts`. Admission snapshots all data. The digest is computed, never supplied
inside the definition.

`SetupDefinitionRegistry(IEnumerable<SetupDefinition>).Resolve(Guid familyId,
string? appVersion = null, ArtifactDigest? digest = null)` selects the highest
compatible revision when `appVersion` is explicit, or rejects an unknown family, incompatible version, or
digest mismatch. A family/revision pair cannot identify two different digests.
Omitting both version and digest requires exactly one registered revision;
ambiguous unversioned requests fail rather than selecting the newest revision.
Compatibility is an exact declared version set, not a guessed version range.
Family identity and publisher claims do not establish trust.

The checked [minimal self-hosted definition](../../examples/minimal-self-hosted/setup-definition.json)
is the canonical neutral sample for native clients and the Browser bundle. Its
family, revision and canonical digest must match across those surfaces; Core's
native conformance test pins literal `.env`, JSON and YAML output and readiness.
The sample definition contains a required credential field without
a default. The checked answer and expected-output fixtures use an explicitly
synthetic placeholder; those answer files are never embedded in Browser.
The separate [Grafana OSS 13.2.2 example](../../examples/grafana-13.2.2/README.md)
proves declared format projection against checked synthetic vectors only. It
has not been accepted or executed by a Grafana target.

`SetupAnswerSnapshot(long inputRevision,
IReadOnlyDictionary<string, JsonElement> values)` clones answer values.
`DefinitionValidator.Validate(SetupDefinition, SetupAnswerSnapshot)` returns
`DefinitionValidationResult` with `IsReady`, `Fields`, and value-free
`Diagnostics`. `Fields` expose `Id`, `IsActive`, `IsRequired`, `IsProvided`,
and `HasError`. `Nodes` carries value-free literal child/row paths and obligation,
presence, missing and validity facts for shared progress. Diagnostics keep their
root `FieldId` and snapshot relative segments in `RelativePath`; duplicate rows
identify the row and declared uniqueness property without including its value.
Presentation owns disclosure; Core has no display-mode policy.
Object fields inherit the strongest sensitivity of their nested properties,
so a public parent label cannot demote protected decisions.

`Configurator.Core.Artifacts.DefinitionArtifactComposer.Compose(SetupDefinition,
string artifactId, SetupAnswerSnapshot)` returns `DefinitionArtifactResult`
with `Succeeded`, `Artifact`, and `Diagnostics`. A successful
`DefinitionArtifact` exposes `Bytes`, `Digest`, `DefinitionDigest`,
`DefinitionRevision`, `InputRevision`, `ArtifactId`, `FileName`, `MediaType`,
`Sensitivity`, `ApplySemantics`, and `Restart`.
All answer-derived artifacts are `Restricted`, including those whose fields
are declared public. No definition value grants filesystem or network authority.

`DefinitionDraftArtifactComposer.Compose` and `ComposeBundle` are distinct
secret-deferred APIs. Their disposable results expose `Artifact`/`Artifacts`,
value-free `Diagnostics`, `Completions`, `Succeeded` and `IsDraft`.
`DefinitionDraftCompletion(ArtifactId, FieldPath, OutputPath)` identifies where
the operator must finish a secret: field paths start with the root ID and use
`/index/property`; output paths use JSON Pointer with `~0`/`~1` escaping.
Draft dotenv emits blank assignments and a draft-warning comment. JSON/YAML
emit empty strings for string/enum secret leaves and null for other secret
leaves; Markdown emits an empty string within the declared list projection.
No placeholder escapes into complete composition.

Draft preparation reuses the same bounded interpreter, serializers and
validator. Missing ordinary fields, malformed provided values and unresolved
secret-dependent conditions reject; strict `DefinitionValidator.IsReady` and
complete composition remain unchanged. An unresolved artifact is withheld with
its artifact-ID diagnostic while unrelated bundle artifacts may remain usable.
A fatal bundle failure clears every owned output/completion. Results own their
artifact buffers and disposal invalidates reads and clears bytes. Neither
`IsDraft` nor successful preparation grants target acceptance.

`DefinitionValidator.ValidateDraft` exposes the shared draft-validation seam
for associated ordinary-field errors without changing strict `Validate` or
complete readiness. `DefinitionBundleComposer.ComposeActive(definition,
answers)` prepares the browser's currently active complete artifact set from
one snapshot. It skips only `artifact-omitted`; fatal/nonsecret validation and
aggregate-budget failures clear the whole retained bundle. Zero active outputs
returns `artifact-empty`, never an empty successful preparation. Native
`Compose` with explicit artifact selection remains strict, including rejection
of a selected omitted artifact. Adapters do not reproduce this selection logic.

Bundle composition creates one internal `ValidatedComposition` bound to its
admitted definition, immutable answer snapshot and complete/draft mode. It
validates once and composes each emitted output once in declared order. No public
entrypoint accepts caller-supplied trusted validation; single-artifact APIs still
validate through the same owner. Fatal or aggregate-limit failure disposes the
whole staged bundle, while draft withholding retains its distinct diagnostics.
Optional cancellation tokens are observed at validation/projection boundaries
and before output transfer; native and CLI callers forward their operation token.
These changed managed signatures require rebuilding compiled Core consumers;
source callers may still omit the optional token.

The local `Configurator.Core.Preparation` diagnostic listener emits only public
artifact IDs and a draft-mode boolean when subscribed. Activity-scoped tests
measure completed validation/composition events independently of mock calls;
there is no transport, answer payload or retained measurement history.
See the [single-pass ownership decision](adr/ADR-single-pass-preparation-ownership.md).

`DefinitionImport.Assess` provides a bounded inverse for present, unconditional
root-object `copy` members in JSON, YAML and dotenv artifacts. Duplicate,
unknown, transformed, conditional, inactive or conflicting input rejects with
a value-free code. It returns an answer snapshot without mutating the caller's
current workspace. A partial prefill may still lack required native answers;
it does not imply native readiness or target acceptance.
`CanImport` uses the same mapping admission as `Assess`. Successful results carry
immutable `ProvidedFields` root identifiers. Callers may pass a provenance-owned
set of replaceable default roots; absent that permission, existing unequal
answers remain conflicts. Full merged snapshot limits and validation still apply.
Rejected imports may carry a value-free `Diagnostic` with an admitted root ID
and literal relative path for conflicts or validation failures. Unknown input
keys never become diagnostic field IDs. Rejection still returns no answer
snapshot; diagnostic metadata does not authorize applying any imported value.

`DotenvEntry`, `DotenvDocument`, `DotenvCodec`, `ArtifactDigest`, and
`SetupArtifactKind`/`SetupArtifactPolicy` remain writer-compatible. The old
Event-specific manifest, tenant-package, identity, legal DTO, and platform
catalogue APIs are removed. Their target-side contracts remain Event's concern.
`SetupCompositionCompiler` remains a bounded offline JSON/YAML/directory
normalizer; results expose canonical bytes and digest, not Event DTOs.
`LocalSecretGenerator.Generate` now accepts an `EnvironmentVariableDefinition`
instead of an Event key string. Only declared secret fields can request the
existing 256-bit URL-safe generator; the entropy floor and disposal behavior
are unchanged.
`SetupCompositionScaleProfiles` is now an instance registry supplied with
`SetupCompositionScaleProfile` evidence by the host. Event benchmark hashes and
target-acceptance claims are not transferable evidence for Configurator.
Admission still rejects mismatched evidence, incompatible output ceilings,
unknown names and the disabled expanded profile without increasing any limit.

## Definition JSON

The root is flat and closed. This is an independently designed envelope:
the research document's nested metadata/spec example was speculative, not a
required wire format. The following is a minimal neutral example:

```json
{
  "schemaVersion": 1,
  "familyId": "69cdb122-41d2-4fab-9b82-7929a889c726",
  "revision": 1,
  "applicationId": "example.service",
  "applicationVersions": ["1.0"],
  "publisher": "example",
  "title": "Example service",
  "entryPoints": ["setup"],
  "locales": ["en"],
  "fields": [
    {
      "id": "service.name",
      "type": "string",
      "required": true,
      "sensitivity": "public",
      "disclosure": "guided",
      "title": "Service name"
    }
  ],
  "artifacts": [
    {
      "id": "config",
      "format": "json",
      "fileName": "service.json",
      "applySemantics": "create",
      "restart": "process",
      "projection": {
        "op": "object",
        "members": {
          "name": {"op": "copy", "field": "service.name"}
        }
      }
    }
  ]
}
```

Field types: `string`, `boolean`, `integer`, `number`, `enum`, `list`, `object`.
Optional field metadata: `help`, `group`, `format`, `default`, `recommended`,
`consequence`, `activeWhen`, `requiredWhen`, `choices`, `minLength`, `maxLength`,
`minimum`, `maximum`, `minItems`, `maxItems`, `itemType`, `properties`, `uniqueBy`, `validator`.
Default/recommended values are allowed only on public fields. `properties`
contains nested field definitions; nested IDs are property names.
`itemType` is a scalar type or `object` for lists. Object lists declare
`properties` and validate each row with the same closed property grammar,
required/type/unknown-member checks and nested sensitivity floor as objects.
`uniqueBy` is a nonempty path of declared property names ending at a string
field; values are compared ordinally across rows and duplicate diagnostics
never include the key value. Admission rejects invalid paths and private
defaults/recommendations, including those hidden below public object/list
labels. No new projection language or executable row behavior is introduced.

Conditions use `{"op":"equals","field":"mode","value":"external"}` and the closed
operations `present`, `equals`, `notEquals`, `in`, `all`, `any`, and `not`.
Compound conditions use `operands`. `in` uses an array `value`.
References must identify declared fields; activation dependency cycles fail.

Projection operations are `literal` (`value`), `copy` (`field`), `omit`,
`object` (`members`), `list` (`items`), and `repeat` (`field`, `item`).
Within a repeat, `{"op":"copy","field":"$item"}` copies the current scalar or object item,
and `{"op":"index"}` emits its zero-based index. These two forms are illegal
outside a repeat; nested repeats are rejected.
Optional `when` uses the same condition grammar. Missing/inactive copies omit
the value; omitted list entries are removed, not converted into nulls.
Formats are `dotenv`, `json`, `yaml`, and `markdown`. Dotenv requires an object
of scalar values. Markdown accepts a `list` of string literals/copies and joins
them without interpreting templates; active HTML and link syntax in substituted
values are escaped. The engine makes no legal-validity claim.

## Presentation, guidance and credentials

`SetupDefinition.Presentation` is an optional immutable `DefinitionPresentation`
snapshot. Its `DefaultExperience` names one of `Experiences`; `Sections` contains
`DefinitionSection` snapshots (`Id`, `Title`, nullable `Description`, `Disclosure`,
ordered root-ID `Fields`). `DefinitionExperience` exposes `Id`, `Title`, nullable
`Description`, `InitialDisclosure`, `Navigation` and ordered section-ID `Sections`.
Disclosure properties use `SetupFieldDisclosure`; navigation is the admitted
`guided`/`sections` string. Omission defaults disclosure to Guided and navigation to
`sections`, without inserting synthetic fields into canonical content. Collections
are copied into read-only arrays, never caller-owned mutable lists.

Wire `presentation` requires `defaultExperience`, `sections`, `experiences`.
Each section requires `id`, `title`, nonempty `fields`; optional members are
`description` and `disclosure`. Each experience requires `id`, `title`, `sections`;
optional members are `description`, `initialDisclosure`, `navigation`.
Section/experience IDs are unique within their own namespace, lowercase ASCII
kebab-case starting with a letter, at most 64 characters. Titles are bounded at
128 and descriptions at 2,048. Admission requires 1-64 sections, 1-16 experiences,
exactly one section per root and every section once per experience. It rejects
unknown/default/duplicate/overlapping/empty references and does not admit nested
layout paths. Disclosure vocabularies are `essential`, `guided`, `advanced`;
navigation is `guided`, `sections`. No answer presets or executable actions exist.

`SetupFieldDefinition.Guidance` optionally exposes `DefinitionFieldGuidance`:
nullable `Instructions`, read-only `Links` of `DefinitionGuidanceLink(Title, Url)`,
and read-only `SearchTerms`. Its closed wire members are `instructions` (4,096),
`links` (at most four, with title 128 and URL 2,048), `searchTerms` (at most sixteen
ordinally distinct nonempty strings, 64 each). Empty optional lists/descriptors are
neutral. Existing `Text` admission rejects blank supplied strings, controls and
HTML delimiters. Instructions additionally reject backticks, Markdown link syntax
and `${`/`{{`/`}}` template markers. Absolute credential-free HTTPS links reject
whitespace and any query/fragment delimiter, including empty queries/fragments.
Metadata never executes commands or expands values. Consumers must render inert
text and open links only deliberately, with no fetch/prefetch, opener or referrer
transfer. Existing `Help` (4,096) and `Consequence` remain separate and unchanged;
YAML comments never become runtime data.

`SetupFieldDefinition.InputPurpose` optionally exposes immutable
`DefinitionInputPurpose(Kind, Acquisition)` from the wire `credential` descriptor.
It describes input intent, not a credential value or live authority. Closed kinds are `password`,
`opaque-token`, `symmetric-key`, `private-key`, `connection-string`; acquisition is
`existing`, `create`, `issuer`. `create` accepts only password/symmetric-key and
`issuer` excludes symmetric-key. Admission requires a string leaf whose own or
ancestor classification is secret, including object-list properties. The inherited
context is tracked during recursive admission independently of `publicAncestors`,
so credential admission cannot weaken private-default/recommendation rejection.
Descriptors never change sensitivity or validation. A public leaf's declared
sensitivity remains public even when credential admission uses its secret ancestor.
Certificates use guidance; non-password descriptors do not grant password-autofill
semantics. No `generation` or duplicate `comment` alias is admitted.

The compiler expands authoring `groups`/logical references before the runtime
boundary; see [source vocabulary](MAINTAINER_AUTHORING.md#authored-presentation-and-guidance).
Runtime has only root-ID `fields` references, never group selectors. `DefinitionDocument`
and its closed wire records feed `DefinitionSchema`; `DefinitionAuthoringSchema`
derives the source shape and adds the sole group-selector vocabulary. Both remain
generated through the native schema generator, with semantic cross-reference,
inherited-secret and resource checks at admission.

All descriptors are canonical definition content; changing them changes the digest
and conflicts with an already admitted family/revision. Optional absence is neutral
for machine-only definitions, not a legacy visibility policy. This increment admits
public data only; Presentation owns any derived neutral experience and later
client navigation, never Core validation or answer presets. Public procedures are
in [definitions](../public/definitions.md#authored-journeys-and-field-guidance).

## Canonicalization and bounds

Canonical JSON uses ordinal property order, preserved array order, invariant
decimal numbers, UTF-8 without BOM, and the standard System.Text.Json escaping.
Equivalent numeric spellings normalize; unsupported/out-of-range numbers fail.
This is the Configurator v1 canonicalization profile, not an RFC 8785 claim.
The SHA-256 digest covers the entire admitted document, including revision.
Source limits retain 4 MiB aggregate bytes, depth 32, 4,096 map/list entries,
65,536 nodes, 65,536 scalar characters, and 1 MiB aggregate scalar characters.
Definitions further cap fields at 1,024, artifacts at 64, and repeated items at
4,096. YAML admits one document and rejects aliases, anchors, tags and directives.
The local YAML dialect accepts lowercase booleans/null and JSON-shaped numeric
tokens, including decimal/exponent forms, through the same exact bounded decimal
canonicalization as JSON. Quoted/literal/folded scalars remain strings. Empty plain
scalars and non-string resolved keys reject; quote intended numeric/boolean/null
keys. Duplicate/NFC/case collisions and merge keys (including quoted `<<`) reject.
Unsupported numeric forms never round or underflow into an admitted value.

## Event coverage handoff

Target-specific tests and original source ownership are recorded below during
the port. No archived target vector is counted as a passing Configurator test.
The parent must port those vectors into Event's definition/exporter and backend
contract tests before declaring the complete extraction workstream finished.

The original target source and test files are preserved verbatim, after the
already-completed namespace rename, as `.cs.txt` under
`tests/Configurator.Core.Tests/EventHandoff/Source` and `EventHandoff/Tests`.
These are handoff evidence, not compiled compatibility shims:

| Original owner | Event handoff obligation | Active Configurator replacement |
| --- | --- | --- |
| `PlatformEnvironmentCatalogue`, `PlatformEnvironmentMetadata`, `EnvironmentCatalogueInvariantTests` | Export the Event environment catalogue, documentation parity and activation vectors from Event-owned metadata. | Generic catalogue remains; neutral activation/collision/sensitivity tests exercise it. |
| `IdentityCorrelationEnvironment`, `IdentityCorrelationEnvironmentTests` | Preserve indexed issuer keys, normalization and duplicate-issuer rejection in the Event exporter/backend. | Explicit bounded list fields and repeat/index projections; no issuer-name special cases. |
| `ReservedFingerprintEnvironmentTests` | Preserve Event privacy-erasure key classifications and startup ownership. | Core tests restricted output independently of labels. |
| `EmailOptionalSetupTests` | Preserve optional mail dispatch and pinned local-capture topology in Event. | No deployment orchestration exists in Core. |
| `OfflinePortabilityContracts`, `OfflinePortabilityWorkflow`, `OfflinePortabilityWorkflowTests` | Preserve Event manifest/package selection, full semantic validation, sections, legal and state-transition vectors in Event. | Bounded typed projections, readiness, snapshotting, workflow, diff and coverage tests. |
| `OperatorIdentityManifestCodec`, `OperatorIdentityManifestTests` | Preserve Event identity document validation, revision and content digests. | Definition family/revision/digest and artifact input-revision/digest tests; structured Markdown remains data. |
| `OfflineLegalDraft` | Preserve Event legal draft scope, audience, provenance and approved-template policy in Event. | Declared string/list projection only; no publication/compliance authority. |
| Mixed `DotenvContractTests` | Preserve Event bootstrap mode/provider/name-pair/identity/email rules in Event. | `NeutralEnvironmentInvariantTests` retains production codec rejection vectors, byte round trips, bounds, provenance, readiness and ambiguity checks. |
| `SetupCompositionScaleTests` generated measurements | Keep measured Event host/Wire/target evidence with Event; do not relabel it as Configurator validation. | Host-supplied scale evidence admission and aggregate-only telemetry tests. |
| `SetupCoreArchitectureTests` Wire-vocabulary assertion | Event owns the availability of its SetupLive Wire contracts. | Compiled guard now rejects every Event/Explore assembly reference and inspects the actual definition/artifact public API. |

`SetupCompositionInvariantTests` and its independent contract vectors remain,
but the original 15 tests mostly inspect the vector inventory rather than run
attacks against the parser. They are not presented as runtime parser proof.
The new definition, neutral parser, dotenv and projection tests exercise the
production boundaries directly.
