# ADR: Native physical ownership and deliberate Desktop close

Date: 2026-10-27
Status: Accepted; exact execution evidence and qualification limits belong in
[Operations](../OPERATIONS.md#native-ownership-qualification-2026-10-27).
Owners: Artifacts, Desktop and Terminal.

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
The required follow-up backlog still owns Browser/CLI/editing/preparation/CI and
broader host obligations. This decision is not whole-program or enterprise
certification.

## API provenance

Official .NET documentation was consulted on 2026-10-27 for interface facts:
`Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, `TimeProvider.CreateTimer`
and `ITimer` lifetime. No dependency was added. The operation model, one-shot
controlled test clocks, UI flow and documentation were independently designed
from Configurator's existing owners; no third-party implementation or prose was
copied. Source:
`https://github.com/dotnet/docs/blob/main/docs/standard/datetime/timeprovider-overview.md`.
