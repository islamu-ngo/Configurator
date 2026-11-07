# Recursive workspace root

> **Status:** Accepted for enterprise architecture quality Phase 1
> **Date:** 2026-11-04
> **Owner:** Presentation

Core admits typed root maps and discriminated root variants. The former
Presentation storage assigned each accepted value to a statically declared
field. Additional root entries had no owner, so import correctly refused
adoption instead of silently losing them.

`DefinitionAcceptedEditingState.Root` now owns all supplied configuration.
Ordinary field state accesses its property through that owner and retains only
editor metadata. Detached document candidates copy the complete accepted root,
origins, omissions and logical rows; adoption swaps one aggregate under the
existing Monitor gate. No synchronized second answer tree is maintained.
Core continues to own validation, effective values and branch selection.

The empty structural coordinate addresses the root. Shared mutation and
explicit private-coordinate access support root maps and variants without a
stored wrapper property. `DefinitionPath` overloads carry literal configuration
coordinates independently of ordinary facade identifiers; accepted omissions
also use literal coordinates. This prevents an additional key from aliasing a
declared property's generated identifier. Diagnostic projections use declared schema properties
and typed map/row ordinals. Private dynamic keys are not diagnostic metadata.

Logical branch authority changes when branches retire, including A to B to A.
Unaffected field facades remain stable. Revision and operation generations still
fence import, document, secret and prepared-output completions and retain
physical buffer cleanup responsibility after logical retirement.

Terminal, Desktop and Browser continue to admit ordinary property roots and
supported nested shapes. `DefinitionEditingSupport` rejects new unsupported
root placements before those clients collect answers. Their root controls and
actual surface qualification belong to later plan phases.

Phase 2/3 implementation follow-through: `RootFieldViewModel`,
`TerminalRootEditor` and `ConfigurationRoot` now place the existing recursive
controls at the empty literal path. Client root-placement guards have been
removed for those admitted shapes. Shared action facts prevent root omission
and array-row field reset; collection removal remains explicit. The paragraph
above records the Phase 1 rollout boundary, not a current renderer restriction.
Owner tests and actual local native runs are recorded separately from final
published Browser, assistive-technology and package qualification.

Restricting Core to property roots would discard admitted configurability.
Adapter-owned root dictionaries would split answer custody and require
synchronization. Both alternatives are rejected. There are no compatibility
aliases, parallel accepted models or new dependencies.

Verification belongs to `DefinitionRootEditingTests`, the affected Presentation
privacy/import/document cases and each client's ordinary workflows and placement
admission regressions. Actual host, assistive technology and release
qualification remain separate evidence.
