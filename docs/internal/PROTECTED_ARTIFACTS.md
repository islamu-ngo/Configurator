# Protected native artifacts

> **Audience:** Contributors | Integrators | AI agents
> **Kind:** Living contract
> **Status:** Current native publication contract; actual-host support is separately scoped
> **Owner:** Artifacts/Security
> **Last Verified:** 2026-10-30
> **Source Anchors:** `src/Configurator.Artifacts/`, `docs/internal/CLI_CONTRACT.md`, `docs/internal/PRESENTATION_STATE.md`
> **Review basis:** Contract reconciliation; no new protected-save qualification

Use this contract to distinguish prepared bytes, installed files and immediate
verification. Operators should start with [native output and recovery](../public/native-output.md).
The [support index](STATUS.md) identifies qualified hosts; unsupported writers
refuse rather than fall back to ordinary files.

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

## Bundle preparation and publication

CLI generated-input secret recipes publish two explicitly selected restricted
files: original-password sidecar first, updated answer snapshot second. Receipt
IDs are `generated-password-output` and `answer-output`; neither includes a
secret digest or private path. Destination preflight uses disposable protected
preparation and actual writes retain create-only conflict handling. A failed
answer write preserves an installed sidecar and its established receipt facts.
No cleanup or automatic regeneration removes that original credential.

The public guarantee is per-file create-only installation, not a whole-bundle
transaction. Preflight can reject before any publication; a later race or
cancellation can leave an installed prefix. Dispose private preparation
buffers, preserve the receipt and recover in a fresh private directory. Never
infer that failure means no file was installed.

## Immutable local profile revisions

`LocalProfileVaultStore` publishes `.cfgvault` and `.cfgcompare.json` revisions
under an adapter-selected absolute root. Names contain only canonical opaque
profile/snapshot IDs and the fixed suffix; application/environment labels never
grant path authority. There is no mutable index, inferred latest revision or
automatic pruning. Each save snapshots caller bytes and delegates both
preparation and commit to `IProtectedArtifactWriter`. Encrypted and stripped
comparison bytes both use the restricted boundary.

`ProfileFileReader` walks root components through held Linux descriptors.
Creation uses descriptor-relative `mkdirat`; no symlink component is followed.
Trusted intermediate directories use the existing owner/write floor, including
the root-owned sticky-directory exception. The final root must belong to the
effective user with mode `0700`; files must be regular, single-link, same-user
`0600` files. Root traversal permits explicit mount boundaries while retaining
no-symlink/no-magic-link/beneath resolution; revision opens remain within the
held root. Only Linux x64/arm64 descriptor ABIs are admitted.

Discovery enumerates the held root through its own procfs descriptor and checks
named/opened file identities before returning a descriptor. Reopen binds that
descriptor to the same root and file, checks size/mode/owner/link count, reads
bounded bytes and rechecks file/name/root identities before publication. Failed
reads clear owned bytes. Listings are unverified metadata; successful reading
does not authenticate the encrypted profile or its filename IDs. The opening
owner must check authenticated payload identity before adoption.

Root paths are bounded to 4096 characters and 64 components. An inventory scans
at most 4096 entries, counting unknown names; exceeding the limit refuses rather
than reporting a complete listing. Per-file bounds are 4 MiB encrypted and 2 MiB
comparison data. No crypto-header scanner is used for discovery. These are
resource limits, not a filesystem-latency or historical-authenticity guarantee.

The returned one-file `ProtectedArtifactBundleResult` preserves installation,
immediate verification, cleanup and cancellation independently. Cancellation
after installation remains cancelled with the installation fact intact.
Root replacement or failed post-install inspection cannot become a fictional
not-installed result. A vault/comparison pair is not a transaction. Native owner
regressions passed on the executing Linux host; this does not qualify Windows,
macOS, every Linux filesystem, keyring recovery or actual release packages.

## Exact-snapshot portable recovery

`ProfileRecipientLifecycle` distinguishes device-bound-only creation from a
configured portable recipient. Device-only creation requires acknowledgement
bound to the profile ID. A configured device recipient occupies index zero
and counts toward the same eight-recipient limit as explicit portable inputs.
Core generates no snapshot identities; recipient changes require a fresh
host-issued snapshot ID and fresh library encryption. Mode and target
credentials remain unchanged.

`ProfileRecoveryReference` holds the creation event's independently retained
ciphertext digest, authenticated profile/snapshot IDs and intended recipient
configuration. Recomputing that reference from an untrusted candidate file is
not verification. `ProfileRecoveryVerifier` rereads the exact descriptor-backed
file and uses a fresh native codec with no OS-keyring dependency to authenticate
one explicitly selected portable recipient. Only matching digest and decrypted
identities produce `VerifiedForSnapshot`. Native unlock, recipient labels and
an earlier revision's successful test do not establish portable recovery for
this file or for other recipients.

There is no application master key, password reset, automatic OS-key deletion,
history pruning or ordinary-save fallback. Resealing changes access to the new
revision only; old files retain their original recipients. Keyring behavior on
uninstall/reinstall, account reset, OS reinstall and device loss is host-specific
and remains an actual-host qualification obligation.

## OS keyring ownership

`IOsKeyringProvider` separates lookup from creation. `OsKeyringProvider` generates
a fresh opaque ID and independent random 32-byte key for each creation. Native
recipient and comparison keys have distinct purposes and entries. OS values use
canonical 44-byte Base64 UTF-8; missing, malformed, locked, denied or unavailable
lookup never creates a replacement. No machine-derived key, app master key,
subprocess secret transport or ordinary key file participates.

`OsKeyringResult` exposes only status, ID, purpose and creation disposition;
its lease is excluded from JSON. `OsKeyringSecretLease` clears owned raw bytes
on disposal and exposes metadata only. Comparison-purpose leases can create a
separate disposable Core commitment owner; trusted Artifacts orchestration owns
and clears any internal recipient-credential copy.

One nonqueued process-wide permit remains held through actual native completion
and cleanup. Cancellation retires result adoption but cannot roll back OS
effects. Preserve a known created ID and distinguish `Created`, `Uncertain` and
`NotAttempted`. Neither disposal nor a failed/uncertain publication deletes an
OS entry; a key may protect an installed or historical snapshot.

Linux uses an independently owned libsecret service proxy and thread-default
GLib context. Async search loads unlocked values without requesting unlock.
Creation checks the default collection and omits replacement; it may still
prompt through the OS API. A thirty-second deadline requests `GCancellable`
cancellation, without guaranteeing physical return by then. Finish callbacks
precede release of native values, references, attributes, cancellation state and
context. Native error text is never copied into diagnostics.

Windows uses the current token's generic Credential Manager store and
`CRED_PERSIST_LOCAL_MACHINE`, which persists for that user on that computer.
`CredReadW` returns one allocation: admit the exact bounded blob, copy it and
release with `CredFree`. Numeric failures remain value-free. `CredWriteW`
replaces an existing target/type and has no create-only mode; only internally
generated fresh IDs enter creation. This avoids intentional shared-target reuse
without inventing atomic collision rejection. Synchronous OS calls run off the
caller thread; cancellation waits for physical completion and cleanup.

macOS pins the user-domain default file keychain and scopes generic-password
service/account queries by purpose and opaque ID. `SecItemAdd` rejects duplicate
creation without replacement. Lookup admits CFData of exactly 44 bytes before
copying. Owned CF objects remain alive through the synchronous call and release
afterward; exported constants are borrowed. This classic file-keychain adapter
saves, disables and restores the documented process interaction policy. That
control is deprecated, has process-level effects and does not claim LAContext
semantics. Restoration failure preserves creation facts and refuses successful
custody. Actual prompt/locking/unsigned-package behavior remains a host gate.

Injected-boundary tests qualify application ownership and mapping, not libsecret
callbacks, Windows execution, actual unlocked/locked/headless keyrings or secure
erasure. Native host and package qualification remain separate. Keyring support
does not grant protected-save support on Windows or macOS.

## Bounded native input lifetime

`Configurator.Artifacts.Composition.CompositionDirectoryCompiler` owns bounded
Linux directory acquisition. It captures descriptor-checked files and directory
identities, sends ordered path-free JSON/YAML fragments to Core, and privately
holds the serialized candidate. Only a successful candidate reaches the caller's
publication barrier, once. It then checks cancellation and recaptures the source
before returning output. This barrier does not install files.

Capture, parse, merge and serialization failures return before the barrier.
Cancellation observed at a checkpoint returns `Cancelled`; after the barrier it
wins before the source recheck. Otherwise a changed source returns
`SourceChanged`. Every failed result has empty output. This deliberately replaces
the former barrier-before-serialization order.

The directory adapter admits Linux x64/arm64 flag layouts explicitly and rejects
other process architectures. It uses fixed-layout 256-byte `statx` with required
basic-stat masks, nanosecond validation and complete stable identity comparison
(device, inode, links, mode, owner/group, device type, size, block count/size and
modification/change timestamps). Access time is excluded because reading may
change it. `openat2` resolution rejects cross-device traversal, magic links,
symbolic links and escape from the retained root. Missing native capabilities
refuse; there is no ordinary-read or alternate-ABI fallback. Real filesystem
evidence qualifies only its executing host. See the
[native acquisition decision](adr/ADR-native-directory-acquisition.md).

`BoundedFileInput` is the native assembly's disposable byte owner for local
definition and import acquisition. Its path or caller-supplied opener selects
file authority; the helper supplies no admission or trust policy. Open, initial
length, read and final length checks execute on the worker, not the calling UI
owner. Empty, oversized, growing or truncated input rejects. Each current native
caller supplies its existing 4 MiB per-file bound.

Cancellation does not abandon a blocking open/read/disposal task or clear storage
still in use. The returned task settles only after physical stream disposal;
rejected storage clears before that task completes. Successful storage remains
owned until the consumer disposes it. Desktop definition/import and Terminal
import retain their existing generation and physical-operation fences, so late
bytes cannot become current. The shared form clears consumed import memory
before Desktop's busy lease ends; outer input disposal clears the remaining
owned allocation. CLI source acquisition returns its existing caller-owned copy
after clearing the helper owner. Public source and private answer-import policy
remain distinct, and Browser has no reference to this native helper.

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

`Configurator.Artifacts.ProtectedArtifactPublicationReceipt` owns the shared
immutable attempt facts. Its public constructor defensively copies at most 64
outcomes and retains installation, verification, completion, cleanup, settlement,
unknown and failure facts without answers or destination paths. Client owners
retain their own history, formatting, dispatch and shutdown policy.

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

Desktop now keeps the physical consumer's lease and CTS until its awaited
publication/read and cleanup settle. `DesktopPublicationOperation` owns save
bytes and bundles; import and definition readers own their buffers through
`finally`. Cancellation/edit/disposal revoke eligibility without clearing
borrowed input or releasing a second operation early. Bundle progress reaches
bounded copied receipt history before final completion, independently of current
generation. Desktop retains 16 settled attempts plus one active attempt, each
bounded to 64 outcomes. Current and historical controls expose only safe facts;
no paths, private bytes or raw exceptions enter receipts. Desktop's pre-close
protocol awaits the exact physical completion without blocking the dispatcher.
At two seconds it retains unknown outcomes in an open window with deliberate
Stay open and Force close actions. Withdrawing the close intent cannot release
the physical owner or restore preparation; forced termination cannot promise
managed cleanup or no write. Client policy remains separate from immutable
Artifacts facts and the native writer's unchanged authority.

The Linux writer and bundle publication suites exercise create-only output,
preflight rejection, every committed prefix, observer/cleanup failures, original
exception preservation and defensive copies. Actual Linux regressions perform a
successful installation followed by deterministic missing/replaced-target
inspection or exceptional/cancelled inspection; they require the installation
fact and the unattempted second output. These Linux checks do not certify
protected saving on other hosts.
See [native output](../public/native-output.md) for operator behavior and host limitations.
