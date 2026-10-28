# Maintainer authoring contract

> **Audience:** Maintainers | Contributors | AI agents
> **Status:** Implemented bounded-v2; qualified pre-release Linux x64 and Browser scope
> **Owner:** Core | Native authoring tooling | Contributor Experience
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/Authoring/`, `src/Configurator.Core/Definitions/SetupDefinitionSourceCodec.cs`, `src/Configurator.Cli/SetupCliDefinitionHandlers.cs`, `schemas/configurator-authoring-v2.schema.json`

## Source and admission

The discriminator is `$schema: urn:islamu:configurator:authoring:v2`.
Required roots are `identity`, `project`, `applicationVersions`, `configuration`
and `outputs`; optional roots are `entryPoints`, `presentation`, `localization`,
`annotations`. Unknown members reject. Source v1 is not an alternate parser path.
The [public reference](../public/definition-reference.md) and
[standard](../public/definition-standard.md) project the implemented bounded-v2
contract. Use the [integration workflow](../public/integrating.md) for adoption,
the [cookbook](../public/definition-cookbook.md) for supported patterns, and the
[technical conformance profile](DEFINITION_CONFORMANCE_PROFILE.md) for detailed
admission mechanisms and limits.

`DefinitionAuthoringCodec.ParseJson/ParseYaml` snapshots bounded exact-key source.
`DefinitionAuthoringCompiler.Compile` builds the runtime envelope, expands output
mappings, then calls `SetupDefinitionCodec.AdmitCompiled`. That owner computes
`profile: bounded-v2` and sorted `requiredCapabilities`, and finishes through
public runtime admission. Runtime parsing verifies the computed set rather than
trusting authors. No second evaluator or general JSON Schema validator exists.

`SetupDefinitionSourceCodec` selects source by the exact `$schema` discriminator
or runtime by `schemaVersion: 2`. Conflicting/unknown discriminators reject;
there is no parser fallback. Public source, runtime, private answer and command
versions are independent. Private answers use `answerContractVersion: 1`;
the command envelope is v3 and check/build handshake versions are 2/2.

Lint shares the source admission path and adds bounded warnings without
publication. Schema discovery returns built-in public documents only; it runs
before authoring-file authority. Precise compiler spans and revision-bound
correction tokens are serialized through the CLI sanitizer, not exception text.
See [the machine contract](CLI_CONTRACT.md) for paging, digests and location bounds.

Project requires `id` and `name`; publisher defaults to name. Optional description,
credential-free public HTTPS repository and definition license are distribution
metadata, not trust. Family is a nonempty UUID; revision is positive. Compilation
never allocates or advances identity. Versions are exact strings; entry points
default to `setup`. Locales derive from localization or default to `en`.

The CLI's public-source adapter uses `BoundedFileInput` for worker-owned bounded
open/stat/read (4 MiB per file). Empty input, changed length and oversize yield
`definition-source-bound`; errors reveal neither content nor private paths.
The adapter returns caller-owned bytes after clearing bounded storage. Shared
acquisition does not give handlers ambient filesystem or private-import authority.

## Fields and projections

`configuration` is a non-null object schema. Objects explicitly choose
`additionalProperties: false` or a typed recursive map schema; arrays require recursive `items`.
`properties` keys are exact names. Types are string, boolean, integer, number,
object and array; nullable type is exactly one base type plus `"null"`.
Enums are typed homogeneous scalar values, optionally including null for a
nullable type. Inclusive/exclusive numeric bounds, exact multipleOf, scalar
length, item/property cardinality, uniqueItems and scalar const are admitted.
Closed discriminated provider branches and acyclic configuration-root local
references are supported. Format is annotation unless assertFormat is true.

`x-configurator` admits sensitivity, initialValue, suggestedValue, notBlank,
uniqueBy, applicableWhen, assertFormat, detail, guidance, credential, choices and units.
Public initial/suggested/default/example/const metadata must pass privacy and
value admission. Private ancestors prevent literal-value leakage through public
leaves; protected descendants raise effective container sensitivity.
Missing sensitivity remains sensitive. Unknown members, invalid constraints,
reference errors, cycles and resource overflow reject with value-free codes.

`SetupFieldDefinition.Name` preserves the exact root data key; `Id` is bounded
metadata. Names fitting the identifier grammar retain their ID unless they start
with reserved `f-`. Other names use `f-` plus full SHA-256 of `"field:"` and the
escaped exact root pointer. Case and Unicode composition are not normalized.
This also hashes literal names beginning `f-`, preventing generated-ID collisions.
Neither human labels nor traversal order determines data identity.

References are closed `{scope: root|item, path: <RFC6901 pointer>}` objects.
Empty pointer selects the context root, `/` the empty-name property; `~0` and
`~1` escape tilde/slash once. Dots and asterisks are literal names. Array indices
are canonical decimal tokens without leading zeroes; `-` is not an existing item.
Item scope is legal only in an admitted repeat context.

Outputs are sanitized basename-keyed objects requiring `format`. Output IDs
default to representable `out-<filename>` or a domain-separated `o-` SHA-256;
explicit bounded IDs are permitted. Expansion uses ordinal output/binding order.
`targetUpdateMode` and `restartRequirement` both default to `unspecified`.
They replace old source `applySemantics`/`restart` vocabulary and grant no apply
or protected-write authority.

Dotenv bindings map literal environment names to scoped non-null scalar
references. JSON/YAML bindings map concrete target pointers to scoped references;
the compiler constructs object members and rejects overlapping target paths.
No dotted-name nesting is inferred. Bindings and projection are mutually
exclusive. Unmapped JSON/YAML copy the effective configuration tree; dotenv and
Markdown require explicit mappings/projections. Projections retain the closed
literal/copy/object/list/repeat/omit/index operations with scoped references.
Generated projection nodes/text are budgeted during expansion before admission.

## Effective values and relationships

Standard `default` is descriptive only. Core resolves eligible `initialValue`
at never-supplied locations, never a separate client seeding policy.
`suggestedValue` changes nothing without deliberate adoption. Explicit omission
suppresses initialization; reset removes override and suppression. Child initial
values do not create absent containers, and required children do not manufacture
an absent optional object. Required means property presence, not nonblank text.
Null is supplied only when nullable; false, zero and empty containers are supplied.

Private snapshots contain exact-name `values` plus distinct concrete `omitted`
pointers under answerContractVersion 1. Supplied/suppressed conflicts and
overlapping omissions reject atomically. Validation, drafts and composition
consume the same effective configuration.

Applicability is admitted on declared properties, including nested properties
and array rows. Root-scoped references can select declared nested controllers;
item-scoped references select only the current array item. Configuration-root
applicability rejects. Invalid or secret-deferred controllers propagate unknown,
not a guessed branch. Object-local `if` guards admit required presence and
property const/enum tests; `then` and optional `else` contain local required-name
sets. `dependentRequired` is also local, including in nested objects and rows.
These are relationship assertions, not visibility toggles or activation edges;
arbitrary conditional subschemas remain unsupported.

Definition source maps carry bounded public schema pointers, document ordinals
and numeric spans through decoding/lowering. Errors use known positions, or an
enclosing safe position when necessary, never raw scalars, arbitrary unknown
keys or native paths. Answer correction coordinates are separate, typed and
revision-bound; private map names become entry ordinals.

## Authored presentation and guidance

Presentation uses sections (`id`, `title`, concrete `pointers`, optional
description), journeys (`id`, `title`, ordered section IDs, optional description),
`defaultJourney` and optional `navigation` (`wizard`/`sections`). Section/journey
IDs are bounded lowercase kebab-case, titles 128 and descriptions 2048 characters;
section/journey counts remain bounded at 64/16. Concrete pointers resolve against
the admitted configuration. There are no old group selectors or experience
aliases. Detail is basic/standard/advanced, independent of requiredness.
Absent presentation is neutral, not an answer preset.

Schema `description` is the ordinary explanation. Extension guidance has optional
instructions (4096), at most four links and sixteen search terms. Instructions
admit multiline plain text and technical punctuation, not template expansion.
Static credential-free HTTPS links may have fragments, not queries. Rendering
and deliberate opening must add no fetching, interpolation or answer transfer.
Useful secret-acquisition help remains an integration-review obligation.

Credential descriptors require an effective secret string context. Kinds are
password, opaque-token, symmetric-key, private-key and connection-string;
acquisition is existing/create/issuer. Create is only password/symmetric-key;
issuer excludes symmetric-key. Intent grants no generator or authentication.

Localization admits `defaultLocale`, locale-keyed translations, optional
ltr/rtl direction and overrides pointing to existing public text. The compiler
derives sorted runtime locales; runtime checks agreement. Native English/Arabic
and Browser RTL observations are qualified only within the recorded pre-release
scope below. Broader localization policy, translation quality and unexercised
cross-client behavior remain followups.
All admitted metadata participates in canonical identity; YAML comments do not.

## Syntax and identity editing

Both formats use exact bounded decimal canonicalization with no floating-point
rounding. YAML is JSON-shaped strings, numbers, lowercase booleans/null, sequences
and mappings with comments/multiline strings. Quote typed-looking keys. Empty
plain scalars, non-string resolved keys, exact duplicates, merge keys even quoted,
directives, tags, anchors, aliases and multiple documents reject. Case- and
NFC-distinct data names remain distinct; invalid Unicode rejects.

`DefinitionAuthoringIdentityEditor.UpdateRevision/Fork` patches parser scalar
spans and recompiles. JSON offsets are UTF-8 bytes; YAML marks are decoded
character offsets. Other source bytes, comments, quotes, BOM and newlines are
preserved. Fork allocates a new family/revision 1. Native source editing is
public-file custody, not the protected answer writer or filesystem CAS against
an uncooperative external editor.

## Local clients and distribution

Local clients select JSON/YAML explicitly and share source compilation before
answers. Browser remote HTTPS/deep links accept canonical runtime JSON only.
Source/runtime agreement does not qualify every client editing path. Mandatory
capabilities must be checked before answer collection; optional layout degrades
only to a reachable neutral form. No compilation or distribution channel adds
network authentication, target apply or persistent browser answer custody.

## Native maintainer commands

`DefinitionCommands` registers init/check/build/revise through the existing
Spectre registry/preflight and command v3 envelope. `SetupCliDefinitionHandlers`
owns scaffolding, compilation and revision comparison. Discovery/read/existence/
staged publication, including schema references, use invocation-scoped
`ISetupCliAuthoringFiles`; `SetupCliIo` requires it explicitly and Program supplies
`PublicDefinitionFiles`. Non-authoring fixtures reject that authority.

Initialization requires project ID/name and exact application version, or an
explicit fork source/destination. Environment scaffolding retains names only,
not example values. YAML is default; JSON has the same source capability.
Discovery admits exactly one configurator.yaml/configurator.yml/configurator.json
at the selected root; explicit source selects one. Check is read-only.

Build is create-only at dist/configurator/revisions/&lt;revision&gt;.json by default,
accepts identical existing canonical bytes and rejects conflicts. Default history
anchors bind family/revision/digest even when another destination is selected.
Revise requires a matching prior family/current revision; semantic changes
increment once, overflow rejects, unchanged canonical content leaves source alone.
A changed revision goes to separate create-only `--output` in the same format;
source adoption and later build are separate actions. Check/build support
`--against` and reject an explicitly missing anchor. A supplied prior cannot
replace different recorded history.

Public paths reject ancestor symlinks. Staged writes use same-directory temporary
files and no-overwrite rename; generated output cannot replace source/discovery
candidates. This path boundary is not captured-handle/root authority or CAS.
Machine diagnostics are bounded structural codes, never source scalars or private
paths. Successful check/build reports authoringContractVersion=2 and
runtimeSchemaVersion=2; downstream independently verifies the canonical digest.
The command envelope is v3, independently of the 2/2 authoring/runtime handshake
and private-answer v1.

## Public schema import

`DefinitionSchemaImporter` is pure computation over bounded supplied schemas;
`DefinitionSchemaSourceFiles` owns root-confined native reads, rejecting links/
traversal with no network fallback. Bounds remain 4 MiB supplied bytes, 64
documents, 16384 parsed nodes, 8192 expanded nodes, depth 24, reference stack 32.
Expansion charges keys, overhead and metadata before emission.

The importer emits `Configuration` and immutable `Transformations`, replacing
the old difference-string API. Each transformation contains Classification,
Code, Location (a bounded public schema pointer) and DocumentOrdinal. No record
contains raw scalar values or native paths. Recursive constrained/enum/nullable
items, typed maps, branches and bounded local refs reach ordinary Core admission.
General unions/combinators, regex, unsupported assertions and nested resource
scopes reject, including assertions in unused retained definitions.

Selected text survives with line breaks/tabs normalized. Defaults/examples are
discarded and reported; format/readOnly/deprecated retain advisory annotations.
Answer-bearing leaves default sensitive; writeOnly ancestry is secret.
Structural containers and public const declarations receive explicit public
classification conversion records without lowering protected descendants.
Standalone null schemas convert to a nullable scalar with enum `[null]`.

Strict import reports `schema-bounded-semantics-required` for encountered
`schema-numeric-bounded-domain` or `schema-nullable-variant-domain`;
acknowledgment permits those named differences,
never unsupported assertions or privacy loss. Native v2 no longer substitutes
required-string-whitespace or UTF-16-length semantics. CLI compiles before source
creation; import is not proof of target-validator equivalence.

## Generated schemas and examples

The native `Configurator.DefinitionSchemaGenerator` owns `--write` and `--check`
for authoring-v2/runtime-v2/answers-v1 schemas, and `--write-examples`,
`--renew-example-families`, `--check-examples` for the three maintained pairs.
Renewal uses the native identity editor and allocates fresh family/revision 1;
ordinary writing does not authorize rewriting published identity history.
See the [schema catalog](../../schemas/README.md) for exact commands.

Native verification records current fresh-family source/runtime agreement 3/3, not target
acceptance. Recorded qualification also completed Core 217, Desktop 229,
Terminal 106 and Architecture 73 passing tests; Linux x64 X11 passed all 19
native scenarios in both English and Arabic. The published AOT Browser was
exercised at real desktop/mobile sizes, RTL, offline and custody flows.
Standalone Linux x64 CLI exercises covered complete preparation, incomplete
answers and create-only conflicts.

This unit read existing documentation and made prose edits only; the parent owns
freshness, link and product checks and exact qualification records. General
assertions outside bounded-v2, application-version ranges, target acceptance,
other hosts, assistive technologies, translation quality, stakeholder/usability
and scholarly proof remain unqualified followups. See the
[integration workflow](../public/integrating.md),
[detailed maintainer guide](../public/integrate-your-tool.md),
[qualification map](DEFINITION_PROFILE.md) and [Core contract](CORE_DEFINITION_CONTRACT.md).
