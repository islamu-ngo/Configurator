# Maintainer authoring contract

> **Audience:** Maintainers | Contributors | AI agents
> **Status:** Implemented source contract; final runtime and package qualification tracked by the workstream
> **Owner:** Core | Native authoring tooling | Contributor Experience
> **Last Verified:** 2026-10-10
> **Source Anchors:** `src/Configurator.Core/Definitions/Authoring/`, `src/Configurator.Core/Definitions/SetupDefinitionSourceCodec.cs`, `src/Configurator.Cli/SetupCliDefinitionHandlers.cs`, `schemas/configurator-authoring-v1.schema.json`

## Source and admission

The authoring discriminator is `$schema: urn:islamu:configurator:authoring:v1`.
Root members are `identity`, `project`, `applicationVersions`, `fields`, `outputs`
and optional `entryPoints`, `locales` and `presentation`. The generated
[source schema](../../schemas/configurator-authoring-v1.schema.json) describes
structure; Core also enforces references, privacy, resource budgets and output
semantics. Authoring version, runtime schema version, family revision and target
application version are separate contracts.

`DefinitionAuthoringCodec.ParseJson/ParseYaml` normalizes bounded syntax into an
immutable `DefinitionAuthoringDocument`. `DefinitionAuthoringCompiler.Compile`
expands it and calls `SetupDefinitionCodec.ParseJson`, yielding admitted canonical
runtime bytes and a computed digest. `SetupDefinitionSourceCodec.ParseJson/ParseYaml`
selects authoring only for that discriminator, runtime only for `schemaVersion`,
and rejects conflicting/unknown discriminators. There is no parser fallback.

The CLI's concrete public-source adapter uses `BoundedFileInput` for worker-owned
open/stat/read with its existing 4 MiB per-file bound. Empty input, changed length
and oversize map to `definition-source-bound`; errors do not reveal file content
or private paths. The adapter still selects and checks source paths and returns
a caller-owned byte copy after clearing bounded storage. Sharing byte acquisition
does not grant command handlers ambient filesystem authority or merge public
authoring policy with private artifact import.

Project requires `id` and `name`; publisher defaults to name. Optional description,
credential-free HTTPS repository and explicit definition license carry distribution
metadata. Local use does not require store metadata. Identity requires a nonempty
family GUID and positive revision; compilation never allocates or advances either.
Versions are exact explicit strings; locales default to `en`, entry points to `setup`.

## Fields and projections

Fields are keyed maps. Their members use the runtime field vocabulary except `id`;
object/list-object `properties` are keyed maps too. Type is mandatory. Title defaults
to a humanized key, disclosure to `guided`, sensitivity to `sensitive`. A public
default/recommendation requires every applicable nested property to be public.
Private ancestor classification also forbids a nested public leaf's default or
recommendation; declaring the leaf public cannot lower its inherited privacy floor.
Nested secret properties raise the effective parent's sensitivity. Unknown members,
private defaults, invalid constraints and condition cycles fail with value-free codes.

Source field/output cardinality is checked before expansion. Generated nodes and
aggregate scalar text share one budget, charged while each projection member is
constructed, so small sources cannot amplify implicit field/output products beyond
the runtime's resource ceilings before admission.

Valid root logical keys retain their runtime ID. Otherwise the ID is `f-` followed
by a full SHA-256 of an NFC-normalized, domain-separated escaped root pointer.
Nested property keys must satisfy runtime identifier grammar, preserving actual
target names. Output IDs default to `out-<filename>` when representable, otherwise
`o-` and a full domain-separated SHA-256. Arrays generated from maps use ordinal key
order; inserting unrelated keys cannot change existing identities. Meaningful explicit
output IDs are permitted. References use declared logical field keys and are rewritten
once; labels, traversal counters and example values never determine identity.

Outputs are keyed by sanitized target filenames and require `format`. Apply semantics
default to `generationOnly`; omitted restart is `none`, with no hot-reload guarantee.
JSON/YAML shorthand copies exact root keys, including literal dots, without nesting.
Dotenv shorthand accepts scalar fields and valid environment names. Optional `bindings`
maps target keys to declared logical fields. It is mutually exclusive with `projection`.
Advanced projections use the existing closed literal/copy/object/list/repeat/omit/index
vocabulary and conditions; Markdown requires an explicit projection. Source compilation
does not supply target acceptance, administration or apply authority. Answer-derived
output remains restricted under the existing composer/writer contracts.

## Authored presentation and guidance

Source `presentation` shares runtime `defaultExperience`, `sections` and
`experiences`. Section objects share `id`, `title`, optional `description` and
`disclosure`, but select roots using a nonempty `fields` array of logical keys
and/or a nonempty `groups` array of existing field-group labels. Unknown groups,
missing roots, duplicate selectors and cross-section overlap reject. Explicit
fields preserve authored order; each group appends matching roots in ordinal
logical-key order. Compilation expands groups once and maps all logical roots
through the existing `FieldId` map; runtime retains only ordered `fields` IDs.
Nested properties are not independently laid out. No selector syntax is inferred
from punctuation in a logical key.

Every root must occur once across 1-64 nonempty sections; every experience must
list all sections once. There are 1-16 experiences, with a declared
`defaultExperience` reference. IDs are unique ASCII lowercase kebab-case starting
with a letter, at most 64 characters; titles 128, optional descriptions 2,048.
Section `disclosure` and experience `initialDisclosure` use the existing
essential/guided/advanced vocabulary and default to guided. Experience
`navigation` is guided/sections, default sections. These authored choices do not
depend on experience names, supply presets or override validation. Absent
presentation is valid and neutral for generic machine-only definitions.

Per-field `help` remains the short authored explanation, not an alias for comments
or a new sidecar. `guidance` accepts only optional `instructions`, `links`,
`searchTerms`; `credential` accepts only required `kind`, `acquisition`. They pass
through the same recursive field expansion, including inherited secret context,
then the closed runtime admission boundary. Guidance bounds and plain-text/link
restrictions, credential-kind acquisition matrix and immutable public API are
defined in the [Core contract](CORE_DEFINITION_CONTRACT.md#presentation-guidance-and-credentials).
First-party interactive secret fields require useful authored help at integration
review; generic machine-only definitions may omit all descriptors.

`DefinitionAuthoringSchema` derives these runtime descriptors from
`DefinitionSchema`, adding only source `groups` and optional logical `fields`
selection. Regenerate both via the native definition schema generator; semantic
coverage, effective-secret and existing source/generated node/scalar/depth budgets
are still enforced by the compiler/codec. Any admitted metadata edit changes the
canonical digest and requires the existing revision workflow. YAML syntax comments
remain excluded. See the [public source example](../public/integrate-your-tool.md#describe-journeys-and-credential-acquisition).

## Syntax and identity editing

Both formats share exact bounded decimal canonicalization; no floating-point conversion
is used. YAML admits JSON-shaped numbers, lowercase `true`, `false`, `null`, strings,
sequences, mappings, comments and multiline strings. Quote intended typed-looking keys
and ambiguous strings. Empty plain scalars, non-string resolved keys, case/NFC collisions,
merge keys (even quoted), directives, tags, anchors, aliases and multiple documents reject.

`DefinitionAuthoringIdentityEditor.UpdateRevision/Fork` operates on source snapshots,
locates parser scalar spans, patches only identity tokens and recompiles the result.
JSON offsets are UTF-8 bytes; YAML marks are decoded character offsets. BOM, Unicode,
newlines, quotes and all other comments/bytes are preserved. Native CLI owns source locks,
digest checks, create-only public builds and atomic replacements. This public-file editing
does not promise filesystem compare-and-swap against an uncooperative external editor:
another writer can race the final replacement window. Protected answer saves use their
separate create-only writer; source editing receives no answers.

## Local clients and distribution

CLI, Terminal and Desktop choose JSON or YAML/YML from an explicitly selected local file
and use the shared source codec before workspace selection. Browser local selection uses
the same bounded compiler before answers; remote HTTPS/deep links accept compiled runtime
JSON only. Compilation adds no network, target authentication, native I/O or persistent
answer custody. Optional store metadata comes from the same source; compiled immutable
canonical revisions are the distribution and conflict-history boundary.

See [public integration](../public/integrate-your-tool.md),
[runtime admission](CORE_DEFINITION_CONTRACT.md) and
[architecture decision](adr/ADR-maintainer-authoring-and-canonical-distribution.md).

## Native maintainer commands

`DefinitionCommands` registers init/check/build/revise through the existing Spectre
registry/preflight and closed machine envelope. `SetupCliDefinitionHandlers` owns
scaffolding, compilation and canonical revision comparison; every source discovery,
read, existence and staged-publication route, including schema references, uses
the required invocation `ISetupCliAuthoringFiles` capability. `SetupCliIo`
requires this authority explicitly; Program supplies `PublicDefinitionFiles`
and ordinary non-authoring fixtures reject it. There is no ambient fallback.
The native staged-file owner is not the protected answer writer. Initialization requires
project ID/name and an exact application version, or an explicit fork source/new
destination. Environment scaffolding extracts only keys and retains no example
values. YAML is default; JSON has identical capability.

Discovery admits exactly one of configurator.yaml/configurator.yml/configurator.json
at the selected root; explicit source selects one. Check is read-only. Build creates
canonical JSON at dist/configurator/revisions/<revision>.json by default, accepts
identical existing bytes and rejects conflicts. Existing default anchors bind
family/revision/digest even when a build selects another destination. Revise requires
a matching prior canonical family/current revision; semantic differences increment
once, overflow rejects and unchanged canonical content leaves the source untouched.
A changed revision requires a separate create-only `--output` candidate with the
same source format. The original source is never replaced, including if an editor
saves after the initial read. Review/adoption and a later build are separate actions.
Fork allocates a new family/revision 1 in a new destination.
Check/build accept `--against` for a separately located canonical anchor and reject
an explicitly named missing anchor.
Default recorded history is verified independently of the supplied anchor; an
explicit prior cannot substitute different content under an already recorded revision.

Public source paths reject symlinks in any ancestor. Create-only public writes use
same-directory temporary files and no-overwrite rename. Revision candidates use
that same create-only boundary; source replacement helpers, mutation locks and
legacy fixed-ceiling read wrappers are removed after caller migration.
Identical-file acceptance is bounded by the current invocation in the public
capability, not a second native read policy. Generated outputs cannot replace
sources/discovery candidates. This path-based boundary does not establish
captured-handle/root authority or filesystem CAS; that remains a separate required gate.
Optional logical-ID maps contain only public semantic keys and IDs. Machine diagnostics
contain bounded codes/structural paths, never source scalars or filesystem paths.
Successful check/build adds authoringContractVersion=1/runtimeSchemaVersion=1 for
the independent pinned compiler handshake; downstream validates the canonical digest.

## Public schema import

`DefinitionSchemaImporter` is pure Core computation over the root schema and a bounded
map of supplied local reference documents. `DefinitionSchemaSourceFiles` owns native
root-confined reads, rejects links/traversal and provides no network fallback. Limits
are 4 MiB aggregate supplied bytes, 64 documents, 16,384 parsed nodes, 8,192 expanded
nodes, expansion depth 24 and reference stack 32. Duplicate/case/NFC collisions reject.
Expanded bytes charge keys, per-node structural overhead, titles/help and choices
before emission; repeated references cannot amplify source metadata beyond 4 MiB.
Annotation text must fit runtime bounds; line breaks/tabs become spaces. Nested
$id scopes and root $id with nonfragment references reject rather than changing
resource resolution silently. Resource checks distinguish schema objects from
definition/property maps, whose member names do not introduce schema keywords.

Supported Draft 2020-12 vocabulary is explicit scalar types, homogeneous string enums,
closed objects with local required sets, homogeneous arrays, representable numeric,
length and item bounds, local $defs/$ref and descriptive annotations. Unsupported
assertions, dynamic/remote refs, cycles, open maps, tuple/union/combinator/conditional
schemas, regex and unrepresentable item constraints fail. Imported defaults are
discarded; writeOnly raises secret sensitivity, including referenced/root annotations.
Format remains explanatory help rather than asserted validation.

Strict import returns schema-bounded-semantics-required with enumerated differences:
schema-required-string-whitespace, schema-string-utf16-length and
schema-numeric-bounded-domain. The explicit acknowledgment permits only these bounded
domain differences and retains warning diagnostics in the success result; unsupported
assertions/privacy are never bypassed. The CLI compiles the generated source before
creating it. Neither import nor compilation certifies target-validator equivalence.
