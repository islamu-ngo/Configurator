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
form applies imported field text as one revision only if the captured revision
still matches and cancellation/disposal have not invalidated the operation.
Manual edits use the same form gate, so a late read cannot overwrite them.
Guided disclosure includes active invalid fields even when their ordinary
disclosure is Advanced, keeping correction controls reachable. `SupplyObject`
and `SupplyList` express deliberate container presence through the same bounded
snapshot and secret-consent gates. An empty object `{}` or list `[]` is distinct
from an absent answer; supplying an existing container preserves its contents.
Clearing an absent nested child does not materialize its ancestors or advance
the input revision. Clearing a supplied container removes it and reevaluates
requiredness. `DefinitionScalarEditing` converts scalar drafts without target
semantics: empty text means absence, whitespace in strings remains literal,
and rejected conversions produce no replacement value. List editors distinguish
an abandoned empty append draft from an existing empty string item.
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
