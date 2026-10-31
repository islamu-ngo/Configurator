---
description: "Scope, portability, and conformance obligations for the bounded-v2 configuration profile."
---

# Definition Standard v2

> **Audience:** Adopters | Integrators | Contributors
> **Kind:** Explanation
> **Status:** Current bounded-v2 profile; qualification is scoped separately
> **Owner:** Core
> **Last Verified:** 2026-10-30
> **Source Anchors:** `docs/internal/DEFINITION_CONFORMANCE_PROFILE.md`, `docs/internal/DEFINITION_PROFILE.md`, `docs/internal/CORE_DEFINITIONS.md`
> **Review basis:** Documentation and source-owner reconciliation, not a new runtime qualification

The bounded-v2 profile is Configurator's portable, data-only configuration
contract. It defines what a consumer must understand before collecting answers,
how those answers become effective values, and what preparation can claim.
It is not unrestricted JSON Schema, a deployment language or target approval.

Use the [definition reference](definition-reference.md) to look up properties and
limits, [definitions](definitions.md) to learn the semantics, and the
[cookbook](definition-cookbook.md) for small authoring patterns. The
[technical conformance profile](../internal/DEFINITION_CONFORMANCE_PROFILE.md)
owns normative admission and evaluation rules. This page explains obligations
and interoperability rather than maintaining a second full grammar.

## Documents and versions

Maintainers publish an authoring source and, where distribution requires it,
canonical compiled runtime JSON. Operators supply a separate private answer
workspace. CLI machine output describes operation results; it is neither a
definition nor an answer file. Generated editor schemas assist authoring but do
not replace semantic admission.

The [version table](definition-reference.md#which-document-are-you-editing)
is the authoritative public lookup for their independent discriminators.
Definition family revision and exact target application version are independent
again. Advancing one contract must not imply that the others advanced. Current
definition admission has no v1 fallback, translator or compatibility alias.

## What a conforming consumer must preserve

### Admit the bounded language before use

Admission checks the complete public document before answers are collected.
Unknown semantic members, unsupported shapes, incompatible assertions and
resource overflow reject; they must not be ignored or approximated as no-ops.

The supported model includes recursive closed objects, typed maps, arrays,
typed scalar enums, nullable values, discriminated object variants, local
requiredness, scoped applicability, exact numeric assertions, bounded local
references and opt-in lexical format assertions. General combinators, arbitrary
regular expressions, remote or cyclic references and executable validators are
outside the profile.

Bounds apply to input bytes, depth, nodes, scalar storage, reference expansion,
evaluation and rendered output. Being below the source-byte ceiling does not
prove that expansion or preparation will fit. Charge work before materialization
and reject without partial admitted state or value-bearing diagnostics.

### Preserve exact data identity

Configuration names are exact data keys, not display labels or bounded metadata
IDs. Dots and asterisks are literal. Case-distinct and Unicode-normalization-
distinct names remain different; exact duplicate decoded names reject.
References use scoped RFC 6901 pointers, with `~0` and `~1` decoded once.
Array indices are positions, not stable application identifiers.

The profile uses ordinal object-key ordering, preserved array order, validated
Unicode and exact normalized decimal numbers for canonical identity. It is a
custom canonical profile, not a claim of JCS conformance. Independent byte and
digest vectors are necessary before claiming another implementation preserves
identity; using the same serializer name is insufficient.

Family/revision binds canonical content, including translations and inert
annotations. A registry rejects different content under a previously admitted
identity even after removal. Active workspaces retain their admitted digest.
Neither a family UUID nor a publisher label authenticates a publisher.

### Preserve presence and intent

Supply, omission and reset are distinct operations. Explicit omission suppresses
initialization; reset removes both the override and suppression. A supplied
nullable `null` is present. `false`, `0`, empty text and empty containers are
values whose validity depends on their own assertions.

`initialValue` applies only at eligible never-supplied locations. `default`
describes metadata, and `suggestedValue` changes nothing until deliberately
adopted. An absent optional parent is not created to initialize or satisfy a
child. The [worked presence example](definitions.md#worked-presence-example)
connects these rules to maintained input and expected output.

One Core evaluation policy serves validation and preparation. Layout, locale,
section expansion and optional detail cannot insert answers or change validity.
Unknown applicability from invalid or deferred controllers stays unknown rather
than selecting a guessed branch. Local relationship assertions are distinct
from applicability; mutually required fields do not create activation cycles.

### Keep private data out of public metadata

Sensitivity propagates through containers and cannot be lowered by relabeling
a child or parent. Public initial, suggested, default and example values must
remain wholly public. Dynamic map keys are private answer data.

Diagnostics expose stable codes and bounded public schema positions or numeric
ordinals, not unknown private names, values, raw exceptions or filesystem paths.
Local correction coordinates are revision-bound and retire on edits.

Secret-deferred drafts defer genuinely missing secret leaves, not malformed
supplied values. They may contain private values already supplied, remain
restricted, and do not establish complete readiness. Unknown secret-dependent
outputs are withheld rather than guessed.

## Portability across clients

Computed mandatory capabilities are part of canonical content. A consumer must
refuse unsupported mandatory semantics before answer collection; an author
cannot omit requirements to force admission. Optional layout may degrade to a
neutral presentation only if essential help and required/error work remain
reachable.

Shared semantics do not promise identical controls. Locale metadata can select
public text and direction but cannot change names, typed values or output bytes.
Static guidance links grant no automatic retrieval or answer-bearing network
authority. Native file protection and browser copy/download have different
custody guarantees.

Core support, shared editing, rendered interaction and actual-host qualification
are separate. The [support index](../internal/STATUS.md) identifies available
evidence; the [scenario map](../internal/DEFINITION_PROFILE.md#scenario-and-support-matrix)
states obligations without treating each row as an executed test.

## Generation is broader than inverse import

Declared outputs use bounded mappings or projection operations such as literal,
copy, omission, object, list and repeat. Output filenames are destination hints,
not filesystem authority. `targetUpdateMode` and `restartRequirement` are advice,
not permission to merge, overwrite, apply or restart.

A projection that generates successfully need not have an unambiguous inverse.
Existing-file comparison and strict autofill have different outcomes: comparison
can identify unaccounted content without adopting it; autofill must reject
unsafe or unsupported adoption atomically. Consult the
[current import contract](../internal/IMPORT_COMPARISON.md) for the admitted
inverse and pending client/package qualification, not an older direct-copy-only
description.

## Demonstrate conformance without overstating support

An integration should retain distinct evidence for:

1. Admission, canonical identity and independent semantic examples.
2. Effective answers and deterministic projection against independent expected
   bytes, including invalid and incomplete cases.
3. Current generated schemas and maintained source/runtime pairs.
4. The actual selected command or rendered client and its custody behavior.
5. Published package closure and protected-save behavior on the actual host.
6. The target application's own validation, permission and acceptance.

A build or editor pass cannot substitute for these layers. Historical reports
retain their original revisions and limitations; documentation edits do not
renew them. Accessibility, translation quality, stakeholder/usability review and
new host qualification require their own evidence.

For a complete adoption path, follow the [integration tutorial](integrating.md),
then the [maintainer lifecycle](integrate-your-tool.md). Keep definition-source,
answer, output and target-acceptance responsibilities separate throughout.
