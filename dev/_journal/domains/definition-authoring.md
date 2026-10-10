# Definition authoring findings

Last Updated: 2026-10-10 Europe/Brussels

## Durable findings

[2026-10-10 Europe/Brussels] — Private ancestors prohibit public-child defaults

**Context**: Independent review of the compact compiler exercised nested privacy declarations through shared runtime admission.

**Symptom / Observation**: A public child default beneath a secret object was admitted before the fix; the focused authoring invariant failed. Checking only each leaf's declared sensitivity lost the parent's effective privacy boundary.

**Root Cause**: `SetupDefinitionCodec.ValidateField` recursively validated children without carrying whether all ancestors were public. Its existing descendant check protected outer defaults containing private children, but did not protect the opposite direction.

**Resolution**: Carry the public-ancestor condition through object and list-property admission. Default and recommended values require both public ancestry and public descendants. The shared codec owns this rule, so authoring and runtime inputs cannot disagree. Exact final verification is recorded in the task context and Operations.

**Why This Matters for Future Work**: Privacy classification flows both downward from ancestors and upward from descendants. A local label cannot grant permission to publish a value hidden by either boundary.

**References**:

- `src/Configurator.Core/Definitions/SetupDefinitionCodec.cs`
- `tests/Configurator.Core.Tests/Definitions/DefinitionAuthoringTests.cs`
- `docs/internal/MAINTAINER_AUTHORING.md`

**Promotion Consideration**: Keep as regression rationale; the privacy invariant is already authoritative in the repository contract.

[2026-10-10 Europe/Brussels] — Admission bounds must apply during expansion

**Context**: Adversarial review examined shorthand outputs and schema-reference reuse in maintainer authoring.

**Symptom / Observation**: Source limits alone permitted a small document to describe millions of generated copy projections or repeatedly expanded help text. Late runtime rejection occurs after those allocations.

**Root Cause**: The compact compiler initially deferred cardinality checks to runtime admission; the importer counted fields and depth without accounting for repeated referenced metadata.

**Resolution**: Check source cardinality before constructing generated arrays, and charge cumulative generated nodes, scalars and bytes while expanding. Runtime admission remains the final semantic boundary. Preserve independent bounded-input regressions; never exercise a multi-gigabyte allocation to prove refusal.

**Why This Matters for Future Work**: A bounded source is not a bounded expansion. Every new shorthand or reference mechanism needs an expansion budget before allocation, including repeated safe public metadata.

**References**:

- `src/Configurator.Core/Definitions/Authoring/DefinitionAuthoringCompiler.cs`
- `src/Configurator.Core/Definitions/Authoring/DefinitionSchemaImporter.cs`
- `tests/Configurator.Core.Tests/DefinitionSchemaImporterTests.cs`

**Promotion Consideration**: Keep as compiler/importer review rationale; data-only admission already requires bounded use.

[2026-10-10 Europe/Brussels] — Embedded schema IDs change reference scope

**Context**: The generated authoring schema reuses the runtime schema's recursive field and projection vocabulary.

**Symptom / Observation**: Retaining the embedded runtime `$id` while rebasing references to `#/$defs/runtime/...` made recursive references resolve against the wrong schema resource. Separately, accepting a nested imported `$id` without implementing its resource semantics could translate a target constraint incorrectly.

**Root Cause**: JSON Schema `$id` is a resource/base identifier, not ordinary descriptive metadata. Physical placement alone does not determine reference resolution.

**Resolution**: Remove the embedded runtime resource ID before rebasing generated references. Refuse imported resource-boundary changes outside the declared subset, including intermediate JSON Pointer ancestors; checking only the selected leaf misses a crossed resource. Reject unsupported URI percent escapes and malformed pointer escapes rather than silently changing their meaning. Regenerate the source schema through its native owner.

**Why This Matters for Future Work**: Functional reuse of schemas must preserve reference semantics. Neither a generated file nor a successful scaffold proves semantic equivalence without that check.

**References**:

- `src/Configurator.Core/Definitions/Authoring/DefinitionAuthoringSchema.cs`
- `src/Configurator.Core/Definitions/Authoring/DefinitionSchemaImporter.cs`
- `eng/configurator/Configurator.DefinitionSchemaGenerator/Program.cs`

**Promotion Consideration**: Keep as the bounded schema reuse lesson; a general resource resolver is outside this contract.

[2026-10-10 Europe/Brussels] — Explicit anchors cannot replace recorded lineage

**Context**: Independent review compared authoring check, build and revise against the default recorded release and an explicitly selected prior snapshot.

**Symptom / Observation**: A genuine CLI regression failed because selecting a different `--against` snapshot bypassed an existing default release's same-family/revision conflict. A custom output destination could then publish a different definition under recorded identity.

**Root Cause**: Explicit anchor selection replaced the default anchor check instead of adding a second check against known history.

**Resolution**: Verify a present recorded default anchor, then verify the explicitly supplied prior snapshot. Revision compares both before no-op or advancement. An output path and a selected comparison file do not erase already known lineage.

**Why This Matters for Future Work**: A caller's chosen evidence can add context, but cannot revoke conflict evidence already owned by the operation. Apply this rule consistently across read-only validation, output generation and identity editing.

**References**:

- `src/Configurator.Cli/SetupCliDefinitionHandlers.cs`
- `tests/Configurator.Cli.Tests/SetupCliDefinitionTests.cs`

**Promotion Consideration**: Keep as lineage regression rationale; canonical family/revision identity is already a critical invariant.
