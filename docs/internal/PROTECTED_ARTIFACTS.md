# Protected native artifacts

`IProtectedArtifactWriter` separates preparation from publication. Its disposable
preparation owns cleanup; `CommitAsync` is a one-shot create-only operation.
The Linux writer uses anonymous-inode preparation and no-replace publication
instead of exposing a partial private file. Boundary checks reject unsafe or
changed destinations. Unsupported platforms return a value-safe refusal.

Linux `open` flags follow the process architecture. Arm64 uses different
`O_DIRECTORY` and `O_NOFOLLOW` values from x64; `O_TMPFILE` includes that
architecture's directory bit. The native adapter selects the combined directory
and anonymous-file flags once from `RuntimeInformation.ProcessArchitecture`.
The descriptor-relative preparation, owner checks, architecture-independent
`statx` layout and create-only `linkat` sequence remain the same.

The interoperability facts were checked on 2026-10-25 against Linux v6.12,
commit `adc218676eef25575469234709c2d87185ca223a`, public syscall-interface files
`arch/arm64/include/uapi/asm/fcntl.h` and `include/uapi/asm-generic/fcntl.h`.
Only interface identifiers and required numeric values inform the existing
Configurator-native flag selection; no kernel implementation, comments,
dependency or asset is transferred. Literal ABI regressions cover both packaged
architectures independently of the executing process architecture, while real writer tests
still establish filesystem behavior only on their actual host.

`CommitAsync` returns immutable `ProtectedArtifactCommitResult` facts:
`Status`, `InstallationEstablished`, `VerificationSucceeded` and `CleanupFailed`.
Named factories cannot construct a `Written` refusal. `Succeeded` requires an
established, verified installation and successful cleanup; status alone is not
the success contract. `EstablishedResult` replaces the old status-only
preparation property, with no compatibility alias.

Linux records installation immediately after successful `linkat`, before
releasing the retained descriptor reference or inspecting the destination.
Inspection failure or identity/mode mismatch leaves installation established
and verification unsuccessful. A subsequent exception also retains that
unverified installation in the preparation. A per-writer internal fault seam
runs only after the real namespace operation for deterministic regression
tests; it is not a public plugin or replacement publication implementation.
Verification describes only that immediate check, never continued integrity.

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
artifact ID and immutable commit result. A null result means no outcome has been established;
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
`ProtectedArtifactPreparation` records installation before verification and
the completed result before cleanup, so either failure cannot erase installation.
Its retained result also records cleanup failure. `CleanupFailed` is
independent of the completion code; both observer and cleanup failures can be
reported together. The immutable result copies its outcomes, and `Succeeded`
requires normal completion, no cleanup failure and every outcome's `Succeeded`.
An installed-but-unverified result is observed and stops the next commit, even
when its status resembles a pre-installation conflict. Exceptions after an
established result preserve that result; requested cancellation also retains its
independent completion code.
Every acquired preparation is disposed and every cloned payload is zeroed even
when observation or cleanup fails. Unexpected exceptions without an established result propagate
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

Desktop's bundle completion uses the full result's `Succeeded` flag. CLI,
Terminal and Desktop migrate the immutable facts directly; none treats a
post-inspection conflict as evidence that installation never occurred. CLI's
regenerated machine contract carries independent installation/verification facts
alongside write status, cleanup, cancellation and receipt delivery. Public stdout
delivery is not native namespace installation. See [CLI contract](CLI_CONTRACT.md)
for exact wire values. This is a breaking replacement, not a legacy adapter.

The Linux writer and bundle publication suites exercise create-only output,
preflight rejection, every committed prefix, observer/cleanup failures, original
exception preservation and defensive copies. Actual Linux regressions perform a
successful installation followed by deterministic missing/replaced-target
inspection or exceptional/cancelled inspection; they require the installation
fact and the unattempted second output. These Linux checks do not certify
protected saving on other hosts.
See [native output](../public/native-output.md) for operator behavior and host limitations.
