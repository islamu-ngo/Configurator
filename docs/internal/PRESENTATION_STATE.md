# Definition presentation state

Native clients use the same admitted definition and answer snapshots.
`DefinitionFormModel` owns typed editing and disclosure; guided/advanced views
do not change validation. `DefinitionPresentationOperation` captures the
definition, artifact ID and input revision for a workspace execution.

The existing session/workspace model allocates generations, invalidates old
work on edit, deactivation, cancellation and disposal, and rejects late results.
Published `DefinitionPreparationMetadata` contains identifiers, digests and
value-safe diagnostic codes but no artifact bytes. The workspace owns the
separate prepared output and clears it when invalidated.

The masking predicate describes rendering requirements for both sensitive and
secret fields. It is not an administration or trust capability. Shared state
contains no target API transport. Event's live adapter remains Event-owned.

The selected suite passed 20 tests, including preserved generation/cancellation
and privacy invariants and neutral definition snapshot integration. Terminal,
desktop and browser verification is separate; this suite is not visual QA.
