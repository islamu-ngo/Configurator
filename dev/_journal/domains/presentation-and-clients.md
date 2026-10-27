# Presentation & Clients Findings

Last Updated: 2026-10-27 Europe/Brussels

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

[2026-10-23 Europe/Brussels] — Sticky search still needs scroll-preserving focus

**Context**: Published-browser qualification of the editor redesign tested
application-bar search while the operator was deep in a configuration section.
Search must not move the edited question or navigate the document.

**Symptom / Observation**: Making the wrapping application bar sticky kept
search visible, but focusing its Mud input still changed document scroll from
500 to 422 CSS pixels. The edited input moved from top 335.640625 to 413.640625.
The search opened and received focus, so checking only those states missed the
geometry failure.

**Root Cause**: A sticky element can remain visible while the focus API still
permits native scrolling. Layout placement and focus-scroll policy are separate
mechanisms; successful input focus does not prove that its surrounding document
stayed put.

**Resolution**: `WorkspaceSearch.OnAfterRenderAsync` delegates its current-owner
focus request to `workspace-interactions.js` `focusEntry`, which selects the
visible input or phone trigger and calls `focus({ preventScroll: true })`.
The published field candidate at commit `2a5c840` retained scroll 500 and
input top 335.640625 after trusted Ctrl-K. The Browser Release build exited 0,
and its existing search behavior tests remained passing.

**Why This Matters for Future Work**: Test the active question's exact rectangle
and document scroll before and after search/help transitions. Focus ownership,
visibility and geometry each need their own observation; neither a sticky
header nor aggregate layout-shift measurements substitute for that proof.

**References**:

- `src/Configurator.Browser/Layout/WorkspaceSearch.razor:133`
- `src/Configurator.Browser/wwwroot/workspace-interactions.js:3`
- `src/Configurator.Browser/Layout/NavMenu.razor.css:1`
- `src/Configurator.Browser/DESIGN.md`
- `docs/internal/adr/ADR-browser-workspace-presentation.md`

**Promotion Consideration**: The workspace ADR and design contract own the
interaction requirement. Keep this entry as the reproducible debugging lesson,
not a claim about untested browsers or devices.

[2026-10-23 Europe/Brussels] — Native zoom can turn a wrapping sticky header into an obstruction

**Context**: Final editor qualification used native 400% Chromium zoom in an
owned X11 display, not CSS zoom or a narrow emulated screenshot.

**Symptom / Observation**: The wrapping header occupied 196.199 CSS pixels of
a 214 px-high viewport. A centered 44 px input was covered by the sticky header,
despite no horizontal overflow. Element-at-point confirmed the obstruction.

**Root Cause**: Sticky header height grows as controls wrap while browser zoom
also reduces available viewport height. Width-only reflow does not guarantee
that the remaining editor is reachable.

**Resolution**: At max-height 30 rem the header/navigation scroll with the
document. Open search fixes only its entry/results within the viewport and
reserves its header space. The current generation stores a focus-return element,
not a value. Commit `6d964af` retained scroll 1498.25 through open/close at native
400%, returned focus to the uncovered input, and preserved desktop sticky
behavior and exact scroll 500/field top 335.640625.

**Why This Matters for Future Work**: Check actual browser zoom, obstruction
at the focused input, search reachability and focus return as separate
properties. An emulated narrow viewport alone misses native zoom behavior.

**References**: `src/Configurator.Browser/Layout/NavMenu.razor.css`,
`src/Configurator.Browser/Layout/WorkspaceSearch.razor.css`,
`src/Configurator.Browser/wwwroot/workspace-interactions.js`,
`docs/internal/OPERATIONS.md`.

**Promotion Consideration**: The workspace ADR and Browser design contract now
own short-height behavior. This finding does not qualify other physical hosts.

[2026-10-23 Europe/Brussels] — Package closure missed stale AOT runtime inputs

**Context**: Browser editor qualification compared an isolated AOT publication
with the normal interpreted package without changing project defaults.

**Symptom / Observation**: Later interpreted publication and all six package
checks passed, but actual startup stayed on the loading screen. Runtime console
reported `MONO interpreter: NIY encountered` in `LocalizationOptions` followed by
an interpreter assertion. Ordinary native Clean and republication produced the
same inventory and left AOT intermediates present.

**Root Cause**: The two compiler modes reused generated output directories.
Changing only the SDK intermediate/output root changed the localization
payload and restored actual Event controls. This supports stale AOT output as
the cause; the SDK's internal reuse mechanism was not traced.

**Resolution**: Perform a separate locked restore and publication with matching
`UseArtifactsOutput=true` and a task-owned `ArtifactsPath`. The first fresh
publication failed because SDK imports were unavailable during initial
evaluation; a separate restore before publishing resolved it. Store inventory
`90ffb31c48c7c28e84d15b8451801550bf6a8c53d3e4dc3f3887aadda5605b3a`
rendered actual Event controls, and both package suites passed 4/4 and 2/2.
Exact native commands and runtime limits belong in Operations.

**Why This Matters for Future Work**: Isolate generated outputs before comparing
compiler modes. Static package completeness cannot establish runtime startup,
and successful native Clean does not prove that all mode-specific intermediates
were removed.

**References**: `docs/internal/OPERATIONS.md`,
`eng/configurator/Configurator.Release.proj:90`, source commit `9a6c357`.

**Promotion Consideration**: Stays in this domain journal as an observed browser
qualification lesson, not a new project-wide compiler-mode policy.

[2026-10-27 Europe/Brussels] — Native cancellation is not physical settlement

**Context**: `cross-platform-quality` separates Desktop eligibility, external
consumer lifetime and historical publication facts without changing the writer.

**Symptom / Observation**: A deterministic consumer held the supplied save bytes
before copying them. Repeated shell disposal zeroed those bytes while they were
still borrowed; the literal ordered-byte assertion failed. A separate actual
window close test reproduced closure before consumer settlement.

**Root Cause**: Shell disposal released the logical operation and child disposal
cleared active save, bundle, import and definition-read storage. Current-generation
feedback also had no independent owner for stale installation facts.

**Resolution**: Consumer-owned cleanup releases the physical completion lease;
cancellation only revokes eligibility. Immutable bounded receipts retain known
progress separately. Desktop close waits responsively, then keeps unknown work
visible after two seconds until settlement, Stay open or deliberate Force close.
`dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress`
passed 201 cases; the native executable passed all seventeen named X11 guards.

**Why This Matters for Future Work**: Register a gate before invoking the external
boundary and pause before consumption, not after a writer has already copied its
input. Preserve real create-only writer tests independently. For Avalonia
headless async checks, use a typed `Dispatch` result and await it: the non-generic
callback call used during test authoring returned before its nested async
assertions. Only the corrected awaited failure is runtime Red evidence.

The native Event guard also needed its declared `Artifact to prepare` answer
selected independently of the artifact output picker. Output selection does not
activate definition-dependent inputs. All existing expected output assertions
remain; a current public fixture must satisfy its entry-point prerequisites.

**References**: `DesktopPublicationLifetimeTests`, `DesktopCloseLifecycleTests`,
`Configurator.Desktop.NativeHost`, the
[ownership ADR](../../../docs/internal/adr/ADR-cross-platform-quality-ownership.md)
and [execution evidence](../../../docs/internal/OPERATIONS.md#native-ownership-qualification-2026-10-27).

**Promotion Consideration**: The ADR and native guides own the delivered lifetime
contract. New hosts, installed payloads, human AT and deferred program slices
retain their separate qualification owners.
