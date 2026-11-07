# Native directory acquisition outside Core

Status: Accepted for the enterprise architecture quality Phase 6 slice.

## Context

Core previously dispatched directory sources, traversed Linux filesystems and
called native APIs. Its compiled architecture verifier allowed all Composition
I/O and did not reject native imports. The directory reader shared node/scalar
budgets but restarted YAML parser-event accounting for every file. It invoked
the barrier before serializing, allowing a later output-limit failure to lose
precedence to a mutation or cancellation at that barrier.

## Decision

Artifacts owns `CompositionDirectoryCompiler`, directory capture/recheck,
traversal limits, the publication barrier and Linux interoperability. Core's
`CompositionCompiler` accepts ordered existing JSON/YAML sources and shares one
semantic budget across them. No additional fragment DTO, assembly, dependency,
compatibility directory source or alias is introduced.

The order is capture, parse/merge, private serialization, one valid-candidate
barrier, cancellation, recapture/equality, final cancellation, return. The first
failed stage wins unless cancellation is observed at that stage's checkpoint.
Failures return empty output. A barrier is coordination, not save authority.

`CompositionBudget` owns checked aggregate YAML parser events.
`EventReader.Advance` charges after each successful parser advance, including
stream/document events and keys/endings, with cancellation checked beforehand.
Standalone definition admission/import create fresh budgets as before. JSON
does not consume YAML events.

Linux uses a fixed 256-byte `statx` identity, validates required masks and
timestamps, selects x64/arm64 flags explicitly and rejects other architectures.
Descriptor-relative `openat2`, bounded file reads, digest snapshots and source
rechecks remain native responsibilities. No alternate ABI or ordinary-read
fallback is permitted. The protected writer is unchanged.

## Consequences and alternatives

Native directory consumers migrate directly to Artifacts. CLI JSON/YAML
comparison retains the in-memory compiler. Browser/Presentation APIs and project
references remain unchanged; final published Browser/package exclusion belongs
to later settled-input verification.

A separate native-input assembly would add another dependency owner without an
independent consumer and was rejected. Compiling files separately would reset
aggregate limits and was rejected. Keeping the Core namespace I/O exception
would retain filesystem authority in the portable engine and was rejected.

Real races use controlled native barriers. ABI layout/flag tests alone do not
qualify another host, libc, filesystem or architecture. See
[protected artifacts](../PROTECTED_ARTIFACTS.md) for the native contract and
[Core definitions](../CORE_DEFINITION_CONTRACT.md) for semantic bounds.
