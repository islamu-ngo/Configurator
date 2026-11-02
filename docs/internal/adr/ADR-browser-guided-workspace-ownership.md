# ADR: Guided Browser workspace ownership

> **Audience:** Contributors | AI agents
> **Kind:** Proposal
> **Status:** Proposed by CTO review; not user-approved or implemented
> **Owner:** Browser
> **Last Verified:** 2026-10-31
> **Source Anchors:** src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs, src/Configurator.Presentation/Form/DefinitionFormModel.cs, docs/internal/BROWSER_UI.md

## Context

The planned Configure/Review/Files redesign unmounts inactive panels. Current
ConfigurationEditor owns the shared form and prepared/export lifetimes; stage/
consent/file focus uses mutable flags while output retirement begins synchronously.
Moving markup into panels must not move the shared answer or retirement authority
into a child whose disposal merely means its view was left.

This is a proposed design decision from the browser-ux-simplification revision-3
review, not evidence of runtime correctness or acceptance. The current living
Browser contract remains authoritative until approved implementation updates it.

## Decision

ConfigurationEditor retains form identity, stage selection and prepared/export
ownership. Review and Files are renderers with callback actions, not new stores.
View-only transitions preserve accepted answers, drafts, consent, input revision
and parent preparation. Leaving Files clears revealed preview and invalidates
its pending reveal/export/focus work. Input changes, consent revoke, clear,
replacement and workspace disposal retire the parent bundle synchronously.

Use existing BrowserArtifactExport.InvalidateAsync for Files exit while retaining
PreparedArtifact.Workspace. Before a later deliberate release, verify current
form/input/privacy/view eligibility and SetOutput from that retained workspace.
No new preparation cache or repeat composition is needed. Fence requests before
the browser transfer is issued; an already-issued deliberate clipboard write
cannot be retracted, and its late completion must not revive current status.

Each pending focus request captures form identity, view generation and target;
only a current mounted eligible target can receive focus after an await/render.
Component disposal releases local JS/DOM/listeners but cannot dispose the shared
form or a bundle still owned by the editor.

Use installed MudBlazor public APIs and existing wrappers. Component parameters
are auto-properties; children raise EventCallback. Shared Core validation and
Presentation mutation remain single owners. One explicit Browser view policy
removes ordinary disclosure without bypassing activation or secret consent.

Component CSS stays in matching .razor.css with one BEM block, logical properties
and existing tokens. Customization uses parameters/Class/theme before a precise
HTML-wrapper-scoped third-party ::deep exception. Preserve global cascade layers;
no new global Mud class overrides, inline hardcoded styles or !important.

## Alternatives and consequences

- Child-owned form/preparation would make panel disposal destructive and permit
  detached late exports; rejected.
- Keeping every panel mounted avoids disposal work but preserves hidden controls
  and stale focus targets; rejected for the proposed exclusive-panel behavior.
- Destroying all preparation on view change is simpler but repeats private
  preparation needlessly; retain it and fence panel actions separately.
- A global transaction/navigation framework adds no needed authority. Use current
  owners and one view-generation fence, with measured control retention.

Breaking UI changes are deliberate; no legacy layout mode or compatibility alias.
Canonical pinned definition identity and live native presentation policy remain
real invariants, not reasons to preserve obsolete Browser interactions.

## Acceptance and evidence required

Before panel extraction, gated external-opening tests must prove no private export
after leave/revoke/replacement, no success resurrection, synchronous byte retirement
and exactly-once release. Rendered tests prove panel round trips preserve form/
drafts/input revision and obsolete correction cannot focus detached/new targets.
Published Browser keyboard/mobile/RTL/theme/offline and performance evidence is
separate from bUnit. No product validation was run for this proposal.

The same implementation plan retains Developer transaction/editor work in phases
5-6, with its own custody/atomicity invariants and dependency gate. This guided
ownership record does not approve those dependencies or close those phases. See
[Browser UI](../BROWSER_UI.md) and the full
[implementation plan](../../../dev/active/browser-ux-simplification/browser-ux-simplification-plan.md).
