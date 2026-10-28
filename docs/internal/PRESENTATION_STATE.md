# Definition presentation state

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented shared state; rendered client qualification is separate
> **Owner:** Presentation
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Presentation/Presentation/`, `src/Configurator.Core/Definitions/`

## Recursive v2 editing and localized display

`DefinitionFormIndex` indexes declared properties, items, typed map values and
variant schemas once. It contains public schema paths, not dynamic entry names.
`TryMetadataNode` traverses actual supplied rows and delegates branch selection
to `DefinitionValidator.SelectVariant`; it does not interpret applicability.
Core node states carry the same revision-bound typed correction locations as
diagnostics. `TryCorrectionPath` resolves private map ordinals locally against
the current effective snapshot. `FocusCorrection` rejects another input revision.
Public draft diagnostics convert local draft paths to typed safe coordinates;
private map keys never become serialized correction names.
`TryResolveCorrection` is an explicit private-coordinate accessor for a local
renderer; it applies the same revision fence and is not a diagnostic or transport
projection. `GetVariantTitle` localizes only a declared branch's public title or
discriminator fallback, never its collected answers.

`SelectChoice` uses the declared enum ordinal and exact JSON value.
`ApplySuggestion`, `SetNull`, `ResetValue` and `SelectVariant` are deliberate
edits under the existing mutation gate. A variant switch supplies only the chosen
discriminator and removes the old branch payload. Core alone initializes missing
eligible children. Requiredness never creates absent containers. Reset removes
the override and omission and advances one input revision, rather than
materializing initialized values into the supplied snapshot.

An optional consumer capability set is checked before the form creates answer
editors. Adapters must advertise their actual editing support there; generic
shared support does not qualify a renderer. Required/error paths remain
reachable independently of optional journey detail.

`DefinitionLocalization` retains only admitted public translation metadata.
`SelectLocale` publishes view changes without input changes or preparation
invalidation. `GetEditorPresentation`, `DisplaySections`, `DisplayExperiences`
and lazy discovery projections resolve public labels/help/units with base-text
fallback. Existing lazy projections capture their locale, so later language
selection cannot change a previously published view. Choice values, schema
identity, numeric values and artifact composition remain unchanged. Guidance
links remain static admitted metadata; these APIs fetch or execute nothing.

Consent checks and revocation traverse declared properties, typed maps and the
Core-selected variant. Revocation mutates the existing transient JSON tree
without reparenting surviving array nodes; public nulls and safe siblings remain.
No new answer store, evaluator, operation scheduler or transport is introduced.
Client rendering, keyboard/accessibility and actual-host qualification remain
separate from this shared-state contract.

Native clients and Browser use the same admitted definition and answer snapshots.
Local compact YAML/JSON sources pass through `SetupDefinitionSourceCodec` before
the client constructs `DefinitionFormModel`. Syntax is explicit and canonical
identity is shared with expanded runtime input. Source bytes and comments do
not become answer snapshots or prepared output; existing load generations fence
replacement/cancellation. Browser remote retrieval remains runtime JSON only.
`DefinitionFormModel` owns typed editing and view state. Authored experiences,
section expansion and discovery publish `ViewChanged`, not `InputChanged`;
they preserve answer revision, consent and current preparation. Definitions
without presentation metadata expose one neutral Configure experience.

`DefinitionPresentationOperation.Adopt` copies an already admitted Core artifact
into a disposable `DefinitionPreparedAdoption`, including only that artifact's
draft completion locations. It retains no answers and performs no composition.
The caller owns and disposes this staged operation even if a workspace never
starts it. Execution transfers its outcome once into the existing workspace
owner; pre-start cancellation/disposal clears staged bytes, while disposal after
transfer cannot clear the workspace's current output. Existing generations retire
cancelled, replaced, deactivated and late outcomes.

Browser prepares one complete/draft bundle, adopts each output into a local staged
list and publishes that list only after every current-generation adoption succeeds.
Rejection or invalidation disposes all staged workspaces/exports and the Core
bundle; no earlier output becomes available after a later failure. Bundle-level
draft withholding diagnostics remain visible even when a particular adopted
artifact has no deferred secret. This reuses the established outcome owner rather
than introducing another preparation store.

## Form state ownership and API migration

`DefinitionEditingState` holds the existing root inputs, privacy mode, input
revision and stable row identities. `DefinitionViewState` holds experience,
disclosure, expansion, promotion and discovery bookkeeping. Neither collaborator
has a lock, sends notifications or duplicates answers. `DefinitionFormModel`
retains the sole mutation gate and notification transaction, together with its
index, facts and projection lifetimes. View-only changes preserve input revision,
drafts, origins and row identity; observers run after the gate releases.

The former workspace `PublicInput`, `SetPublicInput` and `MaxPublicInputLength`
scaffold is removed. Embedders edit the real form through literal-path APIs and
immutable snapshots rather than maintaining a second workspace string.
`SetupAccessibilityStatus`, `AccessibilityStatus`, `ReceivedCompletionCount`
and the counter-only `Receive` implementation are also removed. Observe current
workspace status and operation messages, and derive accessible host feedback from
those real states. Rebuild managed consumers; no compatibility aliases are kept.

Behavior tests now use typed `ISetupPresentationOperation` fixtures, generation
allocators and message recipients. Their operation output is captured before a
held completion is released, so a later fixture edit cannot change the result
already owned by an invocation. Reflection remains for compiled API-shape checks
and passive owned-buffer inspection, not as a behavioral adapter.

`SetDisclosureOverride` selects Essential, Guided (the Standard UI detail level)
or Advanced independently of the selected authored experience. A null override
follows that experience's initial disclosure. Explicit selection survives
experience changes and resets manual expansion overrides, but never edits
answers, advances the input revision or changes secret consent. Visibility and
`IsEditorExpanded` use the same effective disclosure, so the expansion action
cannot disagree with the fields displayed. Required and invalid descendants
remain reachable. Authored experience and section descriptions are preserved
in the shared projection; clients need no new definition vocabulary or
answer-bearing view model.

Each form owns one metadata-only `DefinitionFormIndex` for its pinned definition:
literal schema children, root positions, section membership and experience order,
artifact bindings and activation-controller references. List-item schema metadata
is indexed once, but actual row presence and logical row tokens remain workspace
state. This index contains no answers, origins, activity or readiness.
`GetFieldEditor` retrieves the existing owned root editor and schema through that literal-ID
index. Browser field mounting uses it rather than repeatedly scanning roots and
decoding definition IDs from JSON. Cached display metadata and frozen binding
references are shared by internal discovery entries; the public entry constructor
still snapshots caller-owned collections.
`GetListItemMetadata` exposes the index's admitted item shape without serializing
row answers. Renderers retain responsibility for the enclosing list's inherited
sensitivity, row title and required state; the normalized item shape is not a
new sensitivity authority. Row presence and invalid-path checks still use the
same form, and the accessor does not edit answers or advance their revision.
`DefinitionFormFacts` owns the single current input revision's validation, active
roots, diagnostics, correction/required paths, origins, progress and discovery
entries. It retains no second answer tree. Read-only origin, metadata and identity
traversal uses the already-owned immutable `JsonElement`; an invalid root still
denies access to its last valid collection rows. Mutable JSON parsing is confined
to collection commands that actually need a working tree.

Input intent retires facts, ordered entries, discovery and the visible projection.
Experience changes retire ordered entries and discovery, not answer facts.
Query/facet changes replace discovery and publish a new view projection while
reusing its immutable visibility, progress and relevance. Disclosure changes
derive new visibility and retain discovery. There is one current discovery result, not query
history or a global cache. Each current ordering derives `SectionOrder` from the
selected experience, so reused answer facts cannot freeze an earlier ranking.
These lifetimes preserve the input revision and prepared output on view changes.
Section progress uses indexed root membership, and experience-order copies share
their already-owned immutable metadata collections. Discovery matching/facets
are evaluated on demand by the one current discovery cell. Its factory captures
only immutable entry and selection snapshots, never the live form, validation
answer map or editor drafts; an older projection cannot derive newer answers
when first read after an edit or disposal.
Entry materialization, ordering and controller frontiers are also deferred:
mutation captures value-free node facts and origins, while static factory
methods capture only those snapshots and admitted metadata. Closed discovery
and Review therefore do not allocate their full entry graphs during typing.
`Projection.RelevantEntries` supplies the shared unpaged Review policy: active
required, attention, deferred, supplied or defaulted entries and their literal-path
ancestors. `Projection.Entries` retains the complete inspectable set, including
omitted optional and inactive metadata. Clients page these selections without
re-evaluating activation or suppressing independent correction diagnostics.

The outermost mutation settles nested draft validity, origins and row changes
before deriving correction promotion and releasing the gate. Property/input/view
observers therefore see coherent current facts and sticky correction state even
if nobody rendered the invalid editor before it was corrected. Visibility reads
do not promote corrections. Deliberate collapse or experience navigation clears
old promotion; currently invalid/missing work remains reachable. Disposal retires
index, facts, ordering, discovery and logical identity references without deriving
fresh facts during notifications. Retiring managed JSON/string references is not
physical memory zeroization; owned prepared-output bytes retain their separate
actual zeroization contract.

`DefinitionPresentationOperation` captures the
definition, artifact ID and input revision for a workspace execution.
`DefinitionFormModel.ImportAsync` captures the current immutable answer snapshot
before reading an existing file. After Core assesses its declared inverse, the
form applies cloned imported typed values as one revision only if the captured revision
still matches and cancellation/disposal have not invalidated the operation.
Core returns immutable `ProvidedFields` metadata from the actual parsed mappings,
not a value comparison. Only those roots are applied and assigned imported
provenance; unmentioned drafts, row identities and origins survive. Presentation
captures pristine default roots under the same gate as the original snapshot
and passes replacement permission to Core. Explicit-equal-default edits and
invalid drafts do not qualify; Core still checks the full merged answer budget.
Browser mounts the positive effects list only at the Review workflow location.
It consumes value-free projection entries, origin metadata and declared output
bindings; it never reads answers for review markup. Change uses literal typed
paths and shared focus promotion; returning refreshes validation without an
answer revision or automatic consent.
Known import failures retain their typed Core diagnostic through the existing
operation generation fence. Consent rejection identifies an admitted root and
offers only checkbox focus; inactive-field recovery uses shared active
controller metadata. Other recovery reuses literal correction focus. The editor
clears this recovery capability with every input/output invalidation, import
format change or workspace replacement, so an old rejected file cannot direct
a new workspace operation.
Manual edits use the same form gate, so a late read cannot overwrite them.
Field/property and input-change notifications run after the outermost form
mutation releases that gate. Nested collection edits, imports and consent
revocation first establish the complete revision, then notify observers;
reentrant observers cannot make a partially applied import restore cleared
answers. A failing subscriber does not stop the remaining notification batch
or skip private-state cleanup; notification failures remain observable to the
calling boundary.

`ClearPrivateState()` clears every root requiring masking and its input
diagnostics in one revision, preserving public roots. Public siblings inside
a protected root follow the conservative whole-root clear policy. The method
returns `true` when it publishes that revision, including when protected roots
are already empty: the revision fences a pending import that could otherwise
restore private values. It returns `false` only for definitions without any
protected roots. A client with separate prepared state must still invalidate
that state in the `false` case. Browser consent revocation remains a distinct
secret-leaf clear that preserves nonsecret siblings.
The shared projection keeps active missing obligations and invalid editors
reachable. Editing a missing/invalid path promotes its literal ancestors before
the edit, while new errors and invalid drafts promote at mutation settlement.
An unrelated edit does not make every missing obligation sticky. Corrected editors stay open until deliberate
collapse or experience navigation; optional secrets and consequences do not
alone force disclosure. `FocusEditor` clears obstructing discovery by default
and expands the exact path without granting consent. An explicit
`preserveDiscovery: true` retains the shared query/facets while promoting the
destination; this lets an overlay reopen with its previous search selection.
It does not create another query owner or search answer values. `SupplyObject`
and `SupplyList` express deliberate container presence through the same bounded
snapshot and secret-consent gates. An empty object `{}` or list `[]` is distinct
from an absent answer; supplying an existing container preserves its contents.
Clearing an absent nested child does not materialize its ancestors or advance
the input revision. Clearing a supplied container removes it and reevaluates
requiredness. Typed presence is owned independently of display text.
`DefinitionScalarEditing` converts scalar drafts without target semantics:
explicit empty strings and admitted empty enum choices remain values, whitespace
in strings remains literal, and rejected conversions retain invalid draft state.
Explicit omission removes an answer path without clearing its siblings. Core's
declared-default fallback remains effective for absent answers; omission does
not override that definition policy. Scalar edit
intent, parsing, validity and revision settle under the same mutation gate.
Deliberate invalid, rejected or same-value edits advance one revision and fence
pending imports even if accepted JSON does not change. Programmatic refresh,
untouched navigation and abandoned append slots do not materialize answers or
advance the revision. Browser and native selection controls distinguish an
empty enum choice from omission rather than sharing an empty selection identity.
Browser numeric controls retain raw text with numeric/decimal keyboard hints;
native HTML number sanitization must not turn an incomplete token into absence.
Preparation rejects those invalid drafts through the same shared conversion.
Nested invalid text is retained by the form, not only by a mounted component.
`ReadDraftText` applies the current custody fence; revocation clears denied
drafts synchronously. Nonexistent row paths cannot admit drafts. Stable
workspace row tokens survive sibling deletion, with no answer-derived identity.
Defaulted, imported and explicit origins follow successful mutations rather
than value equality; invalid text does not acquire successful provenance.
Core diagnostics retain `FieldId` as the root and immutable `RelativePath`
segments for children/rows. Dots in admitted identifiers are literal; never
parse semantic paths from display separators.
`DefinitionFormProgress` counts active required leaves and independent list
cardinality obligations without double-counting required objects. Explicit
false/zero count as answers; zero obligations has no percentage. Optional invalid
drafts still block preparation, inactive drafts do not, and deferred secrets
never count as ready.
`DefinitionDiscovery` indexes only admitted IDs, literal metadata paths, titles,
section labels, guidance/search terms and declared artifact/binding keys. Literal
projection values and answer/draft contents never enter the index. Query tokens
use ordinal case-insensitive AND matching, bounded to 256 characters/16 tokens.
Exact binding/field keys precede exact titles, prefixes, title tokens, terms
and help; ties use section order and literal path segments. Facets OR within a
category and AND across categories; counts remove only their own selection.
Inactive entries require deliberate inclusion and cannot activate an answer.
Selection/reset operations are transient view mutations. Clearing filters keeps
the query; clearing search keeps filters; correction clears both unless its
caller explicitly preserves discovery.
`ConfigurationDiscovery` renders that shared snapshot inside a deliberate local
disclosure. Query/facet controls and removable selections call only the form's
view-state APIs; result navigation delegates typed diagnostics to the editor's
existing correction/focus route. Query updates do not move keyboard focus.
`ActiveControllerFields` is copied public metadata for inactive entries: the
projection follows admitted activation operands through inactive dependencies
until current shared field facts identify active controlling roots. It does
not compare answers or evaluate a second Browser activation model. Controller
navigation promotes an editor but cannot enable a feature or grant consent.

`SetEditorExpanded` owns local optional disclosure by semantic editor identity,
including stable row tokens. Collapse preserves provided values and invalid text,
keeps required/essential/error paths reachable and clears only deliberate
correction expansion beneath that editor. Section expansion resets local
overrides only for that section; restoring all defaults or selecting an
experience resets the workspace's disclosure policy. These remain `ViewChanged`
mutations, never input revisions. `SectionProgress` is an immutable map computed
by the same obligation counter as overall progress. Deferred counts include
eligible missing secret leaves, not missing required containers.
Native bundle clients compose a selected set from one captured snapshot through
`DefinitionBundleComposer` and hand the immutable result to the protected
bundle publisher; per-file publication can have a written prefix after a later
failure.

`DefinitionSessionPrivacy` distinguishes native complete custody from
`BrowserNoSecrets` and explicit `BrowserSecretsEnabled` custody. Shared form
editing/import gates recursively inspect declared object/list properties, so a
public parent label or import cannot bypass secret denial. Revocation removes
secret leaves while preserving ordinary answers, increments the input revision
and invalidates pending/prepared output. New browser workspaces always begin
without secret consent; no policy choice persists across navigation.
The Browser editor establishes denial and secret-leaf clearing before awaiting
cleanup. It synchronously detaches and retires every prior workspace/export
owner before awaiting any old retirement. Cleanup operates only on captured
old resources; delayed or failed cleanup cannot restore denied secrets or erase
replacement output. Cleanup faults remain observable through a stable
value-free diagnostic. This cannot revoke earlier external clipboard/file copies.

Typed object-list edits add, modify or remove declared rows in one immutable
snapshot revision. Native Desktop and Terminal controls use this same form
seam rather than inventing tenant semantics. Definition admission and Core
validation enforce item bounds/structure/uniqueness; stale operations cannot
restore a removed row. Browser exports are deliberately local copy/download
through the adapter, with no dependency on native protected writers.

Desktop field viewmodels project bindings and focus only. Their scalar and
container/list actions invoke the shared literal-path commands; no descendant
serialization or invalid-root sentinel replaces the form's accepted values,
retained drafts or origins. Shared diagnostics mark the affected leaf and its
containers, not healthy siblings. The workspace-owned field factory performs
one refresh pass from a current root-state lookup, rather than independent
root subscriptions plus a workspace pass. List bindings reconcile shared
logical row tokens so surviving controls and correction drafts retain their
identity. Explicit Supply and Clear preserve container presence versus absence;
navigation and projection refresh are not answer edits. See the
[Desktop adapter](DESKTOP_CLIENT.md#editing-and-presentation-flow) for native
action resources and the separate surface-qualification boundary.

The existing session/workspace model allocates generations, invalidates old
work on edit, deactivation, cancellation and disposal, and rejects late results.
Published `DefinitionPreparationMetadata` contains identifiers, digests and
value-safe diagnostic codes but no artifact bytes. `SetupPresentationOutcome`
copies operation bytes into owned memory. The workspace clears that memory on
replacement, deactivation, termination and disposal; late rejected outcomes
are cleared before discard notification. Cancellation after a settled result
intentionally retains that result until its next invalidation or replacement.

Synchronous and awaited worker faults, including unrequested cancellation,
atomically match operation identity and generation before clearing private
output and busy state. `Faulted` is a value-safe invalidation reason; the
awaiting caller retains the original exception. If notification or retirement
also fails, the caller receives an aggregate preserving the original failure.
An old fault cannot invalidate a replacement generation. Accepted outcomes
retain matching identity through notification delivery, so
a property/message observer failure clears that outcome without clearing a
reentrant replacement or cancelling its pending worker. Invalidation, changed
properties and command eligibility all receive their post-lock notification
batch even when another notification fails; failures remain owned by the caller.
Workspace-owned operation, result and public-input properties are read-only snapshots; callers
use `SetPublicInput` and lifecycle methods instead of assigning those observable
property setters. Notifications contain only
changed properties and run after coherent state transitions release the gate.
Reentrant callbacks may start new work without a remaining old-state write.

`RequestCancellationAsync()` only requests cancellation and returns its
callback-completion task, allowing signal callers to schedule owner-thread
cleanup separately. Operation tokens are captured while their source is owned;
source disposal waits for both the worker and cancellation callbacks, even
when cancellation has already returned control to the caller. Late outcomes
are disposed and faults observed. `WaitForPendingOperationsAsync()` captures
all currently owned retirement tasks, including detached old workers, for
bounded adapter shutdown. Shared Presentation does not depend on a Terminal
dispatcher or choose a shutdown timeout.

The masking predicate describes rendering requirements for both sensitive and
secret fields. Browser secret controls require consent as well as masking;
ordinary sensitive fields remain locally editable. It is not an administration
or trust capability. Shared state
contains no target API transport. Event's live adapter remains Event-owned.

`DefinitionPresentationOperationTests` covers captured snapshots and the
edit/cancel/disposal import fence; `DefinitionImportTests` covers rejected
inverse inputs, `DefinitionBundleTests` covers aggregate composition, and
`SetupPresentationModelTests` covers accepted/replaced/discarded private-byte
ownership.
Terminal, desktop and browser verification is separate; these tests are not
visual QA.

Browser route custody remains in the browser adapter. `BrowserNavigationState`
registers the current editor's invalidation and discard callbacks. Confirmed
navigation invalidates output/import generations and disposes answer state
before allowing a different route to load public data. Cancellation preserves
the editor and stale confirmations cannot discard a newer owner. Section and
appearance changes remain presentation operations inside the pinned workspace;
they do not introduce shared answer caches or target authority.
