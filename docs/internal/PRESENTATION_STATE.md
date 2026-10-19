# Definition presentation state

Native clients and Browser use the same admitted definition and answer snapshots.
Local compact YAML/JSON sources pass through `SetupDefinitionSourceCodec` before
the client constructs `DefinitionFormModel`. Syntax is explicit and canonical
identity is shared with expanded runtime input. Source bytes and comments do
not become answer snapshots or prepared output; existing load generations fence
replacement/cancellation. Browser remote retrieval remains runtime JSON only.
`DefinitionFormModel` owns typed editing and disclosure; guided/advanced views
do not change validation. `DefinitionPresentationOperation` captures the
definition, artifact ID and input revision for a workspace execution.
`DefinitionFormModel.ImportAsync` captures the current immutable answer snapshot
before reading an existing file. After Core assesses its declared inverse, the
form applies cloned imported typed values as one revision only if the captured revision
still matches and cancellation/disposal have not invalidated the operation.
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
Guided disclosure includes active invalid fields even when their ordinary
disclosure is Advanced, keeping correction controls reachable. `SupplyObject`
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
