# ADR: Terminal editing, operation ownership and publication history

Date: 2026-10-15
Status: Accepted; implementation and independent source review verified. Native
terminal qualification remains separately owned as described below.
Owners: Presentation, Artifacts and Terminal.

## Context

Browsing an optional container must not supply it. Cancelling private work must
prevent old results from returning, while files already created remain real.
An operation can outlive its visible workspace generation, and a callback can
fail after either private results or a protected file have been produced.
These lifetimes need explicit owners rather than one busy flag.

## Decision

`DefinitionFormModel` owns typed presence and answer revisions. Deliberate
Supply/Clear commands represent empty objects and lists; clearing an absent
nested path does not invent its parent. `DefinitionScalarEditing` supplies the
shared scalar conversion boundary. Terminal commits or rejects the visible
draft before navigation or choice cycling, and confirms populated row removal.

Shared private clearing removes protected roots in one revision while retaining
public roots. Even already-empty protected roots advance the revision to fence
an import that captured the old state. The outermost form mutation completes
under its gate before notifications run. Browser consent revocation remains
the separate secret-leaf operation; terminal clearing grants no browser consent.

`SetupPresentationWorkspace` atomically matches and settles each operation.
An accepted outcome retains its operation identity through notification
delivery: a subscriber failure can clear that outcome without disturbing a
newer operation or result. Invalidation attempts all notification groups before
propagating failures. Worker completion and cancellation-callback completion
jointly govern cancellation-source retirement. Cancellation can release the
visible workspace before its discarded worker has finished; the workspace still
owns, observes and clears that late result.

`SetupTerminalImportOperation` owns one bounded physical read and its admission
callback. Blocking file-open work runs off the terminal owner. Admission checks
definition digest, artifact, answer revision, local generation and cancellation,
then uses the form's import boundary on the owner. Cancellation cannot release
physical ownership while admission is executing. Temporary input bytes are
cleared before the physical operation retires.

Window shutdown requests cancellation and clears the locked secret buffer
immediately. Model and control changes run on the terminal owner through the
pinned `IApplication.Invoke` contract. The loop remains available for settlement
for at most two seconds; unresolved publication is marked unknown at the
deadline. Weak owner references, callback generations and observer detachment
prevent late work from accessing disposed controls. Observed workers continue
their cleanup while the process remains alive. Closing selects the active or
latest settled receipt atomically and preserves overall shutdown uncertainty
even when an earlier publication succeeded.

`ProtectedArtifactBundlePublisher` records established per-file status before
calling observers or rechecking cancellation. Observer failure stops subsequent
commits; cleanup failure remains explicit without erasing an established
Written status. Temporary buffers are still cleared. Unexpected programming
faults propagate; they are not converted into a claim that nothing happened.

`SetupTerminalArtifactOperation` separately owns physical publication and copied,
value-free receipts: one active attempt and the newest 16 settled attempts,
each bounded to 64 declared artifact outcomes. A cancelled workspace cannot
start overlapping physical publication. Edits and private clearing retain
history; session disposal removes it. Receipts contain identifiers and status,
never answers, paths or artifact bytes. Unknown in-flight results differ from a
settled unattempted remainder. Inspecting artifacts or history changes neither
prepared readiness nor the selected output set.

## Consequences and limits

Cancellation is not rollback. Operators inspect the reported committed files
and deliberately recover before retrying; the native writer remains create-only
and never automatically deletes or overwrites output. Unsupported protected
save hosts still fail closed. Target acceptance and apply authority remain with
the target application.

Read-only workspace state and the publisher observer parameter are intentional
development-time API breaks. Callers use workspace lifecycle methods and named
`cancellationToken` arguments; there are no compatibility adapters.

Managed strings, operating-system buffers and forced process termination remain
outside owned-buffer clearing guarantees. Headless/control tests and real Linux
filesystem tests do not qualify a published terminal's keys, resizing, signals,
restoration, screen-reader behavior or every host. Those release-support checks
remain in the [terminal follow-up backlog](../../../dev/backlog/terminal-hardening-follow-ups.md).

## Verification and source provenance

Regression owners are `SetupPresentationModelTests`, `DefinitionPrivateClearTests`,
`DefinitionContainerPresenceTests`, `DefinitionScalarEditingTests`,
`SetupTerminalImportOperationTests`, `SetupTerminalPublicationReceiptTests`,
`SetupTerminalGuiAdapterTests` and `ProtectedArtifactBundleTests`. They cover
faulting observers, reentrant replacement, cancelled read/admission, bounded
shutdown and every real-writer committed prefix. Exact execution evidence stays
in task context; durable behavioral contracts are in
[Presentation state](../PRESENTATION_STATE.md),
[Terminal client](../TERMINAL_CLIENT.md),
[protected artifacts](../PROTECTED_ARTIFACTS.md) and the
[operator guide](../../public/terminal.md).

Official interface evidence consulted on 2026-10-14:

- [.NET cancellation-source lifetime](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtokensource).
- [Cancellation callback completion](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtokensource.cancelasync).
- Installed Terminal.Gui **2.5.0** XML documentation for `IApplication.Invoke`,
  `RequestStop` and runnable stop interception. No newer `InvokeAsync` API was assumed.

Requested Tavily and Context7 MCP tools were unavailable in this session;
official documentation and pinned package metadata supplied the interface facts.
The source-free handoff consisted only of platform ownership, cancellation and
dispatch requirements. No third-party implementation, prose, artwork or new
dependency was copied. Repository structure and tests were independently
designed around existing owners. Platform-required API identifiers and common
lifetime constraints were separated from discretionary structure in the
abstraction/filtration/comparison review. This is an engineering provenance
record, not legal certification or unexecuted native qualification.
