# Revision-owned form computation and bounded browser rendering

Date: 2026-10-22

Status: Accepted; published reference-host interaction gates passed on 2026-10-23.

## Context

Changing optional disclosure or search rebuilt answer-derived form projections.
Read-only descendant lookup reparsed whole roots, and closed discovery and large
Review lists still constructed unused controls. Historical Event measurements
showed multi-second local interaction stalls. Those samples were not percentiles.

The shared form also owns private drafts, logical row identities and revisions
that fence imports and prepared output. Moving computation into a Browser cache
would duplicate that authority and risk restoring revoked state.

## Decision

`DefinitionFormModel` owns three distinct lifetimes:

- `DefinitionFormIndex` belongs to the pinned definition/form. It indexes only
  admitted metadata, sections, schema children, activation references and declared
  output bindings. Root editor identity prefixes and literal path keys are also
  precomputed here; dynamic list-row tokens remain owned by the live form.
  Canonical root-ID vectors also serve budget/snapshot traversal and indexed
  `(Input, Schema)` mounting, avoiding repeated JSON ID decoding, root scans
  and schema cloning. Browser sections retain those admitted field IDs.
  Validated edits and collection updates use the same field-position index;
  they do not scan all fields again to rediscover their unique owner.
- `DefinitionFormFacts` belongs to the current input revision. It contains
  validation, diagnostics, origins, obligation progress and Review relevance.
  Reads traverse already-owned immutable JSON rather than another answer tree.
- Current discovery and ordered entries also depend on query/facets and authored
  experience order. Disclosure changes only visible projection state; it does
  not retire valid prepared output or advance input revision.
  Query/facet changes reuse immutable visibility and progress in a new
  projection; the Browser discovery component owns the resulting local render.
  Closed discovery does not force matching/facet computation during an answer
  edit. The single deferred result captures immutable value-free entries and
  selection, not the live form or validation's internal answer map.
  Entry creation itself is deferred from captured value-free node states,
  origin enums, frozen bindings and metadata parents. Static factory methods
  prevent accidental capture of the live form or facts. An unread old
  projection remains its original immutable snapshot after edits and disposal.
  Review relevance-prefix construction is also deferred from those captured
  states, so a closed Review does not build an unused path set.

Editing a missing or invalid field promotes its literal path before mutation.
New validation errors and invalid drafts promote their paths at the outer
mutation boundary before post-lock observers. Missing obligations elsewhere stay
reachable without making an unrelated edit disclose all of them.
Correcting a missing or invalid editor therefore cannot hide it merely because
a visibility getter was not called. Explicit collapse and experience navigation
retain their existing reset rules.

Input intent, including invalid and same-value edits, invalidates answer facts.
Revocation clears denied leaves and output through existing generation fences.
Disposal retires facts, metadata, selections and row identities without rebuilding
them. Managed-reference retirement is not physical erasure; owned output bytes
continue to be cleared by their existing owners.

Browser discovery constructs its controls/results only when opened. Full shared
matching, ranking and facet counts precede 50-entry rendering pages. Review uses
shared `RelevantEntries`, including contextual literal-path ancestors. Separate
view-only controls inspect omitted optional or inactive metadata. Diagnostic
correction actions are not hidden behind effects paging.

Scalar input and change-only password-manager fill use one fenced edit method.
An accepted event advances its own callback's revision, allowing queued typing.
Intervening independent edits or custody transitions still invalidate an old
callback. Unchanged blur after input does not duplicate the edit; deliberate
same-value input does. The adapter does not retain a second copy of the text.
Scalar controls are native browser inputs styled by the existing form rules.
This avoids constructing a component-library input for each newly disclosed
leaf while retaining masked entry, explicit input modes, credential autocomplete,
accessible descriptions and the shared revision fence.
Control identity is computed on demand after each parameter application, so every render
uses the current logical row token without repeating nested traversal for each
label/help/error attribute. Visibility skips identity traversal only when no
local overrides or promoted paths exist; actual overrides and custody checks
remain authoritative.
Access, sensitivity, type and diagnostic reads are reused within one render,
never retained as a separate access decision between renders or input events.

Experience changes keep focus on the persistent native selector. Explicit
workflow, section and correction navigation still focus their destinations.
Requested field/empty-list focus permits native scrolling, so an off-page
correction becomes visible rather than only receiving off-screen focus.
Moving focus to a newly mounted heading after every selector change disrupted
keyboard continuity and added synchronous layout work. Published scale
measurements exposed this cost; neither asynchronous rendering nor a second
input-state store was introduced to hide it.

## Alternatives

A global cache or selector framework obscures private-state ownership. A
Browser-only evaluator duplicates shared policy. Retaining hidden DOM panels
preserves incidental component state at the cost of unnecessary work. These
alternatives are rejected in favor of existing form ownership and explicit
rendering boundaries.

## Consequences and evidence

Native clients retain their consumed form APIs. Browser remains static WASM with
no new dependency, worker custody, answer persistence or network authority.
Literal identifiers and surviving logical row tokens remain stable.

Presentation and Browser tests cover correction order, warm custody, stale
imports/callbacks, queued input, remounting, full counts, paging and masked Review.
Published-browser timing, offline sinks and layout evidence must be recorded
separately in [Operations](../OPERATIONS.md#browser-interaction-qualification-2026-10-22).
Builds and unit tests alone do not establish latency or accessibility conformance.

Responsive preparation and HTTPS compression remain required independent work
in the [followup ledger](../../../dev/backlog/browser-interaction-performance-followups.md).
This decision does not bound snapshot capture, composition, adoption or erasure.
