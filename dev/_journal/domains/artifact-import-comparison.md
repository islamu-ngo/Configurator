# Artifact Import And Comparison Findings

Last Updated: 2026-10-31 Europe/Brussels

## [2026-10-30 Europe/Brussels] Semantic work counters do not bound normalization stalls

**Context**: Published Release/AOT comparison used the actual Event manifest and
a synthetic 4,096-row input on this Linux x64 workstation.

**Symptom / Observation**: The synchronous maximum-input cohort acknowledged
input at p95 517.9 ms at 1x. Yielding every 1,024 semantic work units repaired
most stalls, but a later cohort still recorded an 82 ms warmed task.

**Root Cause**: Canonicalizing recovered JSON and admitting answer snapshots
performed substantial recursive work outside the charged inverse/validation
traversal. Semantic limits prevented unbounded work but did not schedule that
separate normalization work cooperatively.

**Resolution**: `DefinitionJson.CanonicalizeAsync` and snapshot admission share
their implementation with synchronous entrypoints and yield by normalized-node
count without changing semantic work charges. Presentation captures immutable
inputs, computes outside its gate and rechecks ownership after suspension.
`normalized-core-and-client-current.txt` records Core 260/260 and shared
comparison 25/25. Closed-disclosure AOT cohorts are diagnostic evidence only;
they do not certify visible report latency.

**Why This Matters for Future Work**: Measure the entire admission-to-publication
path. A bounded semantic counter alone is not evidence of bounded UI stalls.

**References**:
- `src/Configurator.Core/Definitions/DefinitionJson.cs`
- `src/Configurator.Core/Definitions/SetupDefinition.cs`
- `src/Configurator.Presentation/Presentation/DefinitionImportState.cs`
- `docs/internal/adr/ADR-artifact-import-comparison.md`

**Promotion Consideration**: The mechanism belongs in the Core and Presentation
technical anchors; measurement history remains here.

## [2026-10-30 Europe/Brussels] Retention baselines must warm the measurement path

**Context**: Native comparison metrology measures completed disposal cycles with
forced collection separately from unforced latency samples.

**Symptom / Observation**: Early runs appeared to retain 120-232 bytes even
though assessment, owned input and result weak probes were dead.

**Root Cause**: The no-proposal baseline had not exercised the same weak-probe
counting and disposal-reporting bookkeeping as measured cycles. Moving the
product operation into an isolated helper or removing a yield did not resolve
that instrumentation asymmetry.

**Resolution**: Warm completed cycles through the identical `CountAlive` and
`PrintDisposal` path before establishing the no-proposal range. The native run
`native-comparison-resources-warmed-instrumentation-final.txt` completed all
20 disposal cycles per cohort within that range, without tolerance adjustment.
That run predates final normalization changes and is not final-source
qualification. The meter explicitly distinguishes inclusive managed-live upper
bounds, sampled heap, exact weak-probe counts and native-only scope.

**Why This Matters for Future Work**: Warm instrumentation, not merely product
code. Never loosen the retention budget to compensate for unmatched probes,
or treat WASM memory capacity as comparison-owned live memory.

**Execution Follow-up**: Direct Browser owner probes later exposed 408 bytes
per cycle retained by the meter's own weak-reference ledger, while every
observed private owner was dead. Preallocating that ledger before warmup removed
the per-cycle growth. Task-to-JavaScript bindings also need their own warmup.
Record aggregate heap observations and actual weak-owner liveness separately:
neither a small heap delta nor a recovered aggregate heap proves which private
owners remain. Require live-owner positive controls, completed physical Tasks
and subsequent zero-owner observations. Preserve unexplained runtime or meter
differences rather than assigning them to private data without evidence.

**References**:
- `eng/configurator/MeasureImportComparison.cs`
- `eng/configurator/ImportComparisonOwnershipProbe.cs`
- `eng/configurator/MeasureNativeImportComparison.cs`
- `docs/internal/OPERATIONS.md`

**Promotion Consideration**: Keep as a metrology lesson, not a product invariant
or a claim of managed-string erasure.

## [2026-10-30 Europe/Brussels] Resolve WASM profiles against the exact native image

**Context**: A bounded follow-up profile still showed long tasks after traversal
and normalization became cooperative.

**Symptom / Observation**: Chrome reported numeric WASM function indexes.
The official SDK `WasmEmitSymbolMap` output identified evidence dictionary
construction and runtime major collection in two hot stacks.

**Root Cause**: Cooperative traversal did not cover the subsequent synchronous
`ToDictionary` freeze of completed observations. Reading numeric function
indexes without a matching native image would not establish that attribution.
Ordinary Clean also left publish-time AOT intermediates outside its recorded
file list, so a separate build root was needed after diagnostic compilation.

**Resolution**: Generating a symbol map produced a native image with exactly the
same SHA-256 as the measured candidate; only then were indexes resolved.
The sole evidence constructor caller already privately owns the completed
dictionary, so the constructor now takes that ownership without cloning it.
All six owner suites passed after this repair. New AOT measurements, not the
test pass or an inferred allocation reduction, decide the task-ceiling gate.

**Why This Matters for Future Work**: Include final ownership handoff in the
profiled path. Match diagnostic maps by bytes, isolate diagnostic build outputs
and retain failed timing cohorts rather than treating a local optimization as
proof of the entire performance contract.

**References**:
- `src/Configurator.Core/Definitions/DefinitionImportAssessment.cs`
- `src/Configurator.Core/Definitions/DefinitionImportProjection.cs`
- `docs/internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md`

**Promotion Consideration**: The ownership mechanism belongs in the Core
contract; the diagnostic and build-cache lesson stays in this ledger.

## [2026-10-30 Europe/Brussels] Verify the effective measured surface

**Context**: Successive local AOT publications and directional report cohorts
reuse a worker-controlled Browser session.

**Symptom / Observation**: A measurement labelled with the newly published
manifest actually loaded the previous worker-cached assemblies. Separately,
changing `document.documentElement.dir` left the workspace's explicit saved RTL
override in force, so a document-labelled LTR cohort was not effective LTR.

**Root Cause**: A running worker and an intended package digest do not establish
which resources executed. Likewise, the outer document attribute is not proof
of the report's computed direction when inner layout providers specify `dir`.

**Resolution**: Preserve the affected arrays with corrected provenance rather
than relabelling them as successful qualification. In the task-owned synthetic
browser, retire old public caches/registration before loading the selected
candidate. Match actual resource names to its manifest. Change direction through
the shipped appearance selector, await the workspace attribute and assert
computed report direction for every sample. Require pending-control retirement
before final-content paint, and record host CPU/paging conditions alongside
latency. These checks establish the measured surface, not a performance pass.

**Why This Matters for Future Work**: Verify package identity and effective
layout before expensive sample collection. A successful publish, visible report,
or labelled direction cannot substitute for those observations.

**References**:
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor`
- `src/Configurator.Browser/Components/Configuration/ConfigurationImportReview.razor`
- `docs/internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md`

**Promotion Consideration**: Keep the metrology correction with its failed
evidence; do not turn temporary harness details into a second product contract.

## [2026-10-30 Europe/Brussels] Dispatch completion is not a cooperative yield

**Context**: Actual Terminal comparison timing exposed an approximately
8.1-second maximum-input report, despite fast Core measurements.

**Symptom / Observation**: Replacing zero-duration framework timers with
`IApplication.Invoke` made all timing cohorts fast. However, the installed
Terminal.Gui 2.5.0 API contract says that owner-thread invocation executes
inline, so those measurements did not establish cooperative suspension.
A yield-then-dispatch variant also depended on an active framework loop.

**Root Cause**: Returning a completed callback Task is not a scheduling boundary.
Conversely, coupling every processing checkpoint to the terminal timer cadence
adds repeated main-loop latency to private computation.

**Resolution**: The production callback uses `Task.Yield`, with cancellation
checks before and after suspension. Its deterministic regression keeps an owner
action open, verifies that the checkpoint has not completed, then awaits its
posted continuation after releasing that action. The complete Terminal owner
suite passed 118 tests with `dotnet test --project tests/Configurator.Terminal.Tests
-c Release`. All six settled actual-client timing cohorts passed; the maximum
warm report p95 was 217.9 ms, retaining first-use and every original sample.

**Why This Matters for Future Work**: Establish suspension and cancellation
semantics independently of throughput. A faster callback can accidentally remove
the responsiveness mechanism it was supposed to improve.

**References**:
- `src/Configurator.Terminal/SetupTerminalWindow.cs:651`
- `tests/Configurator.Terminal.Tests/SetupTerminalImportOperationTests.cs:17`
- `eng/configurator/MeasureNativeImportComparison.cs`
- `docs/internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md`

**Promotion Consideration**: Keep as a client scheduling lesson. No dependency
change or generic scheduling layer is implied.

## [2026-10-30 Europe/Brussels] Control history can outlive discarded input owners

**Context**: Actual Terminal compare/clear memory measurements retained about
51 KB per cycle despite zero original input, proposal and evidence weak owners.

**Symptom / Observation**: The slope also appeared during warmup. Isolated
diagnostic cleanup of import-path and read-only-status histories removed the
growth; ordinary-editor history alone did not. Independent user-facing tests
also restored a consumed path, a cleared path and a cleared ordinary draft
through Undo.

**Root Cause**: Persistent native controls retain copied editing state
independently of the objects observed by import-owner probes. Disposing the
original proposal or zeroing its byte buffer does not clear those copies.

**Resolution**: Clear path history when acquisition consumes the path, clear
ordinary-editor history on explicit private-state clearing, and discard
replaced comparison/lifecycle status history. Preserve current draft history
during read-only Compare. All three undo cases failed before correction; the
complete Terminal suite then passed 121 tests using
`dotnet test --project tests/Configurator.Terminal.Tests -c Release`.
Actual resource qualification remains separately required.

**Why This Matters for Future Work**: Trace copied control state as well as
original owner identities. A zero weak-owner count proves neither absence of
independent copies nor a return of the entire runtime heap to its old size.
Warm the actual observer and collection paths before comparing heap ranges.

**References**:
- `src/Configurator.Terminal/SetupTerminalWindow.cs`
- `tests/Configurator.Terminal.Tests/SetupTerminalImportOperationTests.cs`
- `eng/configurator/MeasureNativeImportComparison.cs`
- `docs/internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md`

**Promotion Consideration**: Keep as a native custody and measurement lesson;
no blanket control replacement or dependency patch is justified.

## [2026-10-31 Europe/Brussels] Yield computation without pacing every batch to a frame

**Context**: Actual Browser AOT comparison needed smaller validation slices
while retaining the five-second maximum-input report ceiling.

**Symptom / Observation**: With a display-frame wait at each checkpoint,
256-node validation slices produced a 5029.4 ms warm report p95. The same
validation boundaries with task-only Browser yielding produced a 756.4 ms
maximum RTL warm report p95; all twelve complete normal-package cohorts passed.

**Root Cause**: `YieldComparisonAsync` reused the mounting helper's frame wait.
That helper correctly permits a rendered batch to paint, but private computation
does not need a fresh display frame before every continuation. Increasing
checkpoint frequency therefore also increased repeated frame-pacing delay.
This does not explain every historical long task or sampling gap.

**Resolution**: Extract the helper's existing MessageChannel task boundary as
`nextTask`, close both ports before continuation, and use it for comparison.
Incremental mounting retains `nextFrame`. Actual JavaScript execution verifies
synchronous code, then a microtask, then task completion without requesting a
frame. Browser tests pass 263 cases with
`dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj -c Release`.
Published-package timing, eighty pending cancellations and sixty private-owner
retirement cycles were verified separately; raw failures remain recorded.

**Why This Matters for Future Work**: Distinguish event-loop suspension from
paint pacing. More cancellation checkpoints need not add one frame of latency
each. Check both throughput and actual input/cancellation behavior after changing
the boundary; neither a fast report nor a completed dispatch proves suspension.

**References**:
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:347`
- `src/Configurator.Browser/wwwroot/configuration-rendering.js`
- `docs/internal/LAUNCH_CONTRACT.md`
- `docs/internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md`

**Promotion Consideration**: The current mechanism is documented in the launch
contract. Keep the measurement lesson here; no general scheduler is introduced.
