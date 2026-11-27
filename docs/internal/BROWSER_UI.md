# Browser interface ownership

> **Audience:** Contributors | AI agents
> **Kind:** Living contract
> **Status:** Current browser adapter ownership; published evidence is separate
> **Owner:** Browser
> **Last Verified:** 2026-11-27
> **Source Anchors:** `src/Configurator.Browser/`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/OPERATIONS.md`
> **Review basis:** Source-owner reconciliation and scoped published direct-editing evidence; complete qualification remains separate

## Portable profile integration

`BrowserProfileVaultController` (`src/Configurator.Browser/Profiles/BrowserProfileVaultController.cs`, `sealed class`, `IAsyncDisposable`) owns encrypted profile worker custody, comparison fields, `ProfileRecoveryReference`, export invalidation, `IsBusy` state for `HasWork`, and direct `Form.InputChanged` worker/recovery-reference retirement across transient panel mounts. Passphrase methods accept `ReadOnlyMemory<byte>` / owned `byte[]` exclusively (zero `string` passphrase fields or parameters) and zero buffers in `finally`. `ConfigurationEditor.Profiles.cs` binds one controller per `DefinitionFormModel` lifetime and retires/disposes it **before** `_form.Dispose()` across `OnParametersSet`, `DiscardAsync`, and `DisposeAsync`. `ConfigurationVaultPanel` (`IDisposable`-only pure content view) renders inside the `Profiles` right-side utility dock panel, keeps its six passphrase inputs, reveal toggles, and selected file handles strictly view-local, encodes UTF-8 bytes and wipes all local credentials synchronously **before** awaiting async controller calls, and clears + re-renders on `CredentialsCleared` and `Dispose()`.

`WorkspaceHeader` leads as a single compact primary row with the definition title, tabular progress + `[data-status-slot="reserved"]` (`CLS = 0`), workflow stage buttons (`Configure`, `Review`, `Files`), an always-visible interactive secret posture chip (`[data-workspace-consent-chip]`, toggling `Trust & Privacy`) paired directly beside a one-click `Revoke` button (`[data-action="revoke-secret-entry"]`) whenever secrets are unlocked, a narrow-viewport (`<= 60rem`) lazy-mounted `MudMenu` **Tools** trigger (`[data-action="open-workspace-tools"]` with `[data-tools-utility]`), and a right-aligned **Advanced** progressive-disclosure toggle (`[data-action="toggle-advanced-options"]`, counting only genuine `#workspace-advanced-options` overrides). `#workspace-advanced-options` stays `hidden` and `inert` at initial load and reveals the Scope toggle chip (`[data-action="change-scope"]` controlling `#configuration-context`), **Output task** selector (`#configuration-output`), `[data-configure-ribbon]` (experience/detail/density, Humanized/Technical naming, Undo/Redo, `?` shortcuts), and `[data-digest-disclosure]` source metadata passed from `Launch` (which keeps `<h1 id="launch-heading" class="sr-only">` for `FocusOnNavigate` without stacking a duplicate hero header). Outer `.configuration-editor__controls` uses a semantic `<div role="group">` container instead of `<fieldset>` so navigation/region landmarks are never nested inside a form fieldset. `ConfigurationConfigurePanel` owns the sticky, focus-safe `13–16rem` `ConfigurationNavigation` section rail (`aria-label="Sections"`, visible restrained `Reviewed` indicator, tabular-nums progress, and `<= 60rem` wrapped chip collapse) beside `.configuration-configure-panel__main` containing the segmented `[data-layout-switcher]` (`SectionBySection` / `AllSections`, preserving `ActiveScrollspySectionId` back to `SectionBySection`) and `.configuration-configure-panel__fields`. No stacked bottom returning-user, import, profile, locale, or privacy accordions trail below the form; instead, an untouched-session dismissible onboarding cue (`[data-onboarding-cue]`) appears above Configure until the first edit or dismissal, and a quiet single-line `WorkspaceStatusFooter` (`[data-workspace-status-footer]`, linking to `Trust & Privacy`) closes the workbench. The topbar `LanguagePicker` (`[data-action="toggle-language-picker"]` -> `[data-locale]`) is the single canonical locale selector. Its English/Arabic resource pair
labels modes, the single reviewed algorithm, recipient selection, recovery limits
and value-free findings; declared paths and protocol identifiers use bidi isolation.

`ConfigurationEditor.Profiles.cs` captures the existing editor workspace identity,
operation generation, current form revision and consent epoch for
`BrowserProfileVaultController` / `ProfileWorkerClient`. The panel adds no editable answer tree or scheduler.
Every asynchronous action captures that authority; input changes, cancellation,
revocation and disposal reject stale results and close reachable output even while the `Profiles` utility panel is closed. The
worker remains the only retained-value owner. File acquisition is bounded to
4 MiB; passphrases use strict UTF-8 with the shared 1024-byte limit. Owned input
arrays clear on every exit, without claiming managed-string or runtime erasure.

`BrowserDefinitionRegistry` owns one history of family/revision/digest identities
shared by catalogue admission and profile candidates. Removed identities remain
reserved, conflicts reject before worker adoption, and the 4096-identity budget
counts current and removed identities together. A catalogue replacement first
checks its complete additions, so refusal does not partially change history.
An authenticated profile identity is not publisher authentication.

The shared `DefinitionFormModel.BrowserProfiles.cs` admits only the worker's
safe public projection and root-level retained-path metadata. It additionally
strips values protected by the current target. `ProfileBlocks` applies to common
editing and import/document gates. Confirmed replacement awaits
`RetireAsync(reference, path, token)` before releasing a retained path for
ordinary edits; captured form authority must still match. A rejected replacement
or Undo cannot restore worker custody. Explicit `InspectableEncrypted` disclosure
is the sole path from `InspectAsync` to ordinary consented answers, and only
after current adoption does the form retire the worker.

The persistent stripped-profile flag blocks ordinary preparation and its
complete-readiness claim, including after cancellation or revocation. Private
artifact download and sealing use worker composition with the current ordinary
snapshot. Inspection clears that flag after complete answer adoption. Comparison
files never become restore inputs, and the UI reports protected equality as
unavailable without an independently held key.

Saving retains only a safe `ProfileRecoveryReference`; verification opens the
operator-selected exact copy in a fresh worker with one portable recipient.
It cannot infer recovery from unauthenticated recipient labels or independently
establish that a person has preserved a backup. Profile output uses the dedicated
client's deliberate download route; comparison downloads reuse
`BrowserArtifactExport` and its synchronous invalidation.

The imported host module exposes `createBridge()`: each client owns its bridge
and worker independently, while the managed static admission gate still permits
one active profile operation. Recovery can therefore use fresh custody without
retiring an idle opened profile. Cancellation preserves the cancellation token
when worker termination rejects the transport promise. Profile inputs reuse
`form-control`; retained ordinary fields explain profile custody rather than
asking for consent that is already granted. Locked secret fields invoke the
cascaded `WorkspaceConsentAction` to open and focus `#secret-consent` without
mutating `SessionPrivacy`, and `#secret-consent-help` owns the single
password-manager custody notice so individual secret fields render only their
concise acquisition hint (`CredentialIssuerHelp`, `CredentialCreateHelp`, or
`CredentialExistingHelp`). `DefinitionFormModel.FocusEditor` expands any
collapsed `DefinitionFormSectionGroup` containing the target root field. None of
these UI affordances changes the shared form's edit or disclosure authority.

`BrowserProfileVaultTests` covers safe adoption, edit blocking, asynchronous
replacement fencing, mode-specific disclosure, revocation, stripped readiness,
public comparison and the shared history budget. The final focused cohort passes
48 cases, with eight ordinary-field privacy cases retained. Actual Release/AOT
root and configured-subpath workers opened profiles and composed restricted
output. Root observations additionally exercised two-recipient save, fresh
exact-copy recovery, explicit inspectable disclosure, revocation, active-KDF
cancellation, cached operation without the HTTP host, and update waiting.
Ten English/Arabic desktop/mobile frames were inspected; mobile comparison
words can wrap within a column. These scoped observations establish no physical
WebCrypto termination, memory erasure, AT/WCAG or cross-browser certification.
The [packaging evidence and limits](PACKAGING.md#independent-profile-worker-publication)
separate ordinary publication from unresolved assurance gates. Local execution
records are not distributed with the public source. See the
[public profile workflow](../public/browser.md#portable-saved-profiles).

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

Global application navigation (`NavMenu` inside `DockScope.Shell`'s `240–460px`, default `256px` Start dock) and active form section navigation (`ConfigurationNavigation` directly inside `ConfigurationConfigurePanel`) are structurally separate sibling `<nav>` landmarks (`Application navigation` vs `Sections`), eliminating `<nav>` nesting and keeping form sections visible and interactive whenever the global **Menu** collapses. `NavMenu` groups top workspace destinations (`.nav-menu__workspace`) above a bottom-pinned `Local workspace` custody card (`.nav-menu__footer`), scrolls safely on short screens (`overflow-y: auto`), and renders a visible **Close menu** control whenever `StartNav` degrades into `DockOverlayHost` (with Escape, backdrop click, and Close consistently restoring focus to `#nav-menu-toggle` via `MainLayout.OverlayCloseRequested`). Skip navigation focuses `#main-content` without changing the URL, so it cannot discard a live workspace through fragment navigation.

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

## Synthetic interaction evidence

### Captured output workflow and transfer review

`ConfigurationEditor.Publication` captures one
`DefinitionOutputExportContext` after successful preparation using the captured
input revision and complete eligible artifact set. Each per-file or aggregate
callback retains that exact context across interop. Existing form/view,
prepared-artifact membership and operation-generation checks run before
`RecordOutputExport`. Only acknowledged copy/download requests record the exact
successful IDs; reveal and preparation do not. Output invalidation clears the
held context synchronously before asynchronous private capability retirement.

`ConfigurationEditor.Workflow` owns the current value-free review descriptor.
The visible task selector and next-action controls resolve any pending
Developer document before asking the shared form for review. Confirmation
passes selected IDs and the captured review, never a freshly read substitute.
The parent callback checks both its rendered owner/view and review identity.
`RetireView` clears held UI review state on edits, replacement, locale/view
retirement and disposal. Shared generation/revision/custody checks remain the
final authority.

`ConfigurationNextActionsPanel` renders public metadata, selection IDs and
bounded public previews. Protected rows do not call the preview accessor.
It stores no answer values. Its polite region is mounted for multi-task
workspaces before export, while its visible related-task section requires
qualifying proposals. No related-task region exists for definitions without
multiple selector options. Review focus is requested only by explicit user
actions. Inline fields use the scoped suggestion-review callback and preserve
their existing exact action guards.

The shared implementation and accepted-state mechanisms remain in the
[Presentation contract](PRESENTATION_STATE.md#reviewed-value-transfer).
Browser downloads retain deliberate-request semantics and do not acquire native
protected-save guarantees.

`eng/configurator/MeasureInteractiveWork.cs` validates supplied synthetic timing
records; it neither drives the Browser nor authenticates their producer.
Its input binds independently retained fixture, payload and host SHA-256
identities. Unknown fields, duplicate cohorts, missing journeys, untrusted or
failed records, nonfinite timestamps and incomplete sample sequences reject.
Each journey includes both directions at 1x and 4x, a separate first-use sample,
five warmups and forty measured samples. All nine inherited operator journeys
remain required; descendant/composition cases can extend that set.

Feedback stays strictly below 50/200 milliseconds. Warmed 1x tasks may not exceed
50 milliseconds. Measured final-content p95 must stay below the declared journey
budget; this validator conservatively caps that declaration at 100/400
milliseconds. Supply any tighter inherited threshold explicitly. Completion
and physical settlement are separate intervals; a long complete parse is not
declared a paint-budget success. Aggregation retains raw samples, first use,
all outlier ordinals, maxima and nearest-rank p50/p95. A slow tail remains visible
even when its cohort's p95 passes.

Run `dotnet run --file eng/configurator/MeasureInteractiveWork.cs -- --self-test`
for independent positive/negative synthetic specifications. Validate an explicit
file with `--input FILE --fixture SHA256 --payload SHA256 --host SHA256`, in that
order; each digest is 64 lowercase hexadecimal characters. Exit 0 means the
supplied records pass, 1 rejects the input, 2 retains a valid failed timing
cohort, and 64 rejects arguments. Input is bounded to 16 MiB and JSON depth eight.
Matching hashes and a `trusted` flag do not establish actual trusted events,
detect fabricated records, or replace published AOT, device or AT qualification.

### Public collection fixtures

`eng/configurator/BuildInteractiveWorkFixtures.cs` creates five bounded public
synthetic inputs through the existing Core authoring/admission contract. Run
from the repository root:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/configurator/BuildInteractiveWorkFixtures.cs --configuration Release -- --self-test
dotnet run --file eng/configurator/BuildInteractiveWorkFixtures.cs --configuration Release -- --output "$PWD/.tmp/interactive-work-fixtures"
```

The destination must not exist. Each file is create-only; `manifest.json` is
written last as the completion record. A failed partial directory must not be
treated as a complete fixture set or reused as one. The manifest binds each
source, canonical definition, answer input and independent expected output by
SHA-256. English/Arabic titles are synthetic; all values are public.

| Fixture | Baseline work |
| --- | --- |
| `few-root-deep-tree` | One root entry, 137 answer nodes and 128 scalar leaves |
| `wide-nested-object` | One root entry, 514 answer nodes and 512 scalar leaves |
| `large-map-list` | Two root entries, 256 map entries and 256 list rows; 255–257 bounds allow add/remove/reverse |
| `many-active-corrections` | 256 distinct invalid-value findings, plus valid correction answers |
| `multi-artifact` | 128 scalar leaves and eight complete declared outputs |

Self-checks admit and compose the definitions, compare against independently
authored output specifications, verify changed answers affect output, exercise
collection add/remove/reverse and reject existing destinations. They establish
fixture behavior, not input dispatch, paint, timing or package equivalence.
These five cases extend the nine required journeys; they do not replace them.
Actual collection still requires trusted input, full raw cohorts, independently
bound payload/host identities and the existing stricter timing gates.

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
The shell uses a header/body/footer grid with a collapsible `256px` global navigation drawer (`NavMenu` in `DockScope.Shell`) and an independent sticky `13–16rem` form section rail (`ConfigurationNavigation`) inside `ConfigurationConfigurePanel` that collapses into a wrapped chip bar at `<= 60rem`; mobile full-screen section-index drill-down routing remains cleanly deferred to a follow-up pass. The main column has a shrinkable inline track and the
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
The bridge receives the explicit consent checkbox reference together with the
workspace and editor identities; it does not discover custody through ancestor
DOM queries. Synchronous retirement requires the in-process browser JS bridge.
Managed `_formatSource` bytes are cleared immediately on revocation,
replacement and disposal, before any held format operation returns.
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
from the existing shared progress and input diagnostics, binding optional-missing
counts directly from `WorkspaceProgressSummary.OptionalMissing` without
materializing all discovery entries on frame 1. It does not compose artifacts or
collect another answer tree. Its ready and draft actions invoke the editor's
cooperative `DefinitionPreparationSession` preparation/adoption operation,
yielding via `nextTask` and re-verifying captured form identity, generation,
input revision, privacy consent and disposal across every yield. The inner
effects list remains value-free, indexes only the active page, and for forms
exceeding 128 validation nodes defers effects hydration by one `nextFrame` fenced
by captured form, input revision and disposal so Review opens promptly without
stale updates. Required/optional/secret state, origin labels and recovery text
use the current English or Arabic resource set.

`ConfigurationField` and `ConfigurationConfigurePanel` consume precomputed
`DefinitionNode.HasOptionalDescendant`, `DefinitionNode.RequiredSet` and
`DefinitionFormProjection.FieldStatesById`, caching child path arrays per field
path rather than scanning subtrees or allocating per-render hash sets and
dictionaries on deep nested trees.

`ConfigurationFiles` owns each mounted file's preview element and accessible
actions, not its bytes or workspace. The editor retains preparation, export and
post-await eligibility checks. Per-file status keeps failed Copy recovery beside
an available Download; Reveal/Hide still uses the existing generation-fenced
export service. The localized privacy notice derives from that same `Revealed`
parameter, so it describes the current preview rather than always claiming the
output is hidden. Leaving Files drops component references and preview capability
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

`ConfigurationEditor` queues one captured definition, locale, entry and generation
request before constructing its form. Its null-form branch renders a localized
busy workspace landmark without controls, progress counts or a readiness claim.
After render, the existing module's `paintWorkspace` checks the connected owner
and generation before yielding through `nextFrame`. The editor checks current
startup authority after module import and frame completion, then constructs and
registers the form. Replacement, discard and disposal synchronously retire
obsolete startup authority; public begin/import calls cannot bypass it. Discard
reconstruction uses the same boundary and preserves the explicit restart action.
If module import or the initial paint fails with a JS interop error, the editor
constructs only the empty form, exposes the preparation-failed gate and retry,
and drops a faulted import task so disposal cannot rethrow it. No workspace
lease or answer input starts before the retry.
Import comparison treats JS rendering failures as value-safe read failures: it
clears only the pending comparison, keeps accepted answers, and exposes the
import recovery action. A faulted module-import task is released for retry.
This is a paint opportunity, not evidence of the feedback or uninterrupted-work
budgets; form initialization and later work remain subject to actual timing.

`ConfigurationSectionFields` owns one descendant admission plan for a section
or literal root. Six initial nodes and subsequent ten-node increments apply
across the entire current topology, including container, readonly and denied
shells. Mounted controls retain identity. `ConfigurationMountPlan` borrows
current shared-form facts and values synchronously, retaining only typed
coordinates, logical identities, children and admission; it stores no answers,
JSON values or render fragments.

Declared object children resolve localized titles from their own current
editor presentation. The parent does not reconstruct that full presentation
for a title on every admission render. Explicit root and dynamic-key titles
retain their existing ownership.

Scalar field boxes use native `content-visibility: auto` with an auto intrinsic
block estimate from existing control/target tokens. Object/list group boxes
remain normal flow. This leaves every admitted control in the DOM; the browser
owns offscreen layout and any remembered box size. No application viewport
registry, value cache or scheduler participates. DOM completion is not a claim
that every offscreen box has been painted. Focus, find, correction reveal and
portal overlays remain required real-browser acceptance checks.

Initial discovery advances ordinal routes within that owner, resolving and
admitting six nodes initially and at most six after each boundary. Child
coordinates are captured only during synchronous borrows. Changed immutable
paths and incremental counts avoid rescanning the whole growing tree for
each initial increment; coordinate enumeration and array copying still cost
work and require actual timing. `data-discovery-state` is `pending` until
discovery finishes, while `data-pending-fields` is `unknown`; afterwards the
count is exact. Unknown never means zero. Input or topology-affecting view
changes clear the frontier before full current-state reconciliation, preserving
admitted survivors without an extra initial allowance merely because discovery
becomes complete.

Correction focus prioritizes only its ancestor chain within the same allowance.
The first batch yields through existing `nextFrame`; later batches use existing
`nextTask`, avoiding a forced display frame per increment. Pending count and
`aria-busy` describe DOM completion, not answer readiness. Completion stays
pending through the final render and focus-visibility callback. The module
checks the connected owner, mount generation and zero pending nodes, then
reveals its currently focused descendant without restoring an earlier target.
This prevents later insertions from leaving an early focused control offscreen
while respecting a focus move elsewhere.

Mount generation, form identity, input revision and cancellation reject obsolete
continuations after edits, view changes, replacement or disposal, including
after settlement interop returns. Fields receive the topology's explicit
`MountRevision`; cascading updates cannot render old row coordinates against
newer answers. Supersession during initial discovery does not earn another
initial batch through the newly completed topology. Secret denial and
prepared-output retirement remain synchronous.
`js/configuration-rendering.js` carries only local null-message task signals and
DOM focus operations, never answers or artifacts. It remains a required public
offline asset. Bounded admission alone supplies no latency or AT qualification.

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
resources for result controls, facets and status. Global workspace links precede the bottom-pinned custody card inside the collapsible `NavMenu` drawer while multi-section forms render `ConfigurationNavigation` persistently inside `ConfigurationConfigurePanel`; local previous/next/review actions reuse the same
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
Masked scalar controls inherit declared sensitive or secret ancestors and active
branches (public containers mask their own aggregate value without masking their
public siblings); secret consent still uses the form's edit permission, allowing
public siblings to remain editable. Authored titles are preserved. Mechanical
identifier formatting is used only when title equals key, with the exact key
available separately. Long labels and metadata paths wrap within the field,
including collapsed compound children in narrow RTL layouts; they must not widen
the viewport. List rows obtain their immutable item metadata from the shared
admitted index, not a new JSON serialization on each render. The renderer
supplies the row title, required state and inherited secret scope separately
while obtaining masking from shared `GetFieldValidation` facts. Unchanged
parameter updates may skip a field tree only while the form, input revision,
projection and all
rendering/focus parameters remain current. Parameter delivery still runs; a new
input revision always refreshes the revision-fenced callbacks.
Field correction queries read the reason index in the shared current-revision
`DefinitionFormFacts`. That owner resolves diagnostic coordinates once, including
typed map-entry locations, rather than resolving every diagnostic again for each
rendered field. Input changes retire the index with the other facts; this does
not bypass field masking, edit permission or callback revision checks.
Within one synchronous field render or parameter update, the component reuses a
local correction-facts result for editability, masking, radio selection and action
presentation. The result is not retained across calls; action callbacks still
query current eligibility after checking their owner, revision and generation.
Help visibility uses the same local masking decision. Control identifiers use
the admitted mount node's existing editor identity, which the section owner
resolves and reconciles against the current form, rather than resolving it
again inside each field render.
Field queries likewise reuse that node's immutable literal `Location` instead
of reconstructing a `DefinitionPath` in every getter. The section still refreshes
moved row coordinates, and the field still requires the current mount/input
revision before processing parameters or rendering. Child focus discovery
continues to translate coordinates that do not yet have their own mounted field.
`AppTextField` applies every incoming parameter but skips unchanged
parameter-triggered inner rendering. The enclosing field still emits fresh
input/change callbacks for the current revision and synchronously removes
retired private controls; the gate does not extend an old delegate's authority.
Value, identity, masking, multiline/read-only, input mode, disabled/required,
error state/text and attribute contents all participate. Reused mutable attribute
dictionaries and non-string/non-boolean attribute objects retain ordinary
rendering. Only a temporary suppression flag is stored, with no second answer,
attribute snapshot or revision lease; internal input events keep their own renders.

The independent detail selector delegates to the shared disclosure override.
Comfortable/Compact changes session-local CSS tokens only. Named field-action
menus retain explicit omission, including container descendants.
Their shared `CanOmit` fact excludes the root and array rows.
`ConfigurationRoot` hosts the existing recursive field at the empty literal
path for admitted root maps and variants. Typed literal paths govern editing,
secret actions and correction focus without fabricating a property wrapper.
Branch replacement retires the old mounted editor identity, so returning to
the same branch does not revive its callbacks or payload.
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
and correction buttons before output decisions. It exposes **Origin** filter chips
(`All`, `Explicit`, `Generated`, `Defaulted`, `Imported`) and `[data-action="review-focus-row"]`
navigation buttons to jump back to Configure for any inspected setting. Files reuses the same diagnostic
component and revision-bound correction callbacks after failed preparation;
answers and input drafts survive while previous export eligibility is retired.
Review lists the current output selector's artifact set, retaining the declared
set for definitions without output selection, and surfaces `Form.Projection.ContextSummary`
(`[data-review-scope]`) when deployment recipes/scopes apply. Origins and bindings are secondary
details; Review and Files always render their dedicated top-level stages (`WorkspaceStage.Review` and `WorkspaceStage.Files`) without `CompactPresentation` overrides or duplicate inline panels. The effects region mounts at Review and pages 50 entries
at a time. Its default selection includes active required work, corrections,
deferred secrets and supplied/defaulted entries; separate controls inspect
omitted optional and inactive metadata without enabling it. Diagnostic correction
actions are independent of effect paging. A correction selects the section and focuses its field
or the diagnosed collection row. Explicit field focus permits native scrolling,
so a correction below a long results list becomes visible rather than merely
receiving off-screen focus. Secret recovery also covers required secret
descendants inside optional supplied objects. Draft and complete preparation
remain distinct; filenames and completion locations (`[data-deferred-secret-summary]`,
`[data-completion-field]`, `[data-completion-output]`) come from shared artifacts.
Reveal, copy and download remain deliberate operations owned by
`BrowserArtifactExport` with per-file retry (`[data-action="retry-file-export"]`),
while `ConfigurationFilesPanel` renders `data-files-state` (`complete`, `deferred`, `blocked`,
or `invalidated`), `[data-files-invalidated]`, `[data-custody-disclosure]`, and
`[data-download-custody-note]` explaining browser-local transient custody and download limits.

`ConfigurationConfigurePanel` implements both `ConfigureLayoutMode.SectionBySection` and `ConfigureLayoutMode.AllSections` (`ConfigurationSettingRow` for scalars and lazy `ConfigurationField` for complex containers without any legacy `CompactPresentation` branch) through the shared `ConfigurationSectionFields` mount boundary and `ResolveVisibleSections` visibility helper: switching layouts transitions `DefinitionFormModel` between `(ConfigureLayoutMode.SectionBySection, DefinitionEditorVisibilityPolicy.Authored)` and `(ConfigureLayoutMode.AllSections, DefinitionEditorVisibilityPolicy.Continuous)` while preserving `SelectedExperienceId`, `ActiveScrollspySectionId`, `WorkspaceStage.Configure`, and unparsed input drafts across round-trips. In `AllSections` mode it renders all applicable sections in a continuous flow with `[center ± 1]` section windowing (keeping `<section>` landmark headings static across deferred windows while freeing off-window field components), row virtualization via Blazor `<Virtualize ItemSize="72" OverscanCount="2">` above `16` items (`DirectSliceCount = 4`, height-reserved virtual tracks to prevent off-screen sections collapsing into the initial viewport, plus an in-flow `data-pinned-focus-slot` for deep-focused targets), persistent sticky `ConfigurationNavigation` rail (`position: sticky` below `--isl-sticky-top-offset` updated by `.nav-menu__header.navbar` `ResizeObserver`, alignment-safe `observeScrollspy` with bottom-of-page fallback, container-only `syncActiveNavScroll`, and per-section `SetSectionReviewed` toggles surfacing a restrained visible `Reviewed` badge), omits unexpanded groups and non-matching filtered fields from the DOM without `hidden`/`inert` wrappers, renders `[data-empty-matches-state]` with a `[data-action="reset-discovery-filters"]` CTA using `DiscoveryZeroMatchSummary`, and uses `ProgressSummary.IsReadyForExport` so optional validation errors never produce a false green completion state. `ConfigurationSettingRow` marks requirement via minority `Required` pills (and section-header `[data-section-requirement="all-required"]` indicators when every visible section field is required) alongside a 3 px `inline-start` semantic status stripe and `sr-only` `[data-compact-status]` text. `ConfigurationErrorSummary` surfaces invalid/incomplete items when export validation fails or recovery links trigger, while `ConfigurationEditor` routes `F7` (blocking validation error), `F8` (`value-required` / next actionable field), `F9` (next unreviewed section), and `/` (`WorkspaceSearch` outside editable inputs).

Supporting workspace surfaces cover the Event-aligned `DockLayoutHost` /
`DockSideHost` / `DockOverlayHost` docking architecture (`DockLayoutState` with
priority-ordered cross-scope allocation via `ComputeOverlayPanelIds()`, atomic `OpenExclusive(DockPanelId, DockLayoutChangeReason)` for zero-flicker single-active panel switching in one notification, and `SetMinimumContentWidth`, backed by `DockLayoutHost` parameter-change tracking (`_lastPublishedScope` / `_lastPublishedMinimumContentWidth`), `IAsyncDisposable`-only lifecycle on `DockLayoutHost`/`DockOverlayHost`, and `Program.cs` `ResizeOptions.NotifyOnBreakpointOnly = false` for non-breakpoint resize delivery; resizable Start rail `240–460px` default `256px` and single-active right Workspace Utility Workbench driven by `WorkspaceUtilityDescriptor` / `WorkspaceUtilities.All` and `WorkspaceUtilityDockCoordinator` with five `PersistState: false` descriptors — `Inspector (F2)` (with default **This workspace** overview and single-layer modal dialog ownership in `DockOverlayHost`), `Document (JSON)` (bound via always-mounted `CustodyAnchor` on `<section data-workspace-lifetime data-secret-consent>` plus capture-phase revocation in `configuration-developer.js` and synchronous `RetireDeveloper()` before `SetSecretEntryEnabled(false)`), `Import` (`ConfigurationImportDockPanel`), `Profiles` (`ConfigurationVaultPanel` over `BrowserProfileVaultController`), and `Trust & Privacy` (`ConfigurationTrustPrivacyPanel` routing callbacks through instance methods so `ShouldRender()` skips never leave stale closures), each `300–620px` default `360px`). `.configuration-editor__workbench` pairs `<DockLayoutHost Scope="DockScope.Workspace">` across `Configure`, `Review`, and `Files` with the outermost sticky `44px` trailing `WorkspaceUtilityRail` (`role="toolbar" aria-orientation="vertical"`, roving `tabindex="0"/"-1"` calling `.FocusAsync(preventScroll: true)`, `aria-expanded` + `aria-controls` with zero `aria-pressed`, hidden at `<= 60rem` in favor of the narrow `WorkspaceHeader` `MudMenu` **Tools** button), with density-aware `WorkspaceMinimumContentWidth` adding `+44px` whenever `HasDesktopUtilityRail` is active (`834px` Comfortable / `850px` Spacious / `818px` Compact / `810px` Dense for multi-section `Configure`, `464px` for single-section `Configure`, `Review`, and `Files` above `960px`, and `420px` at `<= 960px` or during in-place Settings),
auto-overlay below `760px` or constrained widths, synchronous overlay unmount on close,
RTL-mirrored `DockResizeHandle`, roving-focus `DockTabStrip`, scoped `WeakRef` focus restoration in `AccessibilityFocusService` / `accessibility.js` (`workspace-utility` and `dock-overlay-*` scopes with `preventScroll: true` and overlay/end-dock double-save protection), `BrowserWorkspaceCommand` surface commands in `ConfigurationDiscovery` (`OpenDocumentEditor`, `Import`, `Profiles`, `Trust & Privacy`), and bounded `<= 1,024` B
`configurator.dock.v1` layout persistence via `LocalStorageDockLayoutPersistence`),
collapsible `[data-configure-ribbon]` inside `#workspace-advanced-options`, streamlined 2-line `ConfigurationNavigation` items (wrapping into a compact chip bar at `<= 60rem`; mobile full-screen section drill-down remains deferred),
`ThemeQuickSwitcher` and in-place `SettingsWorkspace` (rendering all 8 Event presets, 5 modes,
and Custom `Natural + Brand` HSL builder with top-left **← Return to configuration** over
`hidden`/`inert` `<main>` so active answers and secret consent survive unchanged),
2-per-row compact landscape `Catalogue` project cards with offline PNG logos and
`StoreProjectDetailsDialog` (`ⓘ` modal displaying `CC-BY-4.0` attribution, outputs, and digests),
`WorkspaceSearch` / `WorkspaceSearchState` (WAI-ARIA 1.2 `role="combobox"` with `aria-controls`,
`aria-expanded`, `aria-activedescendant`, `role="listbox"`/`role="option"`, `ArrowDown`/`ArrowUp`/`Enter`/`Escape`,
and `<bdi dir="ltr">` wrapping around technical identifiers), `ConfigurationSecretGeneratorDialog`
(modal recipe generator keeping revealed plaintext passwords outside prepared output),
`KeyboardShortcutsDialog` (`?` reference dialog documenting `Ctrl+K`/`/`, `F7`/`F8`/`F9`, `Ctrl+Z`,
`Ctrl+Shift+Z`, `Ctrl+Shift+K`, `F2`, and `?`), `ISnackbar` Undo/Redo notifications
(`Ctrl+Z`/`Ctrl+Shift+Z` via `DefinitionFormUndoHistory` with synchronous secret purge on
consent revocation), collapsible groups auto-expanding on validation errors, and the
read-only `/explore/{FamilyId}` route (`Pages.Explore`) consuming
`BrowserDefinitionRegistry` and `IDefinitionStoreProvider` with zero `DefinitionFormModel`
or `Configurator.Artifacts` dependencies. Automated bUnit component suites and compiled `Configurator.Architecture.Tests` verify local contracts and custody boundaries; published Release/AOT browser execution (`Configurator.BrowserQualification`) and human assistive-technology (`NVDA`/`Orca`/`VoiceOver`) testing remain separate qualification gates.

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
