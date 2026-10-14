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
artifact ID and status. A null status means no outcome has been established;
an active or faulted commit must not be presented as certainly unattempted. A
`Written` prefix remains on disk after a later race, I/O failure or
cancellation; there is no whole-bundle transaction or automatic rollback.
Native callers must surface that prefix for manual recovery without printing
private paths or values in machine output.

`PublishAsync(writer, artifacts, directory, outcomeObserver, cancellationToken)`
accepts an optional synchronous observer before the optional final cancellation
token. Existing positional-token callers must use `cancellationToken: token`
or pass the observer argument first. There is no compatibility overload. The
observer receives each established outcome before the next cancellation check;
it receives no artifact bytes, answers or destination path. Observers must keep
work bounded and marshal UI refresh to the owning loop.

The publisher records the outcome before invoking the observer. An observer
exception stops later commits and returns `CompletionCode.ObserverFailed`.
`ProtectedArtifactPreparation` records an established commit status before
cleanup, so a cleanup exception cannot erase `Written`. `CleanupFailed` is
independent of the completion code; both observer and cleanup failures can be
reported together. The immutable result copies its outcomes, and `Succeeded`
requires normal completion, no cleanup failure and every outcome `Written`.
Every acquired preparation is disposed and every cloned payload is zeroed even
when observation or cleanup fails. Unexpected programming exceptions propagate
after cleanup, preserving the original exception and any already observed
outcomes. Native clients must observe that task; they must not infer rollback.

Terminal keeps one physical publication active until it settles, independently
of workspace cancellation and private readiness. Its copied receipts retain the
newest 16 settled attempts plus the active attempt, with at most 64 declared
artifact outcomes each. Answer clear, edit and cancellation retain that history;
final session disposal clears it. A receipt never restores answers or prepared
output and cannot enable Save. The two-second graceful-close budget marks
unfinished publication as unknown rather than promising that it was cancelled
without writing. Forced process termination cannot guarantee managed cleanup.
See [Terminal ownership](TERMINAL_CLIENT.md) for the lifecycle and rendering.

Desktop's bundle completion uses the full result's `Succeeded` flag. CLI's
status-only bundle adapter raises a stable I/O failure on `CleanupFailed` before
projecting per-file statuses, so an all-`Written` cleanup failure cannot become
a successful command. The public CLI response schema is unchanged.

The Linux writer and bundle publication suites exercise create-only output,
preflight rejection, every committed prefix, observer/cleanup failures, original
exception preservation and defensive copies. These Linux checks do not certify
protected saving on other hosts.
See [native output](../public/native-output.md) for operator behavior and host limitations.
