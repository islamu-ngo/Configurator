# Browser interface ownership

The static WebAssembly shell owns navigation and appearance. `Layout/MainLayout`
provides skip navigation, the header, application navigation, main landmark and footer. Home,
Store, Open configuration and Help are route destinations; the configuration
workspace remains owned by Presentation and the Browser editor.

`Configurator.Browser.csproj` owns Release AOT compilation; `global.json` selects
the SDK and workload set. CI and the Browser container build explicitly provision
the WebAssembly tools, including Python on Linux. Debug stays interpreted.
AOT trades a larger public payload/cache for lower managed interaction cost.
The [public hosting guide](../public/browser.md#hosting) gives the build command;
[Operations](OPERATIONS.md) records exact package costs and runtime evidence.

At every viewport width, application navigation expands inline from a named Menu
button. Escape closes it and restores focus to that button. This disclosure
does not create a modal or trap focus. Skip navigation focuses the main landmark
without changing the URL, so it cannot discard a live workspace through a
fragment navigation.

`AppearanceThemeService` owns a separate immutable `AppearanceState`. The dedicated
`configurator.appearance.v1` storage key accepts only bounded `themeMode` and
`direction` strings. Unsupported, duplicate, unknown, malformed or oversized
preferences fall back to system appearance and automatic direction. Browser
storage failures leave the current tab usable. Appearance has no answer,
artifact, definition, target or transport reference.

The light/dark palettes and fluid typography adapt first-party Event precedents.
High-contrast modes strengthen palette contrast; system mode follows operating
system appearance. Direction changes update the document and MudBlazor RTL
provider without replacing route content or workspace state. System fonts are
used; no remotely hosted font or icon asset is fetched.

Global CSS follows reset, base, tokens, mudblazor-overrides, components and
utilities layers. The local MudBlazor stylesheet is imported into `reset`
before application normalization. An unlayered vendor import would outrank
all application layers, removing native-control borders and page spacing.
Theme-provider styles and component isolation remain unlayered.
Three-tier tokens bind primitive spacing/radii to palette
semantics and component tokens. Colocated component styles use logical
properties. Local MudBlazor assets supply controls and providers. Runtime theme
styles require the explicit CSP style allowance documented in the launch
contract; script origins remain local.

See [Browser guide](../public/browser.md), [launch boundary](LAUNCH_CONTRACT.md)
and [transfer provenance](extraction/BROWSER_UI_TRANSFER.md). Component tests
verify rendered structure, preference admission and presentation changes.
Published-browser keyboard, zoom/reflow, theme, RTL, offline and CSP evidence
is separate from component-test evidence.

## Interface refinement

Shared native text controls explicitly opt into `form-control`; MudBlazor
continues to own its outlined inputs. Control borders use the input palette,
while decorative card borders use the divider palette. Normal themes use a
teal action accent; high-contrast themes retain their monochrome palettes.
The shell uses component spacing tokens without reserving a permanent sidebar. Its main
column fills the viewport and keeps the footer below content without a fixed
overlay. Navigation icons are local MudBlazor SVGs with decorative semantics.

Open configuration groups local-file and HTTPS entry in independently named
sections with labels and connected format/help text. Store filters have a
separate surface and a recoverable no-match state. Cards retain complete
descriptions and align revision actions without clipping content. The editor
groups field view and explicit secret consent in a responsive toolbar,
distinguishes complete and draft preparation, and keeps optional artifact
import in its own labelled section. Container queries reflow editor controls
within the available workspace width; logical spacing supports RTL.

The structure and styling are independently designed from existing repository
components. No packages, remote fonts or external design assets were added.
Research used only functional standards and API facts from
[W3C form guidance](https://www.w3.org/WAI/tutorials/forms/),
[CSS cascade layers](https://www.w3.org/TR/css-cascade-5/#layering), and
[Microsoft CSS isolation documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation?view=aspnetcore-10.0).
No external snippets or expressive UI composition were copied. The AFC/SSO
review filtered platform semantics and standard form grouping; discretionary
layout, tokens and component organization remain repository-owned.

## Guided workspace

`Components/Configuration/ConfigurationEditor` retains the shared
`DefinitionFormModel` and workspace operation generations. Browser-owned
`ConfigurationSection` projections group the shared validated fields. Only the
selected section mounts its editors. Moving between sections preserves typed
answers, unfinished drafts, provenance and logical row identities in the shared
form, not in retained DOM controls. Active hidden fields still participate in
shared validation.

`ConfigurationSectionFields` mounts newly editable roots in bounded batches,
retaining controls already present. The editor supplies its current section's
already-evaluated field states; the child does not rebuild a whole-form lookup
or evaluate answers independently. Its pending count and `aria-busy` describe
DOM completion, not answer readiness. Current correction targets mount directly;
unrelated fields continue through the frame boundary. A dedicated mount generation,
form identity and editor cancellation token reject obsolete continuations after
input, view changes, replacement or disposal. View-only supersession does not
consume another synchronous batch. Secret denial and prepared-output retirement
remain synchronous. `configuration-rendering.js` posts a local null-message task
from an animation callback; neither answers nor artifacts enter that handoff.
The module is a required public offline asset.

`Layout/WorkspaceSearch` exposes an outlined MudBlazor query in the appbar and
mounts `ConfigurationDiscovery` filters/results only after deliberate opening.
`WorkspaceSearchState` holds owner-qualified delegates and a render fragment,
not an answer snapshot or a second query. The editor releases its generation
before replacement, discard or disposal; retired registrations cannot clear or
search the current editor. `workspace-interactions.js` receives only an element,
callback reference and generation for Ctrl-K/Command-K and outside dismissal.
Escape/Close restore the visible query or phone trigger; result selection closes without stealing the
editor's destination focus. Application navigation and workspace search are
separate labelled surfaces, not a partial combobox.
The form retains one current query/facet selection. Ranking and facet
counts cover the complete shared selection; only the current 50-result page
mounts. Selection or experience changes reset the page, and answer changes clamp
it to the current result range. Page navigation does not mutate answers.
Query/facet handlers reset their own result page and rerender discovery without
rerendering unrelated editor sections. Disclosure and experience changes still
notify the editor normally. Repeated result, effect and correction actions use
native buttons with the existing shared action styling.
Experience selection returns to Configure while retaining native selector focus,
instead of scheduling a heading-focus interop call after mounting the new
section. Explicit workflow/section navigation and correction retain their
destination-focus behavior.

`ConfigurationField` sends parsed typed values through the shared form. An absent
boolean differs from false, and absent objects/lists differ from empty values.
`AppTextField` wraps a full-width outlined string-bound MudInput and renders its
connected scalar error directly, including numeric drafts and masked credentials.
Public enum/boolean leaves use MudSelect with
distinct absence and admitted-empty identities. The form still owns parsing and
invalid drafts; raw text input/change and received choice notifications use the
same form/path/revision/mounted-control generation fence. A callback captured
before an editor unmount cannot edit its remounted successor. Focus, input modes
and credential autocomplete remain explicit; no copied answer buffer is added.
Masked scalar controls inherit sensitive or secret ancestors; secret consent
still uses the form's edit permission, allowing public siblings to remain
editable. Authored titles are preserved. Mechanical identifier formatting is
used only when title equals key, with the exact key available separately.
List rows obtain their immutable item metadata from the shared admitted index,
not a new JSON serialization on each render. The renderer supplies the row title,
required state and inherited masking separately. Unchanged parameter updates may
skip a field tree only while the form, input revision, projection and all
rendering/focus parameters remain current. Parameter delivery still runs; a new
input revision always refreshes the revision-fenced callbacks.

The independent detail selector delegates to the shared disclosure override.
Comfortable/Compact changes session-local CSS tokens only. Named field-action
menus retain explicit omission, including container descendants.
Each omission menu is instantiated on first use and retained afterward. Its
custom activator receives focus after cold mounting and after close; the open
state participates in rendering so its expanded state stays current. Unchanged
field chrome may skip visual rendering, but current callbacks and help bindings
still refresh without retaining another answer snapshot.
`ConfigurationFieldHelp` opens `ConfigurationInspector` with admitted public
schema, authored descriptions and lazily projected value-free output bindings.
It closes its own dialog on revocation, replacement or disposal and rejects late
opens. Its named native trigger retains the packaged Mud icon and focus/target
rules without precreating a tooltip overlay for every field. Stable scrollbar
allocation prevents modal geometry shifts; the sticky wrapping appbar keeps
search reachable during deep editing. Its global shortcut yields to an active
dialog rather than escaping modal focus custody.

`ConfigurationReview` projects shared diagnostics into value-free explanations
and correction buttons. The effects region mounts at Review and pages 50 entries
at a time. Its default selection includes active required work, corrections,
deferred secrets and supplied/defaulted entries; separate controls inspect
omitted optional and inactive metadata without enabling it. Diagnostic correction
actions are independent of effect paging. A correction selects the section and focuses its field
or the diagnosed collection row. Explicit field focus permits native scrolling,
so a correction below a long results list becomes visible rather than merely
receiving off-screen focus. Secret recovery also covers required secret
descendants inside optional supplied objects. Draft and complete preparation
remain distinct; filenames and completion locations come from shared artifacts.
Reveal, copy and download remain deliberate operations owned by
`BrowserArtifactExport`, and edits, revocation and disposal invalidate output.

`BrowserNavigationState` and `WorkspaceNavigationGuard` confirm route departure.
Cancellation keeps the workspace. Confirmation invalidates current operations
synchronously, then awaits discard before allowing another source/store route.
See [Presentation state](PRESENTATION_STATE.md) for generation ownership and
[Operations](OPERATIONS.md#browser-custody-and-offline-verification) for actual
published-browser evidence.
