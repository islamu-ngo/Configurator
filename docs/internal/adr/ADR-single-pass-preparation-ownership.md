# Single-pass preparation and staged output ownership

Status: Accepted for the cross-platform quality implementation, 2026-10-27.

## Context

Browser previously composed a whole bundle to check admission, disposed it and
then composed every output again through separate presentation operations.
Repeated validation and rendering duplicated work and created additional private
buffers. Removing that work must preserve fatal aggregate rejection, canonical
output order and the distinction between complete and secret-deferred output.

## Decision

Core creates one internal validated composition for a definition, answer snapshot
and mode. Only Core can create that validation authority. Each emitted artifact
uses it once; public single-artifact composition remains available and validates
normally. Cancellation is checked at meaningful validation/projection and transfer
boundaries, with invocation/physical-operation tokens forwarded by existing callers.

Presentation exposes a disposable prepared-adoption operation. It copies admitted
artifact bytes and matching completion locations, retaining no answers. The
caller owns this staging operation before execution. Execution transfers one
outcome into the existing workspace; cancellation before start, never-started
disposal and rejected late outcomes clear their owned buffers. Disposing staging
after transfer cannot clear current workspace output.

Browser keeps the Core bundle and a local staged list through adoption. It makes
the list current only after all adoptions succeed under the captured form and
generation. Rejection or replacement disposes every staged workspace/export and
the bundle. Draft withholding remains a bundle diagnostic, not a successful
complete-readiness claim.

## Consequences and evidence

There is one composition authority and one existing workspace outcome owner, not
an answer cache or a scheduler. Private byte copies still have deliberate bounded
lifetimes; neither managed clearing nor clipboard/download retirement promises
forensic erasure.

Scoped local diagnostic events carry only artifact IDs and draft mode. Independent
tests observed four compositions for two Browser outputs before adoption, then
require one ordered composition per output for both complete and draft preparation.
Core and Presentation tests retain independent byte/digest/order, aggregate-failure
and never-started/late-owner vectors. This evidence establishes removed duplicate
work and semantic ownership, not the separate main-thread responsiveness budget.

Source anchors:

- [Core bundle composition](../../../src/Configurator.Core/Artifacts/DefinitionBundleComposer.cs)
- [Core artifact composition](../../../src/Configurator.Core/Artifacts/DefinitionArtifactComposer.cs)
- [Prepared adoption](../../../src/Configurator.Presentation/Presentation/DefinitionPresentationOperation.cs)
- [Browser preparation](../../../src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs)
- [Core contract](../CORE_DEFINITION_CONTRACT.md)
- [Presentation contract](../PRESENTATION_STATE.md)
