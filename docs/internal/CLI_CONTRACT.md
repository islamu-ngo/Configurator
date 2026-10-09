# CLI contract

The CLI references only Configurator.Core, Configurator.Artifacts and the admitted
Spectre packages. The public [command usage](../public/cli.md) documents all
options, input semantics, confidentiality and exits.

## Execution

`SetupCliCommandRegistry` is the grammar authority. Spectre settings derive
command metadata; `SetupCliArgumentPreflight` checks the same descriptors before
Spectre can produce diagnostics containing input values. It enforces closed
families/options, single-valued options, explicit file intent, mode exclusivity,
local definition input, bounds and revision grammar. Bind failures emit fixed
codes. Human rendering is isolated from machine output.

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

The single `SetupCliMachineEnvelope` is serialized using source-generated
`SetupCliJsonContext`; it contains fixed diagnostic paths/codes, sorted bounded
coverage and byte digests, never answers or artifact bytes. Definition metadata
IDs are admitted data; free-text titles/help/defaults/choices never enter public
catalogue output. `describe` adds only booleans indicating metadata presence.
Successful preparation has incomplete **host** readiness deliberately: exit 0
means the local operation succeeded, not permission or remote acceptance.

`SetupCliCommandSchemaMetadata.GenerateSchema()` generates the closed
`islamu-configurator-command/v1` envelope plus registry operation/option metadata.
The generator's `--write` and `--check` locate `Configurator.slnx`; `--stdout`
returns schema bytes without needing a repository checkout.

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
