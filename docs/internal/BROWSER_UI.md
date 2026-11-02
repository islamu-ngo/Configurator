# Browser interface ownership

> **Audience:** Contributors | AI agents
> **Kind:** Living contract
> **Status:** Current browser adapter ownership; published evidence is separate
> **Owner:** Browser
> **Last Verified:** 2026-11-01
> **Source Anchors:** `src/Configurator.Browser/`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/OPERATIONS.md`
> **Review basis:** Source-owner reconciliation and scoped published direct-editing evidence; complete qualification remains separate

The static WebAssembly shell owns navigation and appearance. `Layout/MainLayout`
provides skip navigation, the header, application navigation, main landmark and footer. Home,
Store, Open configuration and Help are route destinations; the configuration
workspace remains owned by Presentation and the Browser editor.

`BrowserStrings` contains both interface catalogs, including Developer chrome and
import-review labels; both languages are embedded in the main assembly. Shared
diagnostic, import-finding, compatibility and autofill guidance comes from
Presentation's explicit-locale resources. Counts use the workspace culture,
without changing process culture. Machine finding codes and severity attributes
remain stable while their displayed descriptions localize.

`Configurator.Browser.csproj` owns Release AOT compilation; `global.json` selects
the SDK and workload set. CI and the Browser container build explicitly provision
the WebAssembly tools, including Python on Linux. Debug stays interpreted.
AOT trades a larger public payload/cache for lower managed interaction cost.
The [public hosting guide](../public/browser.md) gives the build command;
[Operations](OPERATIONS.md) records exact package costs and runtime evidence.

At desktop widths, application and section navigation share one visible rail.
On narrow screens, the named Menu button reveals it inline. Escape closes that
disclosure and restores focus to the button. This disclosure
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

## Schema-driven secret controls

`ConfigurationField` classifies sensitivity through `DefinitionSensitivityPolicy`
and reads the admitted `DefinitionNode.Secret` specification. Secret strings
mount `ConfigurationSecretActions` only while the shared form permits editing.
The component delegates generation, validation and operation ownership to
Presentation; it does not evaluate cryptographic policy itself.

Generate/Regenerate, Show/Hide, Copy and Cancel use native keyboard-operable
buttons with field-specific accessible names. Shared English/Arabic requirements
connect to the input through `aria-describedby`; current failures connect through
the existing error element. Invalid draft input remains editable. PKCS8 PEM uses
a read-only masked input until Show deliberately opens a multiline editor;
native single-line inputs would otherwise remove line breaks. Editing that
revealed textarea preserves its visibility and exact draft; Hide, external
edits, revocation and disposal retire the revealed view. Status text
announces safe operation outcomes, never answer values.

Transient derivation input clears before the operation yields. Owned character
and export byte buffers are zeroed. A generated original password remains in
Presentation until an explicit reveal/copy, dismissal, edit or disposal; it has a
distinct masked result surface. Field edits reset reveal state, and retiring
callbacks are fenced by component generation and editor identity. Clipboard
actions reuse `BrowserArtifactExport`, including its asynchronous revocation
checks. No automatic copy, download, storage or worker message is introduced.

Browser task yields allow busy rendering and queued cancellation/revocation
before result application. A synchronous cryptographic primitive itself cannot
be interrupted halfway through on a single-threaded WebAssembly runtime; the
shared operation rejects its result after cancellation. Published AOT validation
is a separate gate from component tests.

## Interface refinement

Shared native text controls explicitly opt into `form-control`; MudBlazor
continues to own its outlined inputs. Control borders use the input palette,
while decorative card borders use the divider palette. Normal themes use a
teal action accent; high-contrast themes retain their monochrome palettes.
The shell uses a header/body/footer grid with one desktop navigation rail spanning
the body. Application and section navigation share that rail; narrow screens use
the named Menu surface. The main column has a shrinkable inline track and the
footer stays below content without a fixed overlay. Navigation icons are local
MudBlazor SVGs with decorative semantics.

`Catalogue` owns the single project query across admitted local definitions and
optional directory metadata. It merges only matching family/revision/digest tuples;
source details retain canonical identity and specific-revision links. Local-file
and HTTPS entry remain independently named recovery sections with connected help.
The editor keeps output/language selection common to all workflow panels and moves
profile/density controls into secondary setting detail. Explicit secret consent
remains a deliberate workspace action. The editor
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

## Local Developer editing

`ConfigurationDeveloper` owns one private editor through
`configuration-developer.js`; `DefinitionFormModel` remains the sole accepted
answer workspace. Its `Form` and optional `EnvironmentArtifactId` parameters
select the shared document session, not a second answer dictionary. Public
coverage is available without consent. Source acquisition and editor mounting
require the current workspace's enabled privacy state.

The component lifecycle and JavaScript lease remain in its code-behind;
`ConfigurationDeveloper.Operations.cs` groups document preview, application and
formatting. `ConfigurationEditor.Import.cs` owns bounded reads/comparison and
`ConfigurationEditor.Publication.cs` groups preparation and deliberate export.
These partials share the existing component state and generation checks; they
do not introduce parallel workspaces, locks or independent mutation authority.

Source admission and editor DOM construction use separate tasks through the
existing `js/configuration-rendering.js.nextFrame` boundary. The component owns the
acquired byte array until transfer, rechecks its lease after the yield and clears
that array on retirement as well as normal completion. Revocation, edits,
replacement and explicit cancellation cannot mount the held old source.

Preview acknowledges its busy state through that same frame boundary before
acquiring editor bytes. It supplies the document session's existing cooperative
publication yield through `nextTask`: owned source bytes have been cleared before
queued cancellation or edits are serviced, and the session rechecks authority
before returning a candidate. The component also rechecks its model lease before
displaying the preview. Bounded parse completion is measured separately from its
initial feedback.

Apply also paints busy state before freezing and committing the captured
candidate. It rechecks the model lease and candidate identity after that frame.
Its completion can clear busy state only for the same uncanceled text version,
so an obsolete Apply cannot make a newer Preview appear idle.

The bridge imports the checked local bundle and owns one token-fenced slot.
Typing reports only version, dirty and history-limit metadata. Preview transfers
UTF-8 bytes into `DefinitionDocumentSession.ParseAsync`; both sides clear owned
transfer buffers. Apply freezes the current JS version, checks the candidate's
session/revision/scope authority and performs the shared atomic swap. After a
successful commit, the panel reopens document metadata and marks the existing
editor clean without replacing its text or history. A successful commit with an
observer failure is still successful, not a rollback.

Panel lifetime, source version, form identity and input revision fence every
continuation. A root render can precede the panel's input notification, so
`OnAfterRenderAsync` refuses to mount obsolete document metadata rather than
rescheduling failed mounts. Same-version dirty/history callbacks do not
invalidate newly reopened authority. Revocation's capture-phase listener
destroys JS state synchronously before asynchronous managed cleanup. Retirement
clears reads, candidates, diagnostics, callbacks and listeners; it does not
promise physical string erasure.
Explicit user cancellation requests focus on the rendered retirement notice,
whose next Tab stop is the reopen action. The request is generation-fenced and
cleared by subsequent retirement; consent revocation, replacement and disposal
do not request this recovery focus.
While mounted, the bridge also subscribes to `configurator-custody-abandoned`,
the existing `pagehide` owner's synchronous event. That event retires the JS
document, undo history and outstanding read buffer before a hidden history entry
can retain an operable editor. Retirement removes this listener together with
the consent listener. The existing `pageshow` policy reloads abandoned realms
rather than restoring their workspace.

`ConfigurationEditor.ResolvePendingAsync` gates editing-mode, workflow, output
and correction transitions. `IBrowserPendingChanges` lets
`BrowserNavigationState` resolve Apply/Discard/Stay before its separate
accepted-workspace loss confirmation. It rechecks the registration lease before
capturing the resulting work generation. Locale/theme changes preserve the
private model. Both Developer resource sets ship in the main assembly, avoiding
answer-time satellite requests.

Locale metadata carries the current public phrase dictionary as well as label,
description and language. The engine reconfigures an owned metadata compartment,
not its document or history. An open search panel is refreshed through the
installed public panel commands, with connected focus restored afterward.
The component reapplies its value-free Core diagnostics after reconfiguration;
source version and candidate authority remain unchanged.

Keyboard/formatting help uses a localized native disclosure above the editor.
Its full description retains the same accessible-description identifier, while
scope, representation and pending status remain visible independently. This
keeps the editing surface closer to the first small-screen viewport without
dropping format, invalid-text or private-history rules.

The engine derives adaptive syntax colors through the public
`HighlightStyle.specs`/`HighlightStyle.define` API and the inherited CSS
`color-scheme`, tinting dark-mode syntax toward the current text token. Gutter
and caret colors also use the existing semantic tokens. No dependency was added
and no third-party implementation source was needed. The AppButton primitive
keeps fill changes immediate so a theme change does not combine its new contrast
text with an old animated background; border and shadow transitions remain.

The engine entry owns bounded inverse-fragment history, local syntax assistance
and metadata-only completion. Core and Presentation remain authoritative for
typed values, scope and application. See the
[shared editing contract](PRESENTATION_STATE.md) and
[asset owner](PACKAGING.md#local-editor-assets). Source/component tests do not
substitute for actual engine, published-cache, IME, assistive-technology or
participant qualification.

## Guided workspace

The workspace help control receives the form's explicit locale, including its
accessible name and inspector dialog. Field and workspace help therefore use
the same locale owner.

`BlazorWebAssemblyLoadAllGlobalizationData` selects the SDK's full ICU data at
startup. Definitions can declare locales beyond the provider's en/ar resource
catalog, and workspace switching does not reload the app. Startup-culture shard
selection is therefore insufficient: an en-US boot otherwise loads EFIGS data,
which omits Arabic; fixing this with the non-CJK shard would instead omit
admitted Japanese, Korean and Chinese cultures. The ICU data is a public startup
asset, never an answer-bearing request. This configuration adds no dependency
or translation-quality claim. Microsoft documents the supported selection
mechanisms in [Blazor globalization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0#net-globalization-and-international-components-for-unicode-icu-support-blazor-webassembly).

Output tasks come from admitted `presentation.outputSelector` metadata, never
an inferred `/operation` field or enum order. `BrowserLaunchSelection` retains
explicit-entry intent; Launch/Open pass only a deliberate entry into a fresh
editor. `DefinitionFormModel.SelectOutput` maps the exact root data name to its
editor ID and uses the existing typed mutation. A resolver fallback and ordinary
rerenders cannot replace initialized, supplied or omitted answer state.
The current Event example and separate optional-store family are revision 2,
with Environment/platform inclusion initialization and meaningful localized
sections. Historical store snapshots and the upstream historical receipt retain
their bytes; no active workspace is upgraded implicitly.

`Components/Configuration/ConfigurationEditor` retains the shared
`DefinitionFormModel` and workspace operation generations. Shared immutable
`DefinitionFormSection` snapshots provide section metadata; Browser
`ConfigurationSection` projections add navigation progress. Only the
selected section mounts its editors. Moving between sections preserves typed
answers, unfinished drafts, provenance and logical row identities in the shared
form, not in retained DOM controls. Active hidden fields still participate in
shared validation.

Configure, Review and Files have separate mounted children, not hidden interactive
copies. Their disposal never disposes the parent form or prepared bundle. A
captured form identity and view generation fence callbacks and post-await focus;
only a currently mounted eligible heading/control may receive focus. Configure
resolves to the mounted Form section or Developer heading. The hidden workspace
label is never a focus target: revealing it on focus and collapsing it on blur
would move lower pointer targets between pointer-down and pointer-up. Leaving
Files invalidates each export capability and clears its preview without disposing
the prepared workspace. A later deliberate action rechecks current eligibility
before rearming bytes. Answer edits, revoke, clear and replacement still retire
the parent bundle synchronously. View navigation cancels pending acquisition,
not a completed comparison owned by the form.

`BrowserNavigationState` leases derive locale/direction from their registered
form rather than storing a second language preference. A real locale change
refreshes chrome; stale leases cannot refresh or clear the current provider.
English and Arabic UI resources are embedded in the main assembly for offline
use. Appearance direction remains an explicit override of the derived direction.

The Review panel derives complete-ready, secret-deferred or blocked presentation
from the existing shared progress and input diagnostics. It does not compose
artifacts or collect another answer tree. Its ready and draft actions invoke the
editor's existing preparation/adoption operation. The inner effects list remains
value-free and names declared output metadata rather than promising inclusion.
Required/optional/secret state, origin labels and recovery text use the current
English or Arabic resource set.

`ConfigurationFiles` owns each mounted file's preview element and accessible
actions, not its bytes or workspace. The editor retains preparation, export and
post-await eligibility checks. Per-file status keeps failed Copy recovery beside
an available Download; Reveal/Hide still uses the existing generation-fenced
export service. Leaving Files drops component references and preview capability
while retaining the parent prepared workspace.

The navigation lease queries actual work from the current editor. Shared
`DefinitionFormModel.HasWorkspaceChanges` checks existing supplied values,
omissions and all invalid drafts, including inactive drafts, without cloning a
snapshot. Pending acquisition, comparisons, provisional Developer work and
prepared files also count.
Untouched-workspace navigation and clearing skip the loss prompt. Cancellation
preserves work; a late confirmation cannot discard a replaced or changed owner.
Work-only notifications refresh the navigation guard, not the layout body or
locale provider. They do not create another dirty-state or answer cache.

`ConfigurationSectionFields` mounts newly editable roots in bounded batches,
retaining controls already present. The editor supplies its current section's
already-evaluated field states; the child does not rebuild a whole-form lookup
or evaluate answers independently. Its pending count and `aria-busy` describe
DOM completion, not answer readiness. Current correction targets mount directly;
unrelated fields continue through the frame boundary. A dedicated mount generation,
form identity and editor cancellation token reject obsolete continuations after
input, view changes, replacement or disposal. View-only supersession does not
consume another synchronous batch. Secret denial and prepared-output retirement
remain synchronous. `js/configuration-rendering.js` posts a local null-message task
from an animation callback; neither answers nor artifacts enter that handoff.
The module is a required public offline asset.

`Layout/WorkspaceSearch` exposes an outlined MudBlazor query in the appbar and
mounts `ConfigurationDiscovery` filters/results only after deliberate opening.
`WorkspaceSearchState` holds owner-qualified delegates and a render fragment,
not an answer snapshot or a second query. The editor releases its generation
before replacement, discard or disposal; retired registrations cannot clear or
search the current editor. `js/workspace-interactions.js` receives only an element,
callback reference and generation for Ctrl-K/Command-K and outside dismissal.
Escape/Close restore the visible query or phone trigger; result selection closes without stealing the
editor's destination focus. Application navigation and workspace search are
separate labelled surfaces, not a partial combobox.
The header establishes its own stacking context at the existing MudBlazor popover
layer so workspace containment cannot intercept result hit targets. Discovery
places results before its filter disclosure in DOM order, with English/Arabic
resources for result controls, facets and status. Section navigation precedes
global links in the shared rail; local previous/next/review actions reuse the same
section callbacks. Output/language and editor/detail controls are native disclosures.
Correction-owned disclosures track summary clicks in component state, so repeated
corrections reopen them after a user closes them. `RequestFocus` opens the relevant
disclosure before scheduling focus. Denied fields receive the editor's cached,
view-fenced consent callback; it only opens consent. The explicit revoke action
remains outside disclosures on every workflow panel. Search navigation captures
the input revision and checks it before and after dirty-document resolution.
The Browser selects shared authored visibility when presentation metadata exists,
starting at Essential disclosure on creation and reset, and retaining direct
visibility for neutral definitions. Secondary tools follow the active work in DOM
order. Shared required/error
promotion continues to override optional disclosure; no Browser evaluator is added.
Closed choices use the existing typed ordinal select and keyboard type-ahead.
Masked choices expose replacement options without preselecting the accepted
value, retaining the existing consent, mutation and stale-control fences.
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
Closed enums use `AppSelectField` with distinct absence and admitted-empty
identities; unmasked booleans without an enum retain their radio group. Scalar,
enum controls and boolean groups receive current requiredness through MudBlazor's `Required`
parameter, which owns the rendered input's `aria-required`. Attribute splatting
alone is insufficient. `AppSelectField` includes requiredness in its parameter
render comparison, so dependency changes update the accessible state. Radio
groups retain an explicit `aria-invalid` from shared validation. The form still owns parsing and
invalid drafts; raw text input/change and received choice notifications use the
same form/path/revision/mounted-control generation fence. A callback captured
before an editor unmount cannot edit its remounted successor. Focus, input modes
and credential autocomplete remain explicit; no copied answer buffer is added.
Masked scalar controls inherit sensitive or secret ancestors; secret consent
still uses the form's edit permission, allowing public siblings to remain
editable. Authored titles are preserved. Mechanical identifier formatting is
used only when title equals key, with the exact key available separately.
Long labels and metadata paths wrap within the field, including collapsed
compound children in narrow RTL layouts; they must not widen the viewport.
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
and correction buttons before output decisions. Files reuses the same diagnostic
component and revision-bound correction callbacks after failed preparation;
answers and input drafts survive while previous export eligibility is retired.
Review lists the current output selector's artifact set, retaining the declared
set for definitions without output selection. Origins and bindings are secondary
details. The effects region mounts at Review and pages 50 entries
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
`AppDiscardDialog` presents internal discard decisions through the shared Mud
dialog host, with an explicit workspace locale, a named dialog, Cancel first and
Escape dismissal. The dialog host owns focus trapping and restoration. A
superseded navigation cancels its pending dialog; caller generations still reject
late decisions. `NavigationLock` retains browser-managed external unload prompts.
See [Presentation state](PRESENTATION_STATE.md) for generation ownership and
[Operations](OPERATIONS.md#browser-custody-and-offline-verification) for actual
published-browser evidence.
