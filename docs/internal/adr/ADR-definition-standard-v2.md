# ADR: Bounded definition standard v2 and one Core authority

Date: 2026-10-28
Status: Accepted through the approved definition-standard-redesign implementation;
Core v2 admission and evaluation are delivered. Verification and responsibility
limits remain separately recorded in Operations and the I-VSD report.
Owners: Core, Presentation, maintainer tooling and client maintainers.
Qualification anchor: `55c951d` (Phase 12).

## Context

The pre-release v1 field model gave root and nested defaults different meanings
and restricted ordinary arrays, nullable values, typed maps and row-local provider
configuration. Expanding it independently in clients would create competing
validation, privacy and initialization rules. The replacement must remain an
offline preparation contract rather than a target validator or execution engine.

This record preserves decisions D01-D12 from the approved redesign. The
[definition standard](../../public/definition-standard.md),
[profile](../DEFINITION_PROFILE.md),
[Core contract](../CORE_DEFINITION_CONTRACT.md) and
[maintainer contract](../MAINTAINER_AUTHORING.md) own the supported grammar.
Earlier authoring ADRs retain their historical rationale; their v1 field/default
descriptions do not establish a second supported interpreter.

## Decisions

### D01 - A bounded profile, not a general schema engine

Authoring uses `urn:islamu:configurator:authoring:v2`; canonical runtime uses
`schemaVersion: 2`. Source configuration uses selected JSON Schema 2020-12
vocabulary and a closed `x-configurator` vocabulary. Required source roots are
`$schema`, `identity`, `project`, `applicationVersions`, `configuration` and
`outputs`; optional entry points, presentation, localization and inert annotations
stay bounded. Local use does not require a store or publisher identity.

The existing compiler lowers source into runtime admission before answers.
Polishing v1 would be cheaper but retain its structural friction. A general
evaluator, including the considered JsonSchema.Net alternative, offers broader
reuse but was not selected: this product needs qualified resource limits, exact
decimal behavior, safe diagnostics and Browser/package closure, not unrestricted
schema coverage. This is not a claim that the library cannot meet those needs.
No production dependency or second evaluator is introduced.

### D02 - One immutable recursive admitted model

`SetupDefinition` remains the aggregate. `DefinitionNode`, `DefinitionPath` and
`DefinitionProfile` represent recursive shape, exact location and supported
vocabulary/capabilities. Properties, items, typed additional properties and
variant branches share that grammar. Immutable inputs are snapshotted; mutable
workspace state and owned buffers retain explicit lifetimes. Metadata indexes
are not another answer tree. This trades unrestricted schema representation for
one admitted model usable by every consumer.

### D03 - Exact keys and scoped concrete references

Definition/answer parsing preserves case-distinct and NFC-distinct Unicode keys,
rejects exact duplicates and invalid UTF-8, and uses bounded RFC 6901 paths.
References explicitly declare `root` or admitted `item` scope; dots and wildcard
guesses do not create nesting. Generic composition/directory collision policy
remains separate. Dynamic keys are private answer data: diagnostics use declared
schema locations and bounded indices, while current workspace identities resolve
local corrections. Broader key support therefore does not broaden disclosure.

### D04 - Recursive selected assertions

The profile supports string, boolean, integer, number, object and array, with a
supported base type plus null for nullability; scalar homogeneous enums and
`const`; local required-property sets; closed objects or typed
`additionalProperties`, including fixed properties alongside typed dynamic
entries; and local acyclic `$defs`/`$ref`.

Selected assertions include numeric bounds and exact-decimal `multipleOf`,
Unicode-scalar string length, item/property bounds, `uniqueItems`, bounded
`x-configurator.uniqueBy`, `dependentRequired`, bounded local `if/then/else` and
discriminated `oneOf` with distinct scalar discriminator constants. Numbers stay
within the disclosed decimal/Int64 domain and reject rather than round.
Required means presence; nonblank is explicit. Formats are descriptive unless
`assertFormat` enables a supported type-compatible lexical assertion: hostname,
email-address, uri or the documented credential-free HTTP(S) URL extension.
None performs network validation.

General combinators/unions, tuples, arbitrary regex and dynamic, recursive or
remote references remain rejected. Their separate
[extension backlog](../../../dev/backlog/definition-profile-advanced-assertions.md)
does not defer any selected v2 assertion or recursive shape.

### D05 - Explicit answer state and recursive initialization

Private answer contract v1 has `answerContractVersion`, `values` and `omitted`.
`SetupAnswerSnapshot` owns one immutable tree, omission metadata and revision.
One Core routine resolves complete/draft validation and composition: supplied
value, explicit omission suppression, then eligible `initialValue`. A location
cannot be both supplied and suppressed. Reset removes override/suppression.
`default` describes; `suggestedValue` needs deliberate adoption.

Initialization fills eligible children only inside present containers. It does
not create absent containers. Null, false, zero, empty strings and containers
remain explicit values subject to their schemas. Public metadata values cannot
violate inherited privacy/writeOnly floors. Drafts defer genuinely missing
secrets, never malformed provided values. The explicit state model costs more
than implicit defaulting but prevents omission and reset from silently agreeing.

### D06 - Applicability is not relationship validation

The existing bounded predicate kernel owns `applicableWhen` with typed root/item
references and exists, equals, notEquals, in, all, any and not. Exists includes
null; equality with missing input is false. Invalid/deferred controllers remain
unknown; applicability cycles reject. Inactive values do not enter effective
validation/emission. Relationship constraints evaluate the effective tree
separately, so mutual requiredness is not an activation cycle. Detail, visibility
and journeys cannot change validity. Clients never acquire their own evaluator.

### D07 - Public-safe source locations and separate version axes

Compiler locations carry bounded document index, schema pointer and numeric
line/column, not source scalars, private dynamic keys, native paths or raw
exceptions. Local correction identity remains workspace-owned. Definition
authoring/runtime v2, machine envelope v3 and private answers v1 are independent
contracts; machine v2 is not silently reinterpreted. Existing bounded diagnostic
continuation uses detail, offset, limit and expected digest. Schema discovery
returns public built-in schemas/capabilities, not answers or arbitrary source.

### D08 - Typed outputs and a proved inverse domain

Outputs remain basename hints plus declared dotenv/JSON/YAML/Markdown formats.
JSON/YAML can copy the effective configuration; explicit bindings use concrete
target pointers or literal environment keys and typed source references.
The existing closed operations and bounded nested repeats suffice without an
expression/template language. Compilation checks result shapes, optionality,
nullability and serializer compatibility before answers.

Only unconditional one-to-one root/nested copies have an inverse. Unknown input,
transforms, conditional mappings and repeated ambiguity reject atomically.
Conflict/default-origin permissions remain; undeclared passthrough is not a
lossless-import promise. Update/restart hints default to `unspecified`, not
permission to apply or a promise that no restart is needed.

### D09 - Portable presentation without semantic authority

Titles/descriptions and bounded detail, guidance, credential intent, option
explanations and units support sections and optional journeys/default journey.
Pointers identify fields. Missing presentation yields a neutral journey;
unlisted fields have a deterministic reachable fallback. Required/error work
stays reachable at every detail level. Units do not convert answers.

Admission computes required semantic capabilities rather than trusting an
author's omissions. Unsupported required editing semantics refuse before
answers; optional layout hints degrade without changing meaning. Shared
Presentation editing/origins/collections allow correction drafts rather than
client-specific restrictions on temporary invalid states.

### D10 - Inert localization and guidance

Public schema-pointer overrides localize text with base fallback while typed
choice values, pointers, numbers and output bytes remain invariant. Direction is
metadata, not inferred from answers. Bounded multiline plain text and static
credential-free HTTPS fragments are inert; query strings, arbitrary HTML or
Markdown execution, interpolation, fetch/prefetch, opener/referrer transfer and
action plugins are excluded. Essential help has a plain-text route in all clients.
Terminal RTL metadata uses end alignment without reversing logical glyph order.

### D11 - Content identity and exact application versions

Family/revision/SHA-256 binds a documented custom canonical profile: ordinal
object ordering, preserved arrays, exact numeric normalization and specified
UTF-8/escaping. It is not relabeled JCS or floating-point canonicalization.
Annotations, localization and metadata participate in identity. Unknown semantic
fields/capabilities reject; active sessions pin admitted content, and registry
removal cannot erase known identity conflicts. Labels and GUIDs confer no trust.

Application versions match exact declared strings. They are not guessed semantic
version ranges, locale selectors or runtime versions. Checked v2 examples use
fresh natively allocated families; retained v1 history is not a migration
interpreter. Aliases, dual live interpreters, guessed migration and v1 fallback
are rejected pre-release compatibility costs. Any future
[declared range scheme](../../../dev/backlog/definition-profile-version-ranges.md)
requires its own explicit supported contract.

### D12 - Existing owners and continuous documentation parity

Core compiles/admits/evaluates/composes; Presentation owns answers, correction
identity and operation generations; clients render and dispatch; Artifacts owns
native protected output. Structural schemas/support metadata are generated from
owning models. Source locations must not become a second parser.

A generic validator, service locator, event bus, scheduler, cache, renderer or
plugin framework would add owners without a required behavior and is rejected.
New dependencies need separate closure/license/publication qualification.
Public guidance and technical anchors ship with each capability; final reference
promotion and this ADR preserve decisions rather than substitute for those updates.
No dynamic execution or target authority is granted by a definition.

## Qualification and independent obligations

Phase 12 qualification at `55c951d` records full Desktop 229 passed and all
19 native X11 guards in both English and Arabic, including D19's actual mixed
fixed-string/typed-integer map and independent output expectation. Terminal
records 106 passed plus actual corrected package interaction. These are inherited
execution facts, not checks rerun for this documentation change. Exact execution
evidence belongs in [Operations](../OPERATIONS.md); Foundation and the I-VSD
report preserve the authority boundaries and continuing responsibilities.

This proves neither target acceptance nor stakeholder, translation, scholarly,
assistive-technology, installed-payload or new-host qualification. Those claims
need their own evidence, including
[target/adopter trials](../../../dev/backlog/definition-profile-target-adoption-trials.md).
Core v2 admission/evaluation and selected client semantics are delivered, not
future trial prerequisites to implement later.

Independent work retains its owners and acceptance:

- [Public authoring custody](../../../dev/backlog/cli-public-authoring-custody.md)
  and [directory source authority](../../../dev/backlog/core-directory-source-boundary.md)
  are not repaired by schema admission.
- [Other native hosts](../../../dev/backlog/configurator-native-host-evidence.md)
  still need actual-host protected-save proof; unsupported hosts fail closed.
- [New formats](../../../dev/backlog/configuration-format-expansion.md) do not
  follow from recursive v2 shapes or bounded inverse support.
- [Event delivery/adoption](../../../dev/backlog/guided-configuration-delivery-event.md)
  retains target-owned semantics, receipts and independent expected bytes.
- [Publisher distribution](../../../dev/backlog/publisher-definition-distribution.md)
  retains hosted publication and signer authority; local packages, schema IDs
  and synthetic trust fixtures do not activate either.

Browser answers remain transient/local with workspace secret consent and no
answer-bearing requests, persistence, caches or worker messages. Native
answer-derived output remains restricted/create-only on verified hosts.
Preparation never grants target apply, administration or host acceptance.
