# Configuration Import And Comparison

> **Audience:** Contributors | Maintainers
> **Status:** Implemented bounded comparison; qualification is candidate-specific
> **Owner:** Presentation
> **Contributing reviewers:** Core owns inverse compilation and semantic evaluation
> **Last Verified:** 2026-10-30
> **Source Anchors:** `src/Configurator.Core/Definitions/DefinitionImport.cs`, `src/Configurator.Core/Definitions/DefinitionValidator.cs`, `src/Configurator.Presentation/Presentation/DefinitionFormModel.cs`

## Purpose And Authority

An ordinary exported configuration file can supply represented settings or reveal
work required by a selected definition. It cannot identify a historical release,
prove that a setting was renamed, or establish target application acceptance.
The selected admitted definition and explicitly selected artifact delimit the
assessment. A filename or publisher label supplies neither identity nor trust.

Compare is read-only. Fill fields is a separate, deliberate atomic mutation.
Comparison findings concern the imported file, not the validity of unrelated
current answers. Current answers, valid and invalid editor drafts, provenance,
omissions, input revision and prepared output must survive comparison and finding
navigation unchanged.

This document describes the implemented contract across the machine CLI,
Browser, Desktop and Terminal. The [execution record](reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md)
binds actual candidates, checks, measurements and remaining limits; it does not
transfer qualification to another host or target application.
The operator projection is
[Import and compare](../public/import-and-compare.md).

## One Inverse And One Evaluator

Core compiles the admitted output projection into bounded inverse instructions.
It reuses the existing JSON, YAML and dotenv parsers; no second import grammar,
serializer package or application configuration binder participates.

The supported algebra is direct root/item copies, objects, structural literals,
fixed lists, complete one-to-one repeats and ordinal checks. A single unambiguous,
schema-valid public scalar equality selector may supply artifact context.
Filtered repetitions, overlapping assignments and guessed hidden values refuse.
Human-readable documents and lossy projections do not acquire a universal inverse.

Every input member must be accounted for as recovered data, structural metadata,
or unrecognized/unassessed input. Literal mismatches can coexist with assessment
of recoverable settings; strict autofill still refuses that file. Unknown keys
are reported by safe input ordinals, never by their raw names.

The sole `DefinitionValidator` resolves activity, requiredness, variants and
constraints. Assessment disables initialization, not absence semantics. Normal
workspace validation and initialization remain distinct from the imported file.

## Observation Evidence

| Evidence | Meaning |
| --- | --- |
| Observed value | A mapped value is present, including null, false, zero and admitted empty values |
| Observed absence | A presence-preserving mapping proves that its represented member is absent |
| Unobserved | The projection does not establish presence or absence |
| Context-derived value | The selected artifact proves a public selector, not a value observed in the file |

An unobserved controller is unknown to dependent evaluation, not false. An invalid
controller is also unknown. Proven absence retains the existing missing-reference
and property-assertion rules: a property assertion alone does not require presence.
An explicitly supplied false remains known false.

A whole-subtree copy can establish complete presence information. Two projected
children do not prove the whole container's cardinality or the absence of its
other children. Partially reconstructed containers cannot become evidence of
complete coverage merely because they are represented internally as objects.
An already-exceeded maximum remains a known blocker; unproved root property
counts do not become compatibility. Invalid variant selectors are unknown
controllers, not usable object-existence evidence.

## Private Owner And Safe Report

One disposable assessment owns recovered private evidence. Its ordinary report
is a separate immutable value-free snapshot, without answer references, candidate
references, lazy closures or private-input byte fingerprints. Even this report
describes a private setup and remains local/private by default.

Three location domains must remain separate:

1. Source coordinates identify the assessed file and use ordinals for unknown
   members and private dynamic names.
2. Schema locations identify declared metadata or the enclosing map/list schema.
3. Workspace corrections identify the current input revision and current entities.

A source row ordinal never authorizes selection of the same ordinal in the
workspace. Before adoption, clients offer schema explanations or safe enclosing
editors without committing drafts or creating rows. After successful adoption,
ordinary current validation produces fresh workspace correction authority.
Strict prefill refusals keep admitted literal properties in a separate `Schema`
address and return no future-workspace correction token. Typed-map source
entries retain distinct safe ordinals, including mixed declared/dynamic maps.

Disposal clears owned byte storage and releases managed private trees. It does
not promise physical erasure of every managed string or runtime copy.

## Independent Result Facts

Execution completion, compatibility, coverage, finding inventory, autofill
eligibility, workspace preparation and target acceptance are separate facts.
Known blocking findings establish changes required even when other coverage is
incomplete. Compatible requires no blockers and complete stated coverage.

Missing required applies only to an applicable member whose absence is proved.
An unrepresented obligation is unassessed. Structural mismatches and unrecognized
input require review, not a guessed migration. An evaluation/resource stop
preserves known blockers and exposes incomplete coverage/inventory.

Display paging does not change the completed assessment. Every retained finding
must remain reachable. A stopped inventory exposes a retained-count lower bound
and no exact total. Continuation advances by returned count, including when byte
fitting shortens a page; an empty nonterminal page is a reporting failure.

## Interactive Custody And Adoption

Presentation remains the single interactive mutation, privacy and generation
owner. It retains at most one proposal under the form's existing gate, not a
second answer store, lock or scheduler. Publication checks the captured definition,
artifact, purpose, input revision, operation generation and privacy authority.

Every raw configuration import is potentially secret-bearing, including obsolete
content under a public-only definition. Browser consumption and retention require
the existing workspace custody acknowledgment and consent before reading.
Revocation denies access synchronously before physical cleanup. Edit,
replacement, cancellation, artifact/purpose change, navigation, failure and
disposal retire current comparison ownership; late reads cannot revive it.
Locale/disclosure changes may reproject safe facts without rereading the input.

Fill fields rechecks current conflicts, drafts, revision, consent and eligibility,
then performs one existing form mutation. Only untouched initialization may be
replaced. Explicit answers, explicit omissions and pending drafts are protected.
Missing required work may remain unfinished; invalid, unrecognized, structurally
incompatible or conflicting content cannot be partially adopted.
If a compatible file contains root entries that the current form has no owned
editor storage for, the form refuses the entire adoption with
`import-workspace-unsupported`; it cannot silently drop those entries.

When comparison succeeds but autofill refuses, the operator repairs a separate
copy and reimports it. Comparison grants no overwrite authority.

## Limits And Qualification

The existing input caps remain 1 MiB in Browser and 4 MiB in native/Core flows.
Existing depth, scalar, parser-event and container limits still apply. Combined
inverse, evidence, validation and finding work is bounded; retaining or paging
findings cannot multiply private answer trees. Cancellation requires bounded
checkpoints and current-operation checks, not an async signature alone.

Actual Event fixtures and independent literal vectors are primary semantic
evidence. Round trips alone cannot establish inverse correctness. Client tests
and actual Release/AOT or native interactions are separate proof obligations.
Measured responsiveness and retained-memory outcomes belong in Operations with
exact fixture/package identities; proposed budgets are not passing evidence.

### Initial Native Operation Measurements

The first qualified semantic baseline uses SDK 10.0.302 on the Linux x64 reference
workstation. The current-input six-class Core slice passed 85 tests; the
seven-class Presentation/Event slice passed 60. These are targeted slices, not
full-suite or package qualification.

Two sequential Release test invocations measured the synchronous inspection
seam, excluding fixture/definition loading. Allocation is the current thread's
cumulative allocation delta, **not** peak live memory or retained state.
These single observations are neither warmed p95 measurements nor Browser AOT
responsiveness evidence:

| Cohort | Input bytes | Allocated bytes | Inspection ms | Work units | Inverse nodes | Validation nodes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Checked-in Event manifest | 547 | 10,136,032 | 58.370 | 22,027 | 48 | 1,098 |
| Maximum repeated-work vector | 237,594 | 207,214,784 | 1,213.565 | 131,072 | 36,869 | 24,222 |

The repeated vector has 4,096 rows with eight integer properties and an
independently invalid first value. It deliberately reaches the combined-work
ceiling: the retained blocker remains changes required while inventory/coverage
are incomplete. It is a maximum-work cohort, not a claim that every input at the
4 MiB byte cap has been measured.

Evidence: `.omo/evidence/20261030-artifact-import-comparison/core-phase-exit-green.txt`,
`presentation-phase-safety-green.txt` and `core-operation-metrics.txt`. The metric
run uses `--output Detailed --show-stdout All --maximum-parallel-tests 1`.
Agent-mode defaults otherwise suppress captured stdout for passing tests.
Peak-live bounds, first-use/warmed client samples and replace/dispose retention
remain separate, unqualified gates.

See [the architectural decision](adr/ADR-artifact-import-comparison.md),
[Core contract](CORE_DEFINITION_CONTRACT.md),
[shared state](PRESENTATION_STATE.md) and
[protected input lifetime](PROTECTED_ARTIFACTS.md#bounded-native-input-lifetime).
