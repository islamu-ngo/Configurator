# CLI contract

The CLI references only Configurator.Core, Configurator.Artifacts and the admitted
Spectre packages. The public [command usage](../public/cli.md) documents all
options, input semantics, confidentiality and exits.

## Execution

`SetupCliCommandRegistry` is the grammar authority. Spectre settings derive
command metadata; `SetupCliArgumentPreflight` checks the same descriptors before
Spectre can produce diagnostics containing input values. It enforces closed
families/options, singleton options except repeated bundle `--artifact`, explicit file intent, mode exclusivity,
local definition input, bounds and revision grammar. Bind failures emit fixed
codes. Human rendering is isolated from machine output.
Both invocation admission and preflight bound arguments to 256 tokens, 32,768
aggregate UTF-8 bytes including one separator byte per token, and 4,096 characters
per argument. Artifact IDs remain bounded to 128 admitted ASCII characters.

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
passes the complete ordered bundle through `ISetupCliWriter.WriteBundle` to
`ProtectedArtifactBundlePublisher`: all destinations are prepared before the
first commit, then create-only commits may leave a written prefix if a later
race or I/O failure occurs. The machine envelope reports the full ID, established
digest, sensitivity, path intent and value-free write status per selected artifact;
it never includes filenames, directory paths or bytes. Dry run
composes but does not preflight destinations or write. Partial publication is
an unsuccessful exit and does not roll back already written files.

Import preparation requires declared import semantics, an explicit expected
revision and a declared string revision field. The selected projection must copy
it unconditionally to a root object member, and validation must confirm it is
active/provided/valid. A conflicting answer rejects. This intentionally rejects
unsupported revision shapes rather than pretending a revision check happened.
Target integrity validation is not duplicated here.

## Publication and machine output

`ISetupCliWriter.WriteArtifact` has no default plain-file implementation.
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

The [runtime decision](adr/ADR-cli-command-runtime.md) records result and delivery
ownership. Cooperative async/cancellation and explicit-input parser/environment
changes are separate work, not guarantees of this receipt increment. Public-file
custody, installed discovery/repair, portable skill lifecycle, four actual host
qualifications and adoption evidence remain open; no enterprise certification is
implied.

## Invariant disposition and Event handoff

Generic replacements retain command closure, deterministic single framing,
culture independence, bounded I/O, duplicate/control/path rejection, explicit
stdin/output, private stdout refusal, dry-run isolation, native create-only
permissions, environment-name rejection without value reads, safe parse failures,
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
