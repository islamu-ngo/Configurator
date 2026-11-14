# ADR: Captured export evidence and reviewed value transfer

> **Status:** Accepted design in the approved output-guidance-and-value-transfer plan; client and package qualification remain separate
> **Date:** 2026-11-13
> **Owners:** Core, Presentation and interactive client maintainers
> **Source Anchors:** `DefinitionAdmission`, `DefinitionFormOutputWorkflow`, `DefinitionFormTransfers`, `DefinitionAcceptedEditingState`

## Context

An operator preparing related outputs needs useful follow-up guidance and a way
to reuse independently editable values. Preparation is not export, and a late
export callback cannot certify newer answers. A transfer preview cannot become
permission to overwrite edits made after that preview.

The workspace already owns one answer tree, a global input revision, immutable
admitted definitions, operation generations and accepted-state adoption.
Adapters already own clipboard/download requests or protected native writes.
Keep these authorities rather than introduce another answer store or service.

## Decision

Core admits closed `nextActions` and `suggestedFrom` metadata. Navigation edges
reference existing output options; suggestion edges reference static scalar
leaves. Core rejects suggestion cycles, generated-secret endpoints, incompatible scalar
domains and sensitivity downgrades through either ancestor chain. Suggestions
remain inert metadata. Truly shared facts continue to use artifact projection.

Presentation captures export authority from a prepared revision and complete
eligible artifact set. Adapters retain their current workspace and operation
fences and report exact acknowledged successes. Delivery kinds distinguish
clipboard copies, browser download requests and verified native saves.
Coverage remains partial until every eligible artifact has an acknowledged
action. Mixed delivery kinds remain distinct. Existing native installation
receipts are retained independently, including partial and unknown outcomes.

Every answer edit stales earlier export evidence. Locale and view changes do
not. Preparation cancellation retires held export authority. Consent
revocation, private-state clearing and disposal clear the transient workflow
and held review authority synchronously.

A transfer review contains only definition identity, lifecycle generation,
input revision, target option, candidate identities, static schema coordinates
and public presence/masking facts. It stores no answer values, private
snapshots or value-reading delegates. Protected previews remain masked with
consent. Secret custody is checked before reading a source.

A supplied, valid retained source may be read while its task is inactive.
Authored source eligibility combines retained supplied branches with absent
members from current effective initialization. Scope-filtered effective values
alone lose inactive sources; raw supplied values alone lose initialized
controllers. Unresolved, deferred and omitted dependencies remain guarded.
This condition input never becomes an answer or prepared output. Destination
eligibility is evaluated in an isolated hypothetical task context. No link
recursively obtains a value from another link. False and zero are supplied
values. A manual destination, omission or invalid draft is never selected by
default.

Confirmation compares the exact current review and revision, resolves selected
sources again, and stages all selected leaves and the selector off the live
tree. Core validates scalar constraints, combined applicability and budgets
before one accepted-state adoption, one revision and one undo entry.
Unselected answers, origins, drafts, omissions and logical row identities
remain intact. Refusal changes no accepted answers or undo history. An observer
failure after commit reports accepted success with `ObserverFailed`; it cannot
roll back the accepted state or a newer reentrant edit.

## Alternatives and consequences

A loop of live `SetValue` calls would expose partial updates and advance several
revisions. A lock alone would not make that loop atomic. Caching preview values
would retain private answers beyond their current review authority. Both
alternatives are rejected.

Global freshness is conservative: selecting another output task stales earlier
evidence even if most values stayed the same. This cost avoids another content
revision, dependency hash scheme or speculative cache.

Clients adapt the same shared behavior and retain platform-specific dispatch,
focus and I/O. CLI inspection remains independent of Presentation and exposes
bounded public structure only. No target application acceptance, native host
qualification, browser save completion or assistive-technology certification
follows from these shared contracts.

Native review commands capture the exact review they were rendered for.
Refreshing localized public labels preserves that authority and deliberate
candidate selections; a held command cannot confirm a later review.

## Provenance and evidence

The implementation structure follows the repository's existing mutation,
privacy, document-adoption and publication owners. No external implementation
source or new dependency is used. Functional API/standard constraints were
checked on 2026-11-13 against Microsoft's `JsonNode.DeepClone` API reference,
WHATWG's downloading-resources algorithm and W3C's status-message guidance;
the approved plan retains the source register. These constrain isolation,
delivery wording and focus behavior, not the discretionary implementation.

The [Presentation contract](../PRESENTATION_STATE.md) owns current mechanisms.
The task-prefixed execution context in
`dev/active/output-guidance-and-value-transfer/` records actual Red/Green
commands and qualification limits. This decision does not promote unexecuted
client, package, host or assistive-technology gates.
