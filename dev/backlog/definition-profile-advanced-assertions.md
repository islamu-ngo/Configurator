# Definition profile assertions beyond the selected v2 contract

Last Updated: 2026-10-28 Europe/Brussels
Status: Conditional extension; not implemented or authorized.
Owners: Core definition admission/evaluation and maintainer authoring maintainers.
Trigger: A named target demonstrates a constraint that cannot be represented by
the delivered bounded v2 profile, with an independent target-owned specification.

## Outcome and current boundary

Evaluate one evidenced extension at a time, not general JSON Schema compliance.
The [v2 ADR](../../docs/internal/adr/ADR-definition-standard-v2.md) preserves
the choice of one Core evaluator. Recursive arrays, fixed properties plus typed
additional properties, nullable values, local acyclic references,
dependentRequired, bounded if/then/else and discriminated oneOf are delivered
v2 behavior, not work deferred to this backlog.

Candidates are general combinators/unions, arbitrary regex, tuples and recursive,
dynamic or remote references. Listing them is not support or an obligation to
admit all of them. Existing refusal remains correct until a separately approved
profile change is qualified. Remote retrieval additionally needs an independently
approved launch/admission authority contract; an assertion change cannot grant it.

## Boundaries

- Preserve one immutable admitted model and Core evaluation authority, exact
  decimal/Int64 and Unicode/key semantics, privacy floors and value-free locations.
- Bound source, reference expansion and evaluation before expensive allocation;
  specify failure without partial admission or answer/output retention.
- No v1 interpreter, executable validator, expression engine, dynamic execution,
  answer-bearing retrieval, arbitrary file/network authority or implicit install.
- Source-free functional requirements and provenance govern externally informed
  design. A library proposal requires a separate dependency/license and actual
  Browser/WASM/package closure decision; this item selects no dependency.
- No new output format, target acceptance, native custody repair, other-host
  support, Event migration, hosted publication or signer activation is included.

## Acceptance criteria

1. A scoped decision names the target constraint, exact proposed vocabulary and
   supported/refused cases, and compares existing-profile modeling with the
   extension. If equivalence or bounds cannot be demonstrated, retain refusal
   and record why rather than claiming general support.
2. Independent conformance vectors specify semantics and rejection, including
   nested privacy, exact numbers/keys, invalid controllers, resource exhaustion
   and safe diagnostics. Unsupported assertions cannot be acknowledged away by
   the schema importer.
3. The same admitted semantics govern source compilation, runtime admission,
   complete/draft evaluation, initialization, composition and any supported
   inverse; no client-side evaluator or silent approximation is introduced.
4. Required capabilities are computed and refused by unsupported consumers
   before answers. Changed shared/client behavior has actual surface evidence,
   separate from Core tests and separate from target-owned validation.
5. Owning generated schemas/capability metadata, importer transformation
   accounting and public/internal references ship together with exact scoped
   verification and any dependency/provenance decision.

## Related ownership

[Maintainer extensions](maintainer-authoring-extensions.md) owns broader parsing
and retrieval boundaries. [Target trials](definition-profile-target-adoption-trials.md)
can supply the trigger but do not authorize implementation. The current
[profile](../../docs/internal/DEFINITION_PROFILE.md) remains authoritative.
