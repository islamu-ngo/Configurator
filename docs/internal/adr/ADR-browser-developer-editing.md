# Public-scope document editing and one accepted-state commit

> **Audience:** Contributors | Maintainers | AI agents
> **Status:** Accepted design in approved browser-ux-simplification revision 3; shared runtime implemented; Browser integration and qualification pending
> **Owner:** Presentation / Browser
> **Date:** 2026-10-31
> **Source Anchors:** `DefinitionDocumentSession.cs`, `DefinitionDocumentEditing.cs`, `DefinitionEditingState.cs`

## Context

Bulk editing must not become another accepted answer store or implicitly expose
protected values. A lock around ordinary field setters is insufficient: those
setters parse, clone, record provenance and publish revision effects. Failure
after an early setter could partially change the form, and restoring a snapshot
could overwrite a newer edit or lose drafts and logical row identities.

The existing form also exposes stable field objects to clients. Replacing the
form or those objects to simplify bulk adoption would invalidate actual callers.

## Decision

Presentation owns one disposable document lease per form. Browser owns the
visible private editor model/history. Canonical text is an explicit public-scope
answer envelope, not a backup; constrained Environment text uses Core's separate
captured editable projection. Neither grants whole-file inverse support,
automatic consent or target acceptance.

The form's existing editing aggregate now owns one accepted payload. Stable
field facades resolve that payload through the existing owner gate. A candidate
owns a detached replacement with no public answer accessor or external mutation
capability. Parsing and validation finish before accepted mutation; successful
adoption transfers that payload once. The candidate cannot be consumed twice.
This is ownership transfer, not a general transaction framework or a second
accepted store.

Apply rechecks the actual form/session, admitted digest, base revision,
document/privacy generation, consent, captured scope and cancellation. Stale
work is rejected rather than rebased. Row tokens allocated by rendering after
capture are reconciled before commit; only changed containers retire their
affected identities. Untouched field data, drafts, omissions, origins and
protected values survive. Changed values receive Explicit provenance.

Canonical absence resets only represented scope; explicit omissions suppress
initialization. Environment merge retains missing keys, whereas deliberate
represented-scope replacement records omissions. Core remains the only schema,
predicate and scalar-validation authority. Well-typed partial answers may apply
with missing required Review work; invalid supplied content cannot.

The session retains no source/history store. Caller-owned source bytes and
opaque candidates have explicit retirement. Browser consent precedes raw
acquisition; revocation, replacement and disposal retire authority. Native
complete privacy remains a distinct supported policy. Retiring references does
not promise physical zeroization of immutable runtime strings.

## Consequences

One accepted Apply publishes one revision/input event, invalidating prior
preparation. Observer failure after commit is reported separately and cannot
turn the result into rejection or undo newer reentrant work. Clients reopen the
lease after Apply or another accepted/privacy change while preserving their
own permitted visible text without automatic formatting.

The selected CodeMirror family still requires exact dependency-closure approval,
local deterministic assets and independent implementation/notice evidence.
This ADR does not approve that dependency, create a runtime CDN or authorize
answer-bearing workers, remote services or arbitrary extensions.

## Evidence and limits

The shared implementation has runtime Red/Green cases for atomicity,
presence/reset/omission, literal paths, protected/dormant state, scope, stale
reads/candidates and notification behavior. Exact commands and counts belong
to the [execution evidence](../reports/BROWSER_UX_SIMPLIFICATION_EXECUTION.md).
Browser integration, complete nine-task comparison, physical-device,
assistive-technology and offline/package qualification remain separate gates.
