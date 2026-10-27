# ADR: Cross-platform quality ownership and physical lifetimes

Date: 2026-10-27
Status: Accepted; exact execution evidence and qualification limits belong in
[Operations](../OPERATIONS.md#native-ownership-qualification-2026-10-27).
Owners: Artifacts, Desktop and Terminal.

Graduation scope: the native decision below is preserved in full. The appended
cross-platform decision records the delivered Browser, shared form, preparation,
acquisition and CLI boundaries. It does not close the parent-owned final gates.

## Context

A Desktop operation can remain inside an asynchronous native consumer after
cancellation, editing or disposal. Clearing its borrowed bytes at that point
changes what the consumer can read. Releasing its logical busy lease also permits
another operation before the first has physically settled. Separately, a file
may already have been installed even though the originating workspace is stale.
Discarding that feedback misleads an operator considering a retry.

## Decision

Keep eligibility, physical settlement and historical publication facts separate.
Presentation still owns answers and generations. Artifacts still owns protected
create-only writes. Cancellation immediately revokes current capabilities; it
does not prove completion or retract installation.

Move the existing bounded immutable receipt into
`Configurator.Artifacts.ProtectedArtifactPublicationReceipt`, deleting the
Terminal-local type and migrating every caller without an alias. Clients retain
their own history, formatting, dispatch and shutdown policy. Each receipt
defensively snapshots at most 64 outcomes. Desktop retains the newest 16 settled
attempts plus one active attempt, with no answer bytes or destination paths.

`DesktopPublicationOperation` retains captured single-save bytes or the composed
bundle until the external consumer and cleanup finish. The real bundle outcome
observer records known progress before later work completes. Import and
definition reads retain their storage in their existing await/finally scopes.
`DesktopOperation.Completion` signals after the consumer releases its lease;
shell disposal cancels but never disposes that lease or CTS early. A stale
completion can update historical facts, never current readiness.

`MainWindow` intercepts close before workspace disposal. One shell close intent
awaits the captured physical completion and a two-second `TimeProvider` deadline
without blocking the dispatcher. Settlement permits normal close only for the
same still-active intent. Deadline expiry keeps the window open with unknown
outcome, known receipts and named Stay open / Force close controls. Stay open
withdraws intent, not cancellation or ownership. Force close is deliberate and
cannot promise no write, rollback or managed erasure. Closing detaches callbacks
and DataContext before workspace disposal; surviving in-process consumers still
own cleanup. Process termination can prevent that cleanup from running.

## Alternatives and consequences

Early zeroization, treating logical disposal as completion, discarding stale
receipts and automatic unknown-outcome closure are rejected because they create
false safety claims. Copying Terminal's automatic-stop policy would override
Desktop's deliberate decision. A universal lifecycle framework, shared answer
store, new evaluator, filesystem abstraction or scheduler is unnecessary.

Memory may remain allocated while a noncooperating consumer is pending. This is
intentional physical ownership, bounded by the existing operation and input
contracts, not persistence. The busy gate prevents overlapping work. Managed
input strings still have no secure-erasure guarantee.

## Verification and boundaries

Pre-consumption and ordinary-close runtime failures were reproduced before their
respective fixes. Deterministic tests use registered completion gates, ordered
literal bytes and controlled time, not sleeps. Real Linux writer checks remain
separate from external-boundary substitutes. The native executable preserves
the original thirteen guards and adds four named receipt/close guards with real
X11 windows and normal/minimum-size rendered captures.

Native executable runs, headless owner tests, local package publication and
installed-payload/human assistive-technology qualification are distinct evidence.
At that native checkpoint, the required follow-up backlog still owned
Browser/CLI/editing/preparation/CI and broader host obligations. The appended
cross-platform decision distinguishes their subsequent delivered slices from
the still-required followups. This decision is not whole-program or enterprise
certification.

## API provenance

Official .NET documentation was consulted on 2026-10-27 for interface facts:
`Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, `TimeProvider.CreateTimer`
and `ITimer` lifetime. No dependency was added. The operation model, one-shot
controlled test clocks, UI flow and documentation were independently designed
from Configurator's existing owners; no third-party implementation or prose was
copied. Source:
`https://github.com/dotnet/docs/blob/main/docs/standard/datetime/timeprovider-overview.md`.

## Cross-platform ownership decision

Date: 2026-10-27. Status: Accepted implementation boundaries; final workstream
qualification and responsibility review remain parent-owned.

The four clients adapt shared computation rather than acquire independent answer
or validation authority. Core admits definitions, validates immutable answers and
composes canonical artifacts. Presentation owns the form mutation transaction,
answer revision and workspace operation generations. Artifacts owns bounded
native acquisition and protected publication. Each client owns its dispatch,
rendering, navigation and deliberate close/export interaction. Browser has no
native Artifacts dependency; the optional static Browser.Host serves public files
and has no product project references or answer processing.

### Form edits, correction drafts and projections

`DefinitionFormModel` is the sole transaction facade. Its internal
`DefinitionEditingState` owns root inputs, privacy mode, revision and stable row
identities; `DefinitionViewState` owns experience, disclosure, expansion and
discovery bookkeeping. Both use the form's existing gate, without independent
locks, notifications or answer copies. Index/facts/projection lifetimes remain
the ones described in the [form computation ADR](ADR-browser-form-computation.md).
Observers receive coherent state only after the mutation gate releases.

Desktop field viewmodels project literal shared paths, origins, row identity and
leaf diagnostics. One coordinator refreshes roots from a current lookup rather
than making each root subscribe to the global change stream. Empty objects/lists
are explicit presence, not missing values. Touched action captions/help use
`DesktopActionText` and neutral/Arabic resources. That proves touched-resource
wiring, not whole-application translation or assistive-technology conformance.

Terminal reads a correction draft for the exact scalar/list-item path before
falling back to its committed value. A rejected correction is not an accepted
answer and must not change accepted origin. Disclosure, experience and resize
preserve that draft; deliberate private clear revokes it. Masked control storage
remains a separate protected boundary. Neither native projection is a second
semantic editor. See [Presentation state](../PRESENTATION_STATE.md).

### Browser navigation, custody, connectivity and exports

Navigation captures an exact owner-and-generation registration lease across
confirmation, discard and release. Component identity alone is insufficient:
the same editor can acquire a newer workspace while older cleanup is awaiting.
Old cleanup can release only its captured registration and must observe the
previous retirement as well as current cleanup before claiming settlement.

`BrowserOfflinePolicy` is a required initialized dependency. Missing/rejected
custody initialization leaves readiness false and denies entry, import and
preparation before private input consumption. Clean-entry source eligibility
uses current native connectivity notifications, not a frozen startup value.
Those boolean notifications never override active-workspace fetch denial.
Online status is not a remote reachability promise; typed fetch failures remain.
Disposal releases the exact listener. Public source admission still precedes
answers and pins canonical identity; a running session keeps its selected revision.

Export sessions explicitly register, open, invalidate and terminally release.
Managed invalidation synchronously denies the generation and clears owned bytes;
it then observes pending exposures before releasing module metadata. Unknown or
retired IDs cannot be resurrected by delayed open. This permits bounded metadata
reclamation without deleting a fence while a pending operation could still use
it. Copy/download remains deliberate custody transfer, not native protected save
or forensic erasure. Answers/artifacts never enter storage, caches, requests or
worker messages. See [launch/custody contract](../LAUNCH_CONTRACT.md).

### Single-pass preparation and staged adoption

Core bundle preparation creates validation authority internally for the exact
definition, immutable snapshot and complete/draft mode. It validates once and
composes each emitted artifact once in declared order. Single-artifact public
entrypoints still validate; callers cannot supply trusted validation.

Browser retains that bundle and stages per-artifact Presentation adoption
operations, with existing owned-copy outcomes and per-artifact completion paths.
Even a never-started adoption has a disposable owner. Execution transfers its
outcome once; disposal after transfer cannot clear the current workspace output.
The staged list becomes current only after aggregate admission and all generation
checks succeed. Fatal rejection clears the entire staged list; secret-deferred
draft withholding stays a bundle diagnostic and never implies complete readiness.
This removes recomposition, not the unmeasured capture/hash/copy/erasure spans or
main-thread blocking. The [single-pass ADR](ADR-single-pass-preparation-ownership.md)
owns the detailed seam; [responsive preparation P](../../../dev/backlog/browser-interaction-performance-followups.md)
remains required.

### Native acquisition and explicit CLI authoring

`BoundedFileInput` owns bounded open/stat/read on a worker away from the UI
dispatcher, plus physical completion and disposal. Desktop, Terminal and CLI
adapters retain format, source/private-import admission and error mapping.
Cancellation revokes eligibility but does not abandon a read or clear a buffer
still used by its consumer. This is native I/O isolation, not a Browser CPU
scheduler. The existing Linux directory-source reader inside Core remains a
qualified exception, with a [focused relocation trigger](../../../dev/backlog/core-directory-source-boundary.md).

CLI `SetupCliIo` requires `ISetupCliAuthoringFiles`; the invocation carries it to
handlers and Program explicitly supplies `PublicDefinitionFiles`. Discovery,
bounded source/anchor/fork/example/schema reads, existence checks and staged
create-only public publication use that capability. Non-authoring fixtures
reject it explicitly; there is no ambient native fallback or compatibility
constructor. `ISetupCliPublicFilePreparation` owns staging/commit/disposal and
preserves ordered publication facts.

Changed revisions use an explicit separate non-source/non-discovery `--output`
candidate, same family and next revision, preserving captured source syntax and
leaving editor saves untouched. Candidate review/adoption is deliberate.
**Logical authoring capability is delivered; captured physical authority is not.**
Path checks and create-only rename do not bind source/destination-parent identity
against every ancestor or hard-link race, nor provide root initialization
exclusion. That [required second increment](../../../dev/backlog/cli-public-authoring-custody.md)
must retain opened authority and kernel-owned root exclusion. Public authoring
never relaxes restricted answer-output protection.

## Evidence and graduation limits

The retained local evidence supplied for this graduation records commits
`dc3c20d` (leases), `3ff18a3` (policy), `b4ec7ca` (Terminal), `f364a7a`
(Desktop), `47b2150` (connectivity), `1bcac73` (exports), `4d71fde`
(single-pass/adoption), `7cf690c` (acquisition), `17b0f3a` (form), `dcc7617`
(CLI candidates) and `8f9803a` (safe evidence). This documentation unit did not
rerun product checks or inspect Git history.

Supplied current owner results are Core 150, Presentation 167, Artifacts 65,
Browser 233, CLI 171, Terminal 101 and Desktop 222, all zero failures/skips.
Actual dynamic coverage measured Core 3,223/3,815 lines and 2,745/3,687 branches;
Presentation 1,699/1,771 lines and 1,192/1,346 branches. Those are measured
owner/run facts, not a product-wide coverage percentage or a support certificate.

Published Browser inventory
`14ee82b9ea6b6c09c7c4da91bd83a0c62ef737b0fd368d6df034d1978b0ec314`
has retained real connectivity/admission/custody/listener evidence and actual
export-module 200-cycle peak 1/final 0 metadata entries. Three redacted incomplete
drafts, rejected incomplete complete-mode with zero outputs, retry/reveal/clear,
and an independently validated 82-byte JSON download establish those semantics.
The initial origin ran an older pinned worker; a fresh origin qualified the
current module. The first download oracle failed because input appended to a
default; selecting/replacing the input corrected the fixture, not the product.
No artifact content was logged.

X11 D01-D18 en/ar evidence covers touched resources, presence, focus and the
named lifecycle flows, not full translation/AT. Published Terminal correction
retention and clear passed; no-save close 4 is expected. Outer 82x28 clipped its
footer, so full minimum viewport remains unqualified.

The actual current CLI binary passed all five native parser cohorts, with binary
SHA-256 `9d41dd6e5f214f82ec87043eb9c9e41fe8c640c6f7a90619d865f3917b2d72c8`
and schema SHA-256 `89c955bce60411c1ce30396a5070e141cc44fbe585cf8d4003780d7099db7e41`.
That supersedes stale-package rejection, not the independent final native
publications, architecture/current X11/Browser smoke, documentation and I-VSD
gates. Their completed execution and final identities belong in
[program qualification](../OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27),
not this decision's historical evidence checkpoint. No remote CI or new host,
installer, accessibility or first-party publication authority is established.

The [required-followup ledger](../../../dev/backlog/cross-platform-quality-required-followups.md)
keeps every deferred obligation visible, separate from those parent close gates.
The [journal](../../../dev/_journal/domains/presentation-and-clients.md)
retains reusable race and evidence lessons. No universal workflow engine, generic
scheduler/cache, duplicate answer owner or speculative evaluator rewrite is
introduced by this decision.
