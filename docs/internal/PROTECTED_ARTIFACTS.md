# Protected native artifacts

`IProtectedArtifactWriter` separates preparation from publication. Its disposable
preparation owns cleanup; `CommitAsync` is a one-shot create-only operation.
The Linux writer uses anonymous-inode preparation and no-replace publication
instead of exposing a partial private file. Boundary checks reject unsafe or
changed destinations. Unsupported platforms return a value-safe status.

Core assigns the restricted floor to every answer-derived artifact.
Presentation snapshots revisions and generations, invalidates prepared output
after edits, and disposes its owned output buffers. A stale operation cannot
publish into the current workspace. Native adapters decide the destination;
definition filename hints grant no filesystem authority.

`DefinitionBundleComposer` selects declared artifact IDs and returns all outputs
in definition order from one immutable answer snapshot. It returns no artifacts
if a later projection fails or the sum of rendered bytes exceeds the 4 MiB
aggregate bound. Each output retains its definition digest, input revision,
restricted sensitivity, literal bytes and individual digest.

Core artifacts and bundle results are disposable. Native callers dispose the
bundle after publication so its owned rendered byte arrays are zeroed; reads
from a disposed artifact fail. Artifact composition also zeros its temporary
canonical and rendered arrays. The publisher retains each private `Bytes` clone
through writer preparation and commit, then zeros all such clones in a `finally`
block, including rejection, cancellation and cleanup-error paths. Callers that
request `Bytes` themselves own those clones and must clear them after use.

`ProtectedArtifactBundlePublisher` copies the selected output list, rejects
duplicate IDs or case-insensitive destination filenames, and asks the writer to
prepare every target before the first commit. `ProtectedArtifactPreparation`
exposes a value-free `RejectionStatus`; null means only that preflight succeeded,
not that a future commit is guaranteed. On preflight failure, all preparations
are disposed and no file is published. Commits then run in selection order and
stop at the first failure or cancellation. Each outcome contains only an
artifact ID and status. A null status means no commit was attempted. A
`Written` prefix remains on disk after a later race, I/O failure or
cancellation; there is no whole-bundle transaction or automatic rollback.
Native callers must surface that prefix for manual recovery without printing
private paths or values in machine output.

The Linux writer and bundle publication suites exercise create-only output,
preflight rejection, partial commits and cancellation. These Linux checks do
not certify protected saving on other hosts.
See `docs/public/native-output.md` for operator behavior and host limitations.
