# Core directory-source boundary

- Status: Required focused follow-up; relocation not implemented by cross-platform quality.
- Owners: Core and Artifacts maintainers; client maintainers for actual callers.
- Origin: cross-platform-quality graduation, 2026-10-27.
- Decision: [ownership ADR](../../docs/internal/adr/ADR-cross-platform-quality-ownership.md).
- Related obligations: [required-followup ledger](cross-platform-quality-required-followups.md).

## Current exception and mechanism

`SetupCompositionCompiler.CompileAsync` routes JSON/YAML byte sources through pure
normalization but routes `SetupCompositionDirectorySource` through Core's
`SetupCompositionDirectoryReader`. That reader performs Linux filesystem and
native-handle I/O. This is a deliberate existing portability exception, not
evidence that Core is entirely I/O-free or that Browser can acquire directories.
Other hosts return `UnsupportedFilesystem`; do not introduce a pathname fallback.

The reader captures the directory tree, parses bounded fragments, merges them,
awaits the publication commit barrier and captures again. Stable identity checks
across both captures reject changes before canonical output becomes available.
Root/path/opened-handle comparisons, descriptor-relative opens, link/special-file
rejection, single-link regular-file checks and ordered paths are substantive
invariants, not incidental plumbing to discard during a move.

`SetupCompositionLimits` currently bounds 256 directories, 1,024 files, 256
entries per directory, 512 relative-path characters, depth 16, 524,288 bytes per
file and 4,194,304 aggregate directory/source bytes. Aggregate directory nodes
are 65,536; parser/normalizer/scalar ceilings also remain enforced. A byte-reader
helper alone does not preserve this tree snapshot, path authority or commit
barrier contract.

## Activation and bounded outcome

Before relocating, map actual `SetupCompositionDirectorySource` callers and
their native/Browser closure. Activate the boundary change when a real caller,
portability requirement or compiled dependency restriction needs it; the durable
exception itself does not authorize a speculative filesystem framework.

Keep pure parsing, merge, canonical bytes/digest and failure vocabulary in Core.
Place native directory acquisition with the native owner only after establishing
the smallest data-only handoff that preserves tree identity and the two-capture
commit check. If that handoff cannot preserve authority, present the concrete
contract decision instead of making Core trust caller-supplied validation.
Do not equate `BoundedFileInput`'s delivered single-file open/stat/read ownership
with directory traversal or add upward Core -> Artifacts references.

## Required acceptance

- Record the current callers and package closure before edits; migrate all actual
  consumers in one coherent source/caller/documentation slice without aliases.
- Preserve independent literal canonical-byte/digest/order and limit oracles for
  JSON/YAML/directory inputs. Keep non-directory computation filesystem-free.
- Use deterministic barriers around capture and publication to reject root,
  ancestor, directory and file replacement, links/special entries, changed
  contents/identity, competing mutation and cancellation. Reuse existing tests
  where they already establish the invariant; add a regression only for a gap.
- Retain all source/node/scalar/tree bounds and value-free failures. Preserve
  `SourceChanged`, unsupported-host refusal and observed physical cleanup.
- Verify compiled dependency direction plus actual caller/package execution on
  each newly claimed host. Cross-publishing or a mocked directory is not native
  authority evidence. Update public and technical contracts together if the
  acquisition entrypoint or supported-host boundary changes.

## Source anchors and exclusions

- [Compiler dispatch](../../src/Configurator.Core/Composition/SetupCompositionCompiler.cs)
- [Directory capture and native identity](../../src/Configurator.Core/Composition/SetupCompositionDirectoryReader.cs)
- [Composition limits](../../src/Configurator.Core/Composition/SetupCompositionLimits.cs)
- [Core contract](../../docs/internal/CORE_DEFINITION_CONTRACT.md)
- [Native protection evidence](configurator-native-host-evidence.md)

No product edit, new platform implementation, dependency, new scheduler, generic
filesystem abstraction, protected-save expansion or directory API redesign is
delivered by this backlog. Arbitrary typed operation-result migration also stays
deferred unless a real consumer contract requires it.
