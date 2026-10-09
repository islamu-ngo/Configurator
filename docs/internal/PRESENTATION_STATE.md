# Definition presentation state

Native clients use the same admitted definition and answer snapshots.
`DefinitionFormModel` owns typed editing and disclosure; guided/advanced views
do not change validation. `DefinitionPresentationOperation` captures the
definition, artifact ID and input revision for a workspace execution.
`DefinitionFormModel.ImportAsync` captures the current immutable answer snapshot
before reading an existing file. After Core assesses its declared inverse, the
form applies imported field text as one revision only if the captured revision
still matches and cancellation/disposal have not invalidated the operation.
Manual edits use the same form gate, so a late read cannot overwrite them.
Native bundle clients compose a selected set from one captured snapshot through
`DefinitionBundleComposer` and hand the immutable result to the protected
bundle publisher; per-file publication can have a written prefix after a later
failure.

The existing session/workspace model allocates generations, invalidates old
work on edit, deactivation, cancellation and disposal, and rejects late results.
Published `DefinitionPreparationMetadata` contains identifiers, digests and
value-safe diagnostic codes but no artifact bytes. `SetupPresentationOutcome`
copies operation bytes into owned memory. The workspace clears that memory on
replacement, deactivation, termination and disposal; late rejected outcomes
are cleared before discard notification. Cancellation after a settled result
intentionally retains that result until its next invalidation or replacement.

The masking predicate describes rendering requirements for both sensitive and
secret fields. It is not an administration or trust capability. Shared state
contains no target API transport. Event's live adapter remains Event-owned.

`DefinitionPresentationOperationTests` covers captured snapshots and the
edit/cancel/disposal import fence; `DefinitionImportTests` covers rejected
inverse inputs, `DefinitionBundleTests` covers aggregate composition, and
`SetupPresentationModelTests` covers accepted/replaced/discarded private-byte
ownership.
Terminal, desktop and browser verification is separate; these tests are not
visual QA.
