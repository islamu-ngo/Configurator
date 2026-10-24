# ADR: Browser workspace presentation and contextual help

Date: 2026-10-23
Updated: 2026-10-24
Status: Accepted design; runtime qualification and limits belong in
[Operations](../OPERATIONS.md#browser-custody-and-offline-verification).
Owners: Presentation and Browser.

## Context

Configuration questions competed with permanent application navigation,
expanding technical detail and repeated field actions. Searching a large
definition also needs a clear entry point without introducing another answer
model or hiding required work. A component library alone does not establish
accessible interaction or acceptable latency.

## Decision

Keep one `DefinitionFormModel` per workspace. Core and Presentation retain
parsing, typed presence, invalid drafts, validation, provenance, row identities
and preparation authority. Browser composes those owners rather than copying
answers into presentation controls.

Separate authored experience from detail. A nullable shared disclosure override
follows the experience when unset; explicit Essential, Standard and All active
choices affect visibility only. Required work and active errors remain
reachable. Comfortable and Compact change session-local spacing tokens and
control height, not text size, consent, answers or validation. Neither extends
persisted appearance preferences.

Use thin outlined Mud controls at the field boundary. `AppTextField` receives
raw string drafts, including incomplete numeric text and masked credentials.
It uses a full-width `MudInput` and renders the connected scalar error directly.
Public enum and boolean fields use `MudSelect` with distinct identities for
absence, explicit false and an admitted empty string. Shared form/path/revision
checks and a mounted-control generation reject callbacks from retired controls.
Raw text input and change notifications preserve queued characters without a
second answer buffer or duplicate blur edit.
`AppSelectField` applies fresh parameters and callbacks on every update even
when unchanged parameter-driven visual output can be skipped. Internal popup
events keep their renders; the adapter retains no second answer snapshot.

Keep current-section mounting separate from answer ownership.
`ConfigurationSectionFields` retains mounted controls and introduces only
metadata root-ID sets, a pending count and a mount generation. New roots arrive
through bounded frame-separated batches; a correction target can mount directly.
Form replacement, view supersession and disposal fence every awaited continuation.
Secret denial and output retirement remain synchronous. A local control-only
channel task separates rendering work without a worker or answer-bearing message.
DOM completion remains distinct from complete-definition validation/readiness.
Pass the editor's current section field states into the mounting component,
instead of rebuilding a second whole-definition lookup there.
Reuse the form index's admitted item metadata for list rows, with explicit row
title/required overrides and inherited masking, instead of serializing another
item schema during every render. Unchanged field output can be skipped without
skipping parameter delivery or fresh callbacks after an input revision.

Put metadata-only search in the wrapping application bar. `WorkspaceSearchState`
registers the current editor using an owner and generation; it holds delegates
and a render fragment, not an answer snapshot. Discovery mounts deliberately,
ranks the complete selection and renders one bounded result page. Retired
registrations cannot search or unregister a newer workspace. The sticky bar
keeps the entry reachable during deep editing; application navigation and
search are separate labelled disclosures. Keyboard search yields to a modal,
and entry focus uses `preventScroll` rather than moving the edited question.
At viewport heights of 30 rem or less the wrapping header and application
navigation scroll with the document, avoiding obstruction at high browser zoom.
Open search instead fixes its entry/results inside that short viewport while
reserving header space. Closing keyboard search returns to the prior connected
control whether or not the header is visible; it retains no answer copy.
Phone search overlays the retained trigger space rather than inserting a new
in-flow row, preserving header height and document geometry.

Keep short essential hints and connected errors beside the input. A named help
button opens `ConfigurationInspector` with admitted public schema, descriptions
and lazily projected definition/output keys only. Interactive links belong in
the dialog, not a tooltip. Revocation, replacement and disposal close the owning
inspector and invalidate late opens. Named field menus retain deliberate
omission, explicitly identifying nested answers when omitting a container.
Collapsing remains distinct from omission.
Help uses a named native button and the packaged Mud icon rather than mounting
a tooltip per field. Omission menus mount on first use and remain ordinary
`MudMenu` instances afterward. Their open state participates in the render
signature, and cold mounting and close explicitly restore activator focus.
Public-metadata render boundaries still refresh current help-binding delegates.

The document remains the form's vertical scroll owner. Bounded navigation,
search results and an overflowing dialog may scroll as transient surfaces.
Stable viewport scrollbar allocation and matching modal scroll-lock styling
avoid moving the active question when help opens.

## Consequences and limits

These adapters add component lifecycle and rendering costs. Native compiler
and semantic tests establish typed/privacy/concurrency behavior; actual
published-browser measurements establish focus, geometry and latency for the
named host only. Keep failed candidates, first-use observations and outliers.
Do not replace those measurements with component counts, relax feedback
budgets or claim speech, physical-device or browser-zoom qualification from
emulated screenshots.

This decision delivers editor foundations, not the entire redesign. Journeys,
table editing, expanded appearance customization, review/export/departure and
entry/help/offline composition remain mandatory separate slices in the
[follow-up backlog](../../../dev/backlog/browser-ui-ux-redesign-followups.md).
They need their own current plans and final surface evidence.

See [Browser ownership](../BROWSER_UI.md#guided-workspace),
[Presentation state](../PRESENTATION_STATE.md),
[custody contract](../LAUNCH_CONTRACT.md#transient-answer-and-output-boundary)
and the [browser design contract](../../../src/Configurator.Browser/DESIGN.md).
