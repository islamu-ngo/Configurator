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

[2026-10-10 Europe/Brussels] — Published inventories distinguish stale files from transport encodings

**Context**: The maintainer workstream verified actual Browser packages with and without the optional store.

**Symptom / Observation**: The first package suite found a removed stylesheet outside the worker inventory and rejected Brotli/gzip store sidecars. Thirty-nine cases passed and two failed; the final corrected suite passed all forty-one.

**Root Cause**: Publishing into an existing destination preserved retired files. Separately, the store test treated SDK transport encodings as additional logical snapshots rather than encoded representations of canonical JSON.

**Resolution**: Replace the generated Browser destination before publication. Keep the store's closed-file inventory, admitting only canonical files and recognized sidecars that independently decode to exactly those bytes with no trailing data. Both clean Browser publications and the final package owner passed; Operations records exact commands and evidence.

**Why This Matters for Future Work**: A successful incremental publish does not prove package closure. Unknown stale files must fail; legitimate transport representations must prove equivalence rather than being silently excluded from inspection.

**References**:

- `eng/configurator/Configurator.Release.proj`
- `tests/Configurator.Architecture.Tests/BrowserStorePackageTests.cs`
- `docs/internal/PACKAGING.md`
- `docs/internal/OPERATIONS.md`

**Promotion Consideration**: The package destination and sidecar policy are documented in Packaging; retain this entry as the failure mechanism and regression rationale.

[2026-10-28 Europe/Brussels] — Mixed maps need two schema-preserving child collections

**Context**: Actual-client qualification of definition-standard v2 exercised an object with a declared required string child and typed integer `additionalProperties`. Core v2 admission/evaluation was delivered; this was a Desktop projection defect, not deferred map support.

**Symptom / Observation**: A dynamic-only map exercise did not establish that fixed and dynamic children coexist with different schemas. The corrected D19 guard now requires the fixed string caption beside a literal-key integer entry, focuses the missing fixed child and checks an independently specified JSON result (`tests/Configurator.Desktop.NativeHost/Program.cs:540`).

**Root Cause**: A typed additional-properties schema applies only to undeclared keys. Treating every object member as a dynamic entry loses each fixed child's own type, requiredness and public title, and can wrongly give declared children private dynamic-key labels.

**Resolution**: `MapFieldViewModel` constructs stable `Children` from declared property schemas and separate `Entries` from `AdditionalProperties`; both participate in descendants, correction focus and disposal. Entry creation excludes declared names, and maximum-property accounting counts all supplied members, not just dynamic entries. The recorded Phase 12 Red/Green and final Desktop owner passed 229 tests; the affected typed class passed six. Qualification commit `55c951d` records all 19 real X11 guards in English and Arabic, including mixed-map D19. No commands were rerun for this journal append; exact executed commands/results remain with parent-owned Operations.

**Why This Matters for Future Work**: A pure typed-map test cannot prove a mixed map. Use a fixed child whose schema differs from the additional-properties schema and independently verify missing-required correction plus emitted meaning. Preserve one shared answer owner; two schema-specific view collections are projections, not duplicate answer state.

**References**:
- `src/Configurator.Desktop/ViewModels/Fields/MapFieldViewModel.cs:20`
- `src/Configurator.Desktop/ViewModels/Fields/MapFieldViewModel.cs:52`
- `tests/Configurator.Desktop.NativeHost/Program.cs:540`
- `tests/Configurator.Desktop.NativeHost/Program.cs:581`
- `docs/internal/adr/ADR-definition-standard-v2.md`
- `docs/internal/OPERATIONS.md`
- Qualification commit: `55c951d`

**Promotion Consideration**:
- [x] Stays in journal only (regression and actual-surface qualification lesson); the supported recursive shape is already preserved in the v2 ADR/profile.

[2026-10-28 Europe/Brussels] — RTL alignment and presence updates must preserve editing

**Context**: Real Terminal package qualification of definition-standard v2 exercised Arabic direction metadata, Latin fallback/technical text and transitions from omitted to supplied input. These observations complemented shared Core semantics rather than establishing a second client evaluator.

**Symptom / Observation**: Actual probes exposed reversed glyph order under RTL and a stale omitted annotation after inline editing. Fixing metadata display must not replace the editor or disturb its input/correction state.

**Root Cause**: Blanket RightLeft direction reverses Latin fallback and technical text; RTL metadata needs end alignment without that reversal. The presence annotation is a projection of current answer origin and must refresh when the answer changes, without rebuilding the active editor.

**Resolution**: `SetupTerminalWindow` keeps metadata text in logical order and selects `Alignment.End` for RTL. Presence text refreshes inline while retaining editor identity. `RtlLocaleAlignsMetadataWithoutReversingLatinFallback` guards alignment and unchanged answers/revision; `InlineEditsRefreshPresenceWithoutReplacingTheEditor` guards origin-label refresh and the same editor instance. Recorded final Terminal owner: 106 passed, zero failed/skipped. Corrected packages were observed at 82x28 with keyboard navigation, masked literal-key entry, retained invalid-draft correction and typed/null/reset/suggestion actions. The recorded import/prepare/Arabic/protected-save flow matched Browser/CLI bytes and Linux mode 600. These are inherited `55c951d` qualification facts; no build, test or interactive command ran for this append.

**Why This Matters for Future Work**: Locale metadata does not license glyph reversal or editor reconstruction. Verify logical technical text and uninterrupted input separately from output invariance. English/Arabic local-client evidence does not prove translation quality, stakeholder usability, scholarly review, target acceptance or protected save on another host.

**References**:
- `src/Configurator.Terminal/SetupTerminalWindow.cs:410`
- `src/Configurator.Terminal/SetupTerminalWindow.cs:419`
- `tests/Configurator.Terminal.Tests/SetupTerminalGuiAdapterTests.cs:21`
- `tests/Configurator.Terminal.Tests/SetupTerminalGuiAdapterTests.cs:37`
- `docs/internal/OPERATIONS.md`
- `dev/backlog/definition-profile-target-adoption-trials.md`
- Qualification commit: `55c951d`

**Promotion Consideration**:
- [x] Stays in journal only (Terminal rendering/update regression rationale); the v2 ADR retains inert, output-invariant localization and separate qualification boundaries.
