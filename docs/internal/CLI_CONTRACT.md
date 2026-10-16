# CLI contract

The CLI references only Configurator.Core, Configurator.Artifacts and the admitted
Spectre packages. The public [command usage](../public/cli.md) documents all
options, input semantics, confidentiality and exits.

## Execution

`SetupCliCommandRegistry` declares the public command tree and typed settings;
strict Spectre binding is the sole grammar authority. It admits `--option=value`
and `--option value`, ordinary `-` values and the `--` option terminator, with
strict command/option case and repeated bundle `--artifact`. The terminator does
not admit undeclared positional operands. Framework-owned `cli`/`export` routes
are not public commands.

`SetupCliArgumentPreflight` is not a second parser. Its checks are limited to
bounded arguments, control characters, mode selection/conflicts, public-route
admission and duplicate-singleton gaps. After Spectre binds settings, typed
`ValidateInput` checks operation requirements, explicit file/stdin intent and
revision/format constraints before any I/O. Binding and validation failures map to
fixed value-safe codes rather than leaking framework diagnostics. No secret-word
substring filter rejects filenames or admitted IDs, and no ambient environment
enumeration supplies inputs or rejects an invocation.

`SetupCliInvocation(arguments, mode, io)` has no environment constructor argument;
`SetupCliEnvironmentPresence` is removed without a compatibility adapter. Answers
come only from explicitly selected bounded files or redirected stdin. Removing
ambient/substring filtering does not relax definition admission, path/control
checks or protected output, and actual private values still do not belong in
arguments, filenames or identifiers.
Both invocation admission and preflight bound arguments to 256 tokens, 32,768
aggregate UTF-8 bytes including one separator byte per token, and 4,096 characters
per argument. Artifact IDs remain bounded to 128 admitted ASCII characters.

### Help and human rendering

Root, branch and leaf help use Spectre's command model without requiring operational
inputs or reading files/stdin. `SetupCliHelpProvider` produces a typed help view
for machine mode: `scope` (`root`, `branch`, `leaf`), `commandPath`, public child
`commands` and `options` (`name`, `requiresValue`, `repeatable`). Machine root help
uses `cli`/`help`, branch help uses the selected family/`help`, and leaf help retains
the actual route. The root result identifier does not expose a `cli` command.
Help has no successful `definition check`/`build` compiler-version handshake.

Human help keeps standard Spectre formatting through the injected console and
goes to stdout. Human failures go to stderr. Public catalogue artifact stdout is
not contaminated by status text: human status goes to stderr when `--output -`
is selected. Human result content uses safe `Text`, not markup interpretation;
machine mode never scrapes human help or emits its formatting.

### Bounded operation execution

`SetupCliIoOperations` reads bounded bytes, admits a local definition using
`SetupDefinitionCodec`, rejects duplicate answer properties and snapshots JSON
answers with `SetupAnswerSnapshot`. `SetupCliArtifactHandlers` uses
`DefinitionValidator`, `DefinitionArtifactComposer` and the bounded
`DefinitionImport` inverse; no compiled target DTO, catalogue or transport occurs.
For ordinary artifact operations, input represents answers. `artifact
check-existing` and `artifact prefill` instead read a local projected file for
one selected artifact. Only present, unconditional top-level `copy` members of
a JSON, YAML or dotenv root object can prefill answers. Duplicate, unknown,
conditional, transformed, inactive, invalid or conflicting input rejects the
whole candidate. Missing fields stay missing; this does not establish full
native readiness or target acceptance. `--answers` on these two operations is
an optional existing answer workspace, and `prefill` writes one new restricted
answer snapshot to a create-only destination. Its values never enter the
machine envelope.
Host-specific imports must describe their fields/request projection as data.
With `--answers`, input is a prepared artifact and the answers provide a
recomposition proof: bounded Core JSON/YAML normalization, dotenv parsing, or
exact Markdown comparison checks it against the composer's output. A mismatch
returns data failure without publication. Formatting writes canonical composer
bytes rather than copying untrusted input. Artifact-source diff compares the
bounded baseline with the proven candidate; it never claims target acceptance.
Import-semantic artifacts publish only through `artifact import`; other write
operations reject before reading answers so they cannot bypass revision binding.
`open` and `validate` compose without writing; `coverage` derives required/provided
facts; `diff` compares composed bytes without value disclosure.

`artifact bundle-check` and `artifact bundle-create` take repeated `--artifact ID`
options (at most 64) and one answer snapshot. CSV `--artifacts` is removed without
an alias. Core composes the selected
artifacts in definition order with an aggregate byte limit before native output.
Import-semantic artifacts are excluded so a bundle cannot bypass explicit
revision binding. The check reports local composition metadata only. Create
passes the complete ordered bundle through `ISetupCliWriter.WriteBundleAsync` to
`ProtectedArtifactBundlePublisher`: all destinations are prepared before the
first commit, then create-only commits may leave a written prefix if a later
race or I/O failure occurs. The machine envelope reports the full ID, established
digest, sensitivity, path intent and value-free write status per selected artifact;
it never includes filenames, directory paths or bytes. Dry run
composes but does not preflight destinations or write. Partial publication is
an unsuccessful exit and does not roll back already written files.

The bundle writer forwards established native outcomes to the invocation ledger
as they occur, before returning its aggregate result. An unexpected later exception
therefore cannot erase a known written prefix. Entries with no established outcome
remain `unknown` after publication starts; exceptional cleanup remains `unknown`
unless its result is established. A normal aggregate result still distinguishes
untouched `not-attempted` destinations from uncertain publication.

Import preparation requires declared import semantics, an explicit expected
revision and a declared string revision field. The selected projection must copy
it unconditionally to a root object member, and validation must confirm it is
active/provided/valid. A conflicting answer rejects. This intentionally rejects
unsupported revision shapes rather than pretending a revision check happened.
Target integrity validation is not duplicated here.

## Publication and machine output

`ISetupCliWriter.WriteArtifactAsync` has no default plain-file implementation.
The native adapter calls `IProtectedArtifactWriter.PrepareAsync(kind, path,
bytes, cancellation)` and commits the preparation once. Public catalogue stdout
uses `ProtectedArtifactWriter.WritePublic`. Answer-derived output is classified
restricted before any writer invocation; stdout rejects even on dry run.
Linux create-only owner-only protection and platform rejection remain writer-owned.
No insecure fallback or network apply exists.

`SetupCliOutcomeLedger` retains established publication facts independently of
the command exit. `SetupCliResultPlan` seals and sizes the mandatory result
before effects, reserving bounded outcome slots and optional-detail space.
An impossible response budget rejects before publication, rather than publishing
and then discovering that the receipt cannot fit. The normal maximum is 65,536
UTF-8 bytes including its trailing LF, not a character count; `MaximumOutputBytes`
names that boundary. Definition maxima are not reduced to make results fit.

The single `SetupCliMachineEnvelope`, serialized with source-generated
`SetupCliJsonContext`, separates root `completion`, nullable/unassessed
`preparation`, `targetAcceptance: "unassessed"`, `cleanup` and `delivery: "attempt"`.
Local preparation does not grant target acceptance, and receipt delivery cannot
self-attest acknowledgment inside those same bytes. Root coverage has exact
covered/missing counts plus a sanitized-view SHA-256 digest; readiness has state
and missing/blocked counts. Per-artifact coverage/readiness arrays are not emitted.
Every output ledger entry retains its full ID, established digest, sensitivity,
path intent and write status. Known `written`, untouched `not-attempted` and
indeterminate `unknown` outcomes survive later failures; unknown is never inferred
to mean not-written. Conflicts, blocked destinations and failed commits retain
their specific statuses. Cleanup failure is independent of publication success.

`SetupCliApplication` is the sole renderer/emitter. Runtime delegates return/store
typed results; Program does not reopen stdout or emit fallback JSON. Delivery
starts before the first write. Healthy machine delivery produces exactly one
UTF-8 JSON line without BOM/ANSI; failed delivery returns 74 with fixed best-effort
stderr diagnostics, even if stderr also fails. No appended envelope, rollback
claim or automatic retry follows partial stdout. Process exit is separate transport
evidence; consumers must inspect established outputs before replaying a failed write.

### Async lifetime and owned input

The cooperative lifetime contract replaces the embedded synchronous entrypoints;
callers migrate directly, without compatibility wrappers:

- `SetupCliApplication.RunAsync(invocation, cancellationToken = default)` returns
  `Task<SetupCliExitCode>`.
- `ISetupCliInput.ReadAsync(path, maximumBytes, cancellationToken)` returns
  `ValueTask<SetupCliInputBuffer>`. The disposable buffer owns its `byte[]` and
  clears it on disposal; the consumer must dispose it even when parsing fails.
- `ISetupCliWriter.WriteAsync(..., cancellationToken)` returns `ValueTask`;
  `WriteArtifactAsync(..., cancellationToken)` and
  `WriteBundleAsync(..., cancellationToken)` return
  `ValueTask<SetupCliPublicationResult>`, preserving completion, publication and
   cleanup facts rather than discarding them in an exception path.

Embedded bundle writers also accept an outcome observer and report established
per-artifact facts before later work can throw. Migrate implementations directly;
there is no overload that silently drops those observations.

`Program` owns one invocation CTS, the Ctrl+C subscription and, on Unix, the
SIGTERM registration. It disposes registrations and the CTS on exit. Repeated
signals request the same work cancellation without new output. The installed
Spectre.Console.Cli 0.56.1 `RunAsync(args, token)` and
`AddAsyncDelegate<TSettings>` carry the work token through binding/execution,
family handlers, bounded file/stdin and source/schema reads, and native
preparation/commit. Spectre's cancellation exit is explicitly 130. No `Task.Run`,
blocking `GetResult` bridge or synchronous public execution wrapper is retained.

Reads enforce the byte cap while streaming, including non-seekable input, rather
than trusting stream length. Native `SystemInput.ReadAsync` rejects `-` unless
`Console.IsInputRedirected`, before opening or reading stdin. The resulting
`IOException` maps to safe I/O failure (74) through the ordinary single emitter;
there is no prompt or terminal-read support. Unix pipes use the existing async
standard-stream adapter with owning handles so disposal completes after
cancellation; non-pipe descriptors retain the Console stream fallback.
Original and scratch byte buffers owned by the CLI
are cleared on every exit path, and native preparations are disposed even when
parsing, outcome observation or cleanup fails. Cancellation is checked between
bounded CPU stages and before commit; it does not promise immediate interruption
of parsing or erasure of immutable JSON/string allocations.

`SetupCliApplication` makes its sole final-delivery attempt under a distinct,
bounded delivery token, not the cancelled work token. Delivery failure returns
74 even when operation or cleanup also failed. Otherwise cancellation returns
130 unless an earlier operation/cleanup failure already determines completion;
later cancellation must not mask that failure. Ledger facts are independent:
established writes survive cancellation, untouched outputs remain not-attempted,
and unknown publication never becomes an inferred not-written result. Forced
kill, OS crash and non-cancellable native I/O cannot guarantee cleanup or delivery.

Linux native process tests hold stdin open and establish pipe consumption before
sending SIGINT/SIGTERM, including repeated SIGTERM and broken-stdout delivery
precedence. A real `/usr/bin/script` pseudo-terminal qualifies immediate terminal
stdin rejection without EOF; only its CRLF transport conversion is normalized
before machine-contract verification. Shell-redirected regular-file stdin/stdout
qualifies native artifact bytes and a single machine receipt, not cancellation
of a stalled filesystem call. Other hosts' signal behavior remains unqualified.

Public `definition build` also prepares all canonical-definition/optional-ID-map
outputs before ordered commits. This is not a transaction: later failure preserves
the earlier canonical write and its ledger entry. Public authoring writes remain
separate from the native restricted-artifact authority.

Definition metadata IDs are admitted data; free-text titles/help/defaults/choices
never enter public catalogue output. `describe` adds only booleans indicating
metadata presence. Successful preparation deliberately remains
`host-validation-required`: exit 0 is local success, not permission or acceptance.

### Sanitized diagnostics and detail continuation

Diagnostic `path` uses bounded RFC 6901 pointers, with an empty pointer for the
document root and `~0`/`~1` escaping for member tokens. Optional numeric `line`
and `column` are one-based; column counts Unicode scalars. `byteOffset` is
zero-based in the original UTF-8 source. The v2 slots do not implement deferred
Core source mapping or repair. No raw exception, answer value or private path is
part of the machine view.

At most 16 diagnostic examples are inline. Examples and any requested detail page
share one 8,192-byte optional budget. `diagnosticsTotal` and `diagnosticsOmitted`
remain exact, and specific publication/cleanup error codes remain reportable
without replaying effects. Detailed lists are not repeated in every artifact.

`--detail diagnostics|coverage`, `--offset` (0–2,048, default 0), `--limit`
(1–32, default 16) and `--expected-detail-digest` are admitted only by read-only
`definition check`, `artifact validate`, `artifact check-existing` and
`artifact bundle-check`. A page includes kind, offset, requested limit, total,
digest and sanitized diagnostic or coverage entries. Byte limits can return fewer
entries than requested; advance by the actual count. The ordinary operation exit
continues to describe validation, not just page retrieval.

Noninitial pages require the expected digest; any supplied mismatch rejects with
`detail-digest-mismatch` and performs no publication. The digest binds the public
definition digest, operation, selected IDs and sanitized structural/status view,
including covered/missing IDs and safe diagnostics. It never hashes private
answers or persists them. Same-view private-value changes need not change it.

`SetupCliCommandSchemaMetadata.GenerateSchema()` generates the closed
`islamu-configurator-command/v2` envelope plus registry operation/option metadata.
The generator's `--write` and `--check` locate `Configurator.slnx`; `--stdout`
returns schema bytes without needing a repository checkout.
The native generator retires command v1 through owned-output cleanup; the
`RefreshConfiguratorSchemas` packaging stage refreshes only owned packaged schemas.
Authoring/runtime v1 remain current: changing the command receipt does not change
their vocabularies. The independent specification/verifier must migrate with the
compiled schema, rather than using production serialization as its oracle.

The [runtime decision](adr/ADR-cli-command-runtime.md) records result, cooperative
lifetime, explicit-input parsing, help and delivery ownership. Contract tests exercise
the admitted grammar and typed help; native process tests separately exercise
signal handling, inherited environments, stream routing and receipt framing.

Public-file custody, installed discovery/repair, portable skill lifecycle and
installed adoption evidence remain open. The expanded requested target is .NET 10
on Linux glibc/musl x64/arm64, macOS x64/arm64 and Windows 10/11 x64; Windows 7 is
unsupported. Packaging admission is documented separately in [PACKAGING.md](PACKAGING.md);
the expanded actual-host matrix is not qualified, and native
restricted-save qualification remains Linux-only. No non-Linux protected writer,
actual-host equivalence or enterprise certification is implied.

## Invariant disposition and Event handoff

Generic replacements retain command closure, deterministic single framing,
culture independence, bounded I/O, duplicate/control/path rejection, explicit
stdin/output, private stdout refusal, dry-run isolation, native create-only
permissions, no ambient input discovery, safe parse failures,
digest/exit/readiness/schema checks, real composition and explicit revision binding.
Tests exercise actual CLI execution and real native processes/writer behavior,
not mock call counts or source/prose scraping.

The former CLI `EveryPortabilityOperationUsesRealCoreArtifacts` and section-diff
vectors used Event manifest/tenant DTO acceptance. The specialized operator
identity YAML/export/import roundtrip and tamper vectors used Event identity
content digests and revision hashes. Their **target-specific** acceptance/integrity
oracles belong in Event exporter/backend contract tests. They are not claimed as
generic equivalents or silently considered passing here. Generic CLI tests
separately cover projection determinism, canonical JSON/YAML output, revision
injection/conflict/rejection and non-disclosure. Compiled activation/provider
selection remains Event-owned definition data; generic activation/readiness is
Core/CLI tested.
