# Definition Profile v2

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented bounded-v2; qualified pre-release Linux x64 and Browser scope
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `src/Configurator.Presentation/Presentation/`, `docs/internal/CORE_DEFINITION_CONTRACT.md`

The [public standard](../public/definition-standard.md) owns adopter syntax.
The [technical conformance profile](DEFINITION_CONFORMANCE_PROFILE.md) records
the implemented grammar, admission/evaluation owners and source-observed limits.
This document connects lowering, privacy invariants and scenario obligations to
the qualified pre-release scope. The [Core contract](CORE_DEFINITION_CONTRACT.md)
describes the coordinated cutover. The public
[integration workflow](../public/integrating.md) and
[cookbook](../public/definition-cookbook.md) explain adoption and bounded patterns.
Scenario obligations are not independent pass receipts or proof of the whole
JSON Schema standard.

## Bounded primitive foundation

The v2 codecs route through shared shape and syntax primitives.
`DefinitionPath.Parse` and `FromSegments` snapshot
at most 32 exact tokens within the 65536-character pointer budget; value equality
uses the encoded ordinal pointer. `ToPointer` is deliberate access to location
data; `ToString` remains opaque because tokens may be private.

`DefinitionNode` snapshots ordered properties, local required names, item/map
schemas and variants. Structural equality compares that immutable shape.
Cached expanded-node, scalar-character and depth totals charge each referenced
child occurrence, so shared metadata references cannot evade tree-expansion
budgets. Creating a node neither admits assertions nor evaluates answers.

`DefinitionProfile.CanonicalizeJson` and `CanonicalizeYaml` select explicit
`CompositionKeyPolicy.Exact` in the existing bounded parsers and delegate numeric
identity to the existing `DefinitionJson` owner. Exact-mode canonical numbers
have no exponent, no redundant fractional zeroes, and normalize negative zero
to `0`; object names sort ordinally. System.Text.Json's default escaping emits
non-ASCII characters as Unicode escapes (uppercase hex digits), with standard
short control escapes; there is no BOM or insignificant whitespace.
The conformance corpus independently fixes those actual bytes, including
combining characters and surrogate pairs.

Definition and private-answer codecs select exact key identity. Generic
composition and directory reading retain their collision-safe policy; generic
composition still rejects case/NFC collisions. There is no compatibility
interpreter, second canonicalizer, answer tree, or new package.

`SetupFieldDefinition.Name` is the exact root configuration key; `Id` is bounded
public metadata identity. `DefinitionProfile.FieldId` retains an ordinary ASCII
identifier except for the reserved `f-` prefix. Other names use `f-` plus the
lowercase SHA-256 of UTF-8 `field:` followed by the single-token RFC 6901 pointer.
Reserving that prefix prevents an authored identifier from colliding with a
generated one. It is not authentication or a content digest. Core snapshots and
projection references always use literal names; Presentation translates its
metadata-root editor paths at the snapshot/effective-value boundary.

The relationship subset is local object `if.required` and property const/enum
guards with `then.required`, optional `else.required`, and `dependentRequired`.
Guards lower into Core's existing three-valued predicate kernel and contribute
no activation edges. Row applicability uses typed item references and a
row-local unknown-controller set; invalid or deferred controllers never select
a guessed branch. Missing target uncertainty
propagates without invalidating a supplied valid target. Definite missing-work
witnesses remain blocking even if mixed applicability feedback later becomes
unknown, including definite work in a different row from an invalid controller.
Arbitrary conditional subschemas remain outside the selected profile.

Output admission checks the existing projection family in its root/item schema
context. Nullable repeats and incompatible Markdown shapes reject before
answers; dotenv retains its non-null scalar-member contract. The importer
reconstructs one-to-one nested object copies and validates the candidate through
Core, rather than interpreting applicability itself. Explicit omissions remain
protected independently of replaceable default provenance. Draft item predicates
receive concrete deferred-location metadata from Core so a missing secret never
becomes a guessed false branch or hides an independent row copy.

## Control and data flow

The recursive profile now admits typed additional-properties maps, constrained
nullable/enum items, exclusive numeric bounds, exact multipleOf, structural
uniqueItems, 2-16 closed discriminated branches and acyclic local references.
Core applies the same assertions to roots, properties, items and map values.
Local refs start at configuration `$defs`; admission charges expanded metadata
and checks unused definitions. Read-only/writeOnly annotation merging cannot
lower sensitivity. Root map keys remain private and use entry ordinals in
diagnostic/completion metadata.

The schema importer delegates final semantics to that same compiler. Its
structured transformation ledger replaces Differences and records classifications,
safe public schema pointers and document ordinals. Structural/public-constant
classification is explicit; collected leaves remain sensitive and inherited
writeOnly remains secret. Null-only schemas convert to nullable enum `[null]`.
Numeric-domain and nullable-provider-domain differences require acknowledgment;
unsupported assertions cannot be acknowledged away.

Formats remain annotation unless assertFormat is true. The lexical registry is
ASCII hostname, dot-atom email-address, well-formed ASCII absolute uri and
credential-free http-url. Parsing is local and grants no retrieval authority.
The public standard states exact syntax limits; no full RFC/target-equivalence
or unexercised client qualification follows from these Core capabilities.

```text
bounded source decoding -> authoring lowering -> runtime admission
  -> immutable SetupDefinition (recursive nodes, paths, computed capabilities)
  -> pinned workspace + one immutable answer snapshot
  -> one Core effective-value resolver and validator
  -> existing bounded composers -> owned restricted artifacts
```

Presentation consumes Core facts, owns edit provenance, stable row/correction
tokens and operation generations, and projects inert public metadata.
CLI consumes Core/Artifacts directly. Browser uses Presentation/Core only.
Native adapters retain the existing create-only protected writer. No new
production package, evaluator, answer index, scheduler, cache, or plugin layer
is introduced.

`DefinitionPath` represents concrete exact-key RFC 6901 locations.
`DefinitionNode` represents the recursive immutable schema contract.
`DefinitionProfile` owns vocabulary, limits and required capabilities.
`DefinitionSourceMap` carries bounded public document ordinals, schema pointers,
and numeric source spans through the existing parser/lowering path; it is not a
parallel parser. `SetupAnswerSnapshotCodec` admits the private answer envelope.

Collection inputs are snapshotted. Records describe immutable contracts; mutable
workspace state and owned buffers remain classes. Metadata indexes reference
admitted nodes, not a second private value tree.

## Exact names and canonical identity

Definition/answer callers explicitly request exact-key parsing. Generic document
composition and directory policy retain their existing case/NFC collision rules.
Do not silently change every parser caller to permit ambiguous composition.
The exact policy preserves ordinal key identity and rejects exact duplicates,
invalid UTF-8 and unpaired surrogate sequences before admission.

Canonicalization sorts decoded keys ordinally, preserves arrays, emits compact
validated UTF-8 JSON and normalizes exact decimals. Escaping and number bytes
must be bound by independent vectors, including key `/` and `~`, composed and
decomposed text, control escapes, negative zero and exponent/scale boundaries.
Do not call the profile RFC 8785/JCS or use floating-point conversion.
Do not hand-edit generated definition bytes.

Admission binds family/revision/content; registry removal does not erase known
conflicts. Active sessions retain their admitted revision/digest. The runtime
records the profile and sorted computed semantic capabilities. Unknown semantic
fields or capabilities reject. Annotations and translations are digest material.
Checked v2 examples receive native-allocated fresh families, not mutated v1
revision content.

## Resolution and privacy invariants

The answer snapshot owns values, explicit omission pointers and revision.
Supplied content wins over initialization; explicit omission suppresses it;
reset removes override/suppression. Conflicting supplied/suppressed locations
reject. Initialization recurses only into present containers. Descriptive default
and opt-in suggestion never become implicit answers. One resolver feeds complete
validation, draft validation and composition.

Sensitivity inherits the conservative ancestor floor and protected descendants.
No branch, item or map can carry public metadata disclosing a protected value.
writeOnly raises secret classification. Credential descriptors do not lower it.
Budget every reference expansion, schema node, traversal and emitted output before
allocating/materializing it.

Applicability uses one existing predicate kernel with explicit root/item scopes.
Missing is distinct from null; invalid/deferred operands propagate unknown.
Applicability edges are acyclic; required/dependent/conditional assertions are
validation relationships rather than activation edges.

Diagnostics expose stable codes, declared public schema positions, bounded
document/entry/row ordinals and source coordinates. Dynamic keys, private values,
arbitrary unknown source keys and filesystem paths never enter public messages or
completion metadata. Current workspace tokens resolve local correction; stale
tokens cannot navigate into replacement state.

Edits, invalid drafts, omission/reset, branch/map changes, replacement, disposal,
and consent denial invalidate captured generations. Secret denial recursively
clears secret leaves and owned prepared bytes. A late operation cannot restore
old answers or output. Browser answers/artifacts never enter storage, public
caches, worker messages or network requests.

## Scenario and support matrix

The table retains independent expected behavior and owner/gate traceability for
the implemented bounded-v2 contract. It is not a per-row execution receipt or a
test that scans prose. [Support and evidence](STATUS.md) routes to the actual
execution records in [Operations](OPERATIONS.md). Desktop X11 exercises,
Terminal process exercises and Browser publication are separate evidence.
Counts alone do not prove every obligation below. General assertions
outside the bounded grammar, version ranges, target acceptance, other hosts,
assistive technologies, translation quality, stakeholder/usability and scholarly
proof remain unqualified followups. Exact application-version strings remain
the implemented selection contract.

| Scenario | Independent expected behavior | Owner / gate |
|---|---|---|
| S01 | v1 and unknown semantic contracts reject without fallback | Core admission / cutover |
| S02 | `A`, `a`, `_internal`, `/`, `~`, dots and Unicode remain distinct; exact duplicate rejects | Core paths / primitives |
| S03 | Byte/node/depth/expansion/work overflow rejects atomically | Core budgets / primitives and recursive validation |
| S04 | Present containers initialize missing eligible children; absent containers stay absent | Core resolution / cutover; shared editing |
| S05 | Omit suppresses initialValue; reset restores eligibility; supplied null/false/zero/empty remain distinct | Core snapshot / cutover; Presentation |
| S06 | Required empty text satisfies presence without an assertion; scalar length and exact numbers hold | Core assertions / recursive validation |
| S07 | Constraints recur through enum items, typed maps, null and variants | Core recursive validation |
| S08 | Item predicates affect only their row; invalid/deferred stays unknown; cycles reject | Core predicates / conditions |
| S09 | Mutual requirements reject either value alone, not both or neither absent other requirements | Core relationships / conditions |
| S10 | Incompatible assertions reject; plain format annotates, explicit supported format asserts | Core profile / recursive validation |
| S11 | Inherited privacy rejects exposing metadata at every recursive position | Core admission / cutover and recursive validation |
| S12 | Synthetic private key/value canaries never reach diagnostics/completion; local correction works | Core diagnostics, Presentation and actual clients |
| S13 | Unsupported source assertion yields code/public schema position/span without raw input/path | Core source map and CLI |
| S14 | Incompatible dotenv/Markdown output shapes reject before answers | Core mapping admission |
| S15 | Typed root/item pointers and bounded nested repeats select exact values | Core projection |
| S16 | Proved direct-copy inverses round-trip; ambiguity/conflict/unknown content reject atomically | Core import |
| S17 | Only missing secret leaves defer; malformed supplied values fail; unknown outputs withhold | Core draft composition |
| S18 | Conflicting known identity rejects after removal; canonical equivalent input pins identical content | Core registry, canonical vectors and packages |
| S19 | Missing mandatory consumer capability refuses before answers; optional hints degrade reachably | Core capabilities, Presentation and clients |
| S20 | Partial/omitted layout and detail changes preserve reachable required/errors and output | Presentation journeys and clients |
| S21 | Equal/translated option labels retain distinct typed values; suggestions need deliberate adoption | Presentation typed choices and clients |
| S22 | Multiline/static-fragment help is inert; locale/RTL cannot change IDs/values/output | Presentation localization and actual clients |
| S23 | Held operations cannot restore private data/output after edit/denial/disposal | Presentation generations and actual clients |
| S24 | Scaffolding reports retained/converted/advisory/discarded/unsupported concepts by safe location | Core importer and CLI |
| S25 | The current machine contract reports actual definition-v2 capabilities/schema/locations; lint stays quality advice | CLI and generated command schema |
| S26 | Absent target update/restart metadata stays unspecified and grants no authority | Core output metadata |
| S27 | Published assets/schemas/examples/store agree; public caches remain answer-free | Native generators and architecture/packages |
| S28 | Support promotion follows executed qualification and examples; prose itself is not pinned | Documentation and native checker |
| S29 | Portable init/check/build uses qualified options and synthetic inputs without target dependencies | Portable skill validation and actual CLI |
| S30 | Synthetic, client, host and target evidence remain separately reported | Published-client/package qualification |
| S31 | Unknown/executable/remote/interpolated constructs reject; bounded annotations stay inert | Core closed grammar |
| S32 | Exact application versions stay distinct from profile/runtime/locale/revision; no silent upgrade | Core registry and qualified packages |

### Capability activation

Shared Presentation now exposes typed map/variant traversal and nullable,
choice, suggestion/reset, locale and correction actions over the admitted Core
nodes. Optional constructor capability admission happens before answer editors.
Core's discriminator selector remains the only branch authority. Public
correction coordinates use declared properties and row/map ordinals, including
temporary invalid drafts; local recovery is fenced by input revision.
Locale changes project text/direction only and do not change output bytes.
Browser/Desktop/Terminal adapter and actual-surface gates have been exercised
within the recorded Linux x64 and published Browser scope above; the shared
feature alone is not evidence for another host or an unexercised interaction.

Derived semantic features include recursive objects/arrays, typed maps, nullable
values, discriminated variants, scoped applicability, exact typed choices,
explicit omission/reset, guidance and localized metadata. A renderer advertises
only features it actually edits. Computed requirements cannot be omitted by an
author. Final activation requires tests at Core/shared seams plus observations of
the changed actual surface; a structural schema or build is insufficient.

Optional presentation differences do not imply different validity. Every
supported interactive client must expose essential help, reachable required/error
work and deliberate suggestion/reset/omit actions.

### Responsibility traceability

| Finding / mitigation | Scenario obligations |
|---|---|
| IVSD-F001 / IVSD-M001 | S04-S07, S11, S17 |
| IVSD-F002 / IVSD-M002 | S02, S08-S09, S20 |
| IVSD-F003 / IVSD-M003 | S03, S11-S13, S17, S23, S27 |
| IVSD-F004 / IVSD-M004 | S21-S22 |
| IVSD-F005 / IVSD-M005 | S19-S23, S30 |
| IVSD-F006 / IVSD-M006 | S14-S17, S24, S26 |
| IVSD-F007 / IVSD-M007 | S01-S03, S18-S19, S31-S32 |
| IVSD-F008 / IVSD-M008 | S25-S30 |

The [responsibility report](../../islamic-value-sensitive-design/workstreams/i-vsd-definition-standard-redesign.md)
retains eight open pairs and its immutable reviewed inputs. This mapping does
not establish operational, usability, stakeholder, scholarly or target proof.

## Provenance and functional handoff

Research refreshed on 2026-10-28 uses only standards/interface facts:
JSON Schema 2020-12 separates presence, annotations and assertions; RFC 6901
defines exact pointer escaping; official .NET documentation describes duplicate
property refusal and System.Text.Json access APIs. The source register is
retained in the [point-in-time audit](reports/DEFINITION_STANDARD_AUDIT_2026-10-28.md#source-register-and-clean-functional-handoff).
No third-party implementation, test corpus, assets, distinctive organization or
production dependency is selected.

The independent design extends repository-native codec/compiler/validator/
composer owners and immutable workspace boundaries. Public syntax requirements
and independent expected results are the handoff, not copied implementation
structure. Abstraction/filtration retains standard vocabulary and observable
constraints only; organization/control flow are independently designed here.
No external evaluator or renderer is adopted. Package/licensing closure remains
unchanged.

## Verification and promotion

Establish the relevant Core baseline once. Add failing public-seam invariants
before privacy/state/admission changes, then close coherent source/caller/docs
slices using the owning Release build and suite with a nonempty guard.
Generate structural schemas and canonical fixtures through their native owners.
Never approximate an unsupported assertion as a no-op.

The current promotion uses the recorded Linux x64 CLI/Terminal/Desktop
and published AOT Browser qualification above. Retain separate records of actual
CLI, Terminal, Desktop and published Browser interactions, including keyboard,
mobile, RTL, map/variant, consent, stale operations, drafts and custody channels;
do not replace them with suite totals or this scenario table.
Do not infer untested hosts, target acceptance or installed/assistive-technology
support. Promote only verified capabilities to current public documentation.
