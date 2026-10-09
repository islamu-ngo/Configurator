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

`SetupAnswerSnapshot(long inputRevision,
IReadOnlyDictionary<string, JsonElement> values)` clones answer values.
`DefinitionValidator.Validate(SetupDefinition, SetupAnswerSnapshot)` returns
`DefinitionValidationResult` with `IsReady`, `Fields`, and value-free
`Diagnostics`. `Fields` expose `Id`, `IsActive`, `IsRequired`, `IsProvided`,
`HasError`, and `IsVisible(SetupDisplayMode)`. Display modes are `Guided`,
`Advanced`, and `AllDeclared`; they never change validation.
Object fields inherit the strongest sensitivity of their nested properties,
so a public parent label cannot hide protected decisions in Guided mode.

`Configurator.Core.Artifacts.DefinitionArtifactComposer.Compose(SetupDefinition,
string artifactId, SetupAnswerSnapshot)` returns `DefinitionArtifactResult`
with `Succeeded`, `Artifact`, and `Diagnostics`. A successful
`DefinitionArtifact` exposes `Bytes`, `Digest`, `DefinitionDigest`,
`DefinitionRevision`, `InputRevision`, `ArtifactId`, `FileName`, `MediaType`,
`Sensitivity`, `ApplySemantics`, and `Restart`.
All answer-derived artifacts are `Restricted`, including those whose fields
are declared public. No definition value grants filesystem or network authority.

`DefinitionImport.Assess` provides a bounded inverse for present, unconditional
root-object `copy` members in JSON, YAML and dotenv artifacts. Duplicate,
unknown, transformed, conditional, inactive or conflicting input rejects with
a value-free code. It returns an answer snapshot without mutating the caller's
current workspace. A partial prefill may still lack required native answers;
it does not imply native readiness or target acceptance.

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
`minimum`, `maximum`, `minItems`, `maxItems`, `itemType`, `properties`, `validator`.
Default/recommended values are allowed only on public fields. `properties`
contains nested field definitions; nested IDs are property names.
`itemType` is a scalar type for lists. Repeated object structures use list
projection items or a bounded repeat over a list field, never executable code.

Conditions use `{"op":"equals","field":"mode","value":"external"}` and the closed
operations `present`, `equals`, `notEquals`, `in`, `all`, `any`, and `not`.
Compound conditions use `operands`. `in` uses an array `value`.
References must identify declared fields; activation dependency cycles fail.

Projection operations are `literal` (`value`), `copy` (`field`), `omit`,
`object` (`members`), `list` (`items`), and `repeat` (`field`, `item`).
Within a repeat, `{"op":"copy","field":"$item"}` copies the current scalar item,
and `{"op":"index"}` emits its zero-based index. These two forms are illegal
outside a repeat; nested repeats are rejected.
Optional `when` uses the same condition grammar. Missing/inactive copies omit
the value; omitted list entries are removed, not converted into nulls.
Formats are `dotenv`, `json`, `yaml`, and `markdown`. Dotenv requires an object
of scalar values. Markdown accepts a `list` of string literals/copies and joins
them without interpreting templates; active HTML and link syntax in substituted
values are escaped. The engine makes no legal-validity claim.

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
The inherited local YAML dialect accepts exact booleans, null and integers;
ambiguous numeric scalars (including floating-point syntax) remain rejected.
JSON supports the bounded decimal canonicalization profile.

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
