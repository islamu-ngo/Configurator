# CLI results preserve publication facts independently of delivery

Status: accepted; explicit-input/help implementation underway, post-change verification pending.
Date: 2026-10-16. Owners: CLI and native packaging.
Implementation anchor: [CLI contract](../CLI_CONTRACT.md).
Operator guidance: [command-line configurator](../../public/cli.md).

## Context

A restricted file can be published before a receipt overflows or stdout fails.
Reporting only command failure hides established effects and can invite an unsafe
retry. Repeated field coverage/readiness arrays also make receipt size grow with
both definition size and selected output count. Public definition/ID-map output
has the same written-prefix problem: ordered commits are not a transaction.

Ambient environment-name checks and secret-word substring bans reject ordinary
filenames and IDs without establishing whether private input was supplied. A
parallel argument parser also diverges from Spectre on equals-form options, stdin
`-` and `--`. Help needs a structural machine view without reading operational
inputs or accidentally advertising framework-only commands.

## Decision

Replace command v1 with v2; keep authoring/runtime v1. A command-owned
`SetupCliResultPlan` and `SetupCliOutcomeLedger` size the mandatory receipt before
effects and retain every selected output's full ID, established digest, sensitivity,
path intent and write outcome. Later publication or cleanup failure cannot erase
known writes or relabel an unknown commit as not-written. Reuse the native protected
publisher rather than introducing a transaction, retry or event framework.

Keep the 65,536-byte UTF-8 receipt bound including newline. Replace duplicated
coverage arrays with exact counts and sanitized-view SHA-256 digests; bounded
read-only detail continuation retrieves coverage/diagnostics without replaying a
write. At most 16 inline diagnostic examples and requested detail share an 8 KiB
budget with exact diagnostic totals/omissions. RFC 6901 pointers and optional
numeric source-position slots describe structure without adding Core repair.
View digests bind public definition, operation, selection and safe status facts,
never a private-answer hash or persistent answer cache.

Bundle selection uses repeated `--artifact` up to 64 times, not CSV `--artifacts`;
there is no compatibility alias. Invocation limits admit the real maximum-ID
selection: 256 tokens, 32,768 aggregate UTF-8 bytes including separators, and
4,096 characters per argument, while artifact IDs remain at most 128 characters.
No Core admission maximum is lowered to make receipts fit.

`SetupCliApplication` owns the only render/delivery attempt. Delegates return/store
typed results; Program never reopens stdout for fallback. Mark delivery started
before writing, and treat broken delivery as exit 74 with fixed best-effort stderr
diagnostics, not as rollback. `delivery: attempt` records intent; the receipt
cannot acknowledge its own receipt. Command completion, nullable/unassessed
preparation, target acceptance and cleanup are independent facts. Target acceptance
stays unassessed because this offline tool never contacts the target.

Prepare all selected destinations before ordered commits, including canonical
definition and optional ID map. Preserve a committed prefix if later work fails.
Regenerate v2 from the compiled schema owner and retire command v1 through owned
generator/package cleanup, preserving unrelated package content and definition
schemas. Independent machine verification must not use production serialization
as its expected-result oracle.

### Cooperative process lifetime

Replace the embedded synchronous execution/I/O surface with
`Task<SetupCliExitCode> RunAsync(invocation, cancellationToken = default)`,
`ValueTask<SetupCliInputBuffer> ReadAsync(path, maximumBytes, cancellationToken)`
and token-aware `WriteAsync`, `WriteArtifactAsync` and `WriteBundleAsync` methods.
The plain write returns `ValueTask`; publication methods return
`ValueTask<SetupCliPublicationResult>` so established outcomes and cleanup stay
observable. The disposable input buffer owns and clears its byte array. Consumers
dispose it on success and failure; readers clear owned scratch buffers as well.
Bound streaming reads without relying on seekability or reported length. This
reduces avoidable byte retention, not immutable JSON/string lifetime.

`Program` owns/disposes one work CTS, Ctrl+C subscription and Unix SIGTERM
registration. Repeated signals request the same cancellation without extra output.
Use the existing Spectre.Console.Cli 0.56.1 `RunAsync` and `AddAsyncDelegate` APIs
with cancellation exit 130. Propagate the work token through handlers, bounded
input/source/schema reads and protected preparation/commit; retain no sync wrapper,
`Task.Run` or blocking async bridge. Observe cancellation between bounded CPU
stages and before commit without promising immediate parser interruption.

Keep final delivery separate: the sole emitter uses a distinct bounded delivery
token, not the cancelled work token. Delivery failure wins with 74. Otherwise
cancellation returns 130 unless an earlier operation or cleanup failure already
determines completion. Native preparations are disposed even when parsing or
outcome observation fails. Established writes survive cancellation and cleanup;
unknown outcomes cannot be recast as not-written. No cancellation path grants
rollback, automatic replay or target acceptance.

### Explicit input and one grammar

Remove ambient environment enumeration, `SetupCliEnvironmentPresence` and the
embedded invocation's environment parameter. Admit answers only through explicitly
selected bounded file/stdin input. Remove secret-word filename/ID bans without
removing bounded ID grammar, control/path checks, definition validation or restricted
publication. Neither ambient names nor substring matches are input authorities;
actual private values must still stay out of arguments and identifiers.

Strict Spectre binding owns command grammar, including equals-form options,
ordinary stdin `-`, the `--` terminator, case sensitivity and repeated bundle
`--artifact`. Preflight only bounds arguments, rejects controls, selects/adjudicates
modes, admits public routes and closes duplicate-singleton gaps. Typed settings'
`ValidateInput` rejects invalid operation inputs before I/O. Do not expose hidden
framework `cli`/`export` routes or retain a second operational parser.

### Structural help and output channels

Use Spectre's command model for machine help at root, branch and leaf scope. The
typed `help` object carries `scope`, `commandPath`, public child `commands`, and
`options` with `name`, `requiresValue` and `repeatable`. Root envelopes identify
`cli`/`help`, branch envelopes family/`help`, and leaves their actual route; none
of this makes `cli` an invocable command. Help requires no operational inputs,
performs no reads and emits no successful compiler handshake.

Keep standard human Spectre help on the injected stdout console, human failures
on stderr, and human status off public-artifact stdout. Render result text with
safe `Text`, not markup. Machine help remains one typed result, not parsed human
output or additional framework diagnostics.

## Consequences and limits

Automation must migrate envelope consumers and CSV bundle arguments together.
Embedded callers must also await the new execution/I/O APIs, forward their work
token and dispose returned input buffers; no synchronous compatibility API remains.
Embedded invocation callers also remove the environment argument. This active
pre-release contract needs no backward-compatibility layer for ambient-name or
substring filtering; existing explicit file/stdin workflows remain the input path.
Validation exits still describe the underlying read-only operation during detail
retrieval. Consumers advance by actual returned entries and guard noninitial pages
with the sanitized-view digest; unchanged structural views may survive value changes.

A healthy channel receives one bounded JSON line. A failed channel can receive
only a prefix or nothing even after successful publication. Consumers inspect
established destinations before retrying; no in-band protocol can guarantee
delivery acknowledgment, rollback or recovery after process loss.

Cooperative cancellation cannot guarantee cleanup or a final receipt after forced
kill, OS crash or a blocked non-cancellable native primitive. Explicit-input/help
work has pre-production Red evidence only here: 119 total, 108 passed, 11 failed;
post-change green is not claimed. Hardened public authoring custody, installed
discovery/repair, portable skill lifecycle and complete installed adoption remain
deferred.

The expanded requested .NET 10 platform matrix covers Linux glibc/musl x64/arm64,
macOS x64/arm64 and Windows 10/11 x64, not Windows 7. It is not yet implemented or
qualified. Current restricted-save qualification remains Linux-only; this decision
does not establish non-Linux protected writers or actual-host equivalence. These
runtime improvements are not enterprise certification.

## Functional provenance and independent design

The repository-native planning handoff recorded official API facts on 2026-10-15.
This document uses that source-free handoff, not external implementation or copied
documentation prose. The relevant sources are:

| Source | Interface or standards fact only |
| --- | --- |
| [Spectre command lifecycle](https://spectreconsole.net/cli/explanation/command-lifecycle-and-execution-flow) | Settings binding/validation precede command execution. |
| [Spectre error handling](https://spectreconsole.net/cli/how-to/handling-errors-and-exit-codes) | Exception propagation changes which handler owns failure mapping. |
| [Spectre testing](https://spectreconsole.net/cli/how-to/testing-command-line-applications) | An injected console supports observing actual command binding/output. |
| [Spectre async commands](https://spectreconsole.net/cli/how-to/async-commands-and-cancellation) | Token-aware execution and async delegates are available in the pinned CLI package, as confirmed by the planning handoff's local API check. |
| [RFC 6901](https://www.rfc-editor.org/rfc/rfc6901.html) | JSON Pointer defines structural member-token escaping. |

Framework API availability does not determine product authority. The single CLI
emitter, pre-sized result ledger, reused protected publisher, owned input buffers,
separate work/delivery lifetimes and count/digest continuation are independently
chosen around Configurator's existing owners and privacy constraints. No package
upgrade, source transfer, new host/container or license approval is inferred;
existing component identities/notices remain intact.
