# Presentation & Clients Findings

Last Updated: 2026-10-15 Europe/Brussels

Use [journal guidance](../README.md), [the finding template](../FINDING_TEMPLATE.md)
and [promotion rules](../PROMOTION_RULES.md) for recording and promotion.
This is Configurator-owned evidence; Event findings are not copied as
Configurator history.

## Durable Findings

Append observed target findings chronologically using the template.

[2026-10-09 Europe/Brussels] — Browser caches are separate from answer persistence

**Context**: While implementing `browser-complete-configuration`, the existing
browser capability ratchet described all persistence as disabled. Published PWA
assets and explicitly retained public definitions need retention without
changing private answer custody.

**Symptom / Observation**: The original capability file had `persistence=false`
and no separate public-cache capability. The new generator emits explicit
public asset/definition cache flags while keeping `persistence=false`,
`answerPersistence=false` and `nativeProtectedSave=false`. Native regeneration
produced all three ratchets successfully; generation alone is not platform QA.

**Root Cause**: A single persistence flag conflates public distribution assets
with transient private workspace state. Browser answer/output custody and
native protected-save support are separate contracts from static asset caching.

**Resolution**: Keep generator-owned capabilities explicit, require consent for
secret entry and user action for restricted export, and check the actual browser
package's Presentation/Core closure, PWA files and capability values.
`TMPDIR="$HOME/.cache/agent-tmp" dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --write`
returned exit 0 and `Generated Configurator ratchets (3/3).` Exact published
network/cache/update evidence remains separately owned by Operations.

**Why This Matters for Future Work**: Do not interpret public offline retention
as permission to persist answers, send private worker messages, or offer native
filesystem guarantees. Consumers of capability metadata must distinguish these
flags rather than infer private capabilities from a PWA package.

**References**:

- `eng/configurator/GenerateConfiguratorRatchets.cs`
- `eng/configurator/generated/browser-release-capabilities.json`
- `tests/Configurator.Architecture.Tests/StandalonePackageTests.cs`
- `docs/internal/adr/ADR-browser-session-trust-and-export.md`
- `docs/internal/OPERATIONS.md#browser-custody-and-offline-verification`

Source register and source-free functional handoff: the browser ADR records
official Microsoft/MDN sources accessed through Firecrawl on 2026-10-09.
For this documentation/capability slice, only platform interface/lifecycle facts
informed requirements; no external implementation, prose, artwork or dependency
was copied. Generator layout, compiled/package checks and documentation were
independently designed around existing Configurator owners. AFC/SSO review
separates platform-required worker/manifest identifiers and common privacy
constraints from discretionary repository structure. This attests engineering
provenance, not legal certification or unexecuted browser evidence.

**Promotion Consideration**: Already reflected in the browser ADR, packaging
contract and browser/privacy rule twins; retain this entry as the source and
capability-separation record.

[2026-10-09 Europe/Brussels] — Browser download hints do not guarantee artifact names

**Context**: The parent execution track tested the actual published Browser
against the neutral independent dotenv/JSON/YAML vectors.

**Symptom / Observation**: Chrome downloaded the `.env` artifact as `env.txt`
even though the download link carried the declared `.env` filename hint. The
artifact-content checks passed; the filename was normalized by the browser.

**Root Cause**: Browser/OS download custody includes filename handling outside
the adapter's authority. Setting the download attribute does not establish
native filename or filesystem-permission guarantees.

**Resolution**: Public browser guidance requires checking the actual saved name
and renaming to the target's declared filename before use. No new archive format,
dependency or native-save fallback was added to conceal the platform boundary.

**Why This Matters for Future Work**: A correct synthetic output vector and
download hint are insufficient proof that a target can discover its expected
file. Real-browser QA must inspect both content and saved filename.

**References**:

- `src/Configurator.Browser/wwwroot/browser-artifacts.js`
- `docs/public/browser.md#copy-and-download`
- `docs/internal/LAUNCH_CONTRACT.md`
- `docs/internal/OPERATIONS.md#browser-custody-and-offline-verification`

**Promotion Consideration**: Already recorded in public/operator and technical
browser guidance; retain as observed platform evidence, not a guarantee of
identical normalization in every browser.

[2026-10-15 Europe/Brussels] — Cancellation separates private readiness from physical completion

**Context**: Terminal hardening introduced asynchronous local import and truthful
protected-publication recovery using the existing shared workspace and writer.

**Symptom / Observation**: Faulting completion observers could leave accepted
private bytes live. Cancelling a workspace could finish its visible operation
while a writer had already created files. Cleanup failure could hide a Written
status, and an already-empty private clear could admit a pending secret import.

**Root Cause**: One visible operation state cannot represent accepted-outcome
ownership, cancellation callback retirement, owner-side import admission and
irreversible native publication. Cancellation also does not undo earlier effects.

**Resolution**: Match active and accepted outcome identities atomically; preserve
worker/callback ownership until retirement and deliver notifications outside
gates. Clear private roots in one revision even when empty to fence pending
imports. Record native status before observers, cancellation checks and cleanup.
Keep bounded value-free publication receipts independent of current private
readiness, and serialize physical imports/publications until their owned work
finishes. Deterministic barriers reproduced the faults; supported-host tests
verify every committed prefix against actual files and modes.

**Why This Matters for Future Work**: UI cancellation can release the visible
workspace without ending physical work. Never use a stale-result fence as proof
of rollback, buffer retirement or filesystem state. Fault cleanup must target
its accepted result without clearing a reentrant replacement. Recovery status
must retain known success even when subsequent observation or cleanup fails.

**References**:

- `src/Configurator.Presentation/Presentation/SetupPresentationWorkspace.cs`
- `src/Configurator.Terminal/SetupTerminalImportOperation.cs`
- `src/Configurator.Terminal/SetupTerminalArtifactOperation.cs`
- `src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs`
- `tests/Configurator.Presentation.Tests/SetupPresentationModelTests.cs`
- `tests/Configurator.Terminal.Tests/SetupTerminalPublicationReceiptTests.cs`
- [Terminal lifetime ADR](../../../docs/internal/adr/ADR-terminal-editing-and-publication-lifetimes.md)

**Promotion Consideration**: The ADR and existing Presentation/Terminal/protected
output anchors contain the maintained contracts. Retain this entry as the
observed root-cause record. Official API facts and independent implementation
provenance are recorded in the ADR; native terminal qualification remains a
separate release-support gate.

[2026-10-22 Europe/Brussels] - Cached form facts do not remove adapter root scans

**Context**: Published-browser qualification followed the revision-owned form
computation and selective rendering changes. Unit tests were green, but the
active Event disclosure budget still needed real frame evidence.

**Symptom / Observation**: Trusted Event disclosure still occupied roughly
1,017-1,107 ms after shared facts were cached. Browser CPU sampling showed the
time in WASM execution, not new requests. A subsequent repaired sample reached
the requested frame in 31.6 ms. These were diagnostic samples, not percentiles.

**Root Cause**: The Browser adapter rebuilt section membership repeatedly with
`Fields.Single` for every declared root, and filtered each root by scanning the
complete validation field list. Shared origin reads also searched roots
repeatedly. A cache at the validation owner did not remove quadratic traversal
in its consumers.

**Resolution**: Index pinned form references once per editor/form replacement,
use keyed field-state lookup during rendering, and use the existing shared root
position index for origin/draft reads. Keep view lifetime distinct from answer
revision and do not introduce an independent Browser evaluator or answer clone.
Presentation 151/151 and Browser 182/182 passed after the shared read repair.
Full final qualification remains recorded separately in Operations.

Later activation qualification exposed the same pattern when new field components
mounted: `SetupFieldDefinition.Id` decodes JSON, so a seemingly simple per-field
`Fields.Single` scan allocated IDs repeatedly. `GetFieldEditor` now reads the
existing literal-ID index and returns the owned input and schema; rendering no
longer clones root constraints. Canonical root IDs also serve budget/snapshot
loops, and root origin reads avoid empty iterator pipelines. Unchanged ordering
reuses immutable entries; closed discovery/Review defer their value-free entry
graphs through static snapshot factories, without capturing the live form.

The retained 40-sample activation run on Store inventory `b0763bf0...767eee`
passed: activating p95/max acknowledgement was 42.1/48.4 ms at 1x and
180.7/199.6 ms at 4x. All intermediate failures remain. These are reference-host
results, not a broader device claim.

The scale experience exposed a separate mounting/focus cost. Caching a control
identity per parameter application, retaining canonical section IDs and skipping
irrelevant root-identity traversal were valid allocation reductions, but their
measured candidates still exceeded maximum acknowledgement. An unnamed WASM
interpreter profile did not isolate individual managed-method CPU shares.
Actual focus inspection showed that every experience change moved from the
persistent selector to a newly mounted section heading. Retaining selector
focus preserves keyboard continuity without asynchronous rendering or duplicate
input state. The combined inventory `07cdcd41...6611d0` passed the 40-sample
experience gate: worst p95/max acknowledgement 48.7/49.4 ms at 1x and
166.1/195.5 ms at 4x, no warmed 1x long task. Do not attribute that combined
improvement solely to one micro-optimization or discard the earlier outliers.
The same package passed scale facets, both 50-row pagers, controls and search.

Qualification also exposed a pre-existing test-fixture race: its completion
helper released an outcome before reading the Output handle. A fast cancellation
cleanup could empty the handle before that read, losing the buffer observation.
Capturing the memory before publishing completion keeps the same physical
zeroization assertion deterministic. No workspace/output contract or assertion
was weakened; the full Presentation suite passed 152 cases afterward.

The final activation repair found four more `Fields.Single` scans in validated
edit/collection paths. `Single` scans the whole collection even when its first
element matches, so every edit decoded all root IDs to rediscover an already
admitted owner. Using the existing field-position index preserves path validation
and collection identity while removing those scans. Combined final inventory
`58a14e64...99a864` passes all92 directional rows (3,680 measured samples):
maximum feedback47.8/183.9 ms at1x/4x and no warmed1x long task. Do not assign
the entire combined improvement to that change or erase intermediate failures.

**Why This Matters for Future Work**: Follow the complete rendered path after
optimizing the shared owner. A green suite or cached validator cannot prove a
latency budget, and a remaining adapter scan can dominate the same interaction.
Keep failed measurements and compare exact published candidates through the
final requested frame rather than timing only event-handler return.

**References**:

- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs`
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor`
- `src/Configurator.Presentation/Presentation/DefinitionEditorState.cs`
- `docs/internal/adr/ADR-browser-form-computation.md`
- `docs/internal/OPERATIONS.md#browser-interaction-qualification-2026-10-22`

**Promotion Consideration**: The ADR owns the lifetime decision. Retain this as
the measured debugging lesson; it does not establish device-wide performance.
