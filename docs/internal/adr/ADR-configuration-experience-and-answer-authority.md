# Configuration experience and answer authority

Date: 2026-10-20
Status: Proposed; planning decision, not implemented or production-qualified.
Owners: Core, Presentation and client maintainers.

## Context

The current Guided visibility policy retains protected/consequence-bearing
editors, while operators need a small route, discoverable advanced settings,
useful secret explanations and a review of actual configuration effects.
Experience selection, filtering and help must not become answer mutations,
secret consent or target administration authority.

The current source seams are `SetupFieldDefinition`, `DefinitionValidator`,
`DefinitionFormModel` and Browser `ConfigurationEditor`/`ConfigurationField`;
Desktop/Terminal directly consume the display contract. These are the offline
Configurator owners, not Event backend layers.

## Decision

1. Core admits bounded data-only experiences, existing help and structured public
   guidance/acquisition metadata into canonical definition identity. It continues
   to own sensitivity, activation, validation and composition.
2. Presentation owns the one projection of sections, literal correction paths,
   stable row/draft state, provenance, progress and metadata discovery. View
   changes preserve answer revision and valid prepared bytes.
3. Adapters render that projection. Required/error reachability and masked review
   cannot be defeated by local disclosure or filters. Progress counts actual
   active obligations, not declared optional fields.
4. Secret entry remains per-workspace consented. Password-manager compatibility
   uses genuine password-purpose hints and stable public identities, not hidden
   credential copies, fake target logins, submitting forms or vendor vault APIs.
   Extension-held copies are outside application-local custody guarantees.
5. Generation is a separate capability with admitted profiles and cryptographic
   entropy. Issuer credentials and cryptographic trust are not random-string
   formats. Additional file formats need target-specific contracts.
6. Replace obsolete visibility/mode contracts directly. Do not preserve aliases,
   upcasters or duplicate client policy for a pre-release interface.

## Consequences and alternatives

Canonical help edits require a new definition revision even if emitted bytes are
unchanged. Direct callers must migrate together. Central draft/row ownership is
required before rendering fewer subtrees safely.

A Browser-only predicate is rejected because clients would disagree on hidden
requirements and effects. YAML syntax comments are rejected as runtime-help
authority because they are not serialized application data. Executable templates,
remote answer-processing and generic "generate any credential" are rejected
because they grant authority absent from this offline utility.

The proposal adds admission/projection work but keeps adapters thin and avoids
new services, packages and answer storage. Actual UX/accessibility/manager
quality still requires candidate-specific evidence; design review is not proof.

## Delivery and verification

The active guided workspace is separate from
[generation](../../../dev/backlog/configuration-secret-generation.md),
[delivery/Event adoption](../../../dev/backlog/guided-configuration-delivery-event.md)
and [future formats](../../../dev/backlog/configuration-format-expansion.md).
Event integration follows completed Configurator capabilities; target validation
and apply authority remain Event's.

Red invariants must prove unchanged answer/output across view changes, literal
correction, truthful progress and denied/stale autofill isolation before relevant
implementation. Release qualification must inspect the actual UI, extension
behavior and application sinks. Promote this ADR from Proposed only when the
implementation and evidence establish the decision; no approval is invented.
